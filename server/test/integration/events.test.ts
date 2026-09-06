import { eq } from 'drizzle-orm';
import { describe, it, expect, afterEach } from 'vitest';
import { buildApp } from '../../src/app';
import { db } from '../../src/db/client';
import { kingdoms, decisions } from '../../src/db/schema';
import { getActiveEventWindow } from '../../src/game/liveOpsEvents';
import { createTestUser } from './helpers/testUser';
import { truncateTables } from './helpers/db';

describe('events routes', () => {
  const app = buildApp();

  afterEach(async () => {
    await truncateTables();
  });

  async function createKingdomAndGetId(userId: string, jwt: string): Promise<string> {
    await app.inject({
      method: 'POST',
      url: '/api/v1/kingdoms',
      headers: { authorization: `Bearer ${jwt}` },
    });
    const [kingdom] = await db.select().from(kingdoms).where(eq(kingdoms.userId, userId)).limit(1);
    return kingdom.id;
  }

  it('GET /api/v1/events/active returns 404 if the caller has no kingdom yet', async () => {
    const user = await createTestUser();

    const response = await app.inject({
      method: 'GET',
      url: '/api/v1/events/active',
      headers: { authorization: `Bearer ${user.jwt}` },
    });

    expect(response.statusCode).toBe(404);
    expect(response.json().error).toBe('No kingdom found for this user');
  });

  it('returns the active event with a well-formed eventId and zero progress for a fresh kingdom', async () => {
    const user = await createTestUser();
    await createKingdomAndGetId(user.userId, user.jwt);

    const response = await app.inject({
      method: 'GET',
      url: '/api/v1/events/active',
      headers: { authorization: `Bearer ${user.jwt}` },
    });

    expect(response.statusCode).toBe(200);
    const body = response.json();
    expect(body.eventId).toMatch(/^W\d{4}-\d{1,2}$/);
    expect(typeof body.name).toBe('string');
    expect(typeof body.narration).toBe('string');
    expect(body.objectiveDecisionCount).toBeGreaterThan(0);
    expect(body.decisionsCompleted).toBe(0);
    expect(body.rewardMood).toBeGreaterThan(0);
    expect(body.rewardLoyalty).toBeGreaterThan(0);
  });

  it('decisionsCompleted counts only decisions created within the active event window (half-open, exclusive upper bound)', async () => {
    const user = await createTestUser();
    const kingdomId = await createKingdomAndGetId(user.userId, user.jwt);

    const { weekStart, weekEnd } = getActiveEventWindow(new Date());
    const inWindow = new Date(weekStart.getTime() + 60 * 60 * 1000); // 1 hour into the window
    const beforeWindow = new Date(weekStart.getTime() - 60 * 60 * 1000); // 1 hour before it starts
    const atWeekEndExclusiveBoundary = weekEnd; // must NOT count -- weekEnd itself belongs to the NEXT week

    await db.insert(decisions).values([
      {
        kingdomId,
        cycleNumber: 1,
        playerRecommendation: {},
        rulerOutcome: {},
        overridden: false,
        createdAt: inWindow,
      },
      {
        kingdomId,
        cycleNumber: 2,
        playerRecommendation: {},
        rulerOutcome: {},
        overridden: false,
        createdAt: beforeWindow,
      },
      {
        kingdomId,
        cycleNumber: 3,
        playerRecommendation: {},
        rulerOutcome: {},
        overridden: false,
        createdAt: atWeekEndExclusiveBoundary,
      },
    ]);

    const response = await app.inject({
      method: 'GET',
      url: '/api/v1/events/active',
      headers: { authorization: `Bearer ${user.jwt}` },
    });

    expect(response.statusCode).toBe(200);
    expect(response.json().decisionsCompleted).toBe(1);
  });
});
