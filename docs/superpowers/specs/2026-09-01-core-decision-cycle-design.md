# Design: Core Decision Cycle (First Playable Slice)

**Date:** 2026-09-01 | **Status:** Approved, pending implementation plan

## Purpose

Turn the `Assets/Scripts/Core/DecisionCycleManager.cs` and
`Assets/Scripts/NPC/RulerNpcController.cs` stubs (from PR #1, currently
`NotImplementedException` throws mapped to FR-01–FR-04 in
`docs/PROJECT_PLAN.md`) into a real, playable vertical slice: the player
submits a recommendation, the ruler NPC accepts or overrides it based on
mood/loyalty/agenda, and the outcome persists across sessions.

## Scope Decisions

- **One recommendation type for this pass: resource allocation only.**
  Army moves and diplomatic choices (also named in FR-01) are deferred to a
  later pass once this loop is proven end-to-end. Rationale: a vertical
  slice with one recommendation type validates the whole loop (evaluate →
  narrate → persist) faster than three half-built types.
- **Persistence: local JSON save file**, not a backend call. The backend
  (Node.js + PostgreSQL, `docs/PROJECT_PLAN.md` §7) hasn't been designed
  yet — that's a separate planning pass. This satisfies FR-03's "persist"
  requirement for a single-device session without inventing backend API
  shape prematurely; the save/load interface is written so a future
  backend-backed implementation can sit behind the same method signatures.
- **Environment constraint:** this design was produced in a sandbox with no
  Unity Editor and no dotnet/mono toolchain available (`dotnet --version`
  fails — confirmed before writing this spec). Nothing in this plan can be
  compiled or test-run in that environment. The design compensates by
  keeping all decision logic in plain C# with zero `UnityEngine`
  dependency, so it's unit-testable the moment a real toolchain (Unity
  Editor locally, or CI) is available — but that testing has not happened
  yet and is explicitly part of the verification section below, owned by
  whoever runs this in an actual Unity install.

## Approach: Override Decision Logic

Three approaches were considered:

| # | Approach | Verdict |
|---|---|---|
| A | Single weighted-probability formula from loyalty+mood | Rejected — risks feeling mechanically flat, same math every cycle |
| **B** | **Rule-based decision table** (ordered conditions → probability) | **Chosen** — reads as "personality" without a full BT engine; simple to tune |
| C | Full behavior-tree (Selector/Sequence/Condition nodes) | Rejected for now — over-engineered for one decision point; revisit when council/rival-ruler AI needs real branching (YAGNI) |

## Components

All new types live under `Assets/Scripts/`. None replace the existing
stubs' public shape (`DecisionCycleManager`, `RulerNpcController`,
`DialogueTemplateEngine`) — they fill in the logic those stubs currently
throw `NotImplementedException` for.

### `ResourceAllocation` (plain C# struct/class, `Assets/Scripts/Core/`)
Data only. Three percentages (`army`, `trade`, `religion`) that must sum to
100 — validated at the UI layer, not here (see Error Handling).

### `RulerState` (plain C# class, `Assets/Scripts/NPC/`, no `UnityEngine` import)
Replaces the bare `mood`/`loyalty`/`agenda` fields currently on the
`RulerNpcController` MonoBehaviour with a standalone, JSON-serializable
class the MonoBehaviour holds a reference to. This is the seam that makes
the logic testable outside Unity.

### `OverrideEvaluator` (plain C# static class, `Assets/Scripts/NPC/`)
Pure function:
```
(bool overridden, int moodDelta, int loyaltyDelta) Evaluate(RulerState state, ResourceAllocation allocation)
```
Implements the rule table, e.g. (exact thresholds tunable during
implementation, not fixed by this design):
1. `loyalty < 20` → override near-certain regardless of allocation
2. allocation misaligned with `state.agenda` (e.g. low army% under an
   Expansionist agenda) → override probability +20%
3. otherwise → baseline low override probability
No side effects, no Unity API calls — this is the unit the Definition of
Done requires tests for once a toolchain exists.

### `RulerSaveData` + `SaveService` (plain C# + one thin Unity-dependent wrapper, `Assets/Scripts/Core/`)
`RulerSaveData` is a JSON-serializable DTO mirroring `RulerState`.
`SaveService` wraps `Application.persistentDataPath` (the one
Unity-specific call in this component) to read/write that JSON. Missing or
corrupt save file → returns a fresh default `RulerState` rather than
throwing (handles first launch).

### `DecisionCycleManager` (MonoBehaviour, existing stub — filled in)
Thin orchestrator only: UI input → `ResourceAllocation` → calls
`OverrideEvaluator.Evaluate` → applies delta to `RulerState` → calls
`SaveService.Save` → passes the mood/override outcome to
`DialogueTemplateEngine.Resolve` for narration. No decision logic of its
own — that all lives in `OverrideEvaluator` so it stays testable.

## Data Flow

```
UI slider input
  -> ResourceAllocation (validated: sums to 100)
  -> OverrideEvaluator.Evaluate(RulerState, ResourceAllocation)
  -> RulerState mutated (mood/loyalty delta applied)
  -> SaveService.Save(RulerState)
  -> DialogueTemplateEngine.Resolve(templateTag, variables)
  -> UI shows outcome text
```

## Error Handling

- Allocation percentages not summing to 100: blocked at the UI layer before
  it ever reaches `OverrideEvaluator` — not this component's concern.
- Missing/corrupt save file on load: `SaveService` returns a fresh default
  `RulerState`, does not throw. This is the only error path in scope for
  this pass.
- Out of scope for this pass: network errors (no backend call exists yet),
  concurrent save writes (single-player, single save slot for now).

## Testing

`OverrideEvaluator`, `RulerState`, and `RulerSaveData` have zero
`UnityEngine` dependency specifically so they can be unit-tested with
Unity's Test Framework (Edit Mode tests) or plain NUnit without needing
Play Mode. **This has not been verified in this session** — no Unity
Editor or dotnet toolchain was available to run it. Verification is the
first step of the implementation plan's Definition of Done, owned by
whoever runs this locally.

## Explicitly Out of Scope for This Pass

- Army move and diplomatic choice recommendation types (FR-01, deferred)
- Backend persistence / API integration (PROJECT_PLAN.md §7, separate
  planning pass)
- Council (FR-07/08) and async PvP (FR-09) — unrelated to this loop
- Any UI visual design — this spec covers logic/data flow only
