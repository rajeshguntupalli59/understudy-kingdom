# Live-Ops Events (Milestone #10) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a weekly-rotating live-ops event (FR-10) with a free-to-play-completable stat-boost reward (FR-11, narrowed scope), computed entirely from existing data with no new database table.

**Architecture:** A pure server-side function (`getActiveEventWindow`) picks one of ~4 hardcoded events by ISO 8601 week number and reports the caller's live decision count within that week's date range via `GET /api/v1/events/active`. The Unity client adds a 5th modal panel (`EventPanelController`) that displays progress and applies the reward client-side exactly once, gated by a new `RulerState.ClaimedEventWeekId` field — mirroring milestone #7's Council reward pattern exactly.

**Tech Stack:** Unity 6000.3.23f1 / C# (client), Node/Fastify/Drizzle/Postgres (server), Vitest (server tests), Unity Test Framework EditMode/PlayMode (client tests).

## Global Constraints

- No premium/IAP tier, no currency system, no cosmetic rewards this pass — every event has exactly one reward, unconditionally F2P-completable.
- Event id must use real ISO 8601 week-numbering-year semantics, not calendar year — they diverge at year boundaries (e.g. Jan 1, 2027 is ISO week 53 of 2026, not week 1 of 2027).
- No new DB table — `decisionsCompleted` is computed live via `COUNT(decisions)` with a date-range filter; nothing about progress or the event itself is persisted server-side.
- The week window is a half-open interval: `createdAt >= weekStart AND createdAt < weekEnd` (weekEnd is the following week's Monday 00:00:00.000 UTC, exclusive) — never `<=`.
- Reward applied client-side exactly once, gated by `RulerState.ClaimedEventWeekId` compared against the current `eventId` — server never re-grants, never tracks claim state. `ClaimedEventWeekId` defaults to `string.Empty`, never `null` (sidesteps `JsonUtility` null-string serialization quirks; use `string.IsNullOrEmpty`/direct `==` comparison, never `!= null`).
- `EventPanelController` is explicitly NOT `DuelModalGate`-aware this pass (that gate lives on the unmerged `feat/duel-modal-gate` branch) — do not reference or import `DuelModalGate`.
- Every new/changed `Initialize()` call site across production code AND all relevant test files must be updated consistently for the new `eventsButton` parameter, or the project won't compile.
- New UI labels must be 24pt per `CoreLoopSceneBuilder.cs`'s established `CreateLabel()` rule comment; panel/section titles are the sole established exception at 28pt.
- All new interactive UI elements (buttons) must be at least 44pt/px tall, matching this project's existing button convention — the new Events panel's own close button uses `(60, 44)`, not the pre-existing History/Council panels' `(60, 40)` (that pre-existing shortfall is out of scope for this milestone; only new code follows the corrected size).
- `server/` must be running locally (`npm run dev`, port 3000) before running any PlayMode test that hits the real backend.
- Never pass `-quit` alongside `-runTests` in any Unity batch-mode command — the combination exits the Editor before the test runner executes, silently producing no results at exit code 0.

---

## Task 1: Server — ISO week rotation + hardcoded event list

**Files:**
- Create: `server/src/game/liveOpsEvents.ts`
- Test: `server/test/unit/liveOpsEvents.test.ts`

**Interfaces:**
- Produces: `EventDefinition` (`{ name: string; narration: string; objectiveDecisionCount: number; rewardMood: number; rewardLoyalty: number }`), `EVENTS: EventDefinition[]`, `IsoWeekInfo` (`{ isoWeekYear: number; isoWeek: number; weekStart: Date; weekEnd: Date }`), `getIsoWeekInfo(now: Date): IsoWeekInfo`, `ActiveEventWindow` (`{ eventId: string; definition: EventDefinition; weekStart: Date; weekEnd: Date }`), `getActiveEventWindow(now: Date): ActiveEventWindow` — all consumed by Task 2's route.

- [ ] **Step 1: Write the failing unit tests**

Create `server/test/unit/liveOpsEvents.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { getIsoWeekInfo, getActiveEventWindow, EVENTS } from '../../src/game/liveOpsEvents';

describe('liveOpsEvents ISO week rotation', () => {
  it('computes isoWeekYear/isoWeek/weekStart/weekEnd for a date in ISO week 1 of 2026', () => {
    const info = getIsoWeekInfo(new Date('2026-01-01T00:00:00.000Z'));
    expect(info.isoWeekYear).toBe(2026);
    expect(info.isoWeek).toBe(1);
    expect(info.weekStart.toISOString()).toBe('2025-12-29T00:00:00.000Z');
    expect(info.weekEnd.toISOString()).toBe('2026-01-05T00:00:00.000Z');
  });

  it('Jan 1 2027 (a Friday) falls in ISO week 53 of 2026, not week 1 of 2027 -- the week-numbering-year boundary case', () => {
    const info = getIsoWeekInfo(new Date('2027-01-01T00:00:00.000Z'));
    expect(info.isoWeekYear).toBe(2026);
    expect(info.isoWeek).toBe(53);
    expect(info.weekStart.toISOString()).toBe('2026-12-28T00:00:00.000Z');
    expect(info.weekEnd.toISOString()).toBe('2027-01-04T00:00:00.000Z');
  });

  it('Jan 4 2027 (a Monday) is the first moment of ISO week 1 of 2027', () => {
    const info = getIsoWeekInfo(new Date('2027-01-04T00:00:00.000Z'));
    expect(info.isoWeekYear).toBe(2027);
    expect(info.isoWeek).toBe(1);
    expect(info.weekStart.toISOString()).toBe('2027-01-04T00:00:00.000Z');
    expect(info.weekEnd.toISOString()).toBe('2027-01-11T00:00:00.000Z');
  });

  it('the last moment of ISO week 53/2026 (one second before rollover) still resolves to week 53', () => {
    const info = getIsoWeekInfo(new Date('2027-01-03T23:59:59.000Z'));
    expect(info.isoWeekYear).toBe(2026);
    expect(info.isoWeek).toBe(53);
  });

  it('getActiveEventWindow produces a "W<isoWeekYear>-<isoWeek>" eventId', () => {
    const window = getActiveEventWindow(new Date('2026-01-01T00:00:00.000Z'));
    expect(window.eventId).toBe('W2026-1');
    expect(window.definition).toBe(EVENTS[1]);
  });

  it('week 1 and week 53 of the same isoWeekYear rotate to the same content but produce different eventIds -- so the reward can be re-earned each real week even when narration repeats', () => {
    const week1 = getActiveEventWindow(new Date('2026-01-01T00:00:00.000Z'));
    const week53 = getActiveEventWindow(new Date('2027-01-01T00:00:00.000Z'));

    expect(week1.definition).toBe(week53.definition);
    expect(week1.eventId).not.toBe(week53.eventId);
    expect(week1.eventId).toBe('W2026-1');
    expect(week53.eventId).toBe('W2026-53');
  });

  it('every hardcoded event defines a positive objective count and positive rewards', () => {
    expect(EVENTS.length).toBeGreaterThan(0);
    for (const definition of EVENTS) {
      expect(definition.objectiveDecisionCount).toBeGreaterThan(0);
      expect(definition.rewardMood).toBeGreaterThan(0);
      expect(definition.rewardLoyalty).toBeGreaterThan(0);
      expect(definition.name.length).toBeGreaterThan(0);
      expect(definition.narration.length).toBeGreaterThan(0);
    }
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd server && npm test -- liveOpsEvents.test.ts`
Expected: FAIL — `Cannot find module '../../src/game/liveOpsEvents'`.

- [ ] **Step 3: Implement `server/src/game/liveOpsEvents.ts`**

```ts
export interface EventDefinition {
  name: string;
  narration: string;
  objectiveDecisionCount: number;
  rewardMood: number;
  rewardLoyalty: number;
}

// Fixed, hardcoded weekly-rotating content -- no DB table, no admin
// tooling, no cron job. This project has exactly one operator and no CMS;
// adding a 5th event later is a one-line change plus a deploy. See
// docs/superpowers/specs/2026-09-03-live-ops-events-design.md.
export const EVENTS: EventDefinition[] = [
  {
    name: 'Harvest Tithe',
    narration:
      "The granaries overflow with the autumn harvest, and the court expects wise stewardship. Submit 3 recommendations this week to see your kingdom through the tithe season.",
    objectiveDecisionCount: 3,
    rewardMood: 15,
    rewardLoyalty: 15,
  },
  {
    name: 'Border Skirmish',
    narration:
      "Rumors of raiders stir unrest along the frontier. Submit 3 recommendations this week to steady your ruler's resolve.",
    objectiveDecisionCount: 3,
    rewardMood: 15,
    rewardLoyalty: 15,
  },
  {
    name: "Pilgrims' Procession",
    narration:
      "A procession of pilgrims passes through your lands, testing your ruler's patience and piety. Submit 3 recommendations this week to guide them well.",
    objectiveDecisionCount: 3,
    rewardMood: 15,
    rewardLoyalty: 15,
  },
  {
    name: 'Market Fair',
    narration:
      'Merchants from distant kingdoms have set up a grand market fair. Submit 3 recommendations this week to make the most of the opportunity.',
    objectiveDecisionCount: 3,
    rewardMood: 15,
    rewardLoyalty: 15,
  },
];

export interface IsoWeekInfo {
  isoWeekYear: number;
  isoWeek: number;
  weekStart: Date;
  weekEnd: Date;
}

/**
 * Real ISO 8601 week-numbering semantics (Monday-start weeks; week 1 is the
 * week containing the year's first Thursday) -- NOT calendar-year-based.
 * isoWeekYear and the calendar year of `now` diverge at year boundaries
 * (e.g. Jan 1, 2027 is a Friday, and falls in ISO week 53 of 2026, not week
 * 1 of 2027). weekStart/weekEnd form a half-open interval: weekStart is
 * that ISO week's Monday 00:00:00.000 UTC, weekEnd is the FOLLOWING week's
 * Monday 00:00:00.000 UTC (exclusive) -- callers must filter with
 * `createdAt >= weekStart AND createdAt < weekEnd`, never `<=`. See
 * docs/superpowers/specs/2026-09-03-live-ops-events-design.md.
 */
export function getIsoWeekInfo(now: Date): IsoWeekInfo {
  const dayNr = (now.getUTCDay() + 6) % 7; // Monday=0 .. Sunday=6

  const weekStart = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() - dayNr));
  const weekEnd = new Date(weekStart.getTime() + 7 * 24 * 60 * 60 * 1000);

  // The Thursday of `now`'s own week determines both the ISO
  // week-numbering year and the week number (standard ISO 8601 algorithm).
  const thursday = new Date(weekStart.getTime() + 3 * 24 * 60 * 60 * 1000);
  const isoWeekYear = thursday.getUTCFullYear();

  const firstThursday = getFirstThursdayOfIsoYear(isoWeekYear);
  const isoWeek = 1 + Math.round((thursday.getTime() - firstThursday.getTime()) / (7 * 24 * 60 * 60 * 1000));

  return { isoWeekYear, isoWeek, weekStart, weekEnd };
}

function getFirstThursdayOfIsoYear(isoWeekYear: number): Date {
  const jan4 = new Date(Date.UTC(isoWeekYear, 0, 4));
  const jan4DayNr = (jan4.getUTCDay() + 6) % 7;
  return new Date(jan4.getTime() - jan4DayNr * 24 * 60 * 60 * 1000 + 3 * 24 * 60 * 60 * 1000);
}

export interface ActiveEventWindow {
  eventId: string;
  definition: EventDefinition;
  weekStart: Date;
  weekEnd: Date;
}

/**
 * The event id is keyed to the real ISO week (`W<isoWeekYear>-<isoWeek>`),
 * NOT to the content array index -- `definition` is selected by
 * `isoWeek % EVENTS.length` and will repeat every EVENTS.length weeks, but
 * `eventId` never repeats, so a player can re-earn the reward every real
 * week even when that week's narration happens to match a previous one.
 */
export function getActiveEventWindow(now: Date): ActiveEventWindow {
  const { isoWeekYear, isoWeek, weekStart, weekEnd } = getIsoWeekInfo(now);
  const definition = EVENTS[isoWeek % EVENTS.length];
  const eventId = `W${isoWeekYear}-${isoWeek}`;
  return { eventId, definition, weekStart, weekEnd };
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd server && npm test -- liveOpsEvents.test.ts`
Expected: PASS, 7/7.

- [ ] **Step 5: Commit**

```bash
git add server/src/game/liveOpsEvents.ts server/test/unit/liveOpsEvents.test.ts
git commit -m "feat: add ISO-week live-ops event rotation logic"
```

---

## Task 2: Server — `GET /api/v1/events/active` route

**Files:**
- Create: `server/src/routes/events.ts`
- Modify: `server/src/app.ts` (register the new route)
- Test: `server/test/integration/events.test.ts`

**Interfaces:**
- Consumes: `getActiveEventWindow(now: Date): ActiveEventWindow` and `EventDefinition` from Task 1 (`server/src/game/liveOpsEvents.ts`); `kingdoms`, `decisions` from `server/src/db/schema.ts` (existing).
- Produces: `GET /api/v1/events/active` → `200 { eventId: string; name: string; narration: string; objectiveDecisionCount: number; decisionsCompleted: number; rewardMood: number; rewardLoyalty: number }`, or `404 { error: "No kingdom found for this user" }` — this exact response shape is what Task 3's `EventResponse.cs` DTO must mirror field-for-field.

- [ ] **Step 1: Write the failing integration tests**

Create `server/test/integration/events.test.ts`:

```ts
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd server && npm test -- events.test.ts`
Expected: FAIL — 404 on all requests (route doesn't exist yet, Fastify's default 404).

- [ ] **Step 3: Implement `server/src/routes/events.ts`**

```ts
import { FastifyPluginAsync } from 'fastify';
import { and, count, eq, gte, lt } from 'drizzle-orm';
import { db } from '../db/client';
import { kingdoms, decisions } from '../db/schema';
import { getActiveEventWindow } from '../game/liveOpsEvents';

const eventsRoutes: FastifyPluginAsync = async (fastify) => {
  fastify.get('/api/v1/events/active', async (request, reply) => {
    const kingdomRows = await db.select().from(kingdoms).where(eq(kingdoms.userId, request.userId)).limit(1);
    if (kingdomRows.length === 0) {
      reply.code(404);
      return { error: 'No kingdom found for this user' };
    }
    const kingdom = kingdomRows[0];

    const { eventId, definition, weekStart, weekEnd } = getActiveEventWindow(new Date());

    const [{ value: decisionsCompleted }] = await db
      .select({ value: count() })
      .from(decisions)
      .where(
        and(
          eq(decisions.kingdomId, kingdom.id),
          gte(decisions.createdAt, weekStart),
          lt(decisions.createdAt, weekEnd),
        ),
      );

    return {
      eventId,
      name: definition.name,
      narration: definition.narration,
      objectiveDecisionCount: definition.objectiveDecisionCount,
      decisionsCompleted,
      rewardMood: definition.rewardMood,
      rewardLoyalty: definition.rewardLoyalty,
    };
  });
};

export default eventsRoutes;
```

- [ ] **Step 4: Register the route in `server/src/app.ts`**

In `server/src/app.ts`, add the import alongside the existing route imports (after `import councilsRoutes from './routes/councils';`):

```ts
import eventsRoutes from './routes/events';
```

And add the registration alongside the existing ones inside `app.register(async (protectedRoutes) => { ... })` (after `await protectedRoutes.register(councilsRoutes);`):

```ts
    await protectedRoutes.register(eventsRoutes);
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd server && npm test -- events.test.ts`
Expected: PASS, 3/3.

- [ ] **Step 6: Run the full server suite to confirm no regressions**

Run: `cd server && npm test && npm run typecheck`
Expected: All tests pass, typecheck clean.

- [ ] **Step 7: Commit**

```bash
git add server/src/routes/events.ts server/src/app.ts server/test/integration/events.test.ts
git commit -m "feat: add GET /api/v1/events/active endpoint"
```

---

## Task 3: Client — `EventResponse.cs` DTO

**Files:**
- Create: `Assets/Scripts/Backend/EventResponse.cs`
- Test: `Assets/Tests/EditMode/EventResponseTests.cs`

**Interfaces:**
- Produces: `EventResponse` (`[Serializable]` class with fields `eventId, name, narration, objectiveDecisionCount, decisionsCompleted, rewardMood, rewardLoyalty` — must match Task 2's JSON response field-for-field) — consumed by Task 4's `BackendApiClient.GetActiveEvent` and Task 7's `EventPanelController`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/EventResponseTests.cs`:

```csharp
using NUnit.Framework;
using UnderstudyKingdom.Backend;
using UnityEngine;

namespace UnderstudyKingdom.Tests
{
    public class EventResponseTests
    {
        [Test]
        public void EventResponse_DeserializesFromServerResponseShape()
        {
            string json = "{\"eventId\":\"W2026-37\",\"name\":\"Harvest Tithe\"," +
                "\"narration\":\"The granaries overflow...\",\"objectiveDecisionCount\":3," +
                "\"decisionsCompleted\":2,\"rewardMood\":15,\"rewardLoyalty\":15}";

            EventResponse response = JsonUtility.FromJson<EventResponse>(json);

            Assert.AreEqual("W2026-37", response.eventId);
            Assert.AreEqual("Harvest Tithe", response.name);
            Assert.AreEqual("The granaries overflow...", response.narration);
            Assert.AreEqual(3, response.objectiveDecisionCount);
            Assert.AreEqual(2, response.decisionsCompleted);
            Assert.AreEqual(15, response.rewardMood);
            Assert.AreEqual(15, response.rewardLoyalty);
        }

        [Test]
        public void EventResponse_DecisionsCompletedMeetingObjective_Deserializes()
        {
            string json = "{\"eventId\":\"W2026-37\",\"name\":\"Harvest Tithe\"," +
                "\"narration\":\"...\",\"objectiveDecisionCount\":3," +
                "\"decisionsCompleted\":3,\"rewardMood\":15,\"rewardLoyalty\":15}";

            EventResponse response = JsonUtility.FromJson<EventResponse>(json);

            Assert.GreaterOrEqual(response.decisionsCompleted, response.objectiveDecisionCount);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testFilter EventResponseTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-event-dto-editmode.xml"`
Expected: compile error / test not found — `EventResponse` doesn't exist yet.

- [ ] **Step 3: Implement `Assets/Scripts/Backend/EventResponse.cs`**

```csharp
using System;

namespace UnderstudyKingdom.Backend
{
    // Response shape for GET /api/v1/events/active -- field names and
    // types must match server/src/routes/events.ts's JSON response
    // exactly. See
    // docs/superpowers/specs/2026-09-03-live-ops-events-design.md.
    [Serializable]
    public class EventResponse
    {
        public string eventId;
        public string name;
        public string narration;
        public int objectiveDecisionCount;
        public int decisionsCompleted;
        public int rewardMood;
        public int rewardLoyalty;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testFilter EventResponseTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-event-dto-editmode.xml"`
Expected: XML shows 2/2 passed, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Backend/EventResponse.cs Assets/Tests/EditMode/EventResponseTests.cs
git commit -m "feat: add EventResponse DTO for the active-event endpoint"
```

---

## Task 4: Client — `BackendApiClient.GetActiveEvent`

**Files:**
- Modify: `Assets/Scripts/Backend/BackendApiClient.cs`
- Test: `Assets/Tests/PlayMode/BackendApiClientEventsTests.cs`

**Interfaces:**
- Consumes: `EventResponse` (Task 3); existing `BackendApiClient.TryExtractServerErrorMessage` (private, existing).
- Produces: `BackendApiClient.GetActiveEvent(string accessToken, Action<EventResponse> onSuccess, Action<string> onError)` — consumed by Task 5's `BackendSyncCoordinator.RequestActiveEvent`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/PlayMode/BackendApiClientEventsTests.cs` (hits the real local `server/` and real Supabase project, mirroring `BackendApiClientCouncilTests`'s structure — start `server/` with `npm run dev` in `server/` before running):

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
    /// BackendApiClientCouncilTests's structure.
    /// </summary>
    public class BackendApiClientEventsTests
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
        public IEnumerator GetActiveEvent_WithNoKingdomYet_ReturnsNoKingdomError()
        {
            EventResponse result = null;
            string error = null;
            apiClient.GetActiveEvent(jwt, r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(result);
            Assert.AreEqual("No kingdom found for this user", error);
        }

        [UnityTest]
        public IEnumerator GetActiveEvent_AfterKingdomCreated_ReturnsWellFormedResult()
        {
            apiClient.EnsureKingdom(jwt, () => { }, err => Assert.Fail($"EnsureKingdom failed: {err}"));
            yield return new WaitForSeconds(1f);

            EventResponse result = null;
            string error = null;
            apiClient.GetActiveEvent(jwt, r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.IsFalse(string.IsNullOrEmpty(result.eventId));
            Assert.IsFalse(string.IsNullOrEmpty(result.name));
            Assert.Greater(result.objectiveDecisionCount, 0);
            Assert.Greater(result.rewardMood, 0);
            Assert.Greater(result.rewardLoyalty, 0);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter BackendApiClientEventsTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-event-apiclient-playmode.xml"`
Expected: compile error — `BackendApiClient.GetActiveEvent` doesn't exist yet.

- [ ] **Step 3: Implement `GetActiveEvent` in `Assets/Scripts/Backend/BackendApiClient.cs`**

Add after the existing `GetCouncilStatus`/`SendGetCouncilStatus` pair (after line 274, before the final `TryExtractServerErrorMessage` method):

```csharp
        /// <summary>
        /// The third GET-based call in this project (see GetDecisionHistory,
        /// GetCouncilStatus). Mirrors SendGetCouncilStatus's shape exactly.
        /// </summary>
        public void GetActiveEvent(string accessToken, Action<EventResponse> onSuccess, Action<string> onError)
        {
            StartCoroutine(SendGetActiveEvent(accessToken, onSuccess, onError));
        }

        private IEnumerator SendGetActiveEvent(string accessToken, Action<EventResponse> onSuccess, Action<string> onError)
        {
            string url = $"{BackendBaseUrl}/api/v1/events/active";
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string message = TryExtractServerErrorMessage(request.downloadHandler.text)
                    ?? $"Active event request to {url} failed: {request.result} ({request.responseCode})";
                onError?.Invoke(message);
                yield break;
            }

            EventResponse response;
            try
            {
                response = JsonUtility.FromJson<EventResponse>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Active event response parse failed: {ex.Message}");
                yield break;
            }

            if (response == null || response.eventId == null)
            {
                onError?.Invoke("Active event response missing expected fields");
                yield break;
            }

            onSuccess?.Invoke(response);
        }
```

- [ ] **Step 4: Run test to verify it passes**

Ensure `server/` is running (`cd server && npm run dev` in a separate terminal), then:

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter BackendApiClientEventsTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-event-apiclient-playmode.xml"`
Expected: XML shows 2/2 passed, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Backend/BackendApiClient.cs Assets/Tests/PlayMode/BackendApiClientEventsTests.cs
git commit -m "feat: add BackendApiClient.GetActiveEvent"
```

---

## Task 5: Client — `BackendSyncCoordinator.RequestActiveEvent`

**Files:**
- Modify: `Assets/Scripts/Backend/BackendSyncCoordinator.cs`
- Test: `Assets/Tests/PlayMode/BackendSyncCoordinatorEventsTests.cs`

**Interfaces:**
- Consumes: `BackendApiClient.GetActiveEvent` (Task 4); existing `BackendSyncCoordinator.EnsureFreshSession`, `kingdomReady` gate (existing, private).
- Produces: `BackendSyncCoordinator.RequestActiveEvent(Action<EventResponse> onSuccess, Action<string> onError)` — consumed by Task 7's `EventPanelController`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/PlayMode/BackendSyncCoordinatorEventsTests.cs`:

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
    public class BackendSyncCoordinatorEventsTests
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
        public IEnumerator RequestActiveEvent_WithReadySession_ReturnsWellFormedResult()
        {
            var coordinator = coordinatorObject.GetComponent<BackendSyncCoordinator>();

            EventResponse result = null;
            string error = null;
            coordinator.RequestActiveEvent(r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.IsFalse(string.IsNullOrEmpty(result.eventId));
            Assert.Greater(result.objectiveDecisionCount, 0);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter BackendSyncCoordinatorEventsTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-event-coordinator-playmode.xml"`
Expected: compile error — `RequestActiveEvent` doesn't exist yet.

- [ ] **Step 3: Implement `RequestActiveEvent` in `Assets/Scripts/Backend/BackendSyncCoordinator.cs`**

Add after `RequestCouncilStatus` (after line 327, before the closing braces of the class):

```csharp
        /// <summary>
        /// Mirrors RequestHistory's structure: refresh-if-needed via the
        /// shared EnsureFreshSession, then the shared kingdomReady gate,
        /// then the send -- like Duel/History (unlike councils), this
        /// endpoint needs kingdomId server-side to compute progress.
        /// </summary>
        public void RequestActiveEvent(Action<EventResponse> onSuccess, Action<string> onError)
        {
            EnsureFreshSession(
                onReady: () => EnsureKingdomThenSendActiveEvent(onSuccess, onError),
                onError: onError);
        }

        private void EnsureKingdomThenSendActiveEvent(Action<EventResponse> onSuccess, Action<string> onError)
        {
            if (!kingdomReady)
            {
                apiClient.EnsureKingdom(currentSession.AccessToken,
                    onSuccess: () =>
                    {
                        kingdomReady = true;
                        apiClient.GetActiveEvent(currentSession.AccessToken, onSuccess, onError);
                    },
                    onError: err => onError?.Invoke($"Your kingdom isn't ready yet: {err}"));
                return;
            }

            apiClient.GetActiveEvent(currentSession.AccessToken, onSuccess, onError);
        }
```

- [ ] **Step 4: Run test to verify it passes**

Ensure `server/` is running, then:

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter BackendSyncCoordinatorEventsTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-event-coordinator-playmode.xml"`
Expected: XML shows 1/1 passed, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Backend/BackendSyncCoordinator.cs Assets/Tests/PlayMode/BackendSyncCoordinatorEventsTests.cs
git commit -m "feat: add BackendSyncCoordinator.RequestActiveEvent"
```

---

## Task 6: Client — `RulerState.ClaimedEventWeekId` persistence

**Files:**
- Modify: `Assets/Scripts/NPC/RulerState.cs`
- Modify: `Assets/Scripts/Core/RulerSaveData.cs`
- Modify: `Assets/Scripts/Core/SaveService.cs`
- Test: `Assets/Tests/EditMode/SaveServiceTests.cs`

**Interfaces:**
- Produces: `RulerState.ClaimedEventWeekId` (`public string`, defaults to `string.Empty`, never `null`) — consumed by Task 7's `EventPanelController`.

- [ ] **Step 1: Write the failing tests**

In `Assets/Tests/EditMode/SaveServiceTests.cs`, add after `Load_NoSaveFile_TutorialCompletedDefaultsFalse` (after line 92, before `Load_CorruptFile_ReturnsDefaultState`):

```csharp
        [Test]
        public void SaveThenLoad_RoundTripsClaimedEventWeekId()
        {
            var original = new RulerState { Mood = 55, Loyalty = 55, Agenda = RulerState.AgendaType.Expansionist, ClaimedEventWeekId = "W2026-37" };

            SaveService.Save(original);
            var loaded = SaveService.Load();

            Assert.AreEqual("W2026-37", loaded.ClaimedEventWeekId);
        }

        [Test]
        public void Load_NoSaveFile_ClaimedEventWeekIdDefaultsToEmptyString()
        {
            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }

            var state = SaveService.Load();

            Assert.AreEqual(string.Empty, state.ClaimedEventWeekId);
        }

        [Test]
        public void Load_SaveFileMissingClaimedEventWeekId_DefaultsToEmptyStringNotNull()
        {
            // Simulates a save file written before this milestone shipped --
            // RulerSaveData's ClaimedEventWeekId field is left at its C#
            // default (null) since it's never explicitly set here, matching
            // how a real pre-milestone-10 save would deserialize.
            var preMilestone10Save = new RulerSaveData { Mood = 50, Loyalty = 50, Agenda = 0 };
            System.IO.File.WriteAllText(SaveService.SavePath, UnityEngine.JsonUtility.ToJson(preMilestone10Save));

            var state = SaveService.Load();

            Assert.AreEqual(string.Empty, state.ClaimedEventWeekId);
            Assert.IsNotNull(state.ClaimedEventWeekId);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testFilter SaveServiceTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-event-savedata-editmode.xml"`
Expected: compile error — `RulerState.ClaimedEventWeekId` doesn't exist yet.

- [ ] **Step 3: Add the field to `Assets/Scripts/NPC/RulerState.cs`**

Add after `TutorialCompleted` (after line 35, before `ApplyDelta`):

```csharp
        // Id (format "W<isoWeekYear>-<isoWeek>") of the live-ops event whose
        // reward has already been applied to THIS player's ruler -- compared
        // against the CURRENT active event's id so the reward is granted
        // once per real calendar week, even though the rotating event
        // list's content repeats every EVENTS.length weeks. Empty string
        // (never null -- sidesteps JsonUtility's string-null serialization
        // quirks) means "nothing claimed yet." See
        // docs/superpowers/specs/2026-09-03-live-ops-events-design.md.
        public string ClaimedEventWeekId = string.Empty;
```

- [ ] **Step 4: Add the field to `Assets/Scripts/Core/RulerSaveData.cs`**

Add after `TutorialCompleted` (after line 25, before the closing brace):

```csharp
        public string ClaimedEventWeekId;
```

- [ ] **Step 5: Thread the field through `Assets/Scripts/Core/SaveService.cs`**

In `Save` (line 27-38), add `ClaimedEventWeekId = state.ClaimedEventWeekId` to the `RulerSaveData` object literal (after `TutorialCompleted = state.TutorialCompleted`):

```csharp
        public static void Save(RulerState state)
        {
            var data = new RulerSaveData
            {
                Mood = state.Mood,
                Loyalty = state.Loyalty,
                Agenda = (int)state.Agenda,
                CouncilRewardApplied = state.CouncilRewardApplied,
                TutorialCompleted = state.TutorialCompleted,
                ClaimedEventWeekId = state.ClaimedEventWeekId
            };
            File.WriteAllText(SavePath, JsonUtility.ToJson(data));
        }
```

In `Load` (line 40-87), add `ClaimedEventWeekId = data.ClaimedEventWeekId ?? string.Empty` to the `RulerState` object literal (after `TutorialCompleted = data.TutorialCompleted`) — the `?? string.Empty` guard is required because a save file written before this milestone has no `ClaimedEventWeekId` in its JSON, so `JsonUtility.FromJson` leaves `data.ClaimedEventWeekId` at its C# default of `null`:

```csharp
                var loaded = new RulerState
                {
                    Mood = data.Mood,
                    Loyalty = data.Loyalty,
                    Agenda = agenda,
                    CouncilRewardApplied = data.CouncilRewardApplied,
                    TutorialCompleted = data.TutorialCompleted,
                    ClaimedEventWeekId = data.ClaimedEventWeekId ?? string.Empty
                };
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testFilter SaveServiceTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-event-savedata-editmode.xml"`
Expected: XML shows all `SaveServiceTests` passing (prior 9 + 3 new = 12/12), 0 failed.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/NPC/RulerState.cs Assets/Scripts/Core/RulerSaveData.cs Assets/Scripts/Core/SaveService.cs Assets/Tests/EditMode/SaveServiceTests.cs
git commit -m "feat: persist RulerState.ClaimedEventWeekId"
```

---

## Task 7: Client — `EventPanelController`

**Files:**
- Create: `Assets/Scripts/UI/EventPanelController.cs`
- Test: `Assets/Tests/PlayMode/EventPanelControllerTests.cs`

**Interfaces:**
- Consumes: `EventResponse` (Task 3), `BackendSyncCoordinator.RequestActiveEvent` (Task 5), `RulerState.ClaimedEventWeekId` (Task 6), `DecisionCycleManager.Ruler.State` (existing), `CoreLoopScreenController.RefreshStatusLabels()` (existing), `SaveService.Save` (existing).
- Produces: `EventPanelController.Initialize(Button eventsButton, GameObject panelRoot, Button closeButton, TextMeshProUGUI nameLabel, TextMeshProUGUI narrationLabel, TextMeshProUGUI progressLabel, TextMeshProUGUI statusMessageText, Button claimButton, BackendSyncCoordinator coordinator, DecisionCycleManager manager, CoreLoopScreenController screenController, Slider armySlider, Slider tradeSlider, Slider religionSlider, Button submitButton, Button challengeButton, Button viewHistoryButton, Button councilButton)` — consumed by Task 9's `CoreLoopSceneBuilder` and Task 10's real-data test.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/PlayMode/EventPanelControllerTests.cs`:

```csharp
using System.Reflection;
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
    public class EventPanelControllerTests
    {
        private GameObject coordinatorObject;
        private GameObject managerObject;
        private GameObject rulerObject;
        private GameObject screenControllerObject;
        private GameObject controllerObject;
        private GameObject canvasObject;
        private GameObject panelRootObject;
        private RulerNpcController ruler;
        private Slider armySlider;
        private Slider tradeSlider;
        private Slider religionSlider;
        private Button submitButton;
        private Button challengeButton;
        private Button viewHistoryButton;
        private Button councilButton;
        private Button eventsButton;
        private Button closeButton;
        private Button claimButton;
        private TextMeshProUGUI nameLabel;
        private TextMeshProUGUI narrationLabel;
        private TextMeshProUGUI progressLabel;
        private TextMeshProUGUI statusMessageText;
        private EventPanelController controller;

        [SetUp]
        public void SetUp()
        {
            // Built inactive so Start() never runs on the coordinator --
            // currentSession stays null, giving RequestActiveEvent's
            // synchronous no-session error path with zero network
            // dependency. Real network paths are covered by
            // BackendSyncCoordinatorEventsTests and
            // EventPanelControllerRealDataTests.
            coordinatorObject = new GameObject("Coordinator");
            coordinatorObject.SetActive(false);
            var coordinator = coordinatorObject.AddComponent<BackendSyncCoordinator>();

            rulerObject = new GameObject("Ruler");
            ruler = rulerObject.AddComponent<RulerNpcController>();

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

            var eventsButtonObject = new GameObject("EventsButton", typeof(Image), typeof(Button));
            eventsButtonObject.transform.SetParent(canvasObject.transform, false);
            eventsButton = eventsButtonObject.GetComponent<Button>();

            panelRootObject = new GameObject("PanelRoot");
            panelRootObject.transform.SetParent(canvasObject.transform, false);

            var closeButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            closeButtonObject.transform.SetParent(panelRootObject.transform, false);
            closeButton = closeButtonObject.GetComponent<Button>();

            nameLabel = CreateLabel("NameLabel", panelRootObject.transform);
            narrationLabel = CreateLabel("NarrationLabel", panelRootObject.transform);
            progressLabel = CreateLabel("ProgressLabel", panelRootObject.transform);
            statusMessageText = CreateLabel("StatusMessageText", panelRootObject.transform);

            var claimButtonObject = new GameObject("ClaimButton", typeof(Image), typeof(Button));
            claimButtonObject.transform.SetParent(panelRootObject.transform, false);
            claimButton = claimButtonObject.GetComponent<Button>();

            controllerObject = new GameObject("Controller");
            controller = controllerObject.AddComponent<EventPanelController>();
            controller.Initialize(eventsButton, panelRootObject, closeButton, nameLabel, narrationLabel,
                progressLabel, statusMessageText, claimButton, coordinator, manager, screenController,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, councilButton);
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

        private void InvokeHandleResult(EventResponse response)
        {
            MethodInfo handleResult = typeof(EventPanelController).GetMethod(
                "HandleResult", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(handleResult, "HandleResult method not found -- EventPanelController internals changed");
            handleResult.Invoke(controller, new object[] { response });
        }

        [Test]
        public void EventsButton_WithNoSessionYet_DisablesControlsAndShowsMessage()
        {
            eventsButton.onClick.Invoke();

            Assert.IsFalse(eventsButton.interactable);
            Assert.IsFalse(viewHistoryButton.interactable);
            Assert.IsFalse(councilButton.interactable);
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
            eventsButton.onClick.Invoke();
            closeButton.onClick.Invoke();

            Assert.IsTrue(eventsButton.interactable);
            Assert.IsTrue(viewHistoryButton.interactable);
            Assert.IsTrue(councilButton.interactable);
            Assert.IsTrue(armySlider.interactable);
            Assert.IsTrue(tradeSlider.interactable);
            Assert.IsTrue(religionSlider.interactable);
            Assert.IsTrue(submitButton.interactable);
            Assert.IsTrue(challengeButton.interactable);
            Assert.IsFalse(panelRootObject.activeSelf);
        }

        [Test]
        public void HandleResult_BelowThreshold_ClaimButtonStaysDisabled()
        {
            InvokeHandleResult(new EventResponse
            {
                eventId = "W2026-37",
                name = "Harvest Tithe",
                narration = "...",
                objectiveDecisionCount = 3,
                decisionsCompleted = 2,
                rewardMood = 15,
                rewardLoyalty = 15
            });

            Assert.IsFalse(claimButton.interactable);
            Assert.AreEqual("2 / 3 decisions", progressLabel.text);
        }

        [Test]
        public void HandleResult_AtThreshold_ClaimButtonBecomesInteractable()
        {
            InvokeHandleResult(new EventResponse
            {
                eventId = "W2026-37",
                name = "Harvest Tithe",
                narration = "...",
                objectiveDecisionCount = 3,
                decisionsCompleted = 3,
                rewardMood = 15,
                rewardLoyalty = 15
            });

            Assert.IsTrue(claimButton.interactable);
        }

        [Test]
        public void Claim_AppliesRewardExactlyOnceAndPersists()
        {
            InvokeHandleResult(new EventResponse
            {
                eventId = "W2026-37",
                name = "Harvest Tithe",
                narration = "...",
                objectiveDecisionCount = 3,
                decisionsCompleted = 3,
                rewardMood = 15,
                rewardLoyalty = 15
            });

            int moodBefore = ruler.State.Mood;
            int loyaltyBefore = ruler.State.Loyalty;

            claimButton.onClick.Invoke();

            Assert.AreEqual(moodBefore + 15, ruler.State.Mood);
            Assert.AreEqual(loyaltyBefore + 15, ruler.State.Loyalty);
            Assert.AreEqual("W2026-37", ruler.State.ClaimedEventWeekId);
            Assert.IsFalse(claimButton.interactable);

            // Clicking again (button is now non-interactable, but exercise
            // the guard directly via onClick.Invoke() -- Unity's Button
            // still permits a direct onClick.Invoke() call regardless of
            // interactable state, so this proves OnClaim's own re-entrancy
            // guard, not just the UI-level disable).
            claimButton.onClick.Invoke();

            Assert.AreEqual(moodBefore + 15, ruler.State.Mood, "Reward must not be applied twice");
            Assert.AreEqual(loyaltyBefore + 15, ruler.State.Loyalty, "Reward must not be applied twice");

            RulerState persisted = SaveService.Load();
            Assert.AreEqual("W2026-37", persisted.ClaimedEventWeekId);
        }

        [Test]
        public void HandleResult_ForAlreadyClaimedEvent_ClaimButtonStaysDisabledAndShowsClaimedStatus()
        {
            ruler.State.ClaimedEventWeekId = "W2026-37";

            InvokeHandleResult(new EventResponse
            {
                eventId = "W2026-37",
                name = "Harvest Tithe",
                narration = "...",
                objectiveDecisionCount = 3,
                decisionsCompleted = 3,
                rewardMood = 15,
                rewardLoyalty = 15
            });

            Assert.IsFalse(claimButton.interactable);
            Assert.AreEqual("Claimed", statusMessageText.text);
        }
    }
}
```

Note: `Claim_AppliesRewardExactlyOnceAndPersists` writes a real file via `SaveService.Save` — add cleanup for it in `TearDown`:

```csharp
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(screenControllerObject);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(coordinatorObject);
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(rulerObject);

            if (System.IO.File.Exists(SaveService.SavePath))
            {
                System.IO.File.Delete(SaveService.SavePath);
            }
        }
```

(This replaces the `TearDown` shown above it in Step 1 — use this final version.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter EventPanelControllerTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-event-panel-playmode.xml"`
Expected: compile error — `EventPanelController` doesn't exist yet.

- [ ] **Step 3: Implement `Assets/Scripts/UI/EventPanelController.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Fifth modal panel alongside Duel/History/Council/Tutorial. Server
    /// computes the active event and this player's live decisionsCompleted
    /// count only; this controller is the ONLY place that ever applies the
    /// event reward, client-side, exactly once, gated by
    /// RulerState.ClaimedEventWeekId -- same pattern as
    /// CouncilPanelController's reward handling. NOT DuelModalGate-aware
    /// this pass -- see
    /// docs/superpowers/specs/2026-09-03-live-ops-events-design.md's
    /// "Known Gap Flagged, Not Fixed Here" section.
    /// </summary>
    public class EventPanelController : MonoBehaviour
    {
        private const string RewardJustAppliedMessage = "This week's efforts have heartened your ruler! (+15 mood, +15 loyalty)";
        private const string RewardAlreadyClaimedMessage = "Claimed";

        [SerializeField] private Button eventsButton;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI narrationLabel;
        [SerializeField] private TextMeshProUGUI progressLabel;
        [SerializeField] private TextMeshProUGUI statusMessageText;
        [SerializeField] private Button claimButton;
        [SerializeField] private BackendSyncCoordinator coordinator;
        [SerializeField] private DecisionCycleManager manager;
        [SerializeField] private CoreLoopScreenController screenController;
        [SerializeField] private Slider armySlider;
        [SerializeField] private Slider tradeSlider;
        [SerializeField] private Slider religionSlider;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button viewHistoryButton;
        [SerializeField] private Button councilButton;

        private EventResponse latestResponse;

        private void Start()
        {
            Bind();
        }

        /// <summary>
        /// Mirrors CouncilPanelController/HistoryPanelController's
        /// Initialize pattern -- called by Start() in the real scene, and
        /// callable directly by tests to bypass Unity lifecycle timing.
        /// </summary>
        public void Initialize(
            Button eventsButton,
            GameObject panelRoot,
            Button closeButton,
            TextMeshProUGUI nameLabel,
            TextMeshProUGUI narrationLabel,
            TextMeshProUGUI progressLabel,
            TextMeshProUGUI statusMessageText,
            Button claimButton,
            BackendSyncCoordinator coordinator,
            DecisionCycleManager manager,
            CoreLoopScreenController screenController,
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button submitButton,
            Button challengeButton,
            Button viewHistoryButton,
            Button councilButton)
        {
            this.eventsButton = eventsButton;
            this.panelRoot = panelRoot;
            this.closeButton = closeButton;
            this.nameLabel = nameLabel;
            this.narrationLabel = narrationLabel;
            this.progressLabel = progressLabel;
            this.statusMessageText = statusMessageText;
            this.claimButton = claimButton;
            this.coordinator = coordinator;
            this.manager = manager;
            this.screenController = screenController;
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.submitButton = submitButton;
            this.challengeButton = challengeButton;
            this.viewHistoryButton = viewHistoryButton;
            this.councilButton = councilButton;

            Bind();
        }

        private void Bind()
        {
            eventsButton.onClick.RemoveAllListeners();
            eventsButton.onClick.AddListener(OnEventsButtonClicked);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnClose);
            claimButton.onClick.RemoveAllListeners();
            claimButton.onClick.AddListener(OnClaim);

            panelRoot.SetActive(false);
        }

        private void OnEventsButtonClicked()
        {
            SetCoreLoopControlsInteractable(false);
            panelRoot.SetActive(true);
            claimButton.interactable = false;
            statusMessageText.text = "Loading...";

            coordinator.RequestActiveEvent(HandleResult, HandleError);
        }

        private void HandleResult(EventResponse response)
        {
            latestResponse = response;
            nameLabel.text = response.name;
            narrationLabel.text = response.narration;
            progressLabel.text = $"{response.decisionsCompleted} / {response.objectiveDecisionCount} decisions";

            bool alreadyClaimed = manager.Ruler.State.ClaimedEventWeekId == response.eventId;
            bool objectiveMet = response.decisionsCompleted >= response.objectiveDecisionCount;

            claimButton.interactable = objectiveMet && !alreadyClaimed;
            statusMessageText.text = alreadyClaimed ? RewardAlreadyClaimedMessage : string.Empty;
        }

        private void HandleError(string error)
        {
            statusMessageText.text = error;
            claimButton.interactable = false;
        }

        private void OnClaim()
        {
            if (latestResponse == null || manager.Ruler.State.ClaimedEventWeekId == latestResponse.eventId)
            {
                return;
            }

            manager.Ruler.State.ApplyDelta(latestResponse.rewardMood, latestResponse.rewardLoyalty);
            manager.Ruler.State.ClaimedEventWeekId = latestResponse.eventId;
            SaveService.Save(manager.Ruler.State);
            screenController.RefreshStatusLabels();

            claimButton.interactable = false;
            statusMessageText.text = RewardJustAppliedMessage;
        }

        private void OnClose()
        {
            panelRoot.SetActive(false);
            SetCoreLoopControlsInteractable(true);
        }

        private void SetCoreLoopControlsInteractable(bool interactable)
        {
            eventsButton.interactable = interactable;
            viewHistoryButton.interactable = interactable;
            councilButton.interactable = interactable;
            armySlider.interactable = interactable;
            tradeSlider.interactable = interactable;
            religionSlider.interactable = interactable;
            submitButton.interactable = interactable;
            challengeButton.interactable = interactable;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter EventPanelControllerTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-event-panel-playmode.xml"`
Expected: XML shows 7/7 passed, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/EventPanelController.cs Assets/Tests/PlayMode/EventPanelControllerTests.cs
git commit -m "feat: add EventPanelController"
```

---

## Task 8: Client — Wire `eventsButton` into History/Council/Tutorial's shared-control sets

**Files:**
- Modify: `Assets/Scripts/UI/HistoryPanelController.cs`
- Modify: `Assets/Scripts/UI/CouncilPanelController.cs`
- Modify: `Assets/Scripts/UI/TutorialOverlayController.cs`
- Modify: `Assets/Tests/PlayMode/HistoryPanelControllerTests.cs`
- Modify: `Assets/Tests/PlayMode/CouncilPanelControllerTests.cs`
- Modify: `Assets/Tests/PlayMode/CouncilPanelControllerRealDataTests.cs`
- Modify: `Assets/Tests/PlayMode/TutorialOverlayControllerTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `HistoryPanelController.Initialize(..., Button councilButton, Button eventsButton)` (new trailing param), `CouncilPanelController.Initialize(..., Button viewHistoryButton, Button eventsButton)` (new trailing param), `TutorialOverlayController.Initialize(..., Button councilButton, Button eventsButton)` (new trailing param) — all three consumed by Task 9's `CoreLoopSceneBuilder`.

This task follows the exact precedent set when Council was added to History's and Tutorial's shared-control sets in milestones #7/#8: every existing modal/overlay that disables the shared control set must also disable (and re-enable) the new Events trigger button while it's open, or a player could tap Events while another modal/the tutorial is up.

- [ ] **Step 1: Update `Assets/Scripts/UI/HistoryPanelController.cs`**

Add a field (after `[SerializeField] private Button councilButton;` on line 30):

```csharp
        [SerializeField] private Button eventsButton;
```

Add a parameter to `Initialize` (after `Button councilButton` on line 53) and the matching assignment (after `this.councilButton = councilButton;` on line 65):

```csharp
            Button councilButton,
            Button eventsButton)
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
            this.eventsButton = eventsButton;
```

Add to `SetCoreLoopControlsInteractable` (after `councilButton.interactable = interactable;` on line 158):

```csharp
            eventsButton.interactable = interactable;
```

- [ ] **Step 2: Update `Assets/Scripts/UI/CouncilPanelController.cs`**

Add a field (after `[SerializeField] private Button viewHistoryButton;` on line 48):

```csharp
        [SerializeField] private Button eventsButton;
```

Add a parameter to `Initialize` (after `Button viewHistoryButton` on line 84) and the matching assignment (after `this.viewHistoryButton = viewHistoryButton;` on line 109):

```csharp
            Button viewHistoryButton,
            Button eventsButton)
        {
            ...
            this.viewHistoryButton = viewHistoryButton;
            this.eventsButton = eventsButton;
```

(The `...` above represents every existing assignment already in the method, unchanged — only the two new lines shown are added.)

Add to `SetCoreLoopControlsInteractable` (after `challengeButton.interactable = interactable;` on line 248):

```csharp
            eventsButton.interactable = interactable;
```

- [ ] **Step 3: Update `Assets/Scripts/UI/TutorialOverlayController.cs`**

Add a field (after `[SerializeField] private Button councilButton;` on line 48):

```csharp
        [SerializeField] private Button eventsButton;
```

Add a parameter to `Initialize` (after `Button councilButton` on line 77) and the matching assignment (after `this.councilButton = councilButton;` on line 93):

```csharp
            Button councilButton,
            Button eventsButton)
        {
            ...
            this.councilButton = councilButton;
            this.eventsButton = eventsButton;
```

Add to `SetCoreLoopControlsInteractable` (after `councilButton.interactable = interactable;` on line 160):

```csharp
            eventsButton.interactable = interactable;
```

- [ ] **Step 4: Update the three existing test files' call sites**

In `Assets/Tests/PlayMode/HistoryPanelControllerTests.cs`'s `SetUp` (line 27-80): add an `eventsButton` `GameObject`/`Button` construction (mirroring the existing `councilButton` construction on lines 53-55) and pass it as the new trailing argument to `Initialize`:

```csharp
            var eventsButtonObject = new GameObject("EventsButton", typeof(Image), typeof(Button));
            eventsButtonObject.transform.SetParent(canvasObject.transform, false);
            var eventsButton = eventsButtonObject.GetComponent<Button>();
```

And update the `Initialize` call (line 78) to:

```csharp
            controller.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, coordinator,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, councilButton, eventsButton);
```

Also add `Assert.IsFalse(eventsButton.interactable);` / `Assert.IsTrue(eventsButton.interactable);` next to the existing `councilButton` assertions in `ViewHistory_WithNoSessionYet_DisablesControlsAndShowsMessage` and `Close_ReEnablesControlsAndHidesPanel`.

In `Assets/Tests/PlayMode/CouncilPanelControllerTests.cs`'s `SetUp` (line 37-132): add an `eventsButton` construction (mirroring `viewHistoryButton` on lines 81-83) and update the `Initialize` call (line 127-131) to:

```csharp
            controller.Initialize(councilButton, panelRootObject, closeButton, notInCouncilViewObject, inCouncilViewObject,
                nameInputField, createButton, joinCodeInputField, joinButton, statusMessageText,
                nameLabel, joinCodeLabel, memberCountLabel, progressLabel, rewardStatusLabel,
                coordinator, manager, screenController,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, eventsButton);
```

Also add `Assert.IsFalse(eventsButton.interactable);` / `Assert.IsTrue(eventsButton.interactable);` next to the existing `viewHistoryButton` assertions in `CouncilButton_WithNoSessionYet_DisablesControlsAndShowsMessage` and `Close_ReEnablesControlsAndHidesPanel`.

In `Assets/Tests/PlayMode/CouncilPanelControllerRealDataTests.cs`'s `UnitySetUp` (line 39-157): add the same `eventsButton` construction and update its `Initialize` call (line 152-156) identically to the above.

In `Assets/Tests/PlayMode/TutorialOverlayControllerTests.cs`, add a field (after `private Button councilButton;` on line 24):

```csharp
        private Button eventsButton;
```

In `BuildScene()`, add after `councilButton = CreateButton("CouncilButton");` (line 52):

```csharp
            eventsButton = CreateButton("EventsButton");
```

In the private `Initialize()` helper (line 105-113), add `eventsButton` as the new trailing argument to `controller.Initialize(...)` (line 109-111):

```csharp
            controller.Initialize(panelRootObject, stepIndicatorLabel, titleLabel, bodyLabel,
                nextButton, nextButtonLabel, skipButton, manager,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, councilButton, eventsButton);
```

Add `Assert.IsFalse(eventsButton.interactable);` next to `Assert.IsFalse(councilButton.interactable);` in `TutorialNotCompleted_ShowsStepOneAndDisablesControls` (line 128). Add `eventsButton.interactable = false;` next to `councilButton.interactable = false;` (line 153) AND `Assert.IsTrue(eventsButton.interactable);` next to `Assert.IsTrue(councilButton.interactable);` (line 164) in `TutorialAlreadyCompleted_ReenablesControlsThatWereDisabledInTheScene`. Add `Assert.IsTrue(eventsButton.interactable);` next to `Assert.IsTrue(councilButton.interactable);` in `Skip_OnFirstStep_CompletesTutorialPersistsAndReenablesControls` (line 199).

- [ ] **Step 5: Run the affected test suites to verify they pass**

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter HistoryPanelControllerTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-event-history-playmode.xml"`
Expected: XML shows all `HistoryPanelControllerTests` passing, 0 failed.

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter CouncilPanelControllerTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-event-council-playmode.xml"`
Expected: XML shows all `CouncilPanelControllerTests` passing, 0 failed.

Run (ensure `server/` is running first): `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter CouncilPanelControllerRealDataTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-event-councilreal-playmode.xml"`
Expected: XML shows all `CouncilPanelControllerRealDataTests` passing, 0 failed.

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter TutorialOverlayControllerTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-event-tutorial-playmode.xml"`
Expected: XML shows all `TutorialOverlayControllerTests` passing, 0 failed.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/UI/HistoryPanelController.cs Assets/Scripts/UI/CouncilPanelController.cs Assets/Scripts/UI/TutorialOverlayController.cs Assets/Tests/PlayMode/HistoryPanelControllerTests.cs Assets/Tests/PlayMode/CouncilPanelControllerTests.cs Assets/Tests/PlayMode/CouncilPanelControllerRealDataTests.cs Assets/Tests/PlayMode/TutorialOverlayControllerTests.cs
git commit -m "feat: wire eventsButton into History/Council/Tutorial's shared-control sets"
```

---

## Task 9: Client — `CoreLoopSceneBuilder` wiring

**Files:**
- Modify: `Assets/Editor/CoreLoopSceneBuilder.cs`
- Modify: `Assets/Tests/PlayMode/CoreLoopSceneTests.cs`

**Interfaces:**
- Consumes: `EventPanelController` (Task 7), updated `HistoryPanelController`/`CouncilPanelController`/`TutorialOverlayController` (Task 8).
- Produces: the real `Assets/Scenes/CoreLoop.unity` scene, regenerated via `Understudy Kingdom > Build Core Loop Scene`, now containing an `EventsButton`, an `EventPanel`, and an `EventPanelController` — consumed by Task 10's real-data test and the milestone's manual Play Mode checkpoint.

- [ ] **Step 1: Write the failing scene smoke test**

In `Assets/Tests/PlayMode/CoreLoopSceneTests.cs`, add a new test after `LoadedCoreLoopScene_SubmitButton_UpdatesNarrationAndStatusLabels` (after line 87, before the `FindLabel` helper), mirroring the existing Council smoke test that milestone #9 added:

```csharp
        [UnityTest]
        public IEnumerator LoadedCoreLoopScene_EventsButton_OpensPanelWithoutThrowing()
        {
            yield return SceneManager.LoadSceneAsync("CoreLoop");
            yield return null;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Canvas not found in the loaded CoreLoop scene.");

            Button eventsButton = FindButton(canvas, "EventsButton");
            Assert.IsNotNull(eventsButton, "EventsButton not found in the loaded CoreLoop scene.");

            GameObject eventPanel = FindChildByName(canvas.transform, "EventPanel");
            Assert.IsNotNull(eventPanel, "EventPanel not found in the loaded CoreLoop scene.");
            Assert.IsFalse(eventPanel.activeSelf, "Expected EventPanel to start inactive.");

            eventsButton.onClick.Invoke();

            Assert.IsTrue(eventPanel.activeSelf,
                "Expected EventPanel to become active after EventsButton is clicked.");
        }
```

This test calls `FindButton` and `FindChildByName`. **On `main` (which this milestone branches from), `Assets/Tests/PlayMode/CoreLoopSceneTests.cs` currently contains only ONE test (`LoadedCoreLoopScene_SubmitButton_UpdatesNarrationAndStatusLabels`) and one helper (`FindLabel`)** — `FindButton`/`FindChildByName` do not exist yet on `main` (they exist only on the separate, unmerged `feat/duel-modal-gate` branch, which this milestone does not depend on or merge from). Add both helpers to this file now, alongside the existing `FindLabel` (after it, before the closing brace of the class):

```csharp
        private static Button FindButton(Canvas canvas, string name)
        {
            foreach (Button candidate in canvas.GetComponentsInChildren<Button>(true))
            {
                if (candidate.gameObject.name == name)
                {
                    return candidate;
                }
            }

            Assert.Fail($"No Button named '{name}' found under the Canvas.");
            return null;
        }

        private static GameObject FindChildByName(Transform parent, string name)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child.gameObject;
                }
            }

            Assert.Fail($"No child named '{name}' found under {parent.name}.");
            return null;
        }
```

`Assets/Tests/PlayMode/CoreLoopSceneTests.cs` already has `using UnityEngine.UI;` in its `using` block (confirmed on `main`) — `Button` is already in scope, no new `using` needed.

- [ ] **Step 2: Run test to verify it fails**

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter CoreLoopSceneTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-event-scene-playmode-before.xml"`
Expected: FAIL — `EventsButton not found in the loaded CoreLoop scene.` (the scene hasn't been rebuilt yet).

- [ ] **Step 3: Add the Events UI and wiring to `Assets/Editor/CoreLoopSceneBuilder.cs`**

Insert the entire block below immediately after the `councilController.Initialize(...)` call (after line 253) and before the `HistoryPanel` construction that starts at line 255 — `eventsButton` must exist before `historyController.Initialize(...)` is called (Step 3 below passes it as a new trailing argument there), and every reference this block makes (`backendCoordinator`, `manager`, `controller`, `armySlider`, `tradeSlider`, `religionSlider`, `button`, `duelButton`, `viewHistoryButton`, `councilButton`) already exists by line 253:

```csharp
            var eventsButtonObject = new GameObject("EventsButton", typeof(Image), typeof(Button));
            eventsButtonObject.transform.SetParent(canvasObject.transform, false);
            var eventsButtonRect = eventsButtonObject.GetComponent<RectTransform>();
            eventsButtonRect.anchoredPosition = new Vector2(0f, -720f);
            eventsButtonRect.sizeDelta = new Vector2(220f, 44f);
            eventsButtonObject.GetComponent<Image>().color = new Color(0.65f, 0.55f, 0.25f, 1f);
            var eventsButton = eventsButtonObject.GetComponent<Button>();
            TextMeshProUGUI eventsButtonLabel = CreateLabel(eventsButtonObject.transform, "Text", 0f, "This Week's Event");
            var eventsButtonLabelRect = eventsButtonLabel.GetComponent<RectTransform>();
            eventsButtonLabelRect.anchorMin = Vector2.zero;
            eventsButtonLabelRect.anchorMax = Vector2.one;
            eventsButtonLabelRect.sizeDelta = Vector2.zero;
            eventsButtonLabelRect.anchoredPosition = Vector2.zero;

            var eventPanelRootObject = new GameObject("EventPanel", typeof(Image));
            eventPanelRootObject.transform.SetParent(canvasObject.transform, false);
            var eventPanelRect = eventPanelRootObject.GetComponent<RectTransform>();
            eventPanelRect.anchoredPosition = Vector2.zero;
            eventPanelRect.sizeDelta = new Vector2(700f, 800f);
            eventPanelRootObject.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            var eventCloseButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            eventCloseButtonObject.transform.SetParent(eventPanelRootObject.transform, false);
            var eventCloseButtonRect = eventCloseButtonObject.GetComponent<RectTransform>();
            eventCloseButtonRect.anchoredPosition = new Vector2(310f, 360f);
            // 44pt tall, not the 60x40 this scene's other close buttons use --
            // this project's touch-target minimum is 44pt; only this new
            // panel's close button is corrected here (see Global Constraints
            // in docs/superpowers/plans/2026-09-03-live-ops-events.md).
            eventCloseButtonRect.sizeDelta = new Vector2(60f, 44f);
            eventCloseButtonObject.GetComponent<Image>().color = new Color(0.6f, 0.3f, 0.3f, 1f);
            var eventCloseButton = eventCloseButtonObject.GetComponent<Button>();
            TextMeshProUGUI eventCloseLabel = CreateLabel(eventCloseButtonObject.transform, "Text", 0f, "X");
            var eventCloseLabelRect = eventCloseLabel.GetComponent<RectTransform>();
            eventCloseLabelRect.anchorMin = Vector2.zero;
            eventCloseLabelRect.anchorMax = Vector2.one;
            eventCloseLabelRect.sizeDelta = Vector2.zero;
            eventCloseLabelRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI eventTitleLabel = CreateLabel(eventPanelRootObject.transform, "Title", 0f, "This Week's Event");
            eventTitleLabel.fontSize = 28f;
            eventTitleLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 340f);

            TextMeshProUGUI eventNameLabel = CreateLabel(eventPanelRootObject.transform, "NameLabel", 0f, string.Empty);
            eventNameLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 280f);

            TextMeshProUGUI eventNarrationLabel = CreateLabel(eventPanelRootObject.transform, "NarrationLabel", 0f, string.Empty);
            eventNarrationLabel.alignment = TextAlignmentOptions.Left;
            var eventNarrationLabelRect = eventNarrationLabel.GetComponent<RectTransform>();
            eventNarrationLabelRect.anchoredPosition = new Vector2(0f, 180f);
            eventNarrationLabelRect.sizeDelta = new Vector2(620f, 140f);

            TextMeshProUGUI eventProgressLabel = CreateLabel(eventPanelRootObject.transform, "ProgressLabel", 0f, string.Empty);
            eventProgressLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 60f);

            TextMeshProUGUI eventStatusMessageText = CreateLabel(eventPanelRootObject.transform, "StatusMessageText", 0f, string.Empty);
            var eventStatusMessageRect = eventStatusMessageText.GetComponent<RectTransform>();
            eventStatusMessageRect.anchoredPosition = new Vector2(0f, 10f);
            eventStatusMessageRect.sizeDelta = new Vector2(620f, 60f);

            var claimButtonObject = new GameObject("ClaimButton", typeof(Image), typeof(Button));
            claimButtonObject.transform.SetParent(eventPanelRootObject.transform, false);
            var claimButtonRect = claimButtonObject.GetComponent<RectTransform>();
            claimButtonRect.anchoredPosition = new Vector2(0f, -60f);
            claimButtonRect.sizeDelta = new Vector2(220f, 44f);
            claimButtonObject.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.7f, 1f);
            var claimButton = claimButtonObject.GetComponent<Button>();
            TextMeshProUGUI claimButtonLabel = CreateLabel(claimButtonObject.transform, "Text", 0f, "Claim Reward");
            var claimButtonLabelRect = claimButtonLabel.GetComponent<RectTransform>();
            claimButtonLabelRect.anchorMin = Vector2.zero;
            claimButtonLabelRect.anchorMax = Vector2.one;
            claimButtonLabelRect.sizeDelta = Vector2.zero;
            claimButtonLabelRect.anchoredPosition = Vector2.zero;

            var eventControllerObject = new GameObject("EventPanelController");
            var eventController = eventControllerObject.AddComponent<EventPanelController>();
            eventController.Initialize(eventsButton, eventPanelRootObject, eventCloseButton, eventNameLabel, eventNarrationLabel,
                eventProgressLabel, eventStatusMessageText, claimButton, backendCoordinator, manager, controller,
                armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton, councilButton);
```

Update the existing `historyController.Initialize(...)` call (line 298-299) to pass `eventsButton` as the new trailing argument:

```csharp
            historyController.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, backendCoordinator,
                armySlider, tradeSlider, religionSlider, button, duelButton, councilButton, eventsButton);
```

Update the existing `councilController.Initialize(...)` call (line 249-253) to pass `eventsButton` as the new trailing argument:

```csharp
            councilController.Initialize(councilButton, councilPanelRootObject, councilCloseButton, notInCouncilViewObject, inCouncilViewObject,
                nameInputField, createButton, joinCodeInputField, joinButton, councilStatusMessageText,
                councilNameLabel, councilJoinCodeLabel, councilMemberCountLabel, councilProgressLabel, councilRewardStatusLabel,
                backendCoordinator, manager, controller,
                armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton, eventsButton);
```

Update the existing `tutorialController.Initialize(...)` call (line 364-366) to pass `eventsButton` as the new trailing argument:

```csharp
            tutorialController.Initialize(tutorialOverlayObject, tutorialStepIndicatorLabel, tutorialTitleLabel, tutorialBodyLabel,
                tutorialNextButton, tutorialNextButtonLabel, tutorialSkipButton, manager,
                armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton, councilButton, eventsButton);
```

- [ ] **Step 4: Add an `EventPanelController` check to `Verify()`**

In `Assets/Editor/CoreLoopSceneBuilder.cs`'s `Verify()` method, add after the `TutorialOverlayController` check (after line 473, before the final `Debug.Log`):

```csharp
            var eventController = Object.FindFirstObjectByType<EventPanelController>();
            if (eventController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no EventPanelController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
```

- [ ] **Step 5: Regenerate the scene**

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Build -quit`
Expected: exits 0, log line `CoreLoopSceneBuilder: saved scene to Assets/Scenes/CoreLoop.unity`. (`-quit` is correct and required here — `-executeMethod` is a different code path than `-runTests`, see Global Constraints.)

Then verify it:

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Verify -quit`
Expected: exits 0, log line `CoreLoopSceneBuilder.Verify: scene opened and controller found successfully.`

- [ ] **Step 6: Run the scene smoke test to verify it passes**

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter CoreLoopSceneTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-event-scene-playmode-after.xml"`
Expected: XML shows all `CoreLoopSceneTests` passing (prior 1 + 1 new = 2/2 on `main` as of this milestone), 0 failed.

- [ ] **Step 7: Commit**

```bash
git add Assets/Editor/CoreLoopSceneBuilder.cs Assets/Tests/PlayMode/CoreLoopSceneTests.cs Assets/Scenes/CoreLoop.unity
git commit -m "feat: wire EventPanelController into the CoreLoop scene"
```

---

## Task 10: Client — Real end-to-end event test

**Files:**
- Create: `Assets/Tests/PlayMode/EventPanelControllerRealDataTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1-9 (real server, real Supabase, the real regenerated scene's `EventPanelController` wiring pattern, constructed by hand the same way `CouncilPanelControllerRealDataTests` does).

- [ ] **Step 1: Write the real end-to-end test**

Create `Assets/Tests/PlayMode/EventPanelControllerRealDataTests.cs` (ensure `server/` is running before executing):

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
    /// Real end-to-end: real Supabase sign-in, real local server/, 3 real
    /// decisions posted via BackendApiClient directly (mirroring
    /// CouncilPanelControllerRealDataTests' precedent of posting decisions
    /// directly rather than through slider/Submit UI, which this project
    /// has no existing automated-testing precedent for) -- only eventsButton
    /// and claimButton are actually clicked.
    /// </summary>
    public class EventPanelControllerRealDataTests
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
        private Button eventsButton;
        private Button claimButton;
        private TextMeshProUGUI progressLabel;
        private TextMeshProUGUI statusMessageText;

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

            // Every hardcoded event this milestone defines has
            // objectiveDecisionCount = 3 -- see
            // server/src/game/liveOpsEvents.ts -- so 3 real decisions always
            // clears the objective regardless of which event is currently
            // active.
            for (int cycle = 1; cycle <= 3; cycle++)
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
            var councilButton = councilButtonObject.GetComponent<Button>();

            var eventsButtonObject = new GameObject("EventsButton", typeof(Image), typeof(Button));
            eventsButtonObject.transform.SetParent(canvasObject.transform, false);
            eventsButton = eventsButtonObject.GetComponent<Button>();

            panelRootObject = new GameObject("EventPanel");
            panelRootObject.transform.SetParent(canvasObject.transform, false);

            var closeButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            closeButtonObject.transform.SetParent(panelRootObject.transform, false);
            var closeButton = closeButtonObject.GetComponent<Button>();

            var nameLabel = CreateLabel("NameLabel", panelRootObject.transform);
            var narrationLabel = CreateLabel("NarrationLabel", panelRootObject.transform);
            progressLabel = CreateLabel("ProgressLabel", panelRootObject.transform);
            statusMessageText = CreateLabel("StatusMessageText", panelRootObject.transform);

            var claimButtonObject = new GameObject("ClaimButton", typeof(Image), typeof(Button));
            claimButtonObject.transform.SetParent(panelRootObject.transform, false);
            claimButton = claimButtonObject.GetComponent<Button>();

            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<EventPanelController>();
            controller.Initialize(eventsButton, panelRootObject, closeButton, nameLabel, narrationLabel,
                progressLabel, statusMessageText, claimButton, coordinator, manager, screenController,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, councilButton);
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
        public IEnumerator EventsButton_AfterThreeRealDecisions_ShowsObjectiveMetAndClaimApplies()
        {
            eventsButton.onClick.Invoke();

            yield return new WaitUntil(() => !string.IsNullOrEmpty(progressLabel.text));

            // Every hardcoded event has objectiveDecisionCount = 3, and 3
            // real decisions were posted in UnitySetUp above, so the
            // objective is exactly met.
            Assert.IsTrue(claimButton.interactable, $"Expected Claim to be interactable with progress '{progressLabel.text}'");

            int moodBefore = ruler.State.Mood;
            int loyaltyBefore = ruler.State.Loyalty;

            claimButton.onClick.Invoke();

            Assert.AreEqual(moodBefore + 15, ruler.State.Mood);
            Assert.AreEqual(loyaltyBefore + 15, ruler.State.Loyalty);
            Assert.IsFalse(string.IsNullOrEmpty(ruler.State.ClaimedEventWeekId));
            Assert.IsFalse(claimButton.interactable);

            RulerState persisted = SaveService.Load();
            Assert.AreEqual(ruler.State.ClaimedEventWeekId, persisted.ClaimedEventWeekId);
            Assert.AreEqual(moodBefore + 15, persisted.Mood);
            Assert.AreEqual(loyaltyBefore + 15, persisted.Loyalty);
        }
    }
}
```

- [ ] **Step 2: Run the test**

Ensure `server/` is running, then:

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter EventPanelControllerRealDataTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-event-realdata-playmode.xml"`
Expected: XML shows 1/1 passed, 0 failed.

- [ ] **Step 3: Commit**

```bash
git add Assets/Tests/PlayMode/EventPanelControllerRealDataTests.cs
git commit -m "test: add real end-to-end event coverage"
```

---

## Definition of Done

- [ ] Full server suite passes: `cd server && npm test && npm run typecheck`
- [ ] Full Unity EditMode suite passes: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-event-full-editmode.xml"`
- [ ] Full Unity PlayMode suite passes (`server/` running): `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-event-full-playmode.xml"`
- [ ] Manual Play Mode checkpoint (same pattern as prior milestones — open the Editor, enter Play Mode on the real `CoreLoop` scene): Events panel opens and shows real narration/progress from the real server; submitting real decisions (via the normal Submit flow) increments progress on next open; Claim becomes enabled at the threshold and actually applies the mood/loyalty boost, visible in the Mood/Loyalty labels; the boost persists across a Stop/Play restart; re-opening the panel after claiming shows "Claimed" rather than re-granting the reward.
- [ ] Update `docs/PROJECT_PLAN.md`'s Implementation Status table (milestone #10: Done, covers FR-10/FR-11-narrowed) and "Known follow-up items" (add: premium/IAP reward tier deferred; `EventPanelController` needs `DuelModalGate` threaded in once milestone #9 merges).
