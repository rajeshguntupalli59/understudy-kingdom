import { FastifyPluginAsync } from 'fastify';
import { eq, ne, sql } from 'drizzle-orm';
import { db } from '../db/client';
import { kingdoms, rulerNpcs, pvpDuels } from '../db/schema';
import { evaluate, RulerState, ResourceAllocation, Agenda } from '../game/overrideEvaluator';

const createDuelSchema = {
  body: {
    type: 'object',
    required: ['recommendation'],
    additionalProperties: false,
    properties: {
      recommendation: {
        type: 'object',
        required: ['army', 'trade', 'religion'],
        additionalProperties: false,
        properties: {
          army: { type: 'integer', minimum: 0 },
          trade: { type: 'integer', minimum: 0 },
          religion: { type: 'integer', minimum: 0 },
        },
      },
    },
  },
} as const;

interface CreateDuelBody {
  recommendation: ResourceAllocation;
}

const duelsRoutes: FastifyPluginAsync = async (fastify) => {
  fastify.post<{ Body: CreateDuelBody }>('/api/v1/duels', { schema: createDuelSchema }, async (request, reply) => {
    const { army, trade, religion } = request.body.recommendation;

    // JSON Schema (draft-07, as used elsewhere in this project) can't easily
    // express "these three fields sum to 100" -- hand-checking a cross-field
    // invariant in the handler, matching this project's existing pattern of
    // trusting the schema for shape and hand-coding invariants it can't
    // express (see decisions.ts's cycle_number uniqueness, enforced via a DB
    // constraint rather than the schema).
    if (army + trade + religion !== 100) {
      reply.code(400);
      return { error: 'recommendation army+trade+religion must sum to 100' };
    }

    const challengerRows = await db.select().from(kingdoms).where(eq(kingdoms.userId, request.userId)).limit(1);
    if (challengerRows.length === 0) {
      reply.code(404);
      return { error: 'No kingdom found for this user' };
    }
    const challenger = challengerRows[0];

    // ruler_npcs is never mutated server-side anywhere in this milestone
    // (kingdoms.ts inserts a row with schema defaults and nothing ever
    // updates it afterward) -- so defenderRuler.mood/loyalty/agenda below,
    // and therefore defenderRulerSnapshot in every response, is always the
    // schema default (50, 50, 'Expansionist') in production. Duel outcomes
    // currently vary only with the challenger's own allocation (via the
    // agenda-alignment check), not with any real defender state. This is a
    // known, deliberate limitation of this pass, not a bug -- meaningful PvP
    // variety across different defenders requires syncing ruler state to the
    // server in a future milestone.
    const defenderRows = await db
      .select({ kingdom: kingdoms, ruler: rulerNpcs })
      .from(kingdoms)
      .innerJoin(rulerNpcs, eq(rulerNpcs.kingdomId, kingdoms.id))
      .where(ne(kingdoms.id, challenger.id))
      .orderBy(sql`random()`)
      .limit(1);

    if (defenderRows.length === 0) {
      reply.code(404);
      return { error: 'No other kingdoms available to challenge' };
    }
    const { kingdom: defenderKingdom, ruler: defenderRuler } = defenderRows[0];

    const allocation: ResourceAllocation = { army, trade, religion };
    // Snapshotted, not a live reference -- see the doc comment on
    // defenderRulerSnapshot in db/schema.ts.
    const defenderState: RulerState = {
      mood: defenderRuler.mood,
      loyalty: defenderRuler.loyalty,
      // ruler_npcs.agenda is unconstrained text (no DB CHECK constraint);
      // this cast trusts existing data -- runtime values are currently
      // always one of the four known agendas since nothing else writes it
      // (see the "never mutated server-side" comment on the query above).
      agenda: defenderRuler.agenda as Agenda,
    };
    const roll = Math.random();
    // result.moodDelta/loyaltyDelta are intentionally discarded below -- the
    // spec's "No mechanical effect on the challenger's own kingdom this
    // pass" scope decision means nothing persists them. Don't wire them up
    // without checking the spec first.
    const result = evaluate(defenderState, allocation, roll);

    await db.insert(pvpDuels).values({
      challengerKingdomId: challenger.id,
      defenderKingdomId: defenderKingdom.id,
      challengerRecommendation: allocation,
      defenderRulerSnapshot: defenderState,
      overridden: result.overridden,
    });

    reply.code(201);
    return {
      overridden: result.overridden,
      defenderRulerSnapshot: defenderState,
    };
  });
};

export default duelsRoutes;
