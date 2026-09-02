import { FastifyPluginAsync } from 'fastify';
import { and, desc, eq, lt } from 'drizzle-orm';
import { db } from '../db/client';
import { kingdoms, decisions } from '../db/schema';

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

      reply.code(201);
      return { decision };
    },
  );

  fastify.get<{ Querystring: { cursor?: string; limit?: string } }>('/api/v1/decisions', async (request, reply) => {
    const limit = Math.min(Math.max(parseInt(request.query.limit ?? '20', 10) || 20, 1), 100);

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
  });
};

export default decisionsRoutes;
