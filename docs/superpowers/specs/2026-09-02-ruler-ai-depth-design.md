# Design: Ruler AI Depth (Milestone #2)

**Date:** 2026-09-02 | **Status:** Approved, pending implementation plan

## Purpose

Milestone #1 shipped a playable core loop, but `OverrideEvaluator`'s decision
logic is a 3-rule if/else cascade, and — as the milestone #1 design doc
explicitly flagged — `RulerState.Mood` is tracked and narrated but has zero
effect on the override decision. This milestone is the "revisit" that same
doc deferred: deepen the ruler's decision-making (FR-04) into a real
lightweight utility-AI, so mood becomes a genuine gameplay lever instead of
flavor text, without over-building into a full behavior tree the project
doesn't need yet.

Milestone #3+ (dialogue variety, decision history/FR-06, ruler AI's
downstream consumers like council/rival-ruler behavior) are explicitly out
of scope here — see "Explicitly Out of Scope" below.

## Scope Decisions

- **Decision logic only, not dialogue.** `DialogueTemplateEngine` keeps its
  existing two templates (`ruler_accept`/`ruler_override`); this milestone
  only changes *which* outcome is chosen, not how it's narrated. Milestone
  #1's data flow (`OverrideEvaluator.Evaluate` → `DialogueTemplateEngine.Resolve`)
  is unchanged.
- **Mood added as a weighted factor, loyalty kept dominant.** Loyalty was the
  near-deterministic factor in the old rule table (`Loyalty < 20` → 95%
  override, regardless of anything else); mood is new. Loyalty keeps a
  larger weight than mood in the new formula so "very low loyalty" still
  behaves like "the ruler has basically stopped listening," while mood adds
  a real but secondary swing on top.
- **No decision-history/streak tracking.** Explicitly deferred to FR-06
  ("relationship history log"), which is its own documented requirement and
  a separate milestone — conflating it here would mean designing new
  persisted state (streak counters) for a feature that isn't scoped yet.
- **Agenda-alignment stays binary**, reusing the existing `IsAligned` check
  unchanged (aligned → 0 contribution, misaligned → fixed bump). Making
  alignment a distance-based score (e.g. "how far under 40% army") is a
  bigger change than "make mood matter" calls for and isn't requested.
- **Utility-AI weighted formula, not a behavior tree.** Still one decision
  point (accept/override) — a branching node graph earns its cost only once
  there's genuine branching behavior (council reactions, rival rulers),
  which is unchanged reasoning from the milestone #1 design doc's own YAGNI
  call on this exact question.

## Approach

Already discussed and approved: a single weighted-sum formula (contributions
from loyalty, mood, and agenda-alignment, clamped to a probability range)
replaces the current if/else cascade in `OverrideEvaluator.OverrideProbability`.
Rejected alternatives (extending the if/else with a 4th mood rule; a full
behavior tree) carry the same trade-offs already covered in milestone #1's
design doc and aren't repeated here.

## Formula

```
OverrideProbability(state, allocation) =
    clamp(
        Baseline
        + (Neutral - state.Loyalty) * LoyaltyWeight
        + (Neutral - state.Mood)    * MoodWeight
        + (IsAligned(state.Agenda, allocation) ? 0 : AgendaMisalignedBump),
        MinProbability,
        MaxProbability
    )
```

- `Neutral = 50` — matches `RulerState`'s own default `Mood`/`Loyalty` values,
  so a freshly-created ruler with untouched stats sits exactly at each
  factor's zero-contribution point.
- `LoyaltyWeight > MoodWeight` — loyalty remains the dominant factor per the
  Scope Decisions above.
- `IsAligned` is the existing method on `OverrideEvaluator`, untouched.
- Exact numeric values for `Baseline`, `LoyaltyWeight`, `MoodWeight`,
  `AgendaMisalignedBump`, `MinProbability`, `MaxProbability` are tunable
  during implementation (same approach milestone #1's design doc took for
  its own thresholds) — the implementation plan picks concrete numbers and
  verifies the intended behavior via tests, rather than this document
  hardcoding untested magic numbers. Two directional constraints the chosen
  values must satisfy, verified by tests in the implementation plan:
  1. At neutral mood (50) and neutral-or-above loyalty, with an aligned
     allocation, the probability must be low (the "usually accepted"
     baseline case).
  2. At very low loyalty (e.g. 10) with neutral mood, the probability must
     be high (preserves the old "ruler has basically stopped listening"
     behavior), and moving mood from neutral to very low, holding loyalty
     and alignment fixed, must measurably increase the probability further
     (proves mood is a real, working lever).

## Components

### `OverrideEvaluator` (modified, `Assets/Scripts/NPC/OverrideEvaluator.cs`)

