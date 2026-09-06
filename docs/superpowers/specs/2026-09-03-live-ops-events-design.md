# Design: Live-Ops Events (Milestone #10)

**Date:** 2026-09-03 | **Status:** Approved, pending implementation plan

## Purpose

`docs/PROJECT_PLAN.md` lists FR-10 (weekly-rotating narrative events) and
FR-11 (every event has a free-to-play-completable reward tier; premium
spend never gates required progress) as the last unbuilt features before
FR-12/FR-14/FR-15. This milestone builds a self-contained slice of both,
scoped down from the FRs' full literal wording — confirmed interactively
before any design work began — because the codebase currently has no
currency/economy system and no IAP integration at all (only
mood/loyalty/agenda stats exist). A "premium reward tier" has nothing to
attach to yet.

## Scope Decisions

Confirmed interactively:

- **No premium tier this pass.** FR-11's "premium spend unlocks
  cosmetic/time-skip rewards" clause is deliberately deferred until a real
  currency/IAP system exists (a future milestone). This milestone ships
  FR-10 in full and FR-11 narrowed to: every event has exactly one
  reward, and it is unconditionally F2P-completable. Cosmetic rewards are
  also out of reach this pass since FR-12 (cosmetics) hasn't shipped —
  there's no visual system to unlock into.
- **Objective = decision-cycle count, not a new gameplay action.** An
  event's objective is "submit N recommendations while this event is
  active," reusing the existing, already-timestamped `decisions` table.
  No new progress-tracking table, no new client interaction beyond what
  Submit already does.
- **Reward = mood/loyalty stat boost, applied client-side exactly once.**
  Mirrors milestone #7's Council reward exactly: server tracks
  eligibility/progress only; the client is the sole place the mutation
  happens, gated by a save-data flag so it can never be re-applied.
- **Event content is a fixed, hardcoded list, rotating deterministically
  by ISO week.** A small array of ~4 events (name, narration, objective
  count, reward amounts) lives in server code. The active event is
  `list[isoWeek % list.length]`; its date range is that ISO week's
  Monday 00:00 UTC through Sunday 23:59:59 UTC, computed on every read.
  No DB content table, no admin tooling, no cron job — this project has
  exactly one operator and no CMS; adding a 5th event later is a one-line
  code change plus a deploy. Rejected alternative: a DB-authored `events`
  table with real scheduling, which would need authoring tooling that
  doesn't exist and isn't justified for this scale yet.

## Approach

### Event identity vs. content

The event id exposed to the client is `W<isoWeekYear>-<isoWeek>` (e.g.
`W2026-37`), **not** the content array's index. `isoWeekYear` is the ISO
8601 week-numbering year, not the calendar year of "now" — they diverge
at year boundaries (e.g. Jan 1, 2027 falls in ISO week 53 of 2026). Using
the calendar year here would either collide two different weeks onto the
same id or reset the rotation index unexpectedly right at New Year's;
using the week-numbering year keeps `isoWeekYear-isoWeek` monotonic and
collision-free across the boundary. The plan's task for this function
must implement (or use a library for) real ISO week-numbering-year
semantics, not `date.getUTCFullYear()`. Since the content list is
short, the same flavor text will recur every `list.length` weeks — keying
identity (and therefore claim-gating) to the real calendar week, not to
which content happens to be showing, means a player can earn the reward
again every week even when that week's narration repeats. The content
array index used to pick *which* entry to show is a separate, derived
value (`isoWeek % list.length`) never exposed as the id.

### Server: no new table

Progress is computed live, not tracked in a new table:

```
decisionsCompleted = COUNT(decisions)
  WHERE decisions.kingdomId = <caller's kingdom>
  AND decisions.createdAt BETWEEN <event week start> AND <event week end>
```

This reuses `decisions.createdAt` (already present, already indexed by
nothing special but queried the same way `listDecisionsSchema` already
does with a cursor). No migration needed beyond nothing.

### New endpoint: `GET /api/v1/events/active`

Mirrors `RequestHistory`'s shape (needs the caller's `kingdomId`, so it
gates on kingdom-exists the same way `/api/v1/decisions` does — unlike
the council endpoints, which never look up a kingdom).

Response:
```json
{
  "eventId": "W2026-37",
  "name": "Harvest Tithe",
  "narration": "The granaries are full and the court expects tribute...",
  "objectiveDecisionCount": 3,
  "decisionsCompleted": 2,
  "rewardMood": 15,
  "rewardLoyalty": 15
}
```

404 if the caller has no kingdom yet (same body/handling as
`/api/v1/decisions` and `/api/v1/history`).

### Client: `EventPanelController` (5th modal)

New file, same shape as `CouncilPanelController`: an `eventsButton`
trigger on the CoreLoop scene opens `panelRoot`, which shows narration +
`"{decisionsCompleted} / {objectiveDecisionCount} decisions"` + a Claim
button. Claim is interactable only once `decisionsCompleted >=
objectiveDecisionCount`. Clicking it:

