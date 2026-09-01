import knex from '../db/knex.js';
import { authMiddleware } from '../auth/middleware.js';

const decisionSchema = {
  body: {
    type: 'object',
    required: ['kingdom_id', 'cycle_number', 'recommendation', 'ruler_outcome', 'overridden'],
    properties: {
      kingdom_id: { type: 'string', format: 'uuid' },
      cycle_number: { type: 'integer' },
      recommendation: { type: 'object' },
      ruler_outcome: { type: 'object' },
      overridden: { type: 'boolean' },
    },
  },
};

export function registerDecisionsRoutes(app) {
  app.post('/api/v1/decisions', { preHandler: authMiddleware, schema: decisionSchema }, async (request, reply) => {
    const { kingdom_id: kingdomId, cycle_number: cycleNumber, recommendation, ruler_outcome: rulerOutcome, overridden } = request.body;

    const kingdom = await knex('kingdoms').where({ id: kingdomId }).first();
    if (!kingdom || kingdom.user_id !== request.userId) {
      reply.code(403).send({ error: 'FORBIDDEN' });
      return;
    }

    // Check-then-insert on (kingdom_id, cycle_number) has a TOCTOU race: two
    // concurrent requests can both pass this lookup before either commits its
    // insert. The DB's unique(['kingdom_id', 'cycle_number']) constraint (see
    // migration 20260901000004) is the real guard -- this lookup is just a
    // fast path to skip a doomed insert in the common (non-racing) case.
    const existing = await knex('decisions').where({ kingdom_id: kingdomId, cycle_number: cycleNumber }).first();
    if (existing) {
      reply.code(409).send({ error: 'CYCLE_ALREADY_RESOLVED' });
      return;
    }

    let decision;
    try {
      [decision] = await knex('decisions')
        .insert({
          kingdom_id: kingdomId,
          cycle_number: cycleNumber,
          player_recommendation: JSON.stringify(recommendation),
          ruler_outcome: JSON.stringify(rulerOutcome),
          overridden,
        })
        .returning(['id', 'overridden']);
    } catch (err) {
      if (err.code === '23505') {
        // Lost a race to resolve this cycle -- another concurrent request
        // for the same kingdom_id + cycle_number won and committed first.
        reply.code(409).send({ error: 'CYCLE_ALREADY_RESOLVED' });
        return;
      }
      throw err;
    }

    reply.code(201).send({ decision_id: decision.id, ruler_outcome: rulerOutcome, overridden: decision.overridden });
  });
}
