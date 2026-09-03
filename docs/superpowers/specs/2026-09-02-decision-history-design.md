# Design: Relationship History Log (Milestone #6, FR-06)

**Date:** 2026-09-02 | **Status:** Approved, pending implementation plan

## Purpose

`docs/PROJECT_PLAN.md` lists FR-06 ("view a relationship history log of past
decisions") as `[Nice to Have]`. `server/`'s `GET /api/v1/decisions` has
existed and worked since milestone #3 — cursor pagination, newest-first,
scoped to the caller's own kingdom — but the Unity client has never called
it. Milestones #3 and #4's final reviews both flagged this as deferred
client-side work. This milestone closes that gap: a player can open a
panel and see their own past decisions, using an endpoint that already
exists and is already tested.

## Scope Decisions

These were confirmed interactively before any design work began:

- **A toggleable panel within the existing single `CoreLoop` scene, not a
  new scene.** This project has never built scene-transition
  infrastructure, and this feature doesn't need it — a "View History"
  button that overlays a panel on top of the existing UI is
  architecturally simpler and matches the project's established
  minimal-UI pattern.
- **The panel is modal.** While open, the sliders, Submit, and Challenge
  buttons are non-interactive. Avoids coordinating "what if a decision
  gets submitted while history is showing a stale list" — a real
  complexity with no payoff for a first pass.
- **A single fixed page, no "Load More," no scrolling.** Up to 10 most
  recent decisions, shown as pre-created fixed row labels — the same
  programmatic-UI-building pattern this project has always used. A real
  scrollable, dynamically-paginated list is a materially larger feature
  (this project has never built `ScrollRect` + dynamic instantiation) and
  isn't needed yet: most players won't have more than 10-20 decisions at
  this stage of the game.
- **History-row formatting is its own small pure function, not a
  `DialogueTemplateEngine` extension.** `DialogueTemplateEngine`'s
  templates are single-purpose flavor narration for the moment a decision
  resolves ("The ruler nods..."); a history row needs a denser summary
  (cycle number, allocation, outcome, mood/loyalty) that doesn't fit that
  engine's existing shape. Keeping them separate avoids stretching an
  existing component's purpose.
- **No tap-outside-to-dismiss.** Close button only. Avoids a
  full-screen invisible hit-catcher for a marginal UX gain.

## Approach

**Reuse, don't reinvent.** The server-side endpoint, its auth, its
pagination, and its response shape all already exist and are already
tested (milestone #3). This milestone is entirely client-side.

**This is the project's first GET-based client call.** Every prior
`BackendApiClient` method (`EnsureKingdom`, `PostDecision`, `PostDuel`) is
a POST. `GetDecisionHistory` uses `UnityWebRequest.Get(url)` instead, but
keeps the same three-way error discrimination (network failure / JSON
parse failure / missing-field failure) and the same "surface the real
server error message to the player" behavior fixed in milestone #5's I-1
finding — not the generic-status-code mistake that finding caught.

**`RequestHistory` mirrors `RequestDuel`'s corrected structure directly,**
not its original structure: refresh-if-needed runs unconditionally first,
then the kingdom-readiness gate, then the send. Milestone #5 shipped this
exact ordering bug once already (session refresh silently skipped when the
kingdom wasn't ready yet) and fixed it after a re-review round; there's no
reason to reintroduce it here by copying the pre-fix shape.

**Empty state is deliberately ambiguous to the player.** `GET
/api/v1/decisions` returns `404` if the caller has no kingdom yet, or `200`
with an empty `decisions` array if they have a kingdom but no decisions
yet. Both cases mean the same thing to a player who's never played:
"nothing to show yet." The client collapses both into one friendly message
rather than making the player parse the distinction.

**Rejected alternative:** a real `ScrollRect`-based infinite list using the
server's actual cursor pagination. Rejected for this pass because the
project has no existing scrolling/dynamic-instantiation UI pattern to
build on, and the single-fixed-page version fully serves the stated
purpose (let a player see their recent history) without that added
subsystem. Revisit if a future milestone's player base genuinely
accumulates enough history that a fixed 10-row page stops being useful.

## Components

### `Assets/Scripts/Backend/DecisionHistoryResponse.cs` (new)
```csharp
using System;

namespace UnderstudyKingdom.Backend
{
    // Reuses PlayerRecommendationDto/RulerOutcomeDto from DecisionSyncRequest.cs --
    // the server stores those jsonb blobs verbatim as originally sent by
    // DecisionSyncRequestFactory, so the nested shape is identical here.
    [Serializable]
    public class DecisionHistoryEntry
    {
        public int cycleNumber;
        public PlayerRecommendationDto playerRecommendation;
        public RulerOutcomeDto rulerOutcome;
        public bool overridden;
    }

    [Serializable]
    public class DecisionHistoryResponse
    {
        public DecisionHistoryEntry[] decisions;
    }
}
```
`nextCursor` (present in the real response) is deliberately not mapped —
unused, since this pass doesn't paginate.

### `Assets/Scripts/Backend/BackendApiClient.cs` (modified)
One new method: `GetDecisionHistory(string accessToken, int limit,
Action<DecisionHistoryEntry[]> onSuccess, Action<string> onError)`. Calls
`GET {BackendBaseUrl}/api/v1/decisions?limit={limit}` via
`UnityWebRequest.Get`, with `Authorization: Bearer` header. On a non-2xx
response, attempts to parse `{"error": "..."}` from the response body and
prefers that message (matching milestone #5's `SendDuelRequest` fix),
falling back to a generic status message only when parsing fails.

### `Assets/Scripts/Backend/BackendSyncCoordinator.cs` (modified)
One new public method: `RequestHistory(int limit, Action<DecisionHistoryEntry[]>
onSuccess, Action<string> onError)`. Structure: session-expiry check and
refresh-if-needed run first and unconditionally; only once the access
token is known-fresh does it check `kingdomReady` (reusing the same flag
`RequestDuel` already maintains) and retry `EnsureKingdom` if needed;
finally calls `BackendApiClient.GetDecisionHistory`.

### `Assets/Scripts/UI/HistoryRowFormatter.cs` (new, pure)
```csharp
public static class HistoryRowFormatter
{
    public static string Format(DecisionHistoryEntry entry)
    {
        string outcome = entry.overridden ? "Overridden" : "Accepted";
        return $"Cycle {entry.cycleNumber}: Army {entry.playerRecommendation.army} / " +
               $"Trade {entry.playerRecommendation.trade} / Religion {entry.playerRecommendation.religion} " +
               $"-> {outcome} (Mood {entry.rulerOutcome.mood}, Loyalty {entry.rulerOutcome.loyalty})";
    }
}
```

### `Assets/Scripts/UI/HistoryPanelController.cs` (new)
Owns: the "View History" button, the panel root (hidden by default), up to
10 pre-created row `TextMeshProUGUI` labels, a close button, and
references to the 5 controls it disables while open (3 sliders, Submit,
Challenge).

- **On open:** disable the 5 controls, show the panel, call
  `BackendSyncCoordinator.RequestHistory(10, ...)`.
- **On success:** for each returned entry (up to 10), set the
  corresponding row's text via `HistoryRowFormatter.Format`; hide any
  unused rows (`gameObject.SetActive(false)`) if fewer than 10 came back.
  If the array is empty, show a single friendly message in the first row
  instead ("No decisions yet — submit your first recommendation!") and
  hide the rest.
- **On error:** if the error is the "no kingdom" 404, show the same
  friendly empty-state message as the empty-array case (see Scope
  Decisions — both mean the same thing to the player). Any other error
  shows the real server-provided message.
- **On close:** hide the panel, re-enable the 5 controls.

## Data Flow

```
Player taps "View History"
  -> HistoryPanelController: disable sliders/Submit/Challenge, show panel
  -> BackendSyncCoordinator.RequestHistory(10, ...)
       -> refresh session if expired (unconditional, first)
       -> ensure kingdom exists (retry if not yet ready)
       -> BackendApiClient.GetDecisionHistory(accessToken, 10, ...)
            -> GET /api/v1/decisions?limit=10
                 -> real, already-tested server logic (milestone #3)
  -> HistoryPanelController: populate up to 10 rows via HistoryRowFormatter,
     or show the friendly empty-state message

Player taps close
  -> HistoryPanelController: hide panel, re-enable sliders/Submit/Challenge
```

## Error Handling

- Network/protocol failure with no parseable body → generic message shown
  in the panel (not silently dropped — this is a player-initiated request,
  same philosophy as milestone #5's duel flow, not milestone #4's silent
  fire-and-forget sync).
- `404` "No kingdom found for this user" → treated identically to an empty
  `decisions` array: friendly "No decisions yet" message.
- Any other real server error (`{"error": "..."}`) → shown verbatim to the
  player, same pattern as `PostDuel`'s fixed error handling.
- Session refresh failure, no-session-yet → same messages `RequestDuel`
  already produces for these cases, reused verbatim for consistency.

## Testing

**EditMode:** `HistoryRowFormatter.Format` (pure, several cases including
overridden/accepted); `DecisionHistoryResponse`/`DecisionHistoryEntry`
deserialization from a hardcoded server-shaped JSON string (including the
nested `playerRecommendation`/`rulerOutcome` objects).

**PlayMode:** `BackendApiClient.GetDecisionHistory` against the real local
`server/` and real Supabase — sign in, `EnsureKingdom`, submit 2-3 real
decisions via the existing `PostDecision`, then fetch history and assert
the real returned entries match what was submitted (cycle numbers,
allocations, outcomes). `BackendSyncCoordinator.RequestHistory` — mirrors
`BackendSyncCoordinatorDuelTests`' structure and its retry-path coverage.
`HistoryPanelController`'s synchronous no-session error path (mirrors
`DuelButtonControllerTests`' deterministic, network-free pattern) plus one
real end-to-end test that opens the panel against a real session with real
submitted decisions and asserts the rendered row text.

## Explicitly Out of Scope for This Pass

- Real pagination (`ScrollRect`, dynamic row instantiation, "Load More"
  using the server's actual `nextCursor`).
- Tap-outside-to-dismiss.
- Any editing/deletion of history.
- Filtering or sorting beyond the server's existing newest-first order.
- Showing another player's history (not exposed by the server; out of
  scope regardless).
