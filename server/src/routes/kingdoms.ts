import { FastifyPluginAsync } from 'fastify';
import { eq } from 'drizzle-orm';
import { db } from '../db/client';
import { kingdoms, rulerNpcs } from '../db/schema';

// A kingdoms row should never exist without a matching ruler_npcs row --
// they're always created together (see below) -- but if that invariant is
// ever violated, surface it as a genuine server-side error rather than
// silently returning `rulerNpc: undefined` to the client.
async function getRulerNpcOrThrow(kingdomId: string) {
  const rulerRows = await db.select().from(rulerNpcs).where(eq(rulerNpcs.kingdomId, kingdomId)).limit(1);

  if (rulerRows.length === 0) {
    throw new Error(`Data consistency error: kingdom ${kingdomId} has no ruler_npcs row`);
  }

  return rulerRows[0];
}

const kingdomsRoutes: FastifyPluginAsync = async (fastify) => {
  fastify.post('/api/v1/kingdoms', async (request, reply) => {
    const existingRows = await db.select().from(kingdoms).where(eq(kingdoms.userId, request.userId)).limit(1);

    if (existingRows.length > 0) {
      const kingdom = existingRows[0];
      const rulerNpc = await getRulerNpcOrThrow(kingdom.id);
      reply.code(200);
      return { kingdom, rulerNpc };
    }

    // kingdoms.userId has a DB-level unique constraint, so two concurrent
    // requests from the same user (e.g. a double-tapped button) can't both
    // insert -- one wins, the other's insert is a no-op here rather than
    // an unhandled constraint-violation error.
    const insertedKingdoms = await db
      .insert(kingdoms)
      .values({ userId: request.userId })
      .onConflictDoNothing({ target: kingdoms.userId })
      .returning();

    if (insertedKingdoms.length === 0) {
      // Lost the race -- re-select the row the other request's insert
      // committed rather than assuming our own insert landed.
      const [kingdom] = await db.select().from(kingdoms).where(eq(kingdoms.userId, request.userId)).limit(1);
      const rulerNpc = await getRulerNpcOrThrow(kingdom.id);
      reply.code(200);
      return { kingdom, rulerNpc };
    }

    const [kingdom] = insertedKingdoms;
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
    const rulerNpc = await getRulerNpcOrThrow(kingdom.id);

    return { kingdom, rulerNpc };
  });
};

export default kingdomsRoutes;
