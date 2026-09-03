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

  it(
    'reflects an already-met milestone immediately in the create-council response for a member with pre-existing decisions (I-2)',
    async () => {
      const member = await createTestUser();
      await createKingdom(member.jwt);

      // Rack up decisions BEFORE the council exists at all -- at this point
      // maybeAdvanceCouncilMilestone is a no-op every time (no council_members
      // row yet), matching the exact "14 pre-existing decisions" scenario
      // from the milestone #7 final review's I-2 finding.
      for (let cycle = 1; cycle <= 14; cycle++) {
        await submitDecision(member.jwt, cycle);
      }

      const createResponse = await app.inject({
        method: 'POST',
        url: '/api/v1/councils',
        headers: { authorization: `Bearer ${member.jwt}` },
        payload: { name: 'Grinders' },
      });

      const body = createResponse.json();
      expect(body.totalDecisions).toBe(14);
      expect(body.milestoneThreshold).toBe(10);
      expect(body.milestoneReached).toBe(true);
      expect(body.rewardEligible).toBe(true);

      const [council] = await db.select().from(councils).where(eq(councils.id, body.id));
      expect(council.milestoneReached).toBe(true);
      const [membership] = await db.select().from(councilMembers).where(eq(councilMembers.userId, member.userId));
      expect(membership.rewardEligible).toBe(true);
    },
    30000,
  );

  it(
    'reflects an already-met milestone immediately in the join-council response for a joiner with pre-existing decisions (I-2)',
    async () => {
      const inviter = await createTestUser();
      await createKingdom(inviter.jwt);

      const createResponse = await app.inject({
        method: 'POST',
        url: '/api/v1/councils',
        headers: { authorization: `Bearer ${inviter.jwt}` },
        payload: { name: 'Grinders' },
      });
      const { joinCode } = createResponse.json();

      const joiner = await createTestUser();
      await createKingdom(joiner.jwt);

      // Same as the create-path test above, but the pre-existing decisions
      // belong to the JOINER, and the milestone is only crossed once their
      // membership is inserted (their decisions get counted into the
      // council's total for the first time).
      for (let cycle = 1; cycle <= 12; cycle++) {
        await submitDecision(joiner.jwt, cycle);
      }

      const joinResponse = await app.inject({
        method: 'POST',
        url: '/api/v1/councils/join',
        headers: { authorization: `Bearer ${joiner.jwt}` },
        payload: { joinCode },
      });

      const body = joinResponse.json();
      expect(body.totalDecisions).toBe(12);
      expect(body.milestoneThreshold).toBe(10);
      expect(body.milestoneReached).toBe(true);
      expect(body.rewardEligible).toBe(true);

      const [council] = await db.select().from(councils).where(eq(councils.id, body.id));
      expect(council.milestoneReached).toBe(true);
      const [joinerMembership] = await db.select().from(councilMembers).where(eq(councilMembers.userId, joiner.userId));
      expect(joinerMembership.rewardEligible).toBe(true);
      // The check flips milestoneReached for the council, so every CURRENT
      // member becomes reward-eligible, including the inviter who had 0
      // decisions of their own.
      const [inviterMembership] = await db.select().from(councilMembers).where(eq(councilMembers.userId, inviter.userId));
      expect(inviterMembership.rewardEligible).toBe(true);
    },
    30000,
  );
});
