# Design: Council / Social (Milestone #7, FR-07/FR-08)

**Date:** 2026-09-03 | **Status:** Approved, pending implementation plan

## Purpose

`docs/PROJECT_PLAN.md` lists FR-07 ("join a council of advisors") and
FR-08 ("shared council milestone grants all members a reward") as
`[Recommended]`/unstarted. This is the project's first genuinely social
feature — the first time one user's request depends on and mutates state
shared with other users, rather than each player's own isolated kingdom.
Milestones #1-6 are complete and merged; this milestone builds Council
membership, join-by-code, and a one-time shared-progress reward on top of
the existing `server/` (Fastify + Drizzle + Postgres, real Supabase
anonymous auth) and the existing single-scene Unity client.

## Scope Decisions

These were confirmed interactively before any design work began:

- **Join-by-code, not browsing or auto-match.** A player creates a
  council and gets a short code to share; others join by entering it.
  Mirrors milestone #5's explicit rejection of an opponent-browsing UI —
  no new list/pagination UI, and it gives players a real reason to
  coordinate with people they know outside the app.
- **Shared milestone = total decisions submitted across all members.**
  Every player generates this just by playing the core loop — no extra
  action required — and it's a trivial `COUNT` through the existing
  `decisions` table. Rejected alternatives: total PvP wins (many players
  may never touch PvP, stalling a council at 0 through no fault of casual
  members) and non-overridden-decision count (thematically nicer but a
  weaker, less obvious "we're all playing" signal).
