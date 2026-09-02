# Design: Core Loop Vertical Slice (Milestone #1)

**Date:** 2026-09-01 | **Status:** Approved, pending implementation plan

## Purpose

`docs/superpowers/specs/2026-09-01-core-decision-cycle-design.md` explicitly
scoped out "any UI visual design — this spec covers logic/data flow only."
That backend logic (`DecisionCycleManager`, `RulerNpcController`,
`OverrideEvaluator`, `DialogueTemplateEngine`, `SaveService`) is now
implemented and covered by 22 passing EditMode tests, but there is still no
scene, no UI, and nothing a player (or a reviewer in the Editor) can
actually click through. This spec covers exactly that gap: the first
playable vertical slice of FR-01/FR-02/FR-03, built entirely on top of the
existing, already-tested backend — no new decision logic.

This is milestone #1 of a larger decomposition (see the plan discussion
that preceded this spec): core loop → ruler AI depth → backend service →
social/PvP → live-ops/monetization → compliance. Only milestone #1 is in
scope here.

## Scope Decisions

- **Target: Unity Editor Play Mode, not a device/emulator build.** Fastest
  iteration loop; an Android build is a separate follow-up milestone once
  the loop itself is proven and looks right. Building/signing an APK and
  setting up an Android SDK/emulator is comparable in effort to the Unity
  Editor install done for this session and shouldn't gate seeing the core
  loop work.
- **One recommendation type: `ResourceAllocation` only** (Army/Trade/
  Religion, sum to 100) — the only type implemented in the backend pass.
  Matches the existing scope decision in the referenced design doc.
- **Repeatable loop, not a single cycle.** After the ruler responds, the
  screen resets for another submission in the same session — this is what
  actually demonstrates FR-03 (state persisting/evolving across cycles)
  rather than a single before/after snapshot.
- **Sliders, not number fields.** Three linked `Slider`s that auto-rebalance
  so the total always stays at 100 — makes an invalid allocation
  structurally impossible, so no validation UI is needed.
- **Mood/Loyalty/Agenda always visible**, not just the narration text —
  makes the persistence/evolution mechanic legible on screen, not just
  implied.
- **No new gameplay/decision logic.** This is UI wiring over the existing,
  tested `DecisionCycleManager.SubmitRecommendation` and
  `LoadPersistedStateIfPresent`. If a UI requirement seems to need new
  decision logic, that's out of scope for this pass.

## Approach: UI Architecture

| # | Approach | Verdict |
|---|---|---|
| **A** | **Single screen controller** (`CoreLoopScreenController`) directly wired to the Canvas widgets and `DecisionCycleManager` | **Chosen** — this is one screen proving one loop; a split-view architecture is premature for a vertical slice with nothing yet to share it with |
| B | Split MVC-ish views (`AllocationInputView` + `RulerStatusView` + thin controller) | Rejected for now — more files/indirection than one screen needs; revisit once a second screen exists and boundaries are worth drawing |
| C | UI Toolkit (UI Elements) instead of uGUI | Rejected — no existing UI convention in this project to match; uGUI (Canvas/Slider/TextMeshProUGUI) is simpler and more battle-tested for a mobile-bound prototype |

## Components

### `Assets/Scenes/CoreLoop.unity` (new scene)
Contains:
- A `Ruler` GameObject with `RulerNpcController` and a `Manager` GameObject
  with `DecisionCycleManager` — the same shape the EditMode tests already
  build in code, now authored in a scene.
- A `Canvas` with:
  - 3 `Slider`s (Army / Trade / Religion), linked so dragging one
    proportionally adjusts the other two and the total always reads 100.
  - 3 `TextMeshProUGUI` labels: current Mood, Loyalty, Agenda.
  - 1 `TextMeshProUGUI` for the ruler's narrated response.
  - 1 `Button`: "Submit Recommendation".

### `CoreLoopScreenController` (new `MonoBehaviour`, `Assets/Scripts/UI/`)
The only new behavior in this pass. Responsibilities:
- Holds serialized references to the three sliders, the three status
  labels, the narration text, and the `DecisionCycleManager`.
- On `Start()`: renders the manager's current `Ruler.State` into the status
  labels (post-`Awake()`/load).
- On slider `onValueChanged`: rebalances the other two sliders so the sum
  stays at 100 (standard proportional-rebalance: the two non-dragged
  sliders absorb the delta in proportion to their current values, clamped
  to zero).
- On Submit click: reads the three slider values into a `ResourceAllocation`,
  calls `manager.SubmitRecommendation(allocation, UnityEngine.Random.value)`
  (matches the existing doc comment on `SubmitRecommendation` — real
  call sites pass `UnityEngine.Random.value`, not a caller-fixed roll),
  writes the returned narration string into the narration text field, and
  refreshes the three status labels from `Ruler.State`.
- Contains no decision logic — every value it displays or submits comes
  from existing, already-tested backend calls.

## Data Flow

```
Slider drag (any of the 3)
  -> CoreLoopScreenController rebalances the other two so sum == 100
Submit click
  -> ResourceAllocation built from slider values (always valid by construction)
  -> DecisionCycleManager.SubmitRecommendation(allocation, Random.value)  [existing, tested]
       -> OverrideEvaluator.Evaluate  [existing, tested]
       -> RulerState mutated, SaveService.Save  [existing, tested]
       -> DialogueTemplateEngine.Resolve  [existing, tested]
  -> CoreLoopScreenController writes narration text + refreshes Mood/Loyalty/Agenda labels
```

## Error Handling

- Invalid (non-100-sum) allocation: structurally impossible by construction
  (linked sliders), so no validation UI is needed — matches
  `ResourceAllocation.IsValid()` staying true at all times.
- Missing/corrupt save file: already handled by the existing `SaveService`
  (returns a fresh default `RulerState`); nothing new needed here.
- No backend/network calls exist in this slice, so no network error
  handling is in scope.

## Testing

- No changes to `DecisionCycleManager`, `OverrideEvaluator`,
  `DialogueTemplateEngine`, `RulerState`, or `SaveService` — the existing 22
  EditMode tests continue to cover that logic unchanged.
- New PlayMode tests for `CoreLoopScreenController`:
  - Dragging one slider rebalances the other two so the sum stays 100.
  - Submitting updates the narration text and the three status labels to
    match the manager's post-submit `Ruler.State`.
- Manual verification (Definition of Done for this pass): open
  `CoreLoop.unity`, enter Play Mode, run several cycles, confirm
  Mood/Loyalty/Agenda visibly change and the narration text matches
  accept/override outcomes; stop and re-enter Play Mode to confirm state
  persists via the existing save/load path.

## Explicitly Out of Scope for This Pass

- Android/iOS build and emulator/device testing — separate follow-up
  milestone once this slice is proven in the Editor.
- Army move and diplomatic choice recommendation types — still deferred
  per the original design doc.
- Any visual polish, art, or theming — placeholder uGUI widgets only.
- A start/menu screen, tutorial (FR-13), or new-game/continue flow — the
  scene loads directly into the loop and silently loads any existing save,
  same as today's `Awake()` behavior.
- Backend/API integration, social/PvP/live-ops/monetization — unrelated
  milestones per the decomposition this spec's Purpose section references.
