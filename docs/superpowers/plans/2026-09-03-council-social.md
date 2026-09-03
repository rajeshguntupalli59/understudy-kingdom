# Council / Social (Milestone #7) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a player create or join a join-code-based "council" with other players, and grant every member present when the council's combined decision count crosses a threshold a one-time, client-applied mood/loyalty boost.

**Architecture:** `server/` owns council membership, join codes, decision counts, and eligibility flags (three new endpoints plus a hook into the existing `POST /api/v1/decisions` handler); the Unity client owns applying the actual `RulerState` reward exactly once, locally, the same way every other mood/loyalty change already works. A third modal panel (`CouncilPanelController`) joins Duel and History in the single `CoreLoop` scene.

**Tech Stack:** Server: Fastify + Drizzle + Postgres (TypeScript), Vitest integration tests against real Supabase + real Postgres. Client: Unity 6000.3.23f1 (C#), Unity Test Framework (EditMode + PlayMode).

## Global Constraints

- Server tracks membership/counts/eligibility only; server NEVER writes to `ruler_npcs` for this feature.
- The reward is a fixed **+10 mood / +10 loyalty**, applied client-side exactly once, clamped to the existing 0-100 range (`RulerState.ApplyDelta`).
- One council per user, DB-enforced via `council_members.userId` as that table's own primary key. No leaving, renaming, or kicking a member this pass.
- Council membership is capped at **20**.
- `councils.milestoneThreshold` defaults to **10** total decisions across the council.
- No browsing UI, no repeating/tiered rewards, no rate limiting, no push notifications — all explicitly out of scope.
- Never pass `-quit` alongside `-runTests` in any Unity batch-mode command (confirmed multiple times this project: the combination exits the Editor before the test runner ever executes, silently producing no results at exit code 0).
- `server/` must be running locally (`npm run dev`, port 3000) for any PlayMode or server-integration test that hits the real backend.
- Prefer real Supabase sign-ins only where genuinely needed for a test's assertions; where a test only needs *N members present* (not N real authenticated sessions), insert `council_members` rows directly via Drizzle instead of creating N real anonymous users — this project's test suite has hit Supabase's anonymous-sign-in rate limit more than once from cumulative real sign-in volume across milestones.

---

## File Structure

**Server (`server/`):**
- `server/src/db/schema.ts` — modify: add `councils`, `councilMembers` tables.
- `server/src/routes/councils.ts` — new: `POST /api/v1/councils`, `POST /api/v1/councils/join`, `GET /api/v1/councils/me`.
- `server/src/routes/decisions.ts` — modify: after a successful decision insert, check and possibly advance the caller's council milestone.
- `server/src/app.ts` — modify: register `councilsRoutes`.
- `server/test/integration/helpers/db.ts` — modify: add `council_members`, `councils` to the TRUNCATE list.
- `server/test/integration/councils.test.ts` — new: create/join/status tests.
- `server/test/integration/councilMilestone.test.ts` — new: threshold-crossing + late-joiner-exclusion tests.

**Unity client:**
- `Assets/Scripts/Backend/CouncilResponse.cs` — new: `CouncilResponse`, `CreateCouncilRequest`, `JoinCouncilRequest` DTOs.
- `Assets/Scripts/Backend/BackendApiClient.cs` — modify: `CreateCouncil`, `JoinCouncil`, `GetCouncilStatus`.
- `Assets/Scripts/Backend/BackendSyncCoordinator.cs` — modify: `RequestCreateCouncil`, `RequestJoinCouncil`, `RequestCouncilStatus`.
- `Assets/Scripts/NPC/RulerState.cs`, `Assets/Scripts/Core/RulerSaveData.cs`, `Assets/Scripts/Core/SaveService.cs` — modify: persisted `CouncilRewardApplied` flag.
- `Assets/Scripts/UI/CoreLoopScreenController.cs` — modify: `RefreshStatusLabels` becomes `public`.
- `Assets/Scripts/UI/CouncilPanelController.cs` — new: the third modal panel, including reward application.
- `Assets/Scripts/UI/HistoryPanelController.cs` — modify: disable the new Council button while History is open (mutual exclusion, matching milestone #6's own I-2 fix).
- `Assets/Editor/CoreLoopSceneBuilder.cs` — modify: build the Council button + panel, grow the canvas, extend `Verify()`.
- Tests: `Assets/Tests/EditMode/CouncilResponseTests.cs`, `Assets/Tests/EditMode/SaveServiceTests.cs` (modified), `Assets/Tests/PlayMode/BackendApiClientCouncilTests.cs`, `Assets/Tests/PlayMode/BackendSyncCoordinatorCouncilTests.cs`, `Assets/Tests/PlayMode/CouncilPanelControllerTests.cs`, `Assets/Tests/PlayMode/CouncilPanelControllerRealDataTests.cs`, `Assets/Tests/PlayMode/HistoryPanelControllerTests.cs` (modified), `Assets/Tests/PlayMode/HistoryPanelControllerRealDataTests.cs` (modified).

---

### Task 1: `councils`/`council_members` schema + migration

**Files:**
- Modify: `server/src/db/schema.ts`
- Create: a new Drizzle migration under `server/drizzle/` (generated, not hand-written)

**Interfaces:**
- Produces: `councils` table (`id`, `name`, `joinCode`, `milestoneThreshold`, `milestoneReached`, `createdAt`) and `councilMembers` table (`userId` PK, `councilId`, `joinedAt`, `rewardEligible`), both exported from `server/src/db/schema.ts`, for Task 2/3 to import.

- [ ] **Step 1: Add the two tables to `server/src/db/schema.ts`**

Add this block at the end of `server/src/db/schema.ts` (after the existing `pvpDuels` export):

```typescript
// council_members.userId has no DB-level FK to Supabase's own auth.users --
// same reasoning as kingdoms.userId (see the comment above that table): this
// project doesn't own or migrate Supabase's auth schema.
export const councils = pgTable('councils', {
  id: uuid('id').primaryKey().defaultRandom(),
  name: text('name').notNull(),
  joinCode: text('join_code').notNull().unique(),
  milestoneThreshold: integer('milestone_threshold').notNull().default(10),
  milestoneReached: boolean('milestone_reached').notNull().default(false),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
});

// userId is this table's own primary key -- not a separate uuid id column --
// which is what makes "one council per user" a DB-enforced invariant rather
// than an application-level check: a second INSERT for the same userId can
// never succeed. rewardEligible is set true for every CURRENT member the
// moment the council's milestone_reached flips to true (see decisions.ts);
// anyone who joins afterward keeps it false forever -- see
// docs/superpowers/specs/2026-09-03-council-social-design.md.
export const councilMembers = pgTable('council_members', {
  userId: uuid('user_id').primaryKey(),
  councilId: uuid('council_id')
    .notNull()
    .references(() => councils.id),
  joinedAt: timestamp('joined_at', { withTimezone: true }).notNull().defaultNow(),
  rewardEligible: boolean('reward_eligible').notNull().default(false),
});
```

- [ ] **Step 2: Generate the migration**

Run: `cd server && npm run db:generate`
Expected: a new file appears under `server/drizzle/`, e.g. `00XX_<name>.sql`, containing `CREATE TABLE "councils" (...)` and `CREATE TABLE "council_members" (...)`. Confirm both tables and the `join_code` unique constraint are present in the generated SQL before proceeding.

- [ ] **Step 3: Apply the migration to the real dev database**

Run: `cd server && npm run db:migrate`
Expected: command completes with no errors; the migration is now applied to the real Postgres database `DATABASE_URL` points at (same external-dependency step as every prior milestone's schema change — this is not optional, later tasks' tests will fail without it).

- [ ] **Step 4: Add the new tables to the test-suite TRUNCATE list**

In `server/test/integration/helpers/db.ts`, change:

```typescript
  await db.execute(sql`TRUNCATE TABLE pvp_duels, decisions, ruler_npcs, kingdoms RESTART IDENTITY CASCADE`);
```

to:

```typescript
  await db.execute(sql`TRUNCATE TABLE council_members, councils, pvp_duels, decisions, ruler_npcs, kingdoms RESTART IDENTITY CASCADE`);
```

- [ ] **Step 5: Run the existing server test suite to confirm nothing broke**

Run: `cd server && npm test`
Expected: same pass count as before this task (no new tests yet — this task only adds schema + migration + a TRUNCATE-list update), all green, no errors from the new tables being unreferenced.

- [ ] **Step 6: Typecheck**

Run: `cd server && npm run typecheck`
Expected: `0 errors`.

- [ ] **Step 7: Commit**

```bash
git add server/src/db/schema.ts server/drizzle/ server/test/integration/helpers/db.ts
git commit -m "feat: add councils/council_members schema and migration"
```

---

### Task 2: `POST /api/v1/councils`, `POST /api/v1/councils/join`, `GET /api/v1/councils/me`

**Files:**
- Create: `server/src/routes/councils.ts`
- Modify: `server/src/app.ts` (register the new route)
- Create: `server/test/integration/councils.test.ts`

**Interfaces:**
- Consumes: `councils`, `councilMembers` from `server/src/db/schema.ts` (Task 1); `db` from `server/src/db/client.ts`.
- Produces: three routes returning the shared shape `{ id, name, joinCode, memberCount, totalDecisions, milestoneThreshold, milestoneReached, rewardEligible }`, for Task 3 (server-side reuse of the same tables) and Task 5 (Unity client DTO parity) to match exactly.

- [ ] **Step 1: Write `server/src/routes/councils.ts`**

```typescript
import { FastifyPluginAsync } from 'fastify';
import { and, count, eq } from 'drizzle-orm';
import { db } from '../db/client';
import { councils, councilMembers, kingdoms, decisions } from '../db/schema';

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
```

- [ ] **Step 2: Register the route in `server/src/app.ts`**

Change:

```typescript
import duelsRoutes from './routes/duels';
```

to:

```typescript
import duelsRoutes from './routes/duels';
import councilsRoutes from './routes/councils';
```

and change:

```typescript
    await protectedRoutes.register(duelsRoutes);
  });
```

to:

```typescript
    await protectedRoutes.register(duelsRoutes);
    await protectedRoutes.register(councilsRoutes);
  });
```

- [ ] **Step 3: Write `server/test/integration/councils.test.ts`**

```typescript
import { randomUUID } from 'crypto';
import { describe, it, expect, afterEach } from 'vitest';
import { buildApp } from '../../src/app';
import { db } from '../../src/db/client';
import { councilMembers } from '../../src/db/schema';
import { createTestUser } from './helpers/testUser';
import { truncateTables } from './helpers/db';

describe('councils routes', () => {
  const app = buildApp();

  afterEach(async () => {
    await truncateTables();
  });

  it('POST /api/v1/councils creates a council with the caller as its sole member', async () => {
    const user = await createTestUser();

    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/councils',
      headers: { authorization: `Bearer ${user.jwt}` },
      payload: { name: 'The Round Table' },
    });

    expect(response.statusCode).toBe(201);
    const body = response.json();
    expect(body.name).toBe('The Round Table');
    expect(body.joinCode).toMatch(/^[A-Z0-9]{6}$/);
    expect(body.memberCount).toBe(1);
    expect(body.totalDecisions).toBe(0);
    expect(body.milestoneThreshold).toBe(10);
    expect(body.milestoneReached).toBe(false);
    expect(body.rewardEligible).toBe(false);
  });

  it('POST /api/v1/councils returns 409 if the caller is already in a council', async () => {
    const user = await createTestUser();
    await app.inject({
      method: 'POST',
      url: '/api/v1/councils',
      headers: { authorization: `Bearer ${user.jwt}` },
      payload: { name: 'First Council' },
    });

    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/councils',
      headers: { authorization: `Bearer ${user.jwt}` },
      payload: { name: 'Second Council' },
    });

    expect(response.statusCode).toBe(409);
    expect(response.json().error).toBe('You are already in a council');
  });

  it('POST /api/v1/councils returns 400 for a malformed body', async () => {
    const user = await createTestUser();

    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/councils',
      headers: { authorization: `Bearer ${user.jwt}` },
      payload: {},
    });

    expect(response.statusCode).toBe(400);
  });

  it('POST /api/v1/councils/join adds the caller to an existing council', async () => {
    const creator = await createTestUser();
    const createResponse = await app.inject({
      method: 'POST',
      url: '/api/v1/councils',
      headers: { authorization: `Bearer ${creator.jwt}` },
      payload: { name: 'Open Council' },
    });
    const joinCode = createResponse.json().joinCode;

    const joiner = await createTestUser();
    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/councils/join',
      headers: { authorization: `Bearer ${joiner.jwt}` },
      payload: { joinCode },
    });

    expect(response.statusCode).toBe(200);
    expect(response.json().memberCount).toBe(2);
  });

  it('POST /api/v1/councils/join returns 404 for an unknown join code', async () => {
    const user = await createTestUser();

    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/councils/join',
      headers: { authorization: `Bearer ${user.jwt}` },
      payload: { joinCode: 'ZZZZZZ' },
    });

    expect(response.statusCode).toBe(404);
    expect(response.json().error).toBe('No council found for that code');
  });

  it('POST /api/v1/councils/join returns 409 if the caller is already in a council', async () => {
    const creatorA = await createTestUser();
    const createResponseA = await app.inject({
      method: 'POST',
      url: '/api/v1/councils',
      headers: { authorization: `Bearer ${creatorA.jwt}` },
      payload: { name: 'Council A' },
    });
    const joinCodeA = createResponseA.json().joinCode;

    const creatorB = await createTestUser();
    const createResponseB = await app.inject({
      method: 'POST',
      url: '/api/v1/councils',
      headers: { authorization: `Bearer ${creatorB.jwt}` },
      payload: { name: 'Council B' },
    });
    const joinCodeB = createResponseB.json().joinCode;

    const joiner = await createTestUser();
    await app.inject({
      method: 'POST',
      url: '/api/v1/councils/join',
      headers: { authorization: `Bearer ${joiner.jwt}` },
      payload: { joinCode: joinCodeA },
    });

    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/councils/join',
      headers: { authorization: `Bearer ${joiner.jwt}` },
      payload: { joinCode: joinCodeB },
    });

    expect(response.statusCode).toBe(409);
    expect(response.json().error).toBe('You are already in a council');
  });

  it('POST /api/v1/councils/join returns 403 once the council has 20 members', async () => {
    const creator = await createTestUser();
    const createResponse = await app.inject({
      method: 'POST',
      url: '/api/v1/councils',
      headers: { authorization: `Bearer ${creator.jwt}` },
      payload: { name: 'Popular Council' },
    });
    const { id: councilId, joinCode } = createResponse.json();

    // Creator is member #1; insert 19 more members directly (fabricated
    // userIds -- council_members.userId has no DB-level FK to a real users
    // table, matching kingdoms.userId's own precedent) to reach the
    // 20-member cap without 19 real Supabase anonymous sign-ins, which would
    // risk the rate limit this project's test suite has hit before.
    const fillerMembers = Array.from({ length: 19 }, () => ({
      userId: randomUUID(),
      councilId,
    }));
    await db.insert(councilMembers).values(fillerMembers);

    const rejectedJoiner = await createTestUser();
    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/councils/join',
      headers: { authorization: `Bearer ${rejectedJoiner.jwt}` },
      payload: { joinCode },
    });

    expect(response.statusCode).toBe(403);
    expect(response.json().error).toBe('That council is full');
  });

  it('GET /api/v1/councils/me returns 404 if the caller is not in a council', async () => {
    const user = await createTestUser();

    const response = await app.inject({
      method: 'GET',
      url: '/api/v1/councils/me',
      headers: { authorization: `Bearer ${user.jwt}` },
    });

    expect(response.statusCode).toBe(404);
    expect(response.json().error).toBe('Not in a council');
  });

  it('GET /api/v1/councils/me reflects real membership and join code', async () => {
    const user = await createTestUser();
    const createResponse = await app.inject({
      method: 'POST',
      url: '/api/v1/councils',
      headers: { authorization: `Bearer ${user.jwt}` },
      payload: { name: 'My Council' },
    });
    const joinCode = createResponse.json().joinCode;

    const response = await app.inject({
      method: 'GET',
      url: '/api/v1/councils/me',
      headers: { authorization: `Bearer ${user.jwt}` },
    });

    expect(response.statusCode).toBe(200);
    const body = response.json();
    expect(body.joinCode).toBe(joinCode);
    expect(body.memberCount).toBe(1);
  });
});
```

- [ ] **Step 4: Run the new tests**

Run: `cd server && npm test -- councils.test.ts`
Expected: all tests in this file pass (server must be reachable, `DATABASE_URL`/Supabase env configured, same preconditions as every prior integration test file).

- [ ] **Step 5: Run the full server test suite**

Run: `cd server && npm test`
Expected: all tests pass, previous count + the new `councils.test.ts` tests, zero failures.

- [ ] **Step 6: Typecheck**

Run: `cd server && npm run typecheck`
Expected: `0 errors`.

- [ ] **Step 7: Commit**

```bash
git add server/src/routes/councils.ts server/src/app.ts server/test/integration/councils.test.ts
git commit -m "feat: add POST /councils, POST /councils/join, GET /councils/me"
```

---

### Task 3: Council milestone triggering from `POST /api/v1/decisions`

**Files:**
- Modify: `server/src/routes/decisions.ts`
- Create: `server/test/integration/councilMilestone.test.ts`

**Interfaces:**
- Consumes: `councils`, `councilMembers` from `server/src/db/schema.ts` (Task 1).
- Produces: no new public interface — this task changes `POST /api/v1/decisions`'s side effects only, so Task 5's/Task 9's client work can rely on `GET /api/v1/councils/me`'s `milestoneReached`/`rewardEligible` fields (from Task 2) actually flipping in response to real gameplay.

- [ ] **Step 1: Add the milestone-advance helper and call it from the decision-insert handler**

In `server/src/routes/decisions.ts`, change the import line:

```typescript
import { kingdoms, decisions } from '../db/schema';
```

to:

```typescript
import { kingdoms, decisions, councils, councilMembers } from '../db/schema';
```

and change:

```typescript
import { and, desc, eq, lt } from 'drizzle-orm';
```

to:

```typescript
import { and, count, desc, eq, lt } from 'drizzle-orm';
```

Add this block right after the existing `createDecisionSchema`/`CreateDecisionBody`/`listDecisionsSchema`/`ListDecisionsQuery` declarations, before `const decisionsRoutes: FastifyPluginAsync = ...`:

```typescript
// Same TxExecutor derivation as kingdoms.ts/councils.ts -- kept local since
// this is the only place in decisions.ts needing a transaction.
type TxExecutor = Parameters<Parameters<typeof db.transaction>[0]>[0];

/**
 * Called after a decision is newly recorded (the 201 path only, never the
 * 409 duplicate path). If the caller is in a council whose milestone hasn't
 * been reached yet, recomputes the council's total decision count and, if
 * it now meets the threshold, atomically flips milestoneReached and grants
 * rewardEligible to every CURRENT member in one transaction -- guarded by
 * `WHERE milestone_reached = false` so two concurrent decisions racing to
 * cross the threshold can only ever flip it once. See
 * docs/superpowers/specs/2026-09-03-council-social-design.md.
 */
async function maybeAdvanceCouncilMilestone(userId: string): Promise<void> {
  const [membership] = await db.select().from(councilMembers).where(eq(councilMembers.userId, userId)).limit(1);
  if (!membership) {
    return;
  }

  const [council] = await db.select().from(councils).where(eq(councils.id, membership.councilId)).limit(1);
  if (!council || council.milestoneReached) {
    return;
  }

  const [{ value: totalDecisions }] = await db
    .select({ value: count() })
    .from(decisions)
    .innerJoin(kingdoms, eq(kingdoms.id, decisions.kingdomId))
    .innerJoin(councilMembers, eq(councilMembers.userId, kingdoms.userId))
    .where(eq(councilMembers.councilId, council.id));

  if (totalDecisions < council.milestoneThreshold) {
    return;
  }

  await db.transaction(async (tx: TxExecutor) => {
    const flipped = await tx
      .update(councils)
      .set({ milestoneReached: true })
      .where(and(eq(councils.id, council.id), eq(councils.milestoneReached, false)))
      .returning();

    if (flipped.length === 0) {
      // Lost the race to a concurrent request that already flipped this --
      // no-op, don't grant eligibility twice.
      return;
    }

    await tx.update(councilMembers).set({ rewardEligible: true }).where(eq(councilMembers.councilId, council.id));
  });
}
```

Then, in the `POST /api/v1/decisions` handler, change:

```typescript
      if (!decision) {
        reply.code(409);
        return { error: 'This cycle_number already has a recorded decision' };
      }

      reply.code(201);
      return { decision };
```

to:

```typescript
      if (!decision) {
        reply.code(409);
        return { error: 'This cycle_number already has a recorded decision' };
      }

      await maybeAdvanceCouncilMilestone(request.userId);

      reply.code(201);
      return { decision };
```

- [ ] **Step 2: Write `server/test/integration/councilMilestone.test.ts`**

```typescript
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
```

- [ ] **Step 3: Run the new tests**

Run: `cd server && npm test -- councilMilestone.test.ts`
Expected: all 3 tests pass.

- [ ] **Step 4: Run the full server test suite (including the existing `decisions.test.ts`, to confirm the new side effect doesn't break the pre-existing decision-recording behavior)**

Run: `cd server && npm test`
Expected: all tests pass, zero failures, zero regressions in `decisions.test.ts`.

- [ ] **Step 5: Typecheck**

Run: `cd server && npm run typecheck`
Expected: `0 errors`.

- [ ] **Step 6: Commit**

```bash
git add server/src/routes/decisions.ts server/test/integration/councilMilestone.test.ts
git commit -m "feat: advance council milestone on decision submission"
```

---

### Task 4: Unity `CouncilResponse` DTOs

**Files:**
- Create: `Assets/Scripts/Backend/CouncilResponse.cs`
- Create: `Assets/Tests/EditMode/CouncilResponseTests.cs`

**Interfaces:**
- Produces: `CouncilResponse` (`id`, `name`, `joinCode`, `memberCount`, `totalDecisions`, `milestoneThreshold`, `milestoneReached`, `rewardEligible`), `CreateCouncilRequest` (`name`), `JoinCouncilRequest` (`joinCode`) — all `[Serializable]`, `UnderstudyKingdom.Backend` namespace — for Task 5 (`BackendApiClient`) to consume.

- [ ] **Step 1: Write `Assets/Scripts/Backend/CouncilResponse.cs`**

```csharp
using System;

namespace UnderstudyKingdom.Backend
{
    // Bundles the request and response DTOs for the council endpoints in one
    // file, matching DuelRequest.cs's own precedent of grouping a feature's
    // small wire-shape types together rather than one file per type.
    [Serializable]
    public class CreateCouncilRequest
    {
        public string name;
    }

    [Serializable]
    public class JoinCouncilRequest
    {
        public string joinCode;
    }

    // Shared response shape for POST /api/v1/councils, POST
    // /api/v1/councils/join, and GET /api/v1/councils/me -- all three
    // server endpoints return this exact shape. See
    // docs/superpowers/specs/2026-09-03-council-social-design.md.
    [Serializable]
    public class CouncilResponse
    {
        public string id;
        public string name;
        public string joinCode;
        public int memberCount;
        public int totalDecisions;
        public int milestoneThreshold;
        public bool milestoneReached;
        public bool rewardEligible;
    }
}
```

- [ ] **Step 2: Write `Assets/Tests/EditMode/CouncilResponseTests.cs`**

```csharp
using NUnit.Framework;
using UnderstudyKingdom.Backend;
using UnityEngine;

namespace UnderstudyKingdom.Tests
{
    public class CouncilResponseTests
    {
        [Test]
        public void CouncilResponse_DeserializesFromServerResponseShape()
        {
            string json = "{\"id\":\"c1\",\"name\":\"Grinders\",\"joinCode\":\"ABC123\"," +
                "\"memberCount\":3,\"totalDecisions\":7,\"milestoneThreshold\":10," +
                "\"milestoneReached\":false,\"rewardEligible\":false}";

            CouncilResponse response = JsonUtility.FromJson<CouncilResponse>(json);

            Assert.AreEqual("c1", response.id);
            Assert.AreEqual("Grinders", response.name);
            Assert.AreEqual("ABC123", response.joinCode);
            Assert.AreEqual(3, response.memberCount);
            Assert.AreEqual(7, response.totalDecisions);
            Assert.AreEqual(10, response.milestoneThreshold);
            Assert.IsFalse(response.milestoneReached);
            Assert.IsFalse(response.rewardEligible);
        }

        [Test]
        public void CouncilResponse_MilestoneReachedAndRewardEligibleTrue_Deserializes()
        {
            string json = "{\"id\":\"c1\",\"name\":\"Grinders\",\"joinCode\":\"ABC123\"," +
                "\"memberCount\":2,\"totalDecisions\":10,\"milestoneThreshold\":10," +
                "\"milestoneReached\":true,\"rewardEligible\":true}";

            CouncilResponse response = JsonUtility.FromJson<CouncilResponse>(json);

            Assert.IsTrue(response.milestoneReached);
            Assert.IsTrue(response.rewardEligible);
        }

        [Test]
        public void CreateCouncilRequest_SerializesToExpectedWireShape()
        {
            var request = new CreateCouncilRequest { name = "Grinders" };
            string json = JsonUtility.ToJson(request);
            Assert.AreEqual("{\"name\":\"Grinders\"}", json);
        }

        [Test]
        public void JoinCouncilRequest_SerializesToExpectedWireShape()
        {
            var request = new JoinCouncilRequest { joinCode = "ABC123" };
            string json = JsonUtility.ToJson(request);
            Assert.AreEqual("{\"joinCode\":\"ABC123\"}", json);
        }
    }
}
```

- [ ] **Step 3: Run the EditMode tests**

Run (no `-quit`):
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testFilter CouncilResponseTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-council-dto-editmode.xml"
```
Expected: XML shows 4/4 passed, 0 failed.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Backend/CouncilResponse.cs Assets/Tests/EditMode/CouncilResponseTests.cs
git commit -m "feat: add CouncilResponse DTOs"
```

---

### Task 5: `BackendApiClient.CreateCouncil` / `JoinCouncil` / `GetCouncilStatus`

**Files:**
- Modify: `Assets/Scripts/Backend/BackendApiClient.cs`
- Create: `Assets/Tests/PlayMode/BackendApiClientCouncilTests.cs`

**Interfaces:**
- Consumes: `CouncilResponse`, `CreateCouncilRequest`, `JoinCouncilRequest` (Task 4).
- Produces: `CreateCouncil(string accessToken, string name, Action<CouncilResponse> onSuccess, Action<string> onError)`, `JoinCouncil(string accessToken, string joinCode, Action<CouncilResponse> onSuccess, Action<string> onError)`, `GetCouncilStatus(string accessToken, Action<CouncilResponse> onSuccess, Action<string> onError)` on `BackendApiClient` — for Task 6 (`BackendSyncCoordinator`) to call.

- [ ] **Step 1: Add the three methods to `Assets/Scripts/Backend/BackendApiClient.cs`**

Add this block after the existing `GetDecisionHistory`/`SendGetDecisionHistory` methods, before `private static string TryExtractServerErrorMessage(...)`:

```csharp
        /// <summary>
        /// Mirrors PostDuel's response-parsing shape (SendDuelRequest) -- the
        /// response body carries real council data, not just a status code.
        /// </summary>
        public void CreateCouncil(string accessToken, string name, Action<CouncilResponse> onSuccess, Action<string> onError)
        {
            string body = JsonUtility.ToJson(new CreateCouncilRequest { name = name });
            StartCoroutine(SendCouncilRequest("POST", $"{BackendBaseUrl}/api/v1/councils", body, accessToken, onSuccess, onError));
        }

        public void JoinCouncil(string accessToken, string joinCode, Action<CouncilResponse> onSuccess, Action<string> onError)
        {
            string body = JsonUtility.ToJson(new JoinCouncilRequest { joinCode = joinCode });
            StartCoroutine(SendCouncilRequest("POST", $"{BackendBaseUrl}/api/v1/councils/join", body, accessToken, onSuccess, onError));
        }

        private IEnumerator SendCouncilRequest(string method, string url, string jsonBody, string accessToken,
            Action<CouncilResponse> onSuccess, Action<string> onError)
        {
            using var request = new UnityWebRequest(url, method);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string message = TryExtractServerErrorMessage(request.downloadHandler.text)
                    ?? $"Council request to {url} failed: {request.result} ({request.responseCode})";
                onError?.Invoke(message);
                yield break;
            }

            CouncilResponse response;
            try
            {
                response = JsonUtility.FromJson<CouncilResponse>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Council response parse failed: {ex.Message}");
                yield break;
            }

            if (response == null || response.id == null)
            {
                onError?.Invoke("Council response missing expected fields");
                yield break;
            }

            onSuccess?.Invoke(response);
        }

        /// <summary>
        /// The second GET-based call in this project (see GetDecisionHistory).
        /// A real "Not in a council" 404 is surfaced via onError like any
        /// other non-2xx response -- the UI layer (CouncilPanelController)
        /// decides whether that specific message means "show the empty
        /// state" rather than "show an error," mirroring
        /// HistoryPanelController's own 404-vs-real-error split.
        /// </summary>
        public void GetCouncilStatus(string accessToken, Action<CouncilResponse> onSuccess, Action<string> onError)
        {
            StartCoroutine(SendGetCouncilStatus(accessToken, onSuccess, onError));
        }

        private IEnumerator SendGetCouncilStatus(string accessToken, Action<CouncilResponse> onSuccess, Action<string> onError)
        {
            string url = $"{BackendBaseUrl}/api/v1/councils/me";
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string message = TryExtractServerErrorMessage(request.downloadHandler.text)
                    ?? $"Council status request to {url} failed: {request.result} ({request.responseCode})";
                onError?.Invoke(message);
                yield break;
            }

            CouncilResponse response;
            try
            {
                response = JsonUtility.FromJson<CouncilResponse>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Council status response parse failed: {ex.Message}");
                yield break;
            }

            if (response == null || response.id == null)
            {
                onError?.Invoke("Council status response missing expected fields");
                yield break;
            }

            onSuccess?.Invoke(response);
        }

```

- [ ] **Step 2: Write `Assets/Tests/PlayMode/BackendApiClientCouncilTests.cs`**

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnderstudyKingdom.Backend;

namespace UnderstudyKingdom.Tests
{
    /// <summary>
    /// Hits the REAL local server/ and REAL Supabase project, mirroring
    /// BackendApiClientHistoryTests's structure.
    /// </summary>
    public class BackendApiClientCouncilTests
    {
        private GameObject apiClientObject;
        private BackendApiClient apiClient;
        private string jwt;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            apiClientObject = new GameObject("ApiClient");
            apiClient = apiClientObject.AddComponent<BackendApiClient>();
            apiClient.BackendBaseUrl = "http://localhost:3000";

            var authObject = new GameObject("Auth");
            var auth = authObject.AddComponent<SupabaseAuthClient>();
            auth.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            auth.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";

            SessionData session = null;
            auth.SignInAnonymously(s => session = s, err => Assert.Fail($"Sign-in failed: {err}"));
            yield return new WaitUntil(() => session != null);
            jwt = session.AccessToken;

            Object.DestroyImmediate(authObject);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(apiClientObject);
        }

        [UnityTest]
        public IEnumerator CreateCouncil_ReturnsWellFormedResult()
        {
            CouncilResponse result = null;
            string error = null;
            apiClient.CreateCouncil(jwt, "Grinders", r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.AreEqual("Grinders", result.name);
            Assert.AreEqual(1, result.memberCount);
        }

        [UnityTest]
        public IEnumerator CreateCouncil_CalledTwiceForSameUser_SecondCallReturnsAlreadyInCouncilError()
        {
            CouncilResponse first = null;
            apiClient.CreateCouncil(jwt, "First", r => first = r, err => Assert.Fail($"First create failed: {err}"));
            yield return new WaitUntil(() => first != null);

            CouncilResponse second = null;
            string error = null;
            apiClient.CreateCouncil(jwt, "Second", r => second = r, err => error = err);
            yield return new WaitUntil(() => second != null || error != null);

            Assert.IsNull(second);
            Assert.AreEqual("You are already in a council", error);
        }

        [UnityTest]
        public IEnumerator JoinCouncil_WithRealJoinCode_AddsSecondMember()
        {
            CouncilResponse created = null;
            apiClient.CreateCouncil(jwt, "Open Council", r => created = r, err => Assert.Fail($"Create failed: {err}"));
            yield return new WaitUntil(() => created != null);

            var joinerAuthObject = new GameObject("JoinerAuth");
            var joinerAuth = joinerAuthObject.AddComponent<SupabaseAuthClient>();
            joinerAuth.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            joinerAuth.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";
            SessionData joinerSession = null;
            joinerAuth.SignInAnonymously(s => joinerSession = s, err => Assert.Fail($"Joiner sign-in failed: {err}"));
            yield return new WaitUntil(() => joinerSession != null);

            CouncilResponse joined = null;
            string error = null;
            apiClient.JoinCouncil(joinerSession.AccessToken, created.joinCode, r => joined = r, err => error = err);
            yield return new WaitUntil(() => joined != null || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.AreEqual(2, joined.memberCount);

            Object.DestroyImmediate(joinerAuthObject);
        }

        [UnityTest]
        public IEnumerator GetCouncilStatus_WithNoCouncilYet_ReturnsNotInACouncilError()
        {
            CouncilResponse result = null;
            string error = null;
            apiClient.GetCouncilStatus(jwt, r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(result);
            Assert.AreEqual("Not in a council", error);
        }

        [UnityTest]
        public IEnumerator GetCouncilStatus_AfterCreating_ReturnsRealMembershipData()
        {
            CouncilResponse created = null;
            apiClient.CreateCouncil(jwt, "Grinders", r => created = r, err => Assert.Fail($"Create failed: {err}"));
            yield return new WaitUntil(() => created != null);

            CouncilResponse status = null;
            string error = null;
            apiClient.GetCouncilStatus(jwt, r => status = r, err => error = err);
            yield return new WaitUntil(() => status != null || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.AreEqual(created.id, status.id);
            Assert.AreEqual(created.joinCode, status.joinCode);
        }
    }
}
```

- [ ] **Step 3: Confirm `server/` is running locally**

Run: `curl http://localhost:3000/health`
Expected: `{"status":"ok"}`. If not running: `cd server && npm run dev` (background) first.

- [ ] **Step 4: Run the new PlayMode tests**

Run (no `-quit`):
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter BackendApiClientCouncilTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-council-apiclient-playmode.xml"
```
Expected: XML shows 6/6 passed, 0 failed.

- [ ] **Step 5: Run the full EditMode + PlayMode suite to confirm no regressions**

Run both (no `-quit`), same pattern as Step 4 but with no `-testFilter`.
Expected: EditMode and PlayMode both pass at their prior counts plus this task's new tests, zero failures.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Backend/BackendApiClient.cs Assets/Tests/PlayMode/BackendApiClientCouncilTests.cs
git commit -m "feat: add BackendApiClient.CreateCouncil/JoinCouncil/GetCouncilStatus"
```

---

### Task 6: `BackendSyncCoordinator.RequestCreateCouncil` / `RequestJoinCouncil` / `RequestCouncilStatus`

**Files:**
- Modify: `Assets/Scripts/Backend/BackendSyncCoordinator.cs`
- Create: `Assets/Tests/PlayMode/BackendSyncCoordinatorCouncilTests.cs`

**Interfaces:**
- Consumes: `EnsureFreshSession(Action onReady, Action<string> onError)` (existing, private — these three new public methods are its next callers); `BackendApiClient.CreateCouncil`/`JoinCouncil`/`GetCouncilStatus` (Task 5).
- Produces: `RequestCreateCouncil(string name, Action<CouncilResponse> onSuccess, Action<string> onError)`, `RequestJoinCouncil(string joinCode, Action<CouncilResponse> onSuccess, Action<string> onError)`, `RequestCouncilStatus(Action<CouncilResponse> onSuccess, Action<string> onError)` — for Task 8 (`CouncilPanelController`) to call.

**Note:** unlike `RequestDuel`/`RequestHistory`, these three do **not** gate on `kingdomReady` — the council endpoints never look up a kingdom server-side (see `councils.ts`), so there is no `EnsureKingdomThen...` retry branch here. Only session freshness matters.

- [ ] **Step 1: Add the three methods to `Assets/Scripts/Backend/BackendSyncCoordinator.cs`**

Add this block at the end of the class, after the existing `EnsureKingdomThenSendHistory` method, before the closing braces:

```csharp
        /// <summary>
        /// Unlike RequestDuel/RequestHistory, council endpoints never look up
        /// a kingdom server-side (see server/src/routes/councils.ts) -- only
        /// session freshness matters here, so there is no kingdomReady gate
        /// or retry branch to mirror.
        /// </summary>
        public void RequestCreateCouncil(string name, Action<CouncilResponse> onSuccess, Action<string> onError)
        {
            EnsureFreshSession(
                onReady: () => apiClient.CreateCouncil(currentSession.AccessToken, name, onSuccess, onError),
                onError: onError);
        }

        public void RequestJoinCouncil(string joinCode, Action<CouncilResponse> onSuccess, Action<string> onError)
        {
            EnsureFreshSession(
                onReady: () => apiClient.JoinCouncil(currentSession.AccessToken, joinCode, onSuccess, onError),
                onError: onError);
        }

        public void RequestCouncilStatus(Action<CouncilResponse> onSuccess, Action<string> onError)
        {
            EnsureFreshSession(
                onReady: () => apiClient.GetCouncilStatus(currentSession.AccessToken, onSuccess, onError),
                onError: onError);
        }
```

- [ ] **Step 2: Write `Assets/Tests/PlayMode/BackendSyncCoordinatorCouncilTests.cs`**

```csharp
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.Tests
{
    public class BackendSyncCoordinatorCouncilTests
    {
        private GameObject rulerObject;
        private GameObject managerObject;
        private GameObject coordinatorObject;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            rulerObject = new GameObject("Ruler");
            var ruler = rulerObject.AddComponent<RulerNpcController>();

            managerObject = new GameObject("Manager");
            var manager = managerObject.AddComponent<DecisionCycleManager>();
            manager.Ruler = ruler;

            coordinatorObject = new GameObject("Coordinator");
            var coordinator = coordinatorObject.AddComponent<BackendSyncCoordinator>();
            coordinator.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            coordinator.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";
            coordinator.BackendBaseUrl = "http://localhost:3000";
            coordinator.DecisionCycleManager = manager;

            yield return new WaitForSeconds(2f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(coordinatorObject);
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(rulerObject);

            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }
            SessionStore.Clear();
        }

        [UnityTest]
        public IEnumerator RequestCreateCouncil_WithReadySession_ReturnsWellFormedResult()
        {
            var coordinator = coordinatorObject.GetComponent<BackendSyncCoordinator>();

            CouncilResponse result = null;
            string error = null;
            coordinator.RequestCreateCouncil("Grinders", r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.AreEqual("Grinders", result.name);
            Assert.AreEqual(1, result.memberCount);
        }

        [UnityTest]
        public IEnumerator RequestCouncilStatus_WithNoCouncilYet_ReturnsNotInACouncilError()
        {
            var coordinator = coordinatorObject.GetComponent<BackendSyncCoordinator>();

            CouncilResponse result = null;
            string error = null;
            coordinator.RequestCouncilStatus(r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(result);
            Assert.AreEqual("Not in a council", error);
        }

        [UnityTest]
        public IEnumerator RequestJoinCouncil_WithUnknownCode_ReturnsRealServerError()
        {
            var coordinator = coordinatorObject.GetComponent<BackendSyncCoordinator>();

            CouncilResponse result = null;
            string error = null;
            coordinator.RequestJoinCouncil("ZZZZZZ", r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(result);
            Assert.AreEqual("No council found for that code", error);
        }
    }
}
```

- [ ] **Step 3: Run the new PlayMode tests**

Run (no `-quit`):
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter BackendSyncCoordinatorCouncilTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-council-coordinator-playmode.xml"
```
Expected: XML shows 3/3 passed, 0 failed.

- [ ] **Step 4: Run the full EditMode + PlayMode suite**

Same pattern as Task 5 Step 5.
Expected: zero failures, count grows by this task's 3 new tests.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Backend/BackendSyncCoordinator.cs Assets/Tests/PlayMode/BackendSyncCoordinatorCouncilTests.cs
git commit -m "feat: add BackendSyncCoordinator.RequestCreateCouncil/RequestJoinCouncil/RequestCouncilStatus"
```

---

### Task 7: Persisted `CouncilRewardApplied` flag

**Files:**
- Modify: `Assets/Scripts/NPC/RulerState.cs`
- Modify: `Assets/Scripts/Core/RulerSaveData.cs`
- Modify: `Assets/Scripts/Core/SaveService.cs`
- Modify: `Assets/Tests/EditMode/SaveServiceTests.cs`

**Interfaces:**
- Produces: `RulerState.CouncilRewardApplied` (bool, default `false`), persisted through the existing `SaveService.Save(RulerState)` / `SaveService.Load()` round trip — for Task 8 (`CouncilPanelController`) to read and set.

**Why here, not a new file/save path:** `RulerState` is already the one object that flows through `SaveService.Save`/`Load` in full; adding a second save file or a separate flag-only read/write path would either duplicate `SaveService`'s file I/O or risk a read-modify-write race against the existing Mood/Loyalty/Agenda fields. Threading it through the same three files exactly like `Agenda` already is keeps this a single, already-tested round trip.

- [ ] **Step 1: Add the field to `RulerState`**

In `Assets/Scripts/NPC/RulerState.cs`, change:

```csharp
        public int Mood = 50;
        public int Loyalty = 50;
        public AgendaType Agenda = AgendaType.Expansionist;
```

to:

```csharp
        public int Mood = 50;
        public int Loyalty = 50;
        public AgendaType Agenda = AgendaType.Expansionist;

        // True once the one-time council-milestone mood/loyalty reward has
        // been applied to THIS player's ruler -- prevents re-applying it on
        // every subsequent council-panel open. See
        // docs/superpowers/specs/2026-09-03-council-social-design.md.
        public bool CouncilRewardApplied = false;
```

- [ ] **Step 2: Add the field to `RulerSaveData`**

In `Assets/Scripts/Core/RulerSaveData.cs`, change:

```csharp
        public int Mood;
        public int Loyalty;
        public int Agenda;
```

to:

```csharp
        public int Mood;
        public int Loyalty;
        public int Agenda;
        public bool CouncilRewardApplied;
```

- [ ] **Step 3: Thread it through `SaveService.Save`/`Load`**

In `Assets/Scripts/Core/SaveService.cs`, change:

```csharp
            var data = new RulerSaveData
            {
                Mood = state.Mood,
                Loyalty = state.Loyalty,
                Agenda = (int)state.Agenda
            };
```

to:

```csharp
            var data = new RulerSaveData
            {
                Mood = state.Mood,
                Loyalty = state.Loyalty,
                Agenda = (int)state.Agenda,
                CouncilRewardApplied = state.CouncilRewardApplied
            };
```

and change:

```csharp
                var loaded = new RulerState
                {
                    Mood = data.Mood,
                    Loyalty = data.Loyalty,
                    Agenda = agenda
                };
```

to:

```csharp
                var loaded = new RulerState
                {
                    Mood = data.Mood,
                    Loyalty = data.Loyalty,
                    Agenda = agenda,
                    CouncilRewardApplied = data.CouncilRewardApplied
                };
```

- [ ] **Step 4: Add round-trip tests to `Assets/Tests/EditMode/SaveServiceTests.cs`**

Add these two tests inside the existing `SaveServiceTests` class, after `SaveThenLoad_RoundTripsState`:

```csharp
        [Test]
        public void SaveThenLoad_RoundTripsCouncilRewardApplied()
        {
            var original = new RulerState { Mood = 60, Loyalty = 60, Agenda = RulerState.AgendaType.Mercantile, CouncilRewardApplied = true };

            SaveService.Save(original);
            var loaded = SaveService.Load();

            Assert.IsTrue(loaded.CouncilRewardApplied);
        }

        [Test]
        public void Load_NoSaveFile_CouncilRewardAppliedDefaultsFalse()
        {
            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }

            var state = SaveService.Load();

            Assert.IsFalse(state.CouncilRewardApplied);
        }
```

- [ ] **Step 5: Run the EditMode tests**

Run (no `-quit`):
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testFilter SaveServiceTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-council-savedata-editmode.xml"
```
Expected: XML shows all `SaveServiceTests` tests passing (prior 5 + 2 new = 7/7), 0 failed.

- [ ] **Step 6: Run the full EditMode + PlayMode suite**

Expected: zero failures, count grows by this task's 2 new tests.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/NPC/RulerState.cs Assets/Scripts/Core/RulerSaveData.cs Assets/Scripts/Core/SaveService.cs Assets/Tests/EditMode/SaveServiceTests.cs
git commit -m "feat: persist CouncilRewardApplied on RulerState"
```

---

### Task 8: `CouncilPanelController`

**Files:**
- Modify: `Assets/Scripts/UI/CoreLoopScreenController.cs` (`RefreshStatusLabels` becomes `public`)
- Create: `Assets/Scripts/UI/CouncilPanelController.cs`
- Create: `Assets/Tests/PlayMode/CouncilPanelControllerTests.cs`

**Interfaces:**
- Consumes: `BackendSyncCoordinator.RequestCreateCouncil`/`RequestJoinCouncil`/`RequestCouncilStatus` (Task 6); `RulerState.CouncilRewardApplied` + `ApplyDelta` (Task 7, `RulerState.cs`); `DecisionCycleManager.Ruler.State`; `CoreLoopScreenController.RefreshStatusLabels()` (this task, widened to `public`).
- Produces: `CouncilPanelController.Initialize(Button councilButton, GameObject panelRoot, Button closeButton, GameObject notInCouncilView, GameObject inCouncilView, TMP_InputField nameInputField, Button createButton, TMP_InputField joinCodeInputField, Button joinButton, TextMeshProUGUI statusMessageText, TextMeshProUGUI nameLabel, TextMeshProUGUI joinCodeLabel, TextMeshProUGUI memberCountLabel, TextMeshProUGUI progressLabel, TextMeshProUGUI rewardStatusLabel, BackendSyncCoordinator coordinator, DecisionCycleManager manager, CoreLoopScreenController screenController, Slider armySlider, Slider tradeSlider, Slider religionSlider, Button submitButton, Button challengeButton, Button viewHistoryButton)` — for Task 9 (`CoreLoopSceneBuilder`) to call with real scene objects.

**Scope note:** this task's own test only exercises the panel's no-session error path and open/close mutual-exclusion (mirroring `HistoryPanelControllerTests`'s exact pattern). Driving the real `nameInputField`/`joinCodeInputField` through a typed value and clicking Create/Join is left to Task 9's real end-to-end test (which sets up the council via `BackendSyncCoordinator` directly, the same way `HistoryPanelControllerRealDataTests` seeds data via a direct `BackendApiClient` rather than the UI) and the mandatory manual Play Mode checkpoint — this project has no existing `TMP_InputField` precedent to model automated typed-input testing on, and the real GUI click-through is what milestone #6's own checkpoint already relies on for exactly this class of interaction.

- [ ] **Step 1: Widen `CoreLoopScreenController.RefreshStatusLabels` to `public`**

In `Assets/Scripts/UI/CoreLoopScreenController.cs`, change:

```csharp
        private void RefreshStatusLabels()
```

to:

```csharp
        public void RefreshStatusLabels()
```

- [ ] **Step 2: Write `Assets/Scripts/UI/CouncilPanelController.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Third modal panel alongside Duel and History. Server tracks
    /// membership/counts/eligibility only; this controller is the ONLY place
    /// that ever applies the council reward, and it does so client-side,
    /// exactly once, the same way every other mood/loyalty change already
    /// works. See docs/superpowers/specs/2026-09-03-council-social-design.md.
    /// </summary>
    public class CouncilPanelController : MonoBehaviour
    {
        private const string NotInCouncilErrorMessage = "Not in a council";
        private const string RewardJustAppliedMessage =
            "Your council's shared effort has lifted your ruler's spirits! (+10 mood, +10 loyalty)";
        private const string RewardAlreadyClaimedMessage = "Reward claimed";
        private const int RewardMoodDelta = 10;
        private const int RewardLoyaltyDelta = 10;

        [SerializeField] private Button councilButton;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject notInCouncilView;
        [SerializeField] private GameObject inCouncilView;
        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private Button createButton;
        [SerializeField] private TMP_InputField joinCodeInputField;
        [SerializeField] private Button joinButton;
        [SerializeField] private TextMeshProUGUI statusMessageText;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI joinCodeLabel;
        [SerializeField] private TextMeshProUGUI memberCountLabel;
        [SerializeField] private TextMeshProUGUI progressLabel;
        [SerializeField] private TextMeshProUGUI rewardStatusLabel;
        [SerializeField] private BackendSyncCoordinator coordinator;
        [SerializeField] private DecisionCycleManager manager;
        [SerializeField] private CoreLoopScreenController screenController;
        [SerializeField] private Slider armySlider;
        [SerializeField] private Slider tradeSlider;
        [SerializeField] private Slider religionSlider;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button viewHistoryButton;

        private void Start()
        {
            Bind();
        }

        /// <summary>
        /// Mirrors HistoryPanelController/DuelButtonController's Initialize
        /// pattern -- called by Start() in the real scene, and callable
        /// directly by tests to bypass Unity lifecycle timing.
        /// </summary>
        public void Initialize(
            Button councilButton,
            GameObject panelRoot,
            Button closeButton,
            GameObject notInCouncilView,
            GameObject inCouncilView,
            TMP_InputField nameInputField,
            Button createButton,
            TMP_InputField joinCodeInputField,
            Button joinButton,
            TextMeshProUGUI statusMessageText,
            TextMeshProUGUI nameLabel,
            TextMeshProUGUI joinCodeLabel,
            TextMeshProUGUI memberCountLabel,
            TextMeshProUGUI progressLabel,
            TextMeshProUGUI rewardStatusLabel,
            BackendSyncCoordinator coordinator,
            DecisionCycleManager manager,
            CoreLoopScreenController screenController,
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button submitButton,
            Button challengeButton,
            Button viewHistoryButton)
        {
            this.councilButton = councilButton;
            this.panelRoot = panelRoot;
            this.closeButton = closeButton;
            this.notInCouncilView = notInCouncilView;
            this.inCouncilView = inCouncilView;
            this.nameInputField = nameInputField;
            this.createButton = createButton;
            this.joinCodeInputField = joinCodeInputField;
            this.joinButton = joinButton;
            this.statusMessageText = statusMessageText;
            this.nameLabel = nameLabel;
            this.joinCodeLabel = joinCodeLabel;
            this.memberCountLabel = memberCountLabel;
            this.progressLabel = progressLabel;
            this.rewardStatusLabel = rewardStatusLabel;
            this.coordinator = coordinator;
            this.manager = manager;
            this.screenController = screenController;
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.submitButton = submitButton;
            this.challengeButton = challengeButton;
            this.viewHistoryButton = viewHistoryButton;

            Bind();
        }

        private void Bind()
        {
            councilButton.onClick.RemoveAllListeners();
            councilButton.onClick.AddListener(OnCouncilButtonClicked);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnClose);
            createButton.onClick.RemoveAllListeners();
            createButton.onClick.AddListener(OnCreate);
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(OnJoin);

            panelRoot.SetActive(false);
        }

        private void OnCouncilButtonClicked()
        {
            SetCoreLoopControlsInteractable(false);
            panelRoot.SetActive(true);
            notInCouncilView.SetActive(false);
            inCouncilView.SetActive(false);
            statusMessageText.text = "Loading...";

            coordinator.RequestCouncilStatus(HandleStatusResult, HandleStatusError);
        }

        private void HandleStatusResult(CouncilResponse response)
        {
            ShowInCouncilView(response);

            if (response.rewardEligible && !manager.Ruler.State.CouncilRewardApplied)
            {
                manager.Ruler.State.ApplyDelta(RewardMoodDelta, RewardLoyaltyDelta);
                manager.Ruler.State.CouncilRewardApplied = true;
                SaveService.Save(manager.Ruler.State);
                screenController.RefreshStatusLabels();
                rewardStatusLabel.text = RewardJustAppliedMessage;
            }
            else if (manager.Ruler.State.CouncilRewardApplied)
            {
                rewardStatusLabel.text = RewardAlreadyClaimedMessage;
            }
            else
            {
                rewardStatusLabel.text = string.Empty;
            }
        }

        // NotInCouncilErrorMessage must stay byte-identical to the 404 body
        // server/src/routes/councils.ts returns for GET /api/v1/councils/me
        // when the caller has no council yet -- see HistoryPanelController's
        // identical NoKingdomErrorMessage comment for the same reasoning.
        private void HandleStatusError(string error)
        {
            ShowNotInCouncilView();
            statusMessageText.text = error == NotInCouncilErrorMessage ? string.Empty : error;
        }

        private void OnCreate()
        {
            // Disable both buttons for the duration of the request, not just
            // the one clicked -- matches DuelButtonController's established
            // disable-during-request pattern, preventing the exact button
            // re-entrancy race milestone #6's final review caught (I-2).
            createButton.interactable = false;
            joinButton.interactable = false;
            statusMessageText.text = "Creating...";
            coordinator.RequestCreateCouncil(nameInputField.text, HandleCreateOrJoinResult, HandleCreateOrJoinError);
        }

        private void OnJoin()
        {
            createButton.interactable = false;
            joinButton.interactable = false;
            statusMessageText.text = "Joining...";
            coordinator.RequestJoinCouncil(joinCodeInputField.text, HandleCreateOrJoinResult, HandleCreateOrJoinError);
        }

        private void HandleCreateOrJoinResult(CouncilResponse response)
        {
            createButton.interactable = true;
            joinButton.interactable = true;
            ShowInCouncilView(response);
            // A council the player just created or joined can never have
            // rewardEligible=true for them yet (a fresh membership row
            // always starts reward_eligible=false server-side) -- no
            // reward-application check needed on this path, unlike
            // HandleStatusResult.
            rewardStatusLabel.text = manager.Ruler.State.CouncilRewardApplied ? RewardAlreadyClaimedMessage : string.Empty;
        }

        private void HandleCreateOrJoinError(string error)
        {
            createButton.interactable = true;
            joinButton.interactable = true;
            statusMessageText.text = error;
        }

        private void ShowNotInCouncilView()
        {
            inCouncilView.SetActive(false);
            notInCouncilView.SetActive(true);
        }

        private void ShowInCouncilView(CouncilResponse response)
        {
            notInCouncilView.SetActive(false);
            inCouncilView.SetActive(true);
            statusMessageText.text = string.Empty;
            nameLabel.text = response.name;
            joinCodeLabel.text = $"Join Code: {response.joinCode}";
            memberCountLabel.text = $"{response.memberCount} members";
            progressLabel.text = $"{response.totalDecisions} / {response.milestoneThreshold} decisions";
        }

        private void OnClose()
        {
            panelRoot.SetActive(false);
            SetCoreLoopControlsInteractable(true);
        }

        private void SetCoreLoopControlsInteractable(bool interactable)
        {
            councilButton.interactable = interactable;
            viewHistoryButton.interactable = interactable;
            armySlider.interactable = interactable;
            tradeSlider.interactable = interactable;
            religionSlider.interactable = interactable;
            submitButton.interactable = interactable;
            challengeButton.interactable = interactable;
        }
    }
}
```

- [ ] **Step 3: Write `Assets/Tests/PlayMode/CouncilPanelControllerTests.cs`**

```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    public class CouncilPanelControllerTests
    {
        private GameObject coordinatorObject;
        private GameObject managerObject;
        private GameObject rulerObject;
        private GameObject screenControllerObject;
        private GameObject controllerObject;
        private GameObject canvasObject;
        private GameObject panelRootObject;
        private Slider armySlider;
        private Slider tradeSlider;
        private Slider religionSlider;
        private Button submitButton;
        private Button challengeButton;
        private Button viewHistoryButton;
        private Button councilButton;
        private Button closeButton;
        private Button createButton;
        private Button joinButton;
        private GameObject notInCouncilViewObject;
        private GameObject inCouncilViewObject;
        private TMP_InputField nameInputField;
        private TMP_InputField joinCodeInputField;
        private TextMeshProUGUI statusMessageText;

        [SetUp]
        public void SetUp()
        {
            // Built inactive so Start() never runs on the coordinator --
            // currentSession stays null, giving RequestCouncilStatus's
            // synchronous no-session error path with zero network
            // dependency. Real network paths are covered by
            // BackendSyncCoordinatorCouncilTests and
            // CouncilPanelControllerRealDataTests.
            coordinatorObject = new GameObject("Coordinator");
            coordinatorObject.SetActive(false);
            var coordinator = coordinatorObject.AddComponent<BackendSyncCoordinator>();

            rulerObject = new GameObject("Ruler");
            var ruler = rulerObject.AddComponent<RulerNpcController>();

            managerObject = new GameObject("Manager");
            var manager = managerObject.AddComponent<DecisionCycleManager>();
            manager.Ruler = ruler;

            canvasObject = new GameObject("Canvas", typeof(Canvas));

            armySlider = CreateSlider("ArmySlider", 40);
            tradeSlider = CreateSlider("TradeSlider", 30);
            religionSlider = CreateSlider("ReligionSlider", 30);

            var moodLabel = CreateLabel("MoodLabel");
            var loyaltyLabel = CreateLabel("LoyaltyLabel");
            var agendaLabel = CreateLabel("AgendaLabel");
            var narrationText = CreateLabel("NarrationText");

            var submitButtonObject = new GameObject("SubmitButton", typeof(Image), typeof(Button));
            submitButtonObject.transform.SetParent(canvasObject.transform, false);
            submitButton = submitButtonObject.GetComponent<Button>();

            screenControllerObject = new GameObject("ScreenController");
            var screenController = screenControllerObject.AddComponent<CoreLoopScreenController>();
            screenController.Initialize(manager, armySlider, tradeSlider, religionSlider,
                moodLabel, loyaltyLabel, agendaLabel, narrationText, submitButton);

            var challengeButtonObject = new GameObject("ChallengeButton", typeof(Image), typeof(Button));
            challengeButtonObject.transform.SetParent(canvasObject.transform, false);
            challengeButton = challengeButtonObject.GetComponent<Button>();

            var viewHistoryButtonObject = new GameObject("ViewHistoryButton", typeof(Image), typeof(Button));
            viewHistoryButtonObject.transform.SetParent(canvasObject.transform, false);
            viewHistoryButton = viewHistoryButtonObject.GetComponent<Button>();

            var councilButtonObject = new GameObject("CouncilButton", typeof(Image), typeof(Button));
            councilButtonObject.transform.SetParent(canvasObject.transform, false);
            councilButton = councilButtonObject.GetComponent<Button>();

            panelRootObject = new GameObject("PanelRoot");
            panelRootObject.transform.SetParent(canvasObject.transform, false);

            var closeButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            closeButtonObject.transform.SetParent(panelRootObject.transform, false);
            closeButton = closeButtonObject.GetComponent<Button>();

            notInCouncilViewObject = new GameObject("NotInCouncilView");
            notInCouncilViewObject.transform.SetParent(panelRootObject.transform, false);

            var nameInputObject = new GameObject("NameInput", typeof(TMP_InputField));
            nameInputObject.transform.SetParent(notInCouncilViewObject.transform, false);
            nameInputField = nameInputObject.GetComponent<TMP_InputField>();

            var createButtonObject = new GameObject("CreateButton", typeof(Image), typeof(Button));
            createButtonObject.transform.SetParent(notInCouncilViewObject.transform, false);
            createButton = createButtonObject.GetComponent<Button>();

            var joinCodeInputObject = new GameObject("JoinCodeInput", typeof(TMP_InputField));
            joinCodeInputObject.transform.SetParent(notInCouncilViewObject.transform, false);
            joinCodeInputField = joinCodeInputObject.GetComponent<TMP_InputField>();

            var joinButtonObject = new GameObject("JoinButton", typeof(Image), typeof(Button));
            joinButtonObject.transform.SetParent(notInCouncilViewObject.transform, false);
            joinButton = joinButtonObject.GetComponent<Button>();

            inCouncilViewObject = new GameObject("InCouncilView");
            inCouncilViewObject.transform.SetParent(panelRootObject.transform, false);

            var nameLabel = CreateLabel("NameLabel", inCouncilViewObject.transform);
            var joinCodeLabel = CreateLabel("JoinCodeLabel", inCouncilViewObject.transform);
            var memberCountLabel = CreateLabel("MemberCountLabel", inCouncilViewObject.transform);
            var progressLabel = CreateLabel("ProgressLabel", inCouncilViewObject.transform);
            var rewardStatusLabel = CreateLabel("RewardStatusLabel", inCouncilViewObject.transform);
            statusMessageText = CreateLabel("StatusMessageText", panelRootObject.transform);

            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<CouncilPanelController>();
            controller.Initialize(councilButton, panelRootObject, closeButton, notInCouncilViewObject, inCouncilViewObject,
                nameInputField, createButton, joinCodeInputField, joinButton, statusMessageText,
                nameLabel, joinCodeLabel, memberCountLabel, progressLabel, rewardStatusLabel,
                coordinator, manager, screenController,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(screenControllerObject);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(coordinatorObject);
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(rulerObject);
        }

        private Slider CreateSlider(string name, float initialValue)
        {
            var sliderObject = new GameObject(name, typeof(Slider));
            sliderObject.transform.SetParent(canvasObject.transform, false);
            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(initialValue);
            return slider;
        }

        private TextMeshProUGUI CreateLabel(string name, Transform parent = null)
        {
            var labelObject = new GameObject(name, typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent != null ? parent : canvasObject.transform, false);
            return labelObject.GetComponent<TextMeshProUGUI>();
        }

        [Test]
        public void CouncilButton_WithNoSessionYet_DisablesControlsAndShowsMessage()
        {
            councilButton.onClick.Invoke();

            Assert.IsFalse(councilButton.interactable);
            Assert.IsFalse(viewHistoryButton.interactable);
            Assert.IsFalse(armySlider.interactable);
            Assert.IsFalse(tradeSlider.interactable);
            Assert.IsFalse(religionSlider.interactable);
            Assert.IsFalse(submitButton.interactable);
            Assert.IsFalse(challengeButton.interactable);
            Assert.IsTrue(panelRootObject.activeSelf);
            Assert.AreEqual("No session available yet -- try again in a moment.", statusMessageText.text);
        }

        [Test]
        public void Close_ReEnablesControlsAndHidesPanel()
        {
            councilButton.onClick.Invoke();
            closeButton.onClick.Invoke();

            Assert.IsTrue(councilButton.interactable);
            Assert.IsTrue(viewHistoryButton.interactable);
            Assert.IsTrue(armySlider.interactable);
            Assert.IsTrue(tradeSlider.interactable);
            Assert.IsTrue(religionSlider.interactable);
            Assert.IsTrue(submitButton.interactable);
            Assert.IsTrue(challengeButton.interactable);
            Assert.IsFalse(panelRootObject.activeSelf);
        }
    }
}
```

- [ ] **Step 4: Run the new PlayMode tests**

Run (no `-quit`):
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter CouncilPanelControllerTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-council-panel-playmode.xml"
```
Expected: XML shows 2/2 passed, 0 failed.

- [ ] **Step 5: Run the full EditMode + PlayMode suite**

Expected: zero failures, count grows by this task's 2 new tests.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/UI/CoreLoopScreenController.cs Assets/Scripts/UI/CouncilPanelController.cs Assets/Tests/PlayMode/CouncilPanelControllerTests.cs
git commit -m "feat: add CouncilPanelController"
```

---

### Task 9: Scene wiring, `HistoryPanelController` mutual exclusion, full regression, real end-to-end reward test, manual verification

**Files:**
- Modify: `Assets/Scripts/UI/HistoryPanelController.cs`
- Modify: `Assets/Tests/PlayMode/HistoryPanelControllerTests.cs`
- Modify: `Assets/Tests/PlayMode/HistoryPanelControllerRealDataTests.cs`
- Modify: `Assets/Editor/CoreLoopSceneBuilder.cs`
- Create: `Assets/Tests/PlayMode/CouncilPanelControllerRealDataTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1-8.
- Produces: the real, playable scene — no further consumers this milestone.

**Why `HistoryPanelController` changes land here, not Task 8:** a real Council button object doesn't exist until this task builds it in `CoreLoopSceneBuilder.Build()`; `HistoryPanelController.Initialize`'s new required parameter can only be filled with a real, meaningful value at that point, and until it's added, the project would fail to compile (the existing `historyController.Initialize(...)` call site in `CoreLoopSceneBuilder.cs` would be missing a required argument the moment the signature changes) — so both must land in the same commit.

- [ ] **Step 1: Add `councilButton` to `HistoryPanelController`**

In `Assets/Scripts/UI/HistoryPanelController.cs`, change:

```csharp
        [SerializeField] private Button submitButton;
        [SerializeField] private Button challengeButton;
```

to:

```csharp
        [SerializeField] private Button submitButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button councilButton;
```

Change the `Initialize` signature:

```csharp
        public void Initialize(
            Button viewHistoryButton,
            GameObject panelRoot,
            Button closeButton,
            TextMeshProUGUI[] rowTexts,
            BackendSyncCoordinator coordinator,
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button submitButton,
            Button challengeButton)
        {
            this.viewHistoryButton = viewHistoryButton;
            this.panelRoot = panelRoot;
            this.closeButton = closeButton;
            this.rowTexts = rowTexts;
            this.coordinator = coordinator;
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.submitButton = submitButton;
            this.challengeButton = challengeButton;

            Bind();
        }
```

to:

```csharp
        public void Initialize(
            Button viewHistoryButton,
            GameObject panelRoot,
            Button closeButton,
            TextMeshProUGUI[] rowTexts,
            BackendSyncCoordinator coordinator,
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button submitButton,
            Button challengeButton,
            Button councilButton)
        {
            this.viewHistoryButton = viewHistoryButton;
            this.panelRoot = panelRoot;
            this.closeButton = closeButton;
            this.rowTexts = rowTexts;
            this.coordinator = coordinator;
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.submitButton = submitButton;
            this.challengeButton = challengeButton;
            this.councilButton = councilButton;

            Bind();
        }
```

Change `SetCoreLoopControlsInteractable`:

```csharp
        private void SetCoreLoopControlsInteractable(bool interactable)
        {
            viewHistoryButton.interactable = interactable;
            armySlider.interactable = interactable;
            tradeSlider.interactable = interactable;
            religionSlider.interactable = interactable;
            submitButton.interactable = interactable;
            challengeButton.interactable = interactable;
        }
```

to:

```csharp
        private void SetCoreLoopControlsInteractable(bool interactable)
        {
            viewHistoryButton.interactable = interactable;
            armySlider.interactable = interactable;
            tradeSlider.interactable = interactable;
            religionSlider.interactable = interactable;
            submitButton.interactable = interactable;
            challengeButton.interactable = interactable;
            councilButton.interactable = interactable;
        }
```

- [ ] **Step 2: Update `HistoryPanelControllerTests.cs`'s call site**

In `Assets/Tests/PlayMode/HistoryPanelControllerTests.cs`, add a `councilButton` field and object next to the existing `challengeButton` one in `SetUp`:

```csharp
            var challengeButtonObject = new GameObject("ChallengeButton", typeof(Image), typeof(Button));
            challengeButtonObject.transform.SetParent(canvasObject.transform, false);
            challengeButton = challengeButtonObject.GetComponent<Button>();
```

Add immediately after:

```csharp
            var councilButtonObject = new GameObject("CouncilButton", typeof(Image), typeof(Button));
            councilButtonObject.transform.SetParent(canvasObject.transform, false);
            councilButton = councilButtonObject.GetComponent<Button>();
```

Add the field declaration next to the existing `challengeButton` field:

```csharp
        private Button challengeButton;
```

to:

```csharp
        private Button challengeButton;
        private Button councilButton;
```

Change the `Initialize` call:

```csharp
            controller.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, coordinator,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton);
```

to:

```csharp
            controller.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, coordinator,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, councilButton);
```

Add one assertion line to both existing interactable-toggle tests: in `ViewHistory_WithNoSessionYet_DisablesControlsAndShowsMessage`, add `Assert.IsFalse(councilButton.interactable);` next to the existing `Assert.IsFalse(challengeButton.interactable);` line; in `Close_ReEnablesControlsAndHidesPanel`, add `Assert.IsTrue(councilButton.interactable);` next to the existing `Assert.IsTrue(challengeButton.interactable);` line.

- [ ] **Step 3: Update `HistoryPanelControllerRealDataTests.cs`'s call site**

Same shape of change as Step 2, without any assertion changes (this file's one test doesn't assert on interactable state). Add the field declaration next to the existing `challengeButton` field:

```csharp
        private Button challengeButton;
```

to:

```csharp
        private Button challengeButton;
        private Button councilButton;
```

In `UnitySetUp`, add immediately after the existing `viewHistoryButtonObject` block:

```csharp
            var viewHistoryButtonObject = new GameObject("ViewHistoryButton", typeof(Image), typeof(Button));
            viewHistoryButtonObject.transform.SetParent(canvasObject.transform, false);
            viewHistoryButton = viewHistoryButtonObject.GetComponent<Button>();
```

Add immediately after:

```csharp
            var councilButtonObject = new GameObject("CouncilButton", typeof(Image), typeof(Button));
            councilButtonObject.transform.SetParent(canvasObject.transform, false);
            councilButton = councilButtonObject.GetComponent<Button>();
```

Change the `Initialize` call:

```csharp
            controller.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, coordinator,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton);
```

to:

```csharp
            controller.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, coordinator,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, councilButton);
```

- [ ] **Step 4: Add the `CreateInputField` helper to `CoreLoopSceneBuilder.cs`**

Add this method after the existing `CreateLabel` method:

```csharp
        private static TMP_InputField CreateInputField(Transform parent, string name, string placeholderText)
        {
            var fieldObject = new GameObject(name, typeof(Image), typeof(TMP_InputField));
            fieldObject.transform.SetParent(parent, false);
            var rect = fieldObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400f, 44f);
            fieldObject.GetComponent<Image>().color = new Color(0.9f, 0.9f, 0.9f, 1f);

            var textAreaObject = new GameObject("Text Area", typeof(RectMask2D));
            textAreaObject.transform.SetParent(fieldObject.transform, false);
            var textAreaRect = textAreaObject.GetComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10f, 6f);
            textAreaRect.offsetMax = new Vector2(-10f, -6f);

            var placeholderObject = new GameObject("Placeholder", typeof(TextMeshProUGUI));
            placeholderObject.transform.SetParent(textAreaObject.transform, false);
            var placeholderRect = placeholderObject.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.sizeDelta = Vector2.zero;
            var placeholder = placeholderObject.GetComponent<TextMeshProUGUI>();
            placeholder.text = placeholderText;
            placeholder.fontSize = 22f;
            placeholder.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;

            var textObject = new GameObject("Text", typeof(TextMeshProUGUI));
            textObject.transform.SetParent(textAreaObject.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = 22f;
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.MidlineLeft;

            var inputField = fieldObject.GetComponent<TMP_InputField>();
            inputField.textViewport = textAreaRect;
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.text = string.Empty;

            return inputField;
        }
```

- [ ] **Step 5: Grow the canvas and add the Council button + panel in `CoreLoopSceneBuilder.Build()`**

Change:

```csharp
            canvasScaler.referenceResolution = new Vector2(800f, 1400f);
```

to:

```csharp
            canvasScaler.referenceResolution = new Vector2(800f, 1600f);
```

Insert the following block right after the existing `duelController.Initialize(...)` line and before the existing `var viewHistoryButtonObject = ...` block:

```csharp
            var councilButtonObject = new GameObject("CouncilButton", typeof(Image), typeof(Button));
            councilButtonObject.transform.SetParent(canvasObject.transform, false);
            var councilButtonRect = councilButtonObject.GetComponent<RectTransform>();
            councilButtonRect.anchoredPosition = new Vector2(0f, -660f);
            councilButtonRect.sizeDelta = new Vector2(220f, 44f);
            councilButtonObject.GetComponent<Image>().color = new Color(0.5f, 0.35f, 0.65f, 1f);
            var councilButton = councilButtonObject.GetComponent<Button>();
            TextMeshProUGUI councilButtonLabel = CreateLabel(councilButtonObject.transform, "Text", 0f, "Council");
            var councilButtonLabelRect = councilButtonLabel.GetComponent<RectTransform>();
            councilButtonLabelRect.anchorMin = Vector2.zero;
            councilButtonLabelRect.anchorMax = Vector2.one;
            councilButtonLabelRect.sizeDelta = Vector2.zero;
            councilButtonLabelRect.anchoredPosition = Vector2.zero;

            var councilPanelRootObject = new GameObject("CouncilPanel", typeof(Image));
            councilPanelRootObject.transform.SetParent(canvasObject.transform, false);
            var councilPanelRect = councilPanelRootObject.GetComponent<RectTransform>();
            councilPanelRect.anchoredPosition = Vector2.zero;
            councilPanelRect.sizeDelta = new Vector2(700f, 800f);
            councilPanelRootObject.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            var councilCloseButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            councilCloseButtonObject.transform.SetParent(councilPanelRootObject.transform, false);
            var councilCloseButtonRect = councilCloseButtonObject.GetComponent<RectTransform>();
            councilCloseButtonRect.anchoredPosition = new Vector2(310f, 360f);
            councilCloseButtonRect.sizeDelta = new Vector2(60f, 40f);
            councilCloseButtonObject.GetComponent<Image>().color = new Color(0.6f, 0.3f, 0.3f, 1f);
            var councilCloseButton = councilCloseButtonObject.GetComponent<Button>();
            TextMeshProUGUI councilCloseLabel = CreateLabel(councilCloseButtonObject.transform, "Text", 0f, "X");
            var councilCloseLabelRect = councilCloseLabel.GetComponent<RectTransform>();
            councilCloseLabelRect.anchorMin = Vector2.zero;
            councilCloseLabelRect.anchorMax = Vector2.one;
            councilCloseLabelRect.sizeDelta = Vector2.zero;
            councilCloseLabelRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI councilTitleLabel = CreateLabel(councilPanelRootObject.transform, "Title", 0f, "Your Council");
            councilTitleLabel.fontSize = 28f;
            councilTitleLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 340f);

            TextMeshProUGUI councilStatusMessageText = CreateLabel(councilPanelRootObject.transform, "StatusMessageText", 0f, string.Empty);
            councilStatusMessageText.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 260f);

            var notInCouncilViewObject = new GameObject("NotInCouncilView", typeof(RectTransform));
            notInCouncilViewObject.transform.SetParent(councilPanelRootObject.transform, false);

            // Persistent field labels, not placeholder-only -- ui-ux-pro-max's
            // Quick Reference (Forms & Feedback, `input-labels`) flags
            // placeholder-only labels as an anti-pattern: the placeholder
            // text on the input fields below disappears the moment the
            // player starts typing.
            TextMeshProUGUI nameFieldLabel = CreateLabel(notInCouncilViewObject.transform, "NameFieldLabel", 0f, "Council Name");
            nameFieldLabel.fontSize = 18f;
            nameFieldLabel.alignment = TextAlignmentOptions.Left;
            var nameFieldLabelRect = nameFieldLabel.GetComponent<RectTransform>();
            nameFieldLabelRect.anchoredPosition = new Vector2(0f, 215f);
            nameFieldLabelRect.sizeDelta = new Vector2(400f, 24f);

            TMP_InputField nameInputField = CreateInputField(notInCouncilViewObject.transform, "NameInput", "Council name");
            nameInputField.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 180f);

            var createButtonObject = new GameObject("CreateButton", typeof(Image), typeof(Button));
            createButtonObject.transform.SetParent(notInCouncilViewObject.transform, false);
            var createButtonRect = createButtonObject.GetComponent<RectTransform>();
            createButtonRect.anchoredPosition = new Vector2(0f, 110f);
            createButtonRect.sizeDelta = new Vector2(220f, 44f);
            createButtonObject.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.7f, 1f);
            var createButton = createButtonObject.GetComponent<Button>();
            TextMeshProUGUI createButtonLabel = CreateLabel(createButtonObject.transform, "Text", 0f, "Create Council");
            var createButtonLabelRect = createButtonLabel.GetComponent<RectTransform>();
            createButtonLabelRect.anchorMin = Vector2.zero;
            createButtonLabelRect.anchorMax = Vector2.one;
            createButtonLabelRect.sizeDelta = Vector2.zero;
            createButtonLabelRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI joinCodeFieldLabel = CreateLabel(notInCouncilViewObject.transform, "JoinCodeFieldLabel", 0f, "Join Code");
            joinCodeFieldLabel.fontSize = 18f;
            joinCodeFieldLabel.alignment = TextAlignmentOptions.Left;
            var joinCodeFieldLabelRect = joinCodeFieldLabel.GetComponent<RectTransform>();
            joinCodeFieldLabelRect.anchoredPosition = new Vector2(0f, 35f);
            joinCodeFieldLabelRect.sizeDelta = new Vector2(400f, 24f);

            TMP_InputField joinCodeInputField = CreateInputField(notInCouncilViewObject.transform, "JoinCodeInput", "Join code");
            joinCodeInputField.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);

            var joinButtonObject = new GameObject("JoinButton", typeof(Image), typeof(Button));
            joinButtonObject.transform.SetParent(notInCouncilViewObject.transform, false);
            var joinButtonRect = joinButtonObject.GetComponent<RectTransform>();
            joinButtonRect.anchoredPosition = new Vector2(0f, -70f);
            joinButtonRect.sizeDelta = new Vector2(220f, 44f);
            joinButtonObject.GetComponent<Image>().color = new Color(0.3f, 0.6f, 0.4f, 1f);
            var joinButton = joinButtonObject.GetComponent<Button>();
            TextMeshProUGUI joinButtonLabel = CreateLabel(joinButtonObject.transform, "Text", 0f, "Join Council");
            var joinButtonLabelRect = joinButtonLabel.GetComponent<RectTransform>();
            joinButtonLabelRect.anchorMin = Vector2.zero;
            joinButtonLabelRect.anchorMax = Vector2.one;
            joinButtonLabelRect.sizeDelta = Vector2.zero;
            joinButtonLabelRect.anchoredPosition = Vector2.zero;

            var inCouncilViewObject = new GameObject("InCouncilView", typeof(RectTransform));
            inCouncilViewObject.transform.SetParent(councilPanelRootObject.transform, false);

            TextMeshProUGUI councilNameLabel = CreateLabel(inCouncilViewObject.transform, "NameLabel", 0f, string.Empty);
            councilNameLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 180f);

            TextMeshProUGUI councilJoinCodeLabel = CreateLabel(inCouncilViewObject.transform, "JoinCodeLabel", 0f, string.Empty);
            councilJoinCodeLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 120f);

            TextMeshProUGUI councilMemberCountLabel = CreateLabel(inCouncilViewObject.transform, "MemberCountLabel", 0f, string.Empty);
            councilMemberCountLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 60f);

            TextMeshProUGUI councilProgressLabel = CreateLabel(inCouncilViewObject.transform, "ProgressLabel", 0f, string.Empty);
            councilProgressLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);

            TextMeshProUGUI councilRewardStatusLabel = CreateLabel(inCouncilViewObject.transform, "RewardStatusLabel", 0f, string.Empty);
            var councilRewardStatusLabelRect = councilRewardStatusLabel.GetComponent<RectTransform>();
            councilRewardStatusLabelRect.anchoredPosition = new Vector2(0f, -80f);
            councilRewardStatusLabelRect.sizeDelta = new Vector2(640f, 80f);

            var councilControllerObject = new GameObject("CouncilPanelController");
            var councilController = councilControllerObject.AddComponent<CouncilPanelController>();
            councilController.Initialize(councilButton, councilPanelRootObject, councilCloseButton, notInCouncilViewObject, inCouncilViewObject,
                nameInputField, createButton, joinCodeInputField, joinButton, councilStatusMessageText,
                councilNameLabel, councilJoinCodeLabel, councilMemberCountLabel, councilProgressLabel, councilRewardStatusLabel,
                backendCoordinator, manager, controller,
                armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton);

