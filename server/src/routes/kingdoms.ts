import { FastifyPluginAsync } from 'fastify';
import { eq } from 'drizzle-orm';
import type { NodePgTransaction } from 'drizzle-orm/node-postgres';
import { db } from '../db/client';
import { kingdoms, rulerNpcs } from '../db/schema';

// Either the top-level `db` handle or a `tx` handed in by `db.transaction`'s
// callback -- both expose the same query-builder surface (`.select()`,
// `.insert()`, ...), so the helpers below can run either standalone
// (GET /me) or as part of a multi-statement transaction (POST, see below).
type TxExecutor = NodePgTransaction<Record<string, never>, Record<string, never>>;
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
};

export default kingdomsRoutes;
