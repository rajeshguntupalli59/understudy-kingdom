import { FastifyPluginAsync } from 'fastify';
import { eq, or } from 'drizzle-orm';
import { db } from '../db/client';
import { kingdoms, rulerNpcs, decisions, pvpDuels, councilMembers } from '../db/schema';

// Either the top-level `db` handle or a `tx` handed in by `db.transaction`'s
// callback -- both expose the same query-builder surface (`.select()`,
// `.insert()`, ...), so the helpers below can run either standalone
// (GET /me) or as part of a multi-statement transaction (POST, see below).
// Derived directly from `db.transaction`'s own signature (rather than
// hand-rolled against `NodePgTransaction`'s generics) so it can't silently
// drift out of sync with the real callback parameter type.
type TxExecutor = Parameters<Parameters<typeof db.transaction>[0]>[0];
type Executor = typeof db | TxExecutor;

// A kingdoms row should never exist without a matching ruler_npcs row --
// they're always created together in one transaction (see below) -- but if
// that invariant is ever violated, surface it as a genuine server-side
// error rather than silently returning `rulerNpc: undefined` to the client.
async function getRulerNpcOrThrow(executor: Executor, kingdomId: string) {
  const rulerRows = await executor.select().from(rulerNpcs).where(eq(rulerNpcs.kingdomId, kingdomId)).limit(1);

  if (rulerRows.length === 0) {
    throw new Error(`Data consistency error: kingdom ${kingdomId} has no ruler_npcs row`);
  }

  return rulerRows[0];
}

const kingdomsRoutes: FastifyPluginAsync = async (fastify) => {
  fastify.post('/api/v1/kingdoms', async (request, reply) => {
    // The kingdom insert and its ruler_npc insert are wrapped in a single
    // transaction so they commit atomically -- no reader can ever observe a
    // kingdom row whose ruler_npc hasn't landed yet, and a crash/error
    // between the two inserts rolls the kingdom insert back too (instead of
    // permanently bricking the user behind the userId unique constraint
    // with no ruler and no way to retry).
    const { kingdom, rulerNpc, created } = await db.transaction(async (tx: TxExecutor) => {
      const existingRows = await tx.select().from(kingdoms).where(eq(kingdoms.userId, request.userId)).limit(1);

      if (existingRows.length > 0) {
        const existingKingdom = existingRows[0];
        const existingRuler = await getRulerNpcOrThrow(tx, existingKingdom.id);
        return { kingdom: existingKingdom, rulerNpc: existingRuler, created: false };
      }

      // kingdoms.userId has a DB-level unique constraint. If a concurrent
      // transaction's insert for this same userId is in flight but not yet
      // committed, Postgres blocks this INSERT ... ON CONFLICT until that
      // other transaction resolves: if it commits, this statement sees the
      // conflict and returns zero rows (handled below); if it rolls back,
      // this insert proceeds as the winner instead. Either way, by the time
      // this transaction can see a conflicting row, that other
      // transaction's kingdom AND ruler_npc are both already committed --
      // there's no window where the kingdom exists without its ruler.
      const insertedKingdoms = await tx
        .insert(kingdoms)
        .values({ userId: request.userId })
        .onConflictDoNothing({ target: kingdoms.userId })
        .returning();

      if (insertedKingdoms.length === 0) {
        // Lost the race -- re-select the row the other transaction
        // committed rather than assuming our own insert landed.
        const [conflictingKingdom] = await tx.select().from(kingdoms).where(eq(kingdoms.userId, request.userId)).limit(1);

        if (!conflictingKingdom) {
          throw new Error(
            `Data consistency error: insert into kingdoms for user ${request.userId} conflicted, but no row could be found`,
          );
        }

        const conflictingRuler = await getRulerNpcOrThrow(tx, conflictingKingdom.id);
        return { kingdom: conflictingKingdom, rulerNpc: conflictingRuler, created: false };
      }

      const [insertedKingdom] = insertedKingdoms;
      const [insertedRuler] = await tx.insert(rulerNpcs).values({ kingdomId: insertedKingdom.id }).returning();

      return { kingdom: insertedKingdom, rulerNpc: insertedRuler, created: true };
    });

    reply.code(created ? 201 : 200);
    return { kingdom, rulerNpc };
  });

  fastify.get('/api/v1/kingdoms/me', async (request, reply) => {
    const rows = await db.select().from(kingdoms).where(eq(kingdoms.userId, request.userId)).limit(1);

    if (rows.length === 0) {
      reply.code(404);
      return { error: 'No kingdom found for this user' };
    }

    const kingdom = rows[0];
    const rulerNpc = await getRulerNpcOrThrow(db, kingdom.id);

    return { kingdom, rulerNpc };
  });

  // Test-only cleanup for Unity's *RealDataTests suites (CouncilPanelController
  // RealDataTests, EventPanelControllerRealDataTests, HistoryPanelController
  // RealDataTests, DecisionCycleManagerSessionResumeTests), which each create
  // a real kingdom + decisions against the real configured DATABASE_URL and,
  // before this route existed, never cleaned any of it up -- unlike the
  // TypeScript integration suite's own truncateTables(), which those Unity
  // tests can't call (different runtime, no HTTP surface for it). Gated
  // behind the same ALLOW_TEST_DB_TRUNCATE flag as truncateTables() -- see
  // test/integration/helpers/db.ts -- rather than a new env var, so there is
  // exactly one safety switch to reason about. Deliberately scoped to
  // request.userId's OWN kingdom only (never an arbitrary id) so even a
  // misconfigured production DATABASE_URL with this flag accidentally left
  // on can only ever let an authenticated caller delete their own data, not
  // anyone else's. Deletes in FK dependency order inside one transaction
  // (no ON DELETE CASCADE is configured in schema.ts) -- everything but the
  // councils row itself, which is intentionally left alone since other
  // members may still belong to it; only this user's own council_members
  // row is removed.
  fastify.delete('/api/v1/kingdoms/me', async (request, reply) => {
    if (process.env.ALLOW_TEST_DB_TRUNCATE !== 'true') {
      reply.code(403);
      return {
        error:
          'Refusing to delete: ALLOW_TEST_DB_TRUNCATE is not set to "true". This test-only cleanup route is disabled unless explicitly enabled against a dedicated test database.',
      };
    }

    const rows = await db.select().from(kingdoms).where(eq(kingdoms.userId, request.userId)).limit(1);
    if (rows.length === 0) {
      reply.code(404);
      return { error: 'No kingdom found for this user' };
    }
    const kingdom = rows[0];

    await db.transaction(async (tx: TxExecutor) => {
      await tx.delete(pvpDuels).where(or(eq(pvpDuels.challengerKingdomId, kingdom.id), eq(pvpDuels.defenderKingdomId, kingdom.id)));
      await tx.delete(decisions).where(eq(decisions.kingdomId, kingdom.id));
      await tx.delete(rulerNpcs).where(eq(rulerNpcs.kingdomId, kingdom.id));
      await tx.delete(councilMembers).where(eq(councilMembers.userId, request.userId));
      await tx.delete(kingdoms).where(eq(kingdoms.id, kingdom.id));
    });

    reply.code(204);
  });
};

export default kingdomsRoutes;
