# Design: Async PvP (Milestone #5)

**Date:** 2026-09-02 | **Status:** Approved, pending implementation plan

## Purpose

`docs/PROJECT_PLAN.md` names Async PvP (FR-09) as `[Recommended]`: "architecturally
avoids matchmaking/bot complaints rather than patching them later." Milestone
#4 made the client a genuine authenticated backend consumer — kingdoms and
decisions are now real, server-side, per-user records. This milestone is the
first feature to actually *use* that: a player submits a resource-allocation
"prepared strategy" (FR-09) which the server judges, server-authoritatively,
against a randomly-chosen other kingdom's current ruler state, reusing the
exact override-probability mechanic already built and tested in milestones
#1-2 — just run against an opponent's stored state instead of the player's
own.

## Scope Decisions

These were confirmed interactively before any design work began:

- **Duel mechanic: challenger's allocation vs. the defender's stored ruler
  state, evaluated via the existing `OverrideEvaluator` formula.** The
  challenger wins if the defender's ruler accepts the allocation
  (`Overridden == false`); loses if overridden. This reuses proven,
  already-tested logic rather than inventing new game-design mechanics, and
  matches FR-09's literal wording ("submit a prepared strategy to be
  judged").
- **Server-authoritative resolution.** The server generates its own roll and
  runs its own copy of the evaluator — neither the challenger nor the
  defender can influence or fake the outcome. Consistent with how every
  other trust-sensitive operation in this project already works (JWT
  verification, DB writes never trusting client-supplied IDs). Cost: the
  override-probability formula now has two implementations (C# and
  TypeScript) that must be kept in sync — mitigated by parity tests (see
  Testing).
- **Random opponent, no browsing/leaderboard UI.** One "Challenge" button;
  the server picks a random other kingdom as the defender and resolves
  immediately. No live matchmaking queue (the "async-only" part of FR-09),
  no new list/leaderboard screen. Matches this project's established
  pattern of shipping the minimum client UI that makes a mechanic real
  (milestone #4 deliberately skipped a history-viewing screen for the same
  reason).
- **No mechanical effect on the challenger's own kingdom this pass.** A duel
  result is narrated and persisted server-side, but does not touch the
  challenger's own `RulerState`/mood/loyalty. Keeps this milestone's scope
  to "prove the duel mechanic is real and server-authoritative"; reward/
  ranking/consequence design is deferred to a later, Live-Ops-flavored
  pass.
- **No kingdom display names.** The schema has no name column for kingdoms,
  and adding one purely for duel narration flavor would be scope creep.
  Result narration is generic ("a rival kingdom's ruler...").
- **`Math.random()` for the server's roll**, not a cryptographic RNG — this
  is a gameplay-fairness value, not a security boundary.

## Approach

**No new infrastructure.** `PROJECT_PLAN.md`'s original architecture sketch
assumed Redis for a PvP duel queue; that assumption predates this project's
actual backend, which milestone #3 built Postgres-only. Because resolution
is synchronous ("random opponent, resolve immediately" — see Scope
Decisions), there is no queue to build: one HTTP request, one DB write, one
response. Revisit only if a future milestone actually needs deferred/queued
resolution (e.g. a real matchmaking system), which this one explicitly does
not.

**The one real architectural addition:** session/access-token management
currently lives entirely inside `BackendSyncCoordinator` as a private field,
because milestone #4's only authenticated client call (decision sync) is
fire-and-forget and background. A duel is this project's first
*player-initiated* authenticated call — the player taps a button and expects
a real result, so it needs a valid (possibly freshly-refreshed) access token
synchronously from the player's perspective. Rather than duplicate
session-refresh logic in a second place, `BackendSyncCoordinator` gains one
new public method (`RequestDuel`) that becomes, alongside the existing
decision-sync path, one of two callers of its private session-management
logic — it remains the single owner of "how to get a valid token."

**Rejected alternative:** letting the challenger's own client resolve the
duel locally and report the result. Rejected for the same reason milestone
#3's auth design never trusted client-supplied identity: a modified client
could always report a win. Explicitly the wrong direction for a milestone
whose whole point is a fair, server-judged mechanic.

## Data Model

```sql
-- New table. No scenario_id (PROJECT_PLAN.md's original sketch assumed a
-- fixed scenario; this milestone's confirmed mechanic uses the challenger's
-- own submitted allocation instead, so scenario_id doesn't apply). No
-- separate winner_kingdom_id column -- derivable from `overridden`, and
-- nothing reads duel history back yet to need it precomputed.
pvp_duels: id (UUID PK), challenger_kingdom_id (UUID FK -> kingdoms),
           defender_kingdom_id (UUID FK -> kingdoms),
           challenger_recommendation (jsonb),
           defender_ruler_snapshot (jsonb),
           overridden (boolean), created_at (timestamptz)
```

`defender_ruler_snapshot` captures the defender's mood/loyalty/agenda **at
duel time**, not a live reference — their kingdom keeps changing afterward,
and the duel record should stay a fair, reproducible historical fact rather
than silently drifting if someone later queries it.

## Components