```

- [ ] **Step 6: Update the existing `historyController.Initialize(...)` call**

Change:

```csharp
            historyController.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, backendCoordinator,
                armySlider, tradeSlider, religionSlider, button, duelButton);
```

to:

```csharp
            historyController.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, backendCoordinator,
                armySlider, tradeSlider, religionSlider, button, duelButton, councilButton);
```

- [ ] **Step 7: Extend `Verify()`**

Change:

```csharp
            var historyController = Object.FindFirstObjectByType<HistoryPanelController>();
            if (historyController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no HistoryPanelController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            Debug.Log("CoreLoopSceneBuilder.Verify: scene opened and controller found successfully.");
```

to:

```csharp
            var historyController = Object.FindFirstObjectByType<HistoryPanelController>();
            if (historyController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no HistoryPanelController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            var councilController = Object.FindFirstObjectByType<CouncilPanelController>();
            if (councilController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no CouncilPanelController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            Debug.Log("CoreLoopSceneBuilder.Verify: scene opened and controller found successfully.");
```

Add the missing using directive at the top of `CoreLoopSceneBuilder.cs` if not already present (it already imports `UnderstudyKingdom.UI`, which is where `CouncilPanelController` lives, so no new `using` line is needed).

- [ ] **Step 8: Write `Assets/Tests/PlayMode/CouncilPanelControllerRealDataTests.cs`**

```csharp
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    /// <summary>
    /// Real end-to-end: real Supabase sign-in, real local server/, a real
    /// council crossing its real milestone threshold. Council creation and
    /// decision submission happen directly through BackendSyncCoordinator/
    /// BackendApiClient (not through nameInputField/typed UI, which this
    /// project has no existing automated-testing precedent for -- see Task
    /// 8/9's scope note); only councilButton is actually clicked, exactly
    /// mirroring HistoryPanelControllerRealDataTests' own structure.
    /// </summary>
    public class CouncilPanelControllerRealDataTests
    {
        private GameObject rulerObject;
        private GameObject managerObject;
        private GameObject coordinatorObject;
        private GameObject screenControllerObject;
        private GameObject controllerObject;
        private GameObject canvasObject;
        private GameObject panelRootObject;
        private GameObject directApiClientObject;
        private RulerNpcController ruler;
        private Button councilButton;
        private TextMeshProUGUI rewardStatusLabel;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            rulerObject = new GameObject("Ruler");
            ruler = rulerObject.AddComponent<RulerNpcController>();

            managerObject = new GameObject("Manager");
            var manager = managerObject.AddComponent<DecisionCycleManager>();
            manager.Ruler = ruler;

            coordinatorObject = new GameObject("Coordinator");
            var coordinator = coordinatorObject.AddComponent<BackendSyncCoordinator>();
            coordinator.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            coordinator.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";
            coordinator.BackendBaseUrl = "http://localhost:3000";
            coordinator.DecisionCycleManager = manager;

            yield return new WaitForSeconds(2f);

            SessionData session = SessionStore.Load();
            Assert.IsNotNull(session, "Coordinator did not persist a session during bootstrap");

            directApiClientObject = new GameObject("DirectApiClient");
            var directApiClient = directApiClientObject.AddComponent<BackendApiClient>();
            directApiClient.BackendBaseUrl = "http://localhost:3000";

            bool councilCreated = false;
            coordinator.RequestCreateCouncil("Grinders", _ => councilCreated = true, err => Assert.Fail($"RequestCreateCouncil failed: {err}"));
            yield return new WaitUntil(() => councilCreated);

            for (int cycle = 1; cycle <= 10; cycle++)
            {
                var dto = new DecisionSyncRequest
                {
                    cycle_number = cycle,
                    player_recommendation = new PlayerRecommendationDto { army = 40, trade = 30, religion = 30 },
                    ruler_outcome = new RulerOutcomeDto { mood = 55, loyalty = 60 },
                    overridden = false
                };
                bool posted = false;
                directApiClient.PostDecision(session.AccessToken, dto, _ => posted = true, err => Assert.Fail($"PostDecision failed: {err}"));
                yield return new WaitUntil(() => posted);
            }

            canvasObject = new GameObject("Canvas", typeof(Canvas));

            var armySlider = CreateSlider("ArmySlider", 40);
            var tradeSlider = CreateSlider("TradeSlider", 30);
            var religionSlider = CreateSlider("ReligionSlider", 30);

            var moodLabel = CreateLabel("MoodLabel");
            var loyaltyLabel = CreateLabel("LoyaltyLabel");
            var agendaLabel = CreateLabel("AgendaLabel");
            var narrationText = CreateLabel("NarrationText");

            var submitButtonObject = new GameObject("SubmitButton", typeof(Image), typeof(Button));
            submitButtonObject.transform.SetParent(canvasObject.transform, false);
            var submitButton = submitButtonObject.GetComponent<Button>();

            screenControllerObject = new GameObject("ScreenController");
            var screenController = screenControllerObject.AddComponent<CoreLoopScreenController>();
            screenController.Initialize(manager, armySlider, tradeSlider, religionSlider,
                moodLabel, loyaltyLabel, agendaLabel, narrationText, submitButton);

            var challengeButtonObject = new GameObject("ChallengeButton", typeof(Image), typeof(Button));
            challengeButtonObject.transform.SetParent(canvasObject.transform, false);
            var challengeButton = challengeButtonObject.GetComponent<Button>();

            var viewHistoryButtonObject = new GameObject("ViewHistoryButton", typeof(Image), typeof(Button));
            viewHistoryButtonObject.transform.SetParent(canvasObject.transform, false);
            var viewHistoryButton = viewHistoryButtonObject.GetComponent<Button>();

            var councilButtonObject = new GameObject("CouncilButton", typeof(Image), typeof(Button));
            councilButtonObject.transform.SetParent(canvasObject.transform, false);
            councilButton = councilButtonObject.GetComponent<Button>();

            panelRootObject = new GameObject("CouncilPanel");
            panelRootObject.transform.SetParent(canvasObject.transform, false);

            var closeButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            closeButtonObject.transform.SetParent(panelRootObject.transform, false);
            var closeButton = closeButtonObject.GetComponent<Button>();

            var notInCouncilViewObject = new GameObject("NotInCouncilView");
            notInCouncilViewObject.transform.SetParent(panelRootObject.transform, false);

            var nameInputObject = new GameObject("NameInput", typeof(TMP_InputField));
            nameInputObject.transform.SetParent(notInCouncilViewObject.transform, false);
            var nameInputField = nameInputObject.GetComponent<TMP_InputField>();

            var createButtonObject = new GameObject("CreateButton", typeof(Image), typeof(Button));
            createButtonObject.transform.SetParent(notInCouncilViewObject.transform, false);
            var createButton = createButtonObject.GetComponent<Button>();

            var joinCodeInputObject = new GameObject("JoinCodeInput", typeof(TMP_InputField));
            joinCodeInputObject.transform.SetParent(notInCouncilViewObject.transform, false);
            var joinCodeInputField = joinCodeInputObject.GetComponent<TMP_InputField>();

            var joinButtonObject = new GameObject("JoinButton", typeof(Image), typeof(Button));
            joinButtonObject.transform.SetParent(notInCouncilViewObject.transform, false);
            var joinButton = joinButtonObject.GetComponent<Button>();

            var inCouncilViewObject = new GameObject("InCouncilView");
            inCouncilViewObject.transform.SetParent(panelRootObject.transform, false);

            var nameLabel = CreateLabel("NameLabel", inCouncilViewObject.transform);
            var joinCodeLabel = CreateLabel("JoinCodeLabel", inCouncilViewObject.transform);
            var memberCountLabel = CreateLabel("MemberCountLabel", inCouncilViewObject.transform);
            var progressLabel = CreateLabel("ProgressLabel", inCouncilViewObject.transform);
            rewardStatusLabel = CreateLabel("RewardStatusLabel", inCouncilViewObject.transform);
            var statusMessageText = CreateLabel("StatusMessageText", panelRootObject.transform);

            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<CouncilPanelController>();
            controller.Initialize(councilButton, panelRootObject, closeButton, notInCouncilViewObject, inCouncilViewObject,
                nameInputField, createButton, joinCodeInputField, joinButton, statusMessageText,
                nameLabel, joinCodeLabel, memberCountLabel, progressLabel, rewardStatusLabel,
                coordinator, manager, screenController,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(screenControllerObject);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(coordinatorObject);
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(rulerObject);
            Object.DestroyImmediate(directApiClientObject);

            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }
            SessionStore.Clear();
        }

        private Slider CreateSlider(string name, float initialValue)
        {
            var sliderObject = new GameObject(name, typeof(Slider));
            sliderObject.transform.SetParent(canvasObject.transform, false);
            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(initialValue);
            return slider;
        }

        private TextMeshProUGUI CreateLabel(string name, Transform parent = null)
        {
            var labelObject = new GameObject(name, typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent != null ? parent : canvasObject.transform, false);
            return labelObject.GetComponent<TextMeshProUGUI>();
        }

        [UnityTest]
        public IEnumerator CouncilButton_AfterRealThresholdCrossing_AppliesRewardAndPersists()
        {
            councilButton.onClick.Invoke();

            yield return new WaitUntil(() => rewardStatusLabel.text != string.Empty);

            Assert.AreEqual(
                "Your council's shared effort has lifted your ruler's spirits! (+10 mood, +10 loyalty)",
                rewardStatusLabel.text);
            Assert.AreEqual(60, ruler.State.Mood);
            Assert.AreEqual(60, ruler.State.Loyalty);
            Assert.IsTrue(ruler.State.CouncilRewardApplied);

            RulerState persisted = SaveService.Load();
            Assert.IsTrue(persisted.CouncilRewardApplied);
            Assert.AreEqual(60, persisted.Mood);
            Assert.AreEqual(60, persisted.Loyalty);
        }
    }
}
```

- [ ] **Step 9: Rebuild the scene**

Run (uses `-quit`, correct here — this is `-executeMethod`, not `-runTests`):
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Build
```
Expected: log line `CoreLoopSceneBuilder: saved scene to Assets/Scenes/CoreLoop.unity`, exit code 0.

- [ ] **Step 10: Verify the rebuilt scene**

Run (uses `-quit`, correct for `-executeMethod`):
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Verify
```
Expected: log line `CoreLoopSceneBuilder.Verify: scene opened and controller found successfully.`, exit code 0.

- [ ] **Step 11: Run the full EditMode + PlayMode suite (no `-quit`, per Global Constraints)**

Run both:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-council-final-editmode.xml"
```
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-council-final-playmode.xml"
```
Expected: both XML files show zero failures, and PlayMode's count includes this task's new `CouncilPanelControllerRealDataTests` test.

- [ ] **Step 12: Run the full server test suite one more time**

Run: `cd server && npm test && npm run typecheck`
Expected: all pass, `0 errors`.

- [ ] **Step 13: Commit**

```bash
git add Assets/Scripts/UI/HistoryPanelController.cs Assets/Tests/PlayMode/HistoryPanelControllerTests.cs Assets/Tests/PlayMode/HistoryPanelControllerRealDataTests.cs Assets/Editor/CoreLoopSceneBuilder.cs Assets/Tests/PlayMode/CouncilPanelControllerRealDataTests.cs Assets/Scenes/CoreLoop.unity
git commit -m "feat: wire CouncilPanelController into the CoreLoop scene"
```

- [ ] **Step 14: Manual Play Mode verification (human)**

This step cannot be scripted — it is the one thing exercising the real, rendered scene layout and real typed-input flow (`nameInputField`/`joinCodeInputField`), which Step 8's automated test deliberately does not drive (see Task 8/9's scope note). Ask the user to:
1. Confirm no other Unity Editor GUI window is open (batch-mode operations above already required this; the human checkpoint needs an interactive Editor instead).
2. Open `Assets/Scenes/CoreLoop.unity` in the Unity Editor and press Play.
3. Click "Council," type a name, click "Create Council" — confirm the panel shows the real join code, member count (1), and progress (0 / 10).
4. Submit several recommendations (existing Submit button) to push the council's decision count toward 10; reopen the Council panel periodically to watch progress update.
5. Once progress reaches 10 / 10 and the panel is reopened, confirm the reward narration appears and the Mood/Loyalty labels (top of screen) visibly increased by 10 each.
6. Confirm the Council button is disabled while the History or Duel panels are open, and vice versa (mutual exclusion).
7. Stop Play Mode; confirm no Console errors were logged.

If any step reveals a real bug (matches this project's own precedent — 3 of the last 3 milestones' manual checkpoints found genuine issues automated tests missed), fix it directly, re-verify the full suite, and ask the user to retest before proceeding to `finishing-a-development-branch`.
