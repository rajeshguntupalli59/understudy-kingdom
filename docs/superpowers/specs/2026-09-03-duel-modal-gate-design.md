# Design: Duel/Modal Gate Fix (Milestone #9)

**Date:** 2026-09-03 | **Status:** Approved, pending implementation plan

## Purpose

Both milestone #6's and milestone #7's final whole-branch reviews flagged
the same real gap, recorded as a deferred item in
`docs/PROJECT_PLAN.md`'s "Known follow-up items": `DuelButtonController`
is the CoreLoop scene's one non-modal action — it disables only its own
`challengeButton` while a duel request is in flight, and re-enables it
unconditionally when the request resolves. Meanwhile
`HistoryPanelController` and `CouncilPanelController` are both modal —
opening either disables a shared set of 7 controls (3 sliders, Submit,
Challenge, the other modal's own trigger button, and itself) and closing
re-enables all 7 unconditionally. History and Council already mutually
exclude each other, but neither is aware of Duel's independent in-flight
state, and Duel isn't aware of whether a modal is currently open. Two
concrete failure modes: (1) a duel resolving while a modal is open
re-enables `challengeButton` underneath it; (2) closing a modal while a
duel is still in flight re-enables `challengeButton` before the duel has
actually resolved, permitting a second concurrent duel request. This
milestone closes both.

## Scope Decisions

These were confirmed interactively before any design work began:

- **A minimal 2-flag shared state object, not a full N-way
  reference-counted gate.** This is genuinely a two-independent-source
  problem, not an N-source one: History and Council already fully
  mutually exclude each other (never both open at once), so the other 6
  shared controls only ever have one thing wanting them disabled at a
  time, and their existing direct-set logic is already correct. Only
  `challengeButton` has two independent, potentially-overlapping sources
  of "wants disabled" — Duel's own in-flight state, and whichever modal
  happens to be open. Rejected alternative: a central MonoBehaviour owning
  all 8 shared-control references with `Acquire(this)`/`Release(this)`
  reference counting — handles arbitrary future overlapping modals
  automatically, but is real surgery across all 4 controllers' `Initialize()`
  signatures for a generality this codebase doesn't currently need.
- **The new state object is a plain C# class, not a `MonoBehaviour`.** It
  holds no Unity component references, just two bools — no reason to make
  it a scene object. Constructed once in `CoreLoopSceneBuilder.Build()`
  and passed by reference into the three controllers' existing
  `Initialize()` calls (one new trailing parameter each), matching this
  project's established dependency-injection-via-`Initialize()`-args
  convention rather than introducing any new static/singleton state.
- **Deterministic tests only, no real duel.** The bug is local
  interactable-flag bookkeeping (does `challengeButton` end up in the
  right state given a sequence of open/close/start/resolve calls), not
  duel-resolution correctness — already covered elsewhere
  (`BackendSyncCoordinatorDuelTests`, `DuelButtonController`'s own
  existing tests). New tests call each controller's relevant methods
  directly, mirroring how `HistoryPanelControllerTests`/
  `CouncilPanelControllerTests` already avoid real network dependencies.
  No real Supabase sign-in needed, and doesn't add to this project's
  cumulative sign-in count (which has hit rate limits before).

## Approach

**`DuelModalGate` is the single new type this milestone adds.** Two
public bool properties, `IsDuelInFlight` and `IsModalOpen`. No methods,
no validation, no events — the three controllers read and write it
directly, matching the simplicity of what's actually being tracked.

**Only `challengeButton`'s enable/disable decisions change.** Every other
control's behavior in `HistoryPanelController`/`CouncilPanelController`/
`DuelButtonController` is untouched.

## Components

### `Assets/Scripts/UI/DuelModalGate.cs` (new)
```csharp
namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Tracks the two independent things that can want the shared
    /// "Challenge a Rival Kingdom" button disabled at once: a duel actually
    /// in flight, and a modal panel (History or Council) currently open.
    /// Nothing else needs this -- the other 6 shared controls only ever have
    /// one thing wanting them disabled (whichever modal is open, since
    /// History and Council already mutually exclude each other), so their
    /// existing direct interactable-toggling logic stays untouched. See
    /// docs/superpowers/specs/2026-09-03-duel-modal-gate-design.md.
    /// </summary>
    public class DuelModalGate
    {
        public bool IsDuelInFlight { get; set; }
        public bool IsModalOpen { get; set; }
    }
}
```

### `Assets/Scripts/UI/DuelButtonController.cs` (modified)
`Initialize(...)` gains a trailing `DuelModalGate gate` parameter.
`OnChallenge()` sets `gate.IsDuelInFlight = true` alongside the existing
`challengeButton.interactable = false`. `HandleResult`/`HandleError` both
set `gate.IsDuelInFlight = false` first, then re-enable `challengeButton`
only if `!gate.IsModalOpen` — otherwise leave it disabled, to be picked up
by whichever modal is open when it closes.

### `Assets/Scripts/UI/HistoryPanelController.cs` / `Assets/Scripts/UI/CouncilPanelController.cs` (modified)
Both gain the same shape of change. `Initialize(...)` gains a trailing
`DuelModalGate gate` parameter. The open handler (`OnViewHistory`/
`OnCouncilButtonClicked`) sets `gate.IsModalOpen = true`. `OnClose` sets
`gate.IsModalOpen = false` before calling the existing
`SetCoreLoopControlsInteractable(true)`. That method's handling of
`challengeButton` changes: when disabling (`interactable == false`), it's
always set unconditionally as before — opening a modal always covers
Challenge regardless of duel state. When enabling
(`interactable == true`), `challengeButton` is skipped entirely if
`gate.IsDuelInFlight` is still true; otherwise it's re-enabled like the
other 6 controls.

### `Assets/Editor/CoreLoopSceneBuilder.cs` (modified)
Constructs one `DuelModalGate` instance before wiring Duel/History/Council,
and passes it as the new trailing argument to all three `Initialize()`
calls.

## Data Flow

```
Player taps Challenge
  -> DuelButtonController.OnChallenge(): challengeButton disabled,
     gate.IsDuelInFlight = true
  -> real duel request in flight

Case A: modal opens while duel is in flight
  -> History/Council's open handler: 7 controls disabled (including
     challengeButton, already disabled -- no-op change there),
     gate.IsModalOpen = true
  -> duel resolves -> HandleResult/HandleError: gate.IsDuelInFlight =
     false; gate.IsModalOpen is true, so challengeButton stays disabled
  -> player closes the modal -> OnClose: gate.IsModalOpen = false ->
     SetCoreLoopControlsInteractable(true) -> gate.IsDuelInFlight is
     false, so challengeButton is re-enabled along with the other 6

Case B: duel resolves normally, no modal ever opens
  -> gate.IsModalOpen stays false throughout -> HandleResult/HandleError
     re-enables challengeButton immediately, as today

Case C: modal opens and closes with no duel involved
  -> gate.IsDuelInFlight stays false throughout -> OnClose's
     SetCoreLoopControlsInteractable(true) re-enables challengeButton
     immediately, as today
```

## Error Handling

None of this feature makes a network call itself — it's pure local state
composition around existing, already-error-handled duel/modal flows. No
new failure modes introduced.

## Testing

**PlayMode (deterministic, no network):** a new test file constructs a
`DuelButtonController` and a `HistoryPanelController` (or `CouncilPanelController`
— either is representative, since both apply the identical fix) sharing
one `DuelModalGate`, and drives both interleavings from the Data Flow
section above via direct method/`onClick.Invoke()` calls, asserting
`challengeButton.interactable` at each step. Existing `DuelButtonController`,
`HistoryPanelController`, and `CouncilPanelController` test files' call
sites need their `Initialize(...)` calls updated for the new trailing
parameter (mirroring how milestone #7's Council/History mutual-exclusion
change updated the same call sites for its own new parameter).

## Explicitly Out of Scope for This Pass

- Generalizing to a full N-way reference-counted gate for other, currently
  nonexistent future modals.
- Any change to the other 6 shared controls' interactable logic.
- Any change to duel resolution, error messaging, or narration.