1. Checks `manager.Ruler.State.ClaimedEventWeekId != response.eventId`.
2. If true: applies `manager.Ruler.State.ApplyDelta(rewardMood,
   rewardLoyalty)`, sets `ClaimedEventWeekId = response.eventId`, saves,
   refreshes status labels — identical shape to Council's
   `HandleStatusResult` reward branch, except the trigger is an explicit
   Claim button click rather than auto-applying on panel open (an event
   reward is a deliberate "claim your prize" moment, not a passive status
   check — this is a UX choice, not a technical one, and is cheap to
   change later if it doesn't feel right in the manual playtest).
3. If already claimed for this `eventId`: Claim button shows "Claimed"
   and stays disabled.

`RulerState` gains one new field: `public string ClaimedEventWeekId =
null;` — threaded through `RulerSaveData`/`SaveService` exactly like
`CouncilRewardApplied`/`TutorialCompleted` were.

### Modal mutual exclusion

`EventPanelController` joins the existing shared-control web the same way
`CouncilPanelController` did in milestone #7: its `SetCoreLoopControlsInteractable`
disables `historyButton`/`councilButton`/`submitButton`/`challengeButton`/
the 3 sliders/itself while open; `HistoryPanelController` and
`CouncilPanelController` each gain `eventsButton` in their own
`SetCoreLoopControlsInteractable` lists and disable it while they're open,
matching exactly how Council was added to History's list in milestone #7.

### `CoreLoopSceneBuilder.cs`

Adds the `EventsButton` UI element (24pt label per the established rule
comment above `CreateLabel()`), the `EventPanel` GameObject tree, and
wires `EventPanelController.Initialize(...)` with the same set of shared
control references History/Council already receive, plus the new
`eventsButton`/panel-specific fields.

## Known Gap Flagged, Not Fixed Here

Milestone #9 (`DuelModalGate`, fixing the duel-in-flight/modal-open race
for History and Council) is implemented but not yet merged to `main` —
its manual playtest is still pending. This milestone branches from `main`
and is therefore built against the **pre-#9** baseline: `EventPanelController`
will have the same latent race #9 fixes for the other two modals (a duel
resolving while the Events panel is open, or the Events panel closing
mid-duel, can leave `challengeButton` in the wrong state). This is
recorded in `docs/PROJECT_PLAN.md`'s "Known follow-up items" so that when
milestone #9 eventually merges, `DuelModalGate` gets threaded into
`EventPanelController` too, alongside History and Council. Not blocking
this milestone — it's the same class of gap those two already shipped
with before #9 existed.

## Data Flow

```
Player opens CoreLoop scene, taps "Events"
  -> EventPanelController.OnEventsButtonClicked():
     SetCoreLoopControlsInteractable(false), panelRoot active,
     coordinator.RequestActiveEvent(...)
  -> BackendSyncCoordinator.RequestActiveEvent: EnsureFreshSession ->
     EnsureKingdomThenSendEvent -> GET /api/v1/events/active
  -> server: isoWeek = ISO week of now; entry = EVENTS[isoWeek % EVENTS.length];
     eventId = "W{isoYear}-{isoWeek}"; range = that week's Mon 00:00 UTC..
     Sun 23:59:59 UTC; decisionsCompleted = COUNT(decisions) for caller's
     kingdom in range
  -> client shows narration + "{decisionsCompleted}/{objectiveDecisionCount}"
  -> if decisionsCompleted >= objectiveDecisionCount: Claim button enabled
  -> player taps Claim (only if ClaimedEventWeekId != eventId):
     ApplyDelta(rewardMood, rewardLoyalty), ClaimedEventWeekId = eventId,
     SaveService.Save, screenController.RefreshStatusLabels()
  -> player closes panel -> OnClose: SetCoreLoopControlsInteractable(true)
```

## Error Handling

`RequestActiveEvent` failures (network error, no kingdom yet, session
refresh failure) surface the error text in the panel's status message,
identical to `HandleStatusError`/`HandleCreateOrJoinError`'s existing
pattern — nothing new. No network call is made by the reward-claim step
itself; it's pure local state mutation, so it cannot fail beyond what
`SaveService.Save` already might (existing, unchanged failure mode).

## Testing

**Server (Vitest):** unit tests for the pure ISO-week-rotation function
(given a fixed date, returns the expected `eventId` and week-range
boundaries — including a week-boundary edge case, e.g. Sunday 23:59:59
UTC vs. Monday 00:00:00 UTC of the next event). Integration test for
`GET /api/v1/events/active` against a real Supabase-backed kingdom with a
few inserted `decisions` rows at controlled timestamps (some inside the
event window, some outside/from a previous week), asserting
`decisionsCompleted` counts only the in-window ones. 404-no-kingdom case
covered the same way `/api/v1/decisions`' existing test does.

**Client (PlayMode, deterministic, no real network):** `EventPanelController`
tests constructed the same way `CouncilPanelControllerTests` are — an
inactive `BackendSyncCoordinator` plus direct method calls — covering:
Claim button disabled below threshold, enabled at/above threshold, reward
applied exactly once (`ApplyDelta` called once even if Claim is clicked
twice / panel reopened), "Claimed" state persists across reopening the
panel (via `ClaimedEventWeekId` already set), and modal mutual-exclusion
with History/Council (opening Events disables their trigger buttons and
vice versa, matching the existing cross-modal tests' shape). A new scene
smoke test (`LoadedCoreLoopScene_EventsButton_OpensPanelWithoutThrowing`)
mirrors the one added for Council in milestone #9.

## Explicitly Out of Scope for This Pass

- Premium/IAP reward tier and any currency system.
- DB-authored or admin-editable event content; any scheduling beyond the
  deterministic ISO-week formula.
- Cosmetic rewards (blocked on FR-12).
- Threading `DuelModalGate` into `EventPanelController` (blocked on
  milestone #9 merging first — tracked as a follow-up item).
- More than one concurrent active event, event history/archive, or
  retroactive claiming of a past week's event.
