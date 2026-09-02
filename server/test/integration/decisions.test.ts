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

  it('handles two concurrent POST /api/v1/decisions requests for the same cycle_number without a conflict race', async () => {
    // Fire both requests "simultaneously" via Promise.all to ensure they
    // genuinely race at the database level, rather than executing sequentially.
    // This verifies that the onConflictDoNothing constraint on
    // (kingdom_id, cycle_number) is atomic and exactly one insert succeeds
    // while the other gets a 409, regardless of which hits the DB first.
    await createKingdom();

    const payload = {
      cycle_number: 1,
      player_recommendation: { army: 40, trade: 30, religion: 30 },
      ruler_outcome: { mood: 55 },
      overridden: false,
    };

    const [first, second] = await Promise.all([
      app.inject({
        method: 'POST',
        url: '/api/v1/decisions',
        headers: { authorization: `Bearer ${jwt}` },
        payload,
      }),
      app.inject({
        method: 'POST',
        url: '/api/v1/decisions',
        headers: { authorization: `Bearer ${jwt}` },
        payload,
      }),
    ]);

    // Exactly one request succeeds (201) and one gets a conflict (409).
    // Which one wins is non-deterministic based on DB timing, so assert
    // on the sorted multiset of status codes.
    expect([first.statusCode, second.statusCode].sort()).toEqual([201, 409]);

    // Verify that exactly one decision row exists for this cycle_number
    // afterward. We do this by fetching all decisions and counting those
    // with cycle_number = 1.
    const decisionsResponse = await app.inject({
      method: 'GET',
      url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${jwt}` },
    });

    expect(decisionsResponse.statusCode).toBe(200);
    const decisionsBody = decisionsResponse.json();
    const cycle1Decisions = decisionsBody.decisions.filter((d: any) => d.cycleNumber === 1);
    expect(cycle1Decisions).toHaveLength(1);
    expect(cycle1Decisions[0].cycleNumber).toBe(1);
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