- **Reward = one-time +10 mood / +10 loyalty to each eligible member's own
  ruler, applied client-side.** The game has no inventory/currency system,
  so a stat bump reusing existing fields is the natural reward. Critically,
  it's applied **client-side**, not written to `ruler_npcs` server-side:
  investigation for this design confirmed the client's `RulerState`
  (mood/loyalty) is entirely client-authoritative today — computed and
  persisted locally by `OverrideEvaluator`/`SaveService` — and the server's
  `ruler_npcs` table is never read back by the client (it's only used,
  inertly, as the PvP defender snapshot; see milestone #5's I-3 finding).
  A server-side write to `ruler_npcs` would be invisible to the player
  without also building a new pull-and-adopt path, which would be the
  first time the server ever overrides local RulerState and needs
  conflict-resolution thinking nothing else in the codebase has. Applying
  the boost client-side keeps this consistent with how every other
  mood/loyalty change already works and avoids that new architecture
  entirely.
- **Reward fires once per council, ever — not repeating tiers.** A single
  `milestone_reached` boolean on the council row. Repeating tiers need
  per-member claim tracking to handle mid-tier joins; not justified without
  usage data on how fast councils actually reach the first threshold.
- **No retroactive reward for late joiners.** Only members present in the
  council at the exact moment the threshold is crossed receive the reward.
  Enforced via a per-member `reward_eligible` flag set `true` for the
  current membership snapshot at trigger time; anyone joining afterward
  keeps `reward_eligible=false` forever.
- **One council per user, enforced by a `UNIQUE` constraint** on
  `council_members.user_id` (the column is that table's primary key). No
  leaving a council, no renaming, no browsing list — deliberately out of
  scope for this pass.
- **Panel/overlay in the existing single `CoreLoop` scene**, matching
  milestones #5 (Duel) and #6 (History). A third modal, disabling the same
  controls the History panel already disables.

## Approach

**Server is the source of truth for shared state (membership, join codes,
decision counts, milestone/eligibility flags). Client is the source of
truth for the player's own `RulerState`.** This split is deliberate, not
incidental — it's the same split the codebase already has for the core
loop (client computes/owns mood/loyalty) versus PvP (server computes/owns
the duel roll and evaluation). Council data doesn't fit either existing
column cleanly since it's shared-but-not-mutating-the-player's-own-stats,
so it gets its own thin server-authoritative slice, with the *reward*
specifically carved out to stay client-authoritative for the reason above.

**Trigger point is the existing decision-submission path, not a new
poll/cron.** `POST /api/v1/decisions` already runs once per player action
and already writes to the `decisions` table the threshold check reads
from. Piggybacking the threshold check there (guarded by
`WHERE milestone_reached=false` for idempotency, matching this codebase's
established `onConflictDoNothing`-style atomic-guard pattern from
milestones #3/#5) avoids any new scheduled job or client-side polling.

**Reward delivery is pull, not push.** The client discovers
`rewardEligible=true` the next time it calls `GetCouncilStatus` (i.e. next
time the player opens the Council panel), the same "check on open" pattern
`HistoryPanelController` already uses for its own data. No push
notification infrastructure exists or is needed for a first pass.

## Data Model

```sql
councils:          id (UUID PK), name (text, not null),
                    join_code (text, unique, not null, 6-char uppercase
                    alphanumeric), milestone_threshold (int, not null,
                    default 10), milestone_reached (bool, not null,
                    default false), created_at (timestamptz, default now())

council_members:    user_id (PK, FK -> users, enforces one-council-per-user),
                    council_id (FK -> councils, not null), joined_at
                    (timestamptz, default now()), reward_eligible (bool,
                    not null, default false)
```

`milestone_threshold` defaults to 10 total decisions across the council —
reachable within a single short test/play session by 1-2 active members,
not trivially reached by a single decision. Council membership capped at
20 (`join` returns 403 once a council has 20 members) — bounds
reward-farming-by-mass-invite; no design goal needs more for a first pass.

`totalDecisions` (surfaced to the client, not stored) is computed via a
join: `council_members` → `kingdoms` (on `kingdoms.userId =
council_members.userId`) → `decisions` (on `decisions.kingdomId =
kingdoms.id`), `COUNT(decisions.id)` filtered to the caller's council.

## Server Endpoints

### `POST /api/v1/councils` (new)
Body: `{ name: string }`. Generates a unique 6-char uppercase-alphanumeric
join code (generate, attempt insert, retry on unique-constraint collision
— astronomically rare at this keyspace size, so no need for a
pre-check-then-insert pattern). Creator becomes the first member
(`reward_eligible=false` until a real trigger). Returns `201` with
`{ id, name, joinCode, memberCount: 1, milestoneThreshold, milestoneReached: false, rewardEligible: false }`.
`409 { error: 'You are already in a council' }` if the caller already has
a `council_members` row (checked before generating a code).

### `POST /api/v1/councils/join` (new)
Body: `{ joinCode: string }`. `404 { error: 'No council found for that code' }`
if the code doesn't match any council. `409 { error: 'You are already in a council' }`
if the caller already has a `council_members` row. `403 { error: 'That council is full' }`
if it already has 20 members. Else inserts membership with
`reward_eligible=false`. Returns the same shape as create, reflecting the
real current `memberCount`/`totalDecisions`/flags.

### `GET /api/v1/councils/me` (new)
`404 { error: 'Not in a council' }` if the caller has no
`council_members` row. Else `200`:
```json
{
  "id": "uuid",
  "name": "string",
  "joinCode": "ABC123",
  "memberCount": 3,
  "totalDecisions": 7,
  "milestoneThreshold": 10,
  "milestoneReached": false,
  "rewardEligible": false
}
```
`rewardEligible` is scoped to the caller's own `council_members` row, not
the council as a whole.

### Modified: `POST /api/v1/decisions`
After a successful (non-409, i.e. newly-inserted) decision insert, look up
the caller's `council_members` row. If found and the council's
`milestone_reached` is still `false`, recompute the council's
`totalDecisions` via the join above. If it now meets
`milestone_threshold`, run one update, guarded by
`WHERE milestone_reached = false`, that sets the council's
`milestone_reached = true` **and** sets `reward_eligible = true` for every
row currently in `council_members` for that council. The `WHERE` guard
makes this safe if two members' decision-submissions race to cross the
threshold concurrently — only the first to execute performs the flip, the
second sees `milestone_reached` already true and no-ops. This mirrors the
`ON CONFLICT DO NOTHING` idempotency pattern already used for the 409
decision-duplicate case in this same route.

## Client

### `Assets/Scripts/Backend/CouncilResponse.cs` (new)
```csharp
using System;

namespace UnderstudyKingdom.Backend
{
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
Reused verbatim for the create/join/status responses — all three server
endpoints return this exact shape.

### `Assets/Scripts/Backend/BackendApiClient.cs` (modified)
Three new methods, each following the existing `Post`/`Get` coroutine +
`TryExtractServerErrorMessage` pattern already used by
`PostDecision`/`PostDuel`/`GetDecisionHistory`:
- `CreateCouncil(string accessToken, string name, Action<CouncilResponse> onSuccess, Action<string> onError)`
  — `POST {BackendBaseUrl}/api/v1/councils`, body `{"name":"..."}`.
- `JoinCouncil(string accessToken, string joinCode, Action<CouncilResponse> onSuccess, Action<string> onError)`
  — `POST {BackendBaseUrl}/api/v1/councils/join`, body `{"joinCode":"..."}`.
- `GetCouncilStatus(string accessToken, Action<CouncilResponse> onSuccess, Action<string> onError)`
  — `GET {BackendBaseUrl}/api/v1/councils/me`. On a `404` whose parsed
  body is exactly `"Not in a council"`, this is treated as a normal
  not-an-error outcome by the caller (see `CouncilPanelController` below),
  the same "expected 404 vs real error" split `HistoryPanelController`
  already does for its own 404 case.

### `Assets/Scripts/Backend/BackendSyncCoordinator.cs` (modified)
Three new public methods — `RequestCreateCouncil`, `RequestJoinCouncil`,
`RequestCouncilStatus` — each following `RequestDuel`/`RequestHistory`'s
exact shape: call the shared `EnsureFreshSession` first, then call the
matching `BackendApiClient` method once the session is fresh. No new
session-handling code — this is exactly the chokepoint
`EnsureFreshSession` (commit `b5dd265`) exists to be reused by.

### `Assets/Scripts/Backend/SaveService.cs` / save data (modified)
Adds one new persisted field, `councilRewardApplied` (bool, default
`false`), alongside the existing save fields (mood, loyalty, agenda, cycle
count). Read/written through the same load/save path already in place —
no new file, no new format.

### `Assets/Scripts/UI/CouncilPanelController.cs` (new)
Owns: the "Council" button, the panel root (hidden by default), and two
mutually-exclusive sub-views:
- **Not-in-a-council view:** a name input field + "Create Council" button,
  and a join-code input field + "Join Council" button.
- **In-a-council view:** council name, join code (shown plainly so the
  player can share it), `"{memberCount} members"`,
  `"{totalDecisions} / {milestoneThreshold} decisions"`, and a reward
  status line.

**On open:** disable the shared controls (see below), show the panel, call
`BackendSyncCoordinator.RequestCouncilStatus`.
- Real `404`/"Not in a council" → show the not-in-a-council view.
- Any other error → show the error message (verbatim for unmapped server
  errors, matching the existing pattern).
- Success → show the in-a-council view populated from the response. If
  `rewardEligible == true` **and** the local save's
  `councilRewardApplied == false`: apply `+10` mood, `+10` loyalty
  (clamped to the existing 0-100 range, via `SaveService`'s existing
  clamp path) to the local `RulerState`, persist
  `councilRewardApplied = true`, and show a one-time narration line
  ("Your council's shared effort has lifted your ruler's spirits!"). If
  `councilRewardApplied` is already `true`, show "Reward claimed" instead
  of re-applying.

**On Create/Join button tap:** call the matching
`BackendSyncCoordinator.RequestCreateCouncil`/`RequestJoinCouncil`; on
success, switch to the in-a-council view with the returned data (a fresh
council never has `rewardEligible == true`, so no reward-application check
is needed on this path); on error, show the message inline near the
relevant input (already-in-a-council / unknown-code / council-full cases
get friendly text, matching the existing 404-empty-state precedent from
milestone #6; anything else shows the real server message).

**On close:** hide the panel, re-enable the shared controls.

**Shared-controls gating:** `councilButton` joins
`SetCoreLoopControlsInteractable`'s existing disabled set (the 3 sliders,
Submit, Challenge, View History), which already grew from 5 to 6 controls
once already (milestone #6's I-2 fix) — this is the same mechanism, one
more entry.

### `Assets/Editor/CoreLoopSceneBuilder.cs` (modified)
Adds a "Council" button (same `CreateSlider`/`CreateLabel`-style creation
pattern as the existing buttons) wired to a new `CouncilPanelController`,
which needs references to the panel root, the two sub-view roots, the
name/join-code input fields, the create/join buttons, the status labels,
and the `BackendSyncCoordinator` instance. `Verify()` gains a
`CouncilPanelController` check mirroring the existing
`HistoryPanelController`/`CoreLoopScreenController` checks.

## Data Flow

```
Player taps "Council" (not yet in one)
  -> CouncilPanelController: disable shared controls, show panel
  -> RequestCouncilStatus -> GET /api/v1/councils/me -> 404 "Not in a council"
  -> show not-in-a-council view

Player enters a name, taps "Create Council"
  -> RequestCreateCouncil -> POST /api/v1/councils
  -> real server logic: generate unique join code, insert council +
     first membership row
  -> show in-a-council view (memberCount=1, totalDecisions=0)

A second player enters the shared join code, taps "Join Council"
  -> RequestJoinCouncil -> POST /api/v1/councils/join
  -> real server logic: validate code, capacity, insert membership row
  -> show in-a-council view (memberCount=2)

Either member submits a decision (existing core loop, unrelated button)
  -> POST /api/v1/decisions (existing path)
       -> insert decision
       -> look up caller's council, recompute totalDecisions
       -> if totalDecisions >= milestoneThreshold and not yet reached:
            flip milestone_reached=true, reward_eligible=true for all
            current members (one atomic guarded update)

Either eligible member later reopens the Council panel
  -> RequestCouncilStatus -> GET /api/v1/councils/me
       -> rewardEligible=true for that member
  -> CouncilPanelController: apply +10 mood/+10 loyalty locally (once),
     persist councilRewardApplied=true, show narration

A THIRD player joins the council after the threshold was already crossed
  -> RequestJoinCouncil succeeds, memberCount increases
  -> that player's GET /api/v1/councils/me always returns
     rewardEligible=false (their council_members row was inserted after
     the trigger, never flipped) -> no reward ever applied for them
```

## Error Handling

Same philosophy as Duel/History: real server messages surfaced verbatim by
default, with the three structured error cases mapped to friendlier
client-side text — already-in-a-council, unknown join code, council full.
`404 "Not in a council"` from the status check is not an error path at
all; it's the expected shape of "no council yet," handled the same way
milestone #6 collapsed its own 404/empty-array cases into one friendly
state.

## Testing

**Server (integration, real Supabase + Postgres, no mocks):** create
happy path (unique join code generated, creator is sole member);
duplicate-create-while-already-in-a-council (409); join happy path
(memberCount reflects both members); join with unknown code (404); join
while already in a council (409); join a council already at 20 members
(403); `GET /me` with no council (404); `GET /me` in a council reflects
real `totalDecisions`/`memberCount`; a real threshold-crossing test where
2+ real users submit real decisions via the existing `POST /decisions`
until the council's total reaches `milestoneThreshold`, asserting
`milestoneReached`/`rewardEligible` flip from `false` to `true` exactly at
that point — and, on the same run, a member who joins *after* the flip has
`rewardEligible=false` while pre-flip members have `true` (the concrete
test that proves the late-joiner exclusion, not just the boolean flip
itself).

**Unity EditMode:** `CouncilResponse` deserialization from a hardcoded
server-shaped JSON string; `SaveService`'s new `councilRewardApplied`
field round-trips through save/load like the existing fields.

**Unity PlayMode:** `BackendApiClient`'s three new methods against the
real local `server/` (create, join with a second real test user, status,
plus at least the already-in-a-council error path); `BackendSyncCoordinator`'s
three new methods confirming they route through `EnsureFreshSession`
(mirrors the existing `RequestDuel`/`RequestHistory` test structure, no
new session-handling behavior to prove); `CouncilPanelController`'s
deterministic no-session error path (mirrors
`DuelButtonControllerTests`'/`HistoryPanelController`'s pattern) plus one
real end-to-end test that creates a council, drives real decisions past
the threshold, reopens the panel, and asserts the local `RulerState`
actually received the +10/+10 boost and `councilRewardApplied` persisted
`true`.

## Explicitly Out of Scope for This Pass

- Leaving a council, renaming a council, kicking a member.
- Browsing a public list of councils.
- Repeating/tiered rewards (only a single one-time threshold this pass).
- Any reward besides the fixed +10 mood / +10 loyalty bump (no cosmetics,
  no currency — none exist yet).
- Rate limiting on council creation/joining (matches milestone #5's own
  explicitly-deferred rate-limiting decision).
- A push/notification mechanism for reward availability — purely
  pull-on-panel-open this pass.
