import { describe, it, expect, afterEach } from 'vitest';
import { eq } from 'drizzle-orm';
import { buildApp } from '../../src/app';
import { db } from '../../src/db/client';
import { kingdoms, pvpDuels } from '../../src/db/schema';
import { createTestUser } from './helpers/testUser';
import { truncateTables } from './helpers/db';

describe('duels routes', () => {
  const app = buildApp();

  afterEach(async () => {
    await truncateTables();
  });

  async function createKingdom(jwt: string): Promise<void> {
    await app.inject({
      method: 'POST',
      url: '/api/v1/kingdoms',
      headers: { authorization: `Bearer ${jwt}` },
    });
  }

  it('POST /api/v1/duels returns 404 if the challenger has no kingdom yet', async () => {
    const challenger = await createTestUser();

    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/duels',
      headers: { authorization: `Bearer ${challenger.jwt}` },
      payload: { recommendation: { army: 40, trade: 30, religion: 30 } },
    });

    expect(response.statusCode).toBe(404);
    expect(response.json().error).toBe('No kingdom found for this user');
  });

  it('POST /api/v1/duels returns 404 if there is no other kingdom to challenge', async () => {
    const challenger = await createTestUser();
    await createKingdom(challenger.jwt);

    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/duels',
      headers: { authorization: `Bearer ${challenger.jwt}` },
      payload: { recommendation: { army: 40, trade: 30, religion: 30 } },
    });

    expect(response.statusCode).toBe(404);
    expect(response.json().error).toBe('No other kingdoms available to challenge');
  });

  it('POST /api/v1/duels returns 400 when the allocation does not sum to 100', async () => {
    const challenger = await createTestUser();
    await createKingdom(challenger.jwt);
    const defender = await createTestUser();
    await createKingdom(defender.jwt);

    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/duels',
      headers: { authorization: `Bearer ${challenger.jwt}` },
      payload: { recommendation: { army: 40, trade: 30, religion: 10 } },
    });

    expect(response.statusCode).toBe(400);
  });

  it('POST /api/v1/duels returns 400 for a malformed body', async () => {
    const challenger = await createTestUser();
    await createKingdom(challenger.jwt);
    const defender = await createTestUser();
    await createKingdom(defender.jwt);

    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/duels',
      headers: { authorization: `Bearer ${challenger.jwt}` },
      payload: { recommendation: { army: 'not-a-number' } },
    });

    expect(response.statusCode).toBe(400);
  });

  it('POST /api/v1/duels resolves a duel against the other kingdom and records it', async () => {
    const challenger = await createTestUser();
    await createKingdom(challenger.jwt);
    const defender = await createTestUser();
    await createKingdom(defender.jwt);

    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/duels',
      headers: { authorization: `Bearer ${challenger.jwt}` },
      payload: { recommendation: { army: 40, trade: 30, religion: 30 } },
    });

    expect(response.statusCode).toBe(201);
    const body = response.json();
    expect(typeof body.overridden).toBe('boolean');
    // The defender's ruler_npcs row was just created with schema defaults
    // (mood 50, loyalty 50, agenda 'Expansionist') and nothing in this test
    // mutates it, so the snapshot must match exactly. This isn't just
    // fixture-local, either: ruler_npcs is never mutated server-side
    // anywhere in this codebase (see the comment in duels.ts above the
    // defender-selection query), so this is also the actual permanent
    // production state of every ruler_npcs row -- every duel's defender
    // snapshot is always these exact schema defaults, not just in this test.
    expect(body.defenderRulerSnapshot).toEqual({ mood: 50, loyalty: 50, agenda: 'Expansionist' });
  });

  it("never selects the challenger's own kingdom as the defender", async () => {
    // No GET /api/v1/duels exists this pass to verify this via the API, so
    // this asserts directly against the DB row the route just wrote --
    // the only way to actually prove the exclusion held, rather than a
    // repeated-success smoke test that would pass even if the query's
    // `ne(kingdoms.id, challenger.id)` clause were silently dropped (a
    // self-duel is still a valid 201 response by shape).
    const challenger = await createTestUser();
    await createKingdom(challenger.jwt);
    const defender = await createTestUser();
    await createKingdom(defender.jwt);

    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/duels',
      headers: { authorization: `Bearer ${challenger.jwt}` },
      payload: { recommendation: { army: 40, trade: 30, religion: 30 } },
    });
    expect(response.statusCode).toBe(201);

    const challengerKingdomRows = await db.select().from(kingdoms).where(eq(kingdoms.userId, challenger.userId));
    const challengerKingdomId = challengerKingdomRows[0].id;

    const duelRows = await db.select().from(pvpDuels).where(eq(pvpDuels.challengerKingdomId, challengerKingdomId));
    expect(duelRows).toHaveLength(1);
    expect(duelRows[0].defenderKingdomId).not.toBe(challengerKingdomId);
  });
});