`OverrideProbability(RulerState, ResourceAllocation)` is rewritten per the
Formula above, replacing its current if/else body. `IsAligned` and
`Evaluate` (which calls `OverrideProbability` then compares against `roll`)
keep their existing signatures — this is a pure internal-logic change, no
public API changes. `OverrideResult`'s shape (`Overridden`, `MoodDelta`,
`LoyaltyDelta`) is unchanged; the mood/loyalty deltas applied on
accept/override still come from the existing fixed constants
(`AcceptedMoodDelta`, `OverriddenLoyaltyDelta`, etc.) — this milestone
changes what drives the *probability* of override, not what happens to
state *after* the outcome is decided.

## Data Flow

Unchanged from milestone #1, except the one box that computes probability:

```
UI slider input -> ResourceAllocation
  -> DecisionCycleManager.SubmitRecommendation(allocation, Random.value)
  -> OverrideEvaluator.Evaluate(RulerState, ResourceAllocation, roll)
       -> OverrideEvaluator.OverrideProbability(RulerState, ResourceAllocation)   [THIS MILESTONE: now weighs loyalty + mood + agenda]
       -> roll < probability ? Overridden : Accepted
  -> RulerState mutated (existing fixed mood/loyalty deltas, unchanged)
  -> SaveService.Save
  -> DialogueTemplateEngine.Resolve (unchanged: still 2 templates)
  -> UI shows outcome text
```

## Error Handling

No new error paths. `RulerState.Mood`/`Loyalty` are already clamped to
[0, 100] by `RulerState.ApplyDelta` (milestone #1), so `OverrideProbability`
never receives out-of-range inputs to guard against.

## Testing

`Assets/Tests/EditMode/OverrideEvaluatorTests.cs` (already exists, covers
the current rule table) gets new/updated cases proving:
- Neutral mood + high loyalty + aligned allocation → low probability.
- Low loyalty + neutral mood → high probability (loyalty still dominant).
- Neutral loyalty + low mood vs. neutral loyalty + neutral mood → the low-mood
  case has a measurably higher probability (proves mood is now a real,
  working factor, not just tracked-and-ignored).
- Misaligned agenda still adds its bump on top of loyalty/mood contributions
  (proves the three factors combine additively, not just whichever wins).
- Probability stays within `[MinProbability, MaxProbability]` at extreme
  inputs (loyalty=0, mood=0, misaligned — and the opposite extreme) — proves
  the clamp works.

`Assets/Tests/EditMode/DecisionCycleManagerTests.cs`'s two existing
probability-dependent tests (`SubmitRecommendation_Accepted_...` and
`SubmitRecommendation_Overridden_...`) currently assert outcomes computed
against the *old* formula. Per the Formula section's callout, their
`roll:` values (and `Loyalty = 10` setup for the override test) will need
recalculating against the new formula so they still deterministically land
on the intended accept/override outcome — this is expected, not a
regression, and the implementation plan handles it explicitly rather than
leaving it as a surprise test failure to debug later.

## Explicitly Out of Scope for This Pass

- Dialogue variety / history-keyed narration (FR-05 beyond what already
  ships) — a separate milestone.
- Decision-history/streak tracking (FR-06, "relationship history log") —
  its own documented requirement, deliberately not folded in here.
- Any change to `ResourceAllocation`, agenda-alignment distance scoring,
  `DialogueTemplateEngine`, `SaveService`, or any UI (`CoreLoopScreenController`,
  the scene) — this milestone is `OverrideEvaluator`'s internal formula
  only.
- Council/rival-ruler AI, or any behavior-tree-requiring branching logic —
  still only one decision point; revisit if that changes.

## Known Tuning Debt (Recorded, Not Fixed)

A final whole-branch review (101x101 grid sweep over Loyalty/Mood, plus a
hand-traced accept-cycle trajectory) found that with the shipped constants
(`Baseline=0.10`, `LoyaltyWeight=0.012`, `MoodWeight=0.005`,
`MinProbability=0.02`), an aligned ruler at neutral mood saturates to the flat
2% floor as soon as `Loyalty >= 57` -- reachable after only about two accepted
recommendation cycles from a fresh ruler (`Loyalty=50, Mood=50` ->
`Loyalty=56, Mood=60` after one accept, past the floor after a second). This
happens because `Baseline` only has 0.08 of headroom down to `MinProbability`,
while loyalty alone can swing the formula by up to ±0.60 (`50 * 0.012`); once
loyalty pushes past that floor, mood's much smaller swing can no longer move
the probability at all. The formula satisfies the design doc's literal
directional constraints (loyalty stays dominant, mood is a real lever in
general), but along the game's most common play path it undercuts the
milestone's headline goal of "mood becomes a genuine gameplay lever." This is
a candidate for a future tuning pass -- e.g. widening the Baseline-to-floor
gap, lowering `MinProbability` further, or reducing `LoyaltyWeight`'s
per-point swing -- not something this milestone attempted to fix, since
re-tuning constants would require re-deriving and re-verifying every existing
test's expected values.
