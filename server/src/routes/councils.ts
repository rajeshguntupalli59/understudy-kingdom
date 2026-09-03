import { FastifyPluginAsync } from 'fastify';
import { and, count, eq } from 'drizzle-orm';
import { db } from '../db/client';
import { councils, councilMembers, kingdoms, decisions } from '../db/schema';
import { maybeAdvanceCouncilMilestone } from './decisions';

const MAX_COUNCIL_MEMBERS = 20;
const JOIN_CODE_ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789';
const JOIN_CODE_LENGTH = 6;
const MAX_JOIN_CODE_ATTEMPTS = 5;

// Same TxExecutor derivation as kingdoms.ts -- kept local rather than
// exported/shared since this is the only other route file needing it.
type TxExecutor = Parameters<Parameters<typeof db.transaction>[0]>[0];

class AlreadyInCouncilError extends Error {}

function generateJoinCode(): string {
  let code = '';
  for (let i = 0; i < JOIN_CODE_LENGTH; i++) {
    code += JOIN_CODE_ALPHABET[Math.floor(Math.random() * JOIN_CODE_ALPHABET.length)];
  }
  return code;
}

const createCouncilSchema = {
  body: {
    type: 'object',
    required: ['name'],
    additionalProperties: false,
    properties: {
      name: { type: 'string', minLength: 1 },
    },
  },
} as const;

interface CreateCouncilBody {
  name: string;
}

const joinCouncilSchema = {
  body: {
    type: 'object',
    required: ['joinCode'],
    additionalProperties: false,
    properties: {
      joinCode: { type: 'string', minLength: 1 },
    },
  },
} as const;

interface JoinCouncilBody {
  joinCode: string;
}

/**
 * Shared response shape for all three endpoints. rewardEligible is scoped to
 * callerUserId's own council_members row, not the council as a whole -- two
 * different members of the same council can see different values for it.
 */
async function buildCouncilStatus(councilId: string, callerUserId: string) {
  const [councilRow] = await db.select().from(councils).where(eq(councils.id, councilId)).limit(1);

  const [{ value: memberCount }] = await db
    .select({ value: count() })
    .from(councilMembers)
    .where(eq(councilMembers.councilId, councilId));

  const [{ value: totalDecisions }] = await db
    .select({ value: count() })
    .from(decisions)
    .innerJoin(kingdoms, eq(kingdoms.id, decisions.kingdomId))
    .innerJoin(councilMembers, eq(councilMembers.userId, kingdoms.userId))
    .where(eq(councilMembers.councilId, councilId));

  const [callerMembership] = await db
    .select()
    .from(councilMembers)
    .where(and(eq(councilMembers.councilId, councilId), eq(councilMembers.userId, callerUserId)))
    .limit(1);

  return {
    id: councilRow.id,
    name: councilRow.name,
    joinCode: councilRow.joinCode,
    memberCount,
    totalDecisions,
    milestoneThreshold: councilRow.milestoneThreshold,
    milestoneReached: councilRow.milestoneReached,
    rewardEligible: callerMembership?.rewardEligible ?? false,
  };
}

const councilsRoutes: FastifyPluginAsync = async (fastify) => {
  fastify.post<{ Body: CreateCouncilBody }>('/api/v1/councils', { schema: createCouncilSchema }, async (request, reply) => {
    let councilId: string;
    try {
      councilId = await db.transaction(async (tx: TxExecutor) => {
        let newCouncilId: string | null = null;
        for (let attempt = 0; attempt < MAX_JOIN_CODE_ATTEMPTS && !newCouncilId; attempt++) {
          const joinCode = generateJoinCode();
          const insertedCouncils = await tx
            .insert(councils)
            .values({ name: request.body.name, joinCode })
            .onConflictDoNothing({ target: councils.joinCode })
            .returning();
          if (insertedCouncils.length > 0) {
            newCouncilId = insertedCouncils[0].id;
          }
        }
        if (!newCouncilId) {
          throw new Error('Failed to generate a unique council join code after multiple attempts');
        }

        // council_members.userId is the table's own primary key (one council
        // per user, DB-enforced). A zero-row insert here means a concurrent
        // request already created this user's membership elsewhere --
        // throwing rolls back the council insert above too (db.transaction
        // rolls back on a thrown error), so no orphaned, member-less council
        // is left behind. Mirrors kingdoms.ts's atomic-pair-insert pattern.
        const insertedMembers = await tx
          .insert(councilMembers)
          .values({ userId: request.userId, councilId: newCouncilId })
          .onConflictDoNothing({ target: councilMembers.userId })
          .returning();

        if (insertedMembers.length === 0) {
          throw new AlreadyInCouncilError();
        }

        return newCouncilId;
      });
    } catch (err) {
      if (err instanceof AlreadyInCouncilError) {
        reply.code(409);
        return { error: 'You are already in a council' };
      }
      throw err;
    }

    // The membership insert above already committed (db.transaction resolved
    // before this line runs), so the lookup inside
    // maybeAdvanceCouncilMilestone will find it. Covers a member who already
    // has enough pre-existing decisions to clear the milestone the instant
    // they create the council, rather than waiting for their next decision
    // submission -- see the I-2 finding in the milestone #7 final review.
    await maybeAdvanceCouncilMilestone(request.userId);

    reply.code(201);
    return buildCouncilStatus(councilId, request.userId);
  });

  fastify.post<{ Body: JoinCouncilBody }>('/api/v1/councils/join', { schema: joinCouncilSchema }, async (request, reply) => {
    const [council] = await db.select().from(councils).where(eq(councils.joinCode, request.body.joinCode)).limit(1);
    if (!council) {
      reply.code(404);
      return { error: 'No council found for that code' };
    }

    const [{ value: memberCount }] = await db
      .select({ value: count() })
      .from(councilMembers)
      .where(eq(councilMembers.councilId, council.id));

    if (memberCount >= MAX_COUNCIL_MEMBERS) {
      reply.code(403);
      return { error: 'That council is full' };
    }

    // council_members.userId is the table's own primary key -- trust the
    // constraint over a separate pre-check SELECT, matching kingdoms.ts's
    // and decisions.ts's established pattern. The capacity check above has a
    // narrow, accepted TOCTOU race (two concurrent joins near the cap could
    // both pass the count check and push membership 1-2 over
    // MAX_COUNCIL_MEMBERS) -- a low-stakes soft-cap overshoot, not a data
    // integrity issue, not worth a transaction for this pass.
    const inserted = await db
      .insert(councilMembers)
      .values({ userId: request.userId, councilId: council.id })
      .onConflictDoNothing({ target: councilMembers.userId })
      .returning();

    if (inserted.length === 0) {
      reply.code(409);
      return { error: 'You are already in a council' };
    }

    // Same reasoning as the create path above: the membership insert just
    // committed, so this will find it and immediately recognize a milestone
    // the joiner's pre-existing decisions already satisfy.
    await maybeAdvanceCouncilMilestone(request.userId);

    return buildCouncilStatus(council.id, request.userId);
  });

  fastify.get('/api/v1/councils/me', async (request, reply) => {
    const [membership] = await db.select().from(councilMembers).where(eq(councilMembers.userId, request.userId)).limit(1);
    if (!membership) {
      reply.code(404);
      return { error: 'Not in a council' };
    }

    return buildCouncilStatus(membership.councilId, request.userId);
  });
};

export default councilsRoutes;
