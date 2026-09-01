import { describe, it, expect, afterEach, afterAll } from 'vitest';
import { buildServer } from '../../src/server.js';
import { issueTokenPair } from '../../src/auth/tokens.js';
import { truncateAll, knex } from '../helpers/testDb.js';

async function seedUserAndKingdom() {
  const [user] = await knex('users').insert({ device_id: `dev-${Date.now()}-${Math.random()}` }).returning('id');
  const [kingdom] = await knex('kingdoms').insert({ user_id: user.id }).returning('id');
  return { user, kingdom };
}

describe('POST /api/v1/decisions', () => {
  afterEach(async () => {
    await truncateAll();
  });
  afterAll(async () => {
    await knex.destroy();
  });

  it('records a decision for a kingdom the caller owns', async () => {
    const { user, kingdom } = await seedUserAndKingdom();
    const { accessToken } = issueTokenPair(user.id);
    const app = buildServer();

    const response = await app.inject({
      method: 'POST', url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${accessToken}` },
      payload: {
        kingdom_id: kingdom.id, cycle_number: 1,
        recommendation: { army: 40, trade: 30, religion: 30 },
        ruler_outcome: { mood_delta: 5, loyalty_delta: 3 },
        overridden: false,
      },
    });

    expect(response.statusCode).toBe(201);
    const body = response.json();
    expect(body.decision_id).toBeTruthy();
    expect(body.overridden).toBe(false);

    const rows = await knex('decisions').where({ kingdom_id: kingdom.id, cycle_number: 1 });
    expect(rows).toHaveLength(1);
  });

  it('returns 401 with no Bearer token', async () => {
    const { kingdom } = await seedUserAndKingdom();
    const app = buildServer();
    const response = await app.inject({
      method: 'POST', url: '/api/v1/decisions',
      payload: { kingdom_id: kingdom.id, cycle_number: 1, recommendation: {}, ruler_outcome: {}, overridden: false },
    });
    expect(response.statusCode).toBe(401);
  });

  it('returns 403 when the caller does not own the kingdom', async () => {
    const { kingdom } = await seedUserAndKingdom(); // kingdom belongs to user A
    const [userB] = await knex('users').insert({ device_id: `dev-b-${Date.now()}` }).returning('id');
    const { accessToken } = issueTokenPair(userB.id); // token for user B

    const app = buildServer();
    const response = await app.inject({
      method: 'POST', url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${accessToken}` },
      payload: { kingdom_id: kingdom.id, cycle_number: 1, recommendation: {}, ruler_outcome: {}, overridden: false },
    });
    expect(response.statusCode).toBe(403);
  });

  it('returns 409 when cycle_number is already resolved for that kingdom', async () => {
    const { user, kingdom } = await seedUserAndKingdom();
    const { accessToken } = issueTokenPair(user.id);
    const app = buildServer();

    const payload = { kingdom_id: kingdom.id, cycle_number: 1, recommendation: {}, ruler_outcome: {}, overridden: false };
    await app.inject({ method: 'POST', url: '/api/v1/decisions', headers: { authorization: `Bearer ${accessToken}` }, payload });

    const response = await app.inject({
      method: 'POST', url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${accessToken}` }, payload,
    });
    expect(response.statusCode).toBe(409);
  });

  it('returns 400 for a malformed request missing kingdom_id', async () => {
    const { user } = await seedUserAndKingdom();
    const { accessToken } = issueTokenPair(user.id);
    const app = buildServer();
    const response = await app.inject({
      method: 'POST', url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${accessToken}` },
      payload: { cycle_number: 1, recommendation: {}, ruler_outcome: {}, overridden: false },
    });
    expect(response.statusCode).toBe(400);
  });

  it('returns 400 (not 500) for a non-UUID kingdom_id', async () => {
    const { user } = await seedUserAndKingdom();
    const { accessToken } = issueTokenPair(user.id);
    const app = buildServer();
    const response = await app.inject({
      method: 'POST', url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${accessToken}` },
      payload: {
        kingdom_id: 'not-a-uuid-at-all', cycle_number: 1,
        recommendation: {}, ruler_outcome: {}, overridden: false,
      },
    });
    expect(response.statusCode).toBe(400);
  });

  it('returns 400 (not 500) for a cycle_number outside the Postgres integer range', async () => {
    const { user, kingdom } = await seedUserAndKingdom();
    const { accessToken } = issueTokenPair(user.id);
    const app = buildServer();
    const response = await app.inject({
      method: 'POST', url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${accessToken}` },
      payload: {
        kingdom_id: kingdom.id, cycle_number: 9999999999,
        recommendation: {}, ruler_outcome: {}, overridden: false,
      },
    });
    expect(response.statusCode).toBe(400);
    const body = response.json();
    expect(body.error).toBe('VALIDATION_FAILED');
    // Never leak raw driver/SQL error text (e.g. Postgres code '22003').
    expect(JSON.stringify(body)).not.toMatch(/22003/);
  });

  it('returns 401 (not 400) for no Bearer token even with a malformed body', async () => {
    const { kingdom } = await seedUserAndKingdom();
    const app = buildServer();
    const response = await app.inject({
      method: 'POST', url: '/api/v1/decisions',
      // No authorization header, AND the body is missing required fields --
      // auth must be checked (and win with a 401) before schema validation
      // runs, so this must not come back as a 400.
      payload: { kingdom_id: kingdom.id },
    });
    expect(response.statusCode).toBe(401);
  });

  it('handles two concurrent submissions for the same kingdom_id + cycle_number without a 500', async () => {
    const { user, kingdom } = await seedUserAndKingdom();
    const { accessToken } = issueTokenPair(user.id);
    const app = buildServer();

    const payload = {
      kingdom_id: kingdom.id, cycle_number: 1,
      recommendation: { army: 40, trade: 30, religion: 30 },
      ruler_outcome: { mood_delta: 5, loyalty_delta: 3 },
      overridden: false,
    };

    const [response1, response2] = await Promise.all([
      app.inject({ method: 'POST', url: '/api/v1/decisions', headers: { authorization: `Bearer ${accessToken}` }, payload }),
      app.inject({ method: 'POST', url: '/api/v1/decisions', headers: { authorization: `Bearer ${accessToken}` }, payload }),
    ]);

    const codes = [response1.statusCode, response2.statusCode].sort();
    expect(codes).toEqual([201, 409]);

    const rows = await knex('decisions').where({ kingdom_id: kingdom.id, cycle_number: 1 });
    expect(rows).toHaveLength(1);
  });
});
