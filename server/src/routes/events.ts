import { FastifyPluginAsync } from 'fastify';
import { and, count, eq, gte, lt } from 'drizzle-orm';
import { db } from '../db/client';
import { kingdoms, decisions } from '../db/schema';
import { getActiveEventWindow } from '../game/liveOpsEvents';

const eventsRoutes: FastifyPluginAsync = async (fastify) => {
  fastify.get('/api/v1/events/active', async (request, reply) => {
    const kingdomRows = await db.select().from(kingdoms).where(eq(kingdoms.userId, request.userId)).limit(1);
    if (kingdomRows.length === 0) {
      reply.code(404);
      return { error: 'No kingdom found for this user' };
    }
    const kingdom = kingdomRows[0];

    const { eventId, definition, weekStart, weekEnd } = getActiveEventWindow(new Date());

    const [{ value: decisionsCompleted }] = await db
      .select({ value: count() })
      .from(decisions)
      .where(
        and(
          eq(decisions.kingdomId, kingdom.id),
          gte(decisions.createdAt, weekStart),
          lt(decisions.createdAt, weekEnd),
        ),
      );

    return {
      eventId,
      name: definition.name,
      narration: definition.narration,
      objectiveDecisionCount: definition.objectiveDecisionCount,
      decisionsCompleted,
      rewardMood: definition.rewardMood,
      rewardLoyalty: definition.rewardLoyalty,
    };
  });
};

export default eventsRoutes;
