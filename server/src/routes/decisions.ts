import { FastifyPluginAsync } from 'fastify';
import { and, count, desc, eq, lt } from 'drizzle-orm';
import { db } from '../db/client';
import { kingdoms, decisions, councils, councilMembers } from '../db/schema';

const createDecisionSchema = {
  body: {
    type: 'object',
    required: ['cycle_number', 'player_recommendation', 'ruler_outcome', 'overridden'],
    properties: {
      cycle_number: { type: 'integer' },
      player_recommendation: { type: 'object' },
      ruler_outcome: { type: 'object' },
      overridden: { type: 'boolean' },
    },
  },
} as const;

interface CreateDecisionBody {
  cycle_number: number;
  player_recommendation: unknown;
  ruler_outcome: unknown;
  overridden: boolean;
}

const listDecisionsSchema = {
  querystring: {
    type: 'object',
    properties: {
      cursor: { type: 'string', format: 'date-time' },
      limit: { type: 'integer', minimum: 1, maximum: 100, default: 20 },
    },
  },
} as const;

interface ListDecisionsQuery {
  cursor?: string;
  limit: number;
}

// Same TxExecutor derivation as kingdoms.ts/councils.ts -- kept local since
// this is the only place in decisions.ts needing a transaction.
type TxExecutor = Parameters<Parameters<typeof db.transaction>[0]>[0];

/**
 * Called after a decision is newly recorded (the 201 path only, never the
 * 409 duplicate path). If the caller is in a council whose milestone hasn't
 * been reached yet, recomputes the council's total decision count and, if
 * it now meets the threshold, atomically flips milestoneReached and grants
 * rewardEligible to every CURRENT member in one transaction -- guarded by
 * `WHERE milestone_reached = false` so two concurrent decisions racing to
 * cross the threshold can only ever flip it once. See
 * docs/superpowers/specs/2026-09-03-council-social-design.md.
 */
async function maybeAdvanceCouncilMilestone(userId: string): Promise<void> {
  const [membership] = await db.select().from(councilMembers).where(eq(councilMembers.userId, userId)).limit(1);
  if (!membership) {
    return;
  }

  const [council] = await db.select().from(councils).where(eq(councils.id, membership.councilId)).limit(1);
  if (!council || council.milestoneReached) {
    return;
  }

  const [{ value: totalDecisions }] = await db
    .select({ value: count() })
    .from(decisions)
    .innerJoin(kingdoms, eq(kingdoms.id, decisions.kingdomId))
    .innerJoin(councilMembers, eq(councilMembers.userId, kingdoms.userId))
    .where(eq(councilMembers.councilId, council.id));

  if (totalDecisions < council.milestoneThreshold) {
    return;
  }

  await db.transaction(async (tx: TxExecutor) => {
    const flipped = await tx
      .update(councils)
      .set({ milestoneReached: true })
      .where(and(eq(councils.id, council.id), eq(councils.milestoneReached, false)))
      .returning();

    if (flipped.length === 0) {
      // Lost the race to a concurrent request that already flipped this --
      // no-op, don't grant eligibility twice.
      return;
    }

    await tx.update(councilMembers).set({ rewardEligible: true }).where(eq(councilMembers.councilId, council.id));
  });
}

const decisionsRoutes: FastifyPluginAsync = async (fastify) => {
  fastify.post<{ Body: CreateDecisionBody }>(
    '/api/v1/decisions',
    { schema: createDecisionSchema },
    async (request, reply) => {
      const kingdomRows = await db.select().from(kingdoms).where(eq(kingdoms.userId, request.userId)).limit(1);
      if (kingdomRows.length === 0) {
        reply.code(404);
        return { error: 'No kingdom found for this user' };
      }
      const kingdom = kingdomRows[0];

      // decisions has a DB-level unique constraint on (kingdom_id,
      // cycle_number). Rely on that constraint via onConflictDoNothing
      // rather than a separate SELECT-then-INSERT check -- the latter has a
      // race where two concurrent requests for the same cycle_number both
      // pass the existence check and one insert fails with an unhandled
      // Postgres error instead of a clean 409. A zero-row `returning()`
      // means the row already existed (matches the kingdoms.ts pattern of
      // trusting the constraint over a pre-check).
      const [decision] = await db
        .insert(decisions)
        .values({
          kingdomId: kingdom.id,
          cycleNumber: request.body.cycle_number,
          playerRecommendation: request.body.player_recommendation,
          rulerOutcome: request.body.ruler_outcome,
          overridden: request.body.overridden,
        })
        .onConflictDoNothing({ target: [decisions.kingdomId, decisions.cycleNumber] })
        .returning();

      if (!decision) {
        reply.code(409);
        return { error: 'This cycle_number already has a recorded decision' };
      }

      await maybeAdvanceCouncilMilestone(request.userId);

      reply.code(201);
      return { decision };
    },
  );

  fastify.get<{ Querystring: ListDecisionsQuery }>(
    '/api/v1/decisions',
    { schema: listDecisionsSchema },
    async (request, reply) => {
      const { limit } = request.query;

      const kingdomRows = await db.select().from(kingdoms).where(eq(kingdoms.userId, request.userId)).limit(1);
      if (kingdomRows.length === 0) {
        reply.code(404);
        return { error: 'No kingdom found for this user' };
      }
      const kingdom = kingdomRows[0];

      const conditions = [eq(decisions.kingdomId, kingdom.id)];
      if (request.query.cursor) {
        conditions.push(lt(decisions.createdAt, new Date(request.query.cursor)));
      }

      const rows = await db
        .select()
        .from(decisions)
        .where(and(...conditions))
        .orderBy(desc(decisions.createdAt))
        .limit(limit);

      const nextCursor = rows.length === limit ? rows[rows.length - 1].createdAt.toISOString() : null;

      return { decisions: rows, nextCursor };
    },
  );
};

export default decisionsRoutes;
