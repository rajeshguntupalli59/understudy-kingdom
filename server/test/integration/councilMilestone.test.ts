import { describe, it, expect, afterEach } from 'vitest';
import { eq } from 'drizzle-orm';
import { buildApp } from '../../src/app';
import { db } from '../../src/db/client';
import { councils, councilMembers } from '../../src/db/schema';
import { createTestUser } from './helpers/testUser';
import { truncateTables } from './helpers/db';

describe('council milestone triggering (via POST /api/v1/decisions)', () => {
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

  async function submitDecision(jwt: string, cycleNumber: number): Promise<void> {
    await app.inject({
      method: 'POST',
      url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${jwt}` },
      payload: {
        cycle_number: cycleNumber,
        player_recommendation: { army: 40, trade: 30, religion: 30 },
        ruler_outcome: { mood: 55 },
        overridden: false,
      },
    });
  }

  it(
    'flips milestoneReached and grants reward_eligible to the current member once the council crosses its threshold',
    async () => {
      const member = await createTestUser();
      await createKingdom(member.jwt);

      const createResponse = await app.inject({
        method: 'POST',
        url: '/api/v1/councils',
        headers: { authorization: `Bearer ${member.jwt}` },
        payload: { name: 'Grinders' },
      });
      const { id: councilId } = createResponse.json();

      for (let cycle = 1; cycle <= 9; cycle++) {
        await submitDecision(member.jwt, cycle);
      }

      const [beforeCouncil] = await db.select().from(councils).where(eq(councils.id, councilId));
      expect(beforeCouncil.milestoneReached).toBe(false);
      const [beforeMembership] = await db.select().from(councilMembers).where(eq(councilMembers.userId, member.userId));
      expect(beforeMembership.rewardEligible).toBe(false);

      // The 10th decision crosses the default milestoneThreshold of 10.
      await submitDecision(member.jwt, 10);

      const [afterCouncil] = await db.select().from(councils).where(eq(councils.id, councilId));
      expect(afterCouncil.milestoneReached).toBe(true);
      const [afterMembership] = await db.select().from(councilMembers).where(eq(councilMembers.userId, member.userId));
      expect(afterMembership.rewardEligible).toBe(true);
    },
    30000,
  );

  it(
    'does NOT grant reward_eligible to a member who joins after the threshold was already crossed',
    async () => {
      const earlyMember = await createTestUser();
      await createKingdom(earlyMember.jwt);

      const createResponse = await app.inject({
        method: 'POST',
        url: '/api/v1/councils',
        headers: { authorization: `Bearer ${earlyMember.jwt}` },
        payload: { name: 'Grinders' },
      });
      const { joinCode } = createResponse.json();

      for (let cycle = 1; cycle <= 10; cycle++) {
        await submitDecision(earlyMember.jwt, cycle);
      }

      const [earlyMembership] = await db.select().from(councilMembers).where(eq(councilMembers.userId, earlyMember.userId));
      expect(earlyMembership.rewardEligible).toBe(true);

      const lateJoiner = await createTestUser();
      await app.inject({
        method: 'POST',
        url: '/api/v1/councils/join',
        headers: { authorization: `Bearer ${lateJoiner.jwt}` },
        payload: { joinCode },
      });

      const [lateMembership] = await db.select().from(councilMembers).where(eq(councilMembers.userId, lateJoiner.userId));
      expect(lateMembership.rewardEligible).toBe(false);

      // Confirm via the real API too, matching what the client will see.
      const statusResponse = await app.inject({
        method: 'GET',
        url: '/api/v1/councils/me',
        headers: { authorization: `Bearer ${lateJoiner.jwt}` },
      });
      expect(statusResponse.json().rewardEligible).toBe(false);
    },
    30000,
  );

  it(
    'stays idempotent once already reached -- later decisions in the same council do not error or re-flip',
    async () => {
      const member = await createTestUser();
      await createKingdom(member.jwt);

      await app.inject({
        method: 'POST',
        url: '/api/v1/councils',
        headers: { authorization: `Bearer ${member.jwt}` },
        payload: { name: 'Grinders' },
      });

      for (let cycle = 1; cycle <= 11; cycle++) {
        await submitDecision(member.jwt, cycle);
      }

      const response = await app.inject({
        method: 'POST',
        url: '/api/v1/decisions',
        headers: { authorization: `Bearer ${member.jwt}` },
        payload: {
          cycle_number: 12,
          player_recommendation: { army: 40, trade: 30, religion: 30 },
          ruler_outcome: { mood: 55 },
          overridden: false,
        },
      });

      expect(response.statusCode).toBe(201);
    },
    30000,
  );
});