### `server/src/game/overrideEvaluator.ts` (new)
Faithful TypeScript port of `Assets/Scripts/NPC/OverrideEvaluator.cs`'s
exact constants and formula:
```
Baseline = 0.10, LoyaltyWeight = 0.012, MoodWeight = 0.005,
AgendaMisalignedBump = 0.25, clamped to [0.02, 0.95]
```
Doc comment cross-references the C# source path so a future change to
either implementation prompts checking the other. Exports
`overrideProbability(state, allocation)` and
`evaluate(state, allocation, roll)`, structurally mirroring the C# API.

### `server/src/db/schema.ts` (modified)
Adds the `pvpDuels` table above.

### `server/src/routes/duels.ts` (new)
`POST /api/v1/duels`, auth required (existing JWT hook). Body:
`{ recommendation: { army, trade, religion } }`, JSON-Schema validated as
integers summing to 100 (matching `ResourceAllocation.IsValid()`'s existing
client-side invariant, now also enforced server-side). Steps: resolve the
challenger's kingdom from `request.userId`; pick one random other kingdom
(`ORDER BY random() LIMIT 1`, excluding the challenger's own — acceptable at
this project's current scale, not built to scale past it); generate a roll
via `Math.random()`; run the ported evaluator against the defender's current
`ruler_npcs` row; insert the `pvp_duels` record; respond.

Response `201`: `{ overridden: boolean, defender_ruler_snapshot: { mood,
loyalty, agenda } }`.

### `Assets/Scripts/Backend/BackendApiClient.cs` (modified)
One new method: `PostDuel(string accessToken, DuelRequest dto,
Action<DuelResult> onSuccess, Action<string> onError)`, following the exact
`UnityWebRequest` pattern `EnsureKingdom`/`PostDecision` already use.

### `Assets/Scripts/Backend/BackendSyncCoordinator.cs` (modified)
One new public method: `RequestDuel(ResourceAllocation recommendation,
Action<DuelResult> onSuccess, Action<string> onError)`. Ensures the session
is valid (refreshing first if expired — reuses the exact logic
`HandleDecisionRecorded` already has for the same purpose), then calls
`BackendApiClient.PostDuel`. `currentSession` remains private and untouched
by anything outside this class.

### `Assets/Scripts/UI/DuelButtonController.cs` (new)
Owns one new "Challenge a Rival Kingdom" button. On click: reads the same
slider values `CoreLoopScreenController` would submit for a normal
recommendation; shows a brief "Resolving..." state (the one place in this
milestone where the player legitimately sees network latency — a duel is an
explicit, player-initiated request, unlike decision-sync's deliberate
silence); calls `BackendSyncCoordinator.RequestDuel`; on result, narrates
via the existing `DialogueTemplateEngine` using two new template tags
(`duel_win`/`duel_lose`, generic wording — no kingdom name field exists).

## Data Flow

```
Player taps "Challenge a Rival Kingdom"
  -> DuelButtonController reads current slider values -> ResourceAllocation
  -> BackendSyncCoordinator.RequestDuel(allocation, ...)
       -> ensure valid session (refresh if expired, same logic as decision sync)
       -> BackendApiClient.PostDuel(accessToken, dto, ...)
            -> POST /api/v1/duels
                 -> resolve challenger's kingdom from request.userId
                 -> pick random other kingdom + its ruler_npcs row
                 -> roll = Math.random()
                 -> result = evaluate(defenderRulerState, allocation, roll)
                 -> insert pvp_duels row (snapshotting defender state)
                 -> respond { overridden, defender_ruler_snapshot }
  -> DuelButtonController narrates result via DialogueTemplateEngine
```

## Error Handling

- Malformed body / allocation not summing to 100 → `400` (Fastify schema
  validation, same pattern as `decisions.ts`).
- Challenger has no kingdom yet → `404 "No kingdom found for this user"`
  (reuses the exact existing message from `kingdoms.ts`/`decisions.ts`).
- No other kingdom exists to challenge → `404 "No other kingdoms available
  to challenge"` — client shows a friendly "no rivals yet" message.
- Any network/auth/server failure on the client side is shown to the
  player as a real, visible error — this is the one place in the milestone
  where failure is deliberately surfaced, not silently dropped, because the
  player took an explicit action and is waiting on a result (contrast with
  decision-sync's deliberate silence per milestone #4's design).

## Testing

**Server:** unit tests for the ported evaluator asserting parity with
`OverrideEvaluatorTests.cs`'s known cases (e.g. `Mood=50, Loyalty=50,
aligned → probability===0.10`; `Mood=0, Loyalty=0, misaligned → 0.95`) —
catches drift between the two implementations immediately rather than as a
live gameplay bug. Integration tests hitting the real endpoint (real
Supabase-authenticated users, real Postgres, following `decisions.ts`'s own
established test pattern): successful resolution, no-opponent 404,
invalid-allocation 400, and a concurrency check that duel creation is safe
under simultaneous requests (matching the race-condition discipline already
established for the `decisions` table's unique-constraint handling).

**Client:** EditMode tests for the new DTOs' mapping and any pure logic in
`DuelButtonController`. PlayMode tests hitting the real local `server/` for
the actual duel round-trip, following milestone #4's established real-
network, no-mocks pattern.

## Explicitly Out of Scope for This Pass

- Opponent browsing or leaderboard UI.
- Duel history viewing (no `GET /api/v1/duels`).
- Any mechanical effect of a duel's outcome on the challenger's own
  `RulerState`.
- Kingdom display names.
- Defender-side notifications ("someone challenged you").
- Rate limiting on challenge frequency.
