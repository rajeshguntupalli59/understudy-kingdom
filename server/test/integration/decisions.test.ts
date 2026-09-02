import { describe, it, expect, beforeAll, afterEach } from 'vitest';
import { buildApp } from '../../src/app';
import { createTestUser } from './helpers/testUser';
import { truncateTables } from './helpers/db';

describe('decisions routes', () => {
  const app = buildApp();
  let jwt: string;

  beforeAll(async () => {
    const user = await createTestUser();
    jwt = user.jwt;
  });

  afterEach(async () => {
    await truncateTables();
  });

  async function createKingdom(): Promise<void> {
    await app.inject({
      method: 'POST',
      url: '/api/v1/kingdoms',
      headers: { authorization: `Bearer ${jwt}` },
    });
  }

  it('POST /api/v1/decisions returns 404 if the caller has no kingdom yet', async () => {
    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${jwt}` },
      payload: {
        cycle_number: 1,
        player_recommendation: { army: 40, trade: 30, religion: 30 },
        ruler_outcome: {},
        overridden: false,
      },
    });

    expect(response.statusCode).toBe(404);
  });

  it('POST /api/v1/decisions records a decision', async () => {
    await createKingdom();

    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${jwt}` },
      payload: {
        cycle_number: 1,
        player_recommendation: { army: 40, trade: 30, religion: 30 },
        ruler_outcome: { mood: 55 },
        overridden: false,
      },
    });

    expect(response.statusCode).toBe(201);
    expect(response.json().decision.cycleNumber).toBe(1);
  });

  it('POST /api/v1/decisions returns 409 for a duplicate cycle_number', async () => {
    await createKingdom();
    const payload = { cycle_number: 1, player_recommendation: {}, ruler_outcome: {}, overridden: false };

    await app.inject({ method: 'POST', url: '/api/v1/decisions', headers: { authorization: `Bearer ${jwt}` }, payload });
    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${jwt}` },
      payload,
    });

    expect(response.statusCode).toBe(409);
  });

  it('POST /api/v1/decisions returns 400 for a malformed body', async () => {
    await createKingdom();

    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${jwt}` },
      payload: { cycle_number: 'not-a-number' },
    });

    expect(response.statusCode).toBe(400);
  });

  it('GET /api/v1/decisions returns this user\'s decisions newest-first', async () => {
    await createKingdom();
    for (let cycle = 1; cycle <= 3; cycle++) {
      await app.inject({
        method: 'POST',
        url: '/api/v1/decisions',
        headers: { authorization: `Bearer ${jwt}` },
        payload: { cycle_number: cycle, player_recommendation: {}, ruler_outcome: {}, overridden: false },
      });
    }

    const response = await app.inject({
      method: 'GET',
      url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${jwt}` },
    });

    expect(response.statusCode).toBe(200);
    const body = response.json();
    expect(body.decisions).toHaveLength(3);
    expect(body.decisions[0].cycleNumber).toBe(3);
    expect(body.decisions[2].cycleNumber).toBe(1);
  });
});
