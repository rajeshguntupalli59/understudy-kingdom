import { FastifyPluginAsync } from 'fastify';
import { eq } from 'drizzle-orm';
import { db } from '../db/client';
import { kingdoms, rulerNpcs } from '../db/schema';

const kingdomsRoutes: FastifyPluginAsync = async (fastify) => {
  fastify.post('/api/v1/kingdoms', async (request, reply) => {
    const existingRows = await db.select().from(kingdoms).where(eq(kingdoms.userId, request.userId)).limit(1);

    if (existingRows.length > 0) {
      const kingdom = existingRows[0];
      const rulerRows = await db.select().from(rulerNpcs).where(eq(rulerNpcs.kingdomId, kingdom.id)).limit(1);
      reply.code(200);
      return { kingdom, rulerNpc: rulerRows[0] };
    }

    const [kingdom] = await db.insert(kingdoms).values({ userId: request.userId }).returning();
    const [rulerNpc] = await db.insert(rulerNpcs).values({ kingdomId: kingdom.id }).returning();

    reply.code(201);
    return { kingdom, rulerNpc };
  });

  fastify.get('/api/v1/kingdoms/me', async (request, reply) => {
    const rows = await db.select().from(kingdoms).where(eq(kingdoms.userId, request.userId)).limit(1);

    if (rows.length === 0) {
      reply.code(404);
      return { error: 'No kingdom found for this user' };
    }

    const kingdom = rows[0];
    const rulerRows = await db.select().from(rulerNpcs).where(eq(rulerNpcs.kingdomId, kingdom.id)).limit(1);

    return { kingdom, rulerNpc: rulerRows[0] };
  });
};

export default kingdomsRoutes;
