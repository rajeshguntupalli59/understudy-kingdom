import { describe, it, expect, afterEach } from 'vitest';
import { buildApp } from '../../src/app';
import { createTestUser } from './helpers/testUser';
import { truncateTables } from './helpers/db';

// Fix I-2: the highest-value gap in the existing suite -- every other test
// exercises a single user acting on their own kingdom/decisions, so nothing
// actually proved that one user's data is invisible to a different user.
// Every route scopes its query by `eq(kingdoms.userId, request.userId)` /
// `eq(decisions.kingdomId, kingdom.id)` where `kingdom` was itself looked up
// by the caller's own userId -- this test is what would catch a regression
// where that scoping is accidentally dropped (e.g. a stray `db.select()`
// with no `.where()`, or a swapped variable).
//
// Uses two independent real anonymous Supabase users (createTestUser() does
// a real signInAnonymously() call each time, so each call returns a fresh
// user with a fresh JWT) rather than sharing a JWT or crafting one by hand.
describe('cross-user data isolation', () => {
  const app = buildApp();

  afterEach(async () => {
    // This file's own users/kingdom rows -- same helper, same afterEach
    // pattern as kingdoms.test.ts/decisions.test.ts. vitest.config.ts sets
    // fileParallelism: false specifically so these TRUNCATEs across test
    // files never race each other.
    await truncateTables();
  });

  it("keeps user A's kingdom and decisions invisible to user B", async () => {
    const userA = await createTestUser();
    const userB = await createTestUser();

    const createKingdomResponse = await app.inject({
      method: 'POST',
      url: '/api/v1/kingdoms',
      headers: { authorization: `Bearer ${userA.jwt}` },
    });
    expect(createKingdomResponse.statusCode).toBe(201);

    const decisionResponse = await app.inject({
      method: 'POST',
      url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${userA.jwt}` },
      payload: {
        cycle_number: 1,
        player_recommendation: { army: 40, trade: 30, religion: 30 },
        ruler_outcome: { mood: 55 },
        overridden: false,
      },
    });
    expect(decisionResponse.statusCode).toBe(201);

    // User B has never created a kingdom -- GET /kingdoms/me must return
    // 404 for B, never A's kingdom.
    const meAsB = await app.inject({
      method: 'GET',
      url: '/api/v1/kingdoms/me',
      headers: { authorization: `Bearer ${userB.jwt}` },
    });
    expect(meAsB.statusCode).toBe(404);

    // GET /decisions with no kingdom yet is a 404 in the current
    // implementation (routes/decisions.ts: `if (kingdomRows.length === 0)
    // { reply.code(404); ... }`, the same guard POST /decisions uses) --
    // asserting the actual current behavior here, not an assumed one.
    const decisionsAsB = await app.inject({
      method: 'GET',
      url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${userB.jwt}` },
    });
    expect(decisionsAsB.statusCode).toBe(404);
  });
});
