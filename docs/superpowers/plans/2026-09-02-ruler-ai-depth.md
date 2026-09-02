# Ruler AI Depth Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `OverrideEvaluator`'s 3-rule if/else cascade with a weighted utility-AI formula so ruler mood becomes a real factor in the accept/override decision, alongside loyalty and agenda-alignment.

**Architecture:** One pure function (`OverrideEvaluator.OverrideProbability`) is rewritten from branching rules to a weighted sum of three contributions (loyalty, mood, agenda-alignment), clamped to a probability range. No other component changes — `Evaluate`'s signature, `OverrideResult`, and everything downstream (`DecisionCycleManager`, `DialogueTemplateEngine`, `SaveService`, UI) stays untouched.

**Tech Stack:** Unity 6000.3.23f1 (already installed/licensed at `C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe`), C#, NUnit via Unity Test Framework (EditMode only — no PlayMode/UI changes in this milestone).

## Global Constraints

- Only `Assets/Scripts/NPC/OverrideEvaluator.cs` changes in production code — no changes to `DecisionCycleManager`, `RulerState`, `DialogueTemplateEngine`, `SaveService`, `SliderRebalancer`, `CoreLoopScreenController`, or any scene/UI file, per `docs/superpowers/specs/2026-09-02-ruler-ai-depth-design.md`.
- `IsAligned`, `Evaluate`'s signature, and `OverrideResult`'s shape are unchanged.
- `LoyaltyWeight` must be strictly greater than `MoodWeight` (loyalty stays the dominant factor).
- The full EditMode suite (35 tests after this milestone: 29 existing + 6 new in `OverrideEvaluatorTests.cs`) must pass. No PlayMode changes needed.
- Every git commit message ends with:
  ```
  Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_017JNi1ThZutdGUt6toaGBmq
  ```

---

## Task 1: Weighted override-probability formula

**Files:**
- Modify: `Assets/Scripts/NPC/OverrideEvaluator.cs`
- Modify: `Assets/Tests/EditMode/OverrideEvaluatorTests.cs` (full rewrite of its contents)
- Modify: `Assets/Tests/EditMode/DecisionCycleManagerTests.cs` (comment-only changes, 2 lines — see Step 6)

**Interfaces:**
- Consumes: `UnderstudyKingdom.Core.ResourceAllocation` (existing, unchanged: `Army`/`Trade`/`Religion` ints). `UnderstudyKingdom.Npc.RulerState` (existing, unchanged: `Mood`/`Loyalty` ints, `Agenda` enum).
- Produces: `OverrideEvaluator.OverrideProbability(RulerState state, ResourceAllocation allocation) -> double` (already public, signature unchanged, only its internal formula changes — this is the method later tasks/milestones would call if they need the raw probability, e.g. a future UI hint). `OverrideEvaluator.Evaluate(RulerState, ResourceAllocation, double roll) -> OverrideResult` (unchanged signature). `OverrideEvaluator.IsAligned(RulerState.AgendaType, ResourceAllocation) -> bool` (unchanged, reused as-is).

- [ ] **Step 1: Write the new/changed failing tests**

Replace the entire contents of `Assets/Tests/EditMode/OverrideEvaluatorTests.cs` with:

```csharp
using NUnit.Framework;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.Tests
{
    public class OverrideEvaluatorTests
    {
        [Test]
        public void VeryLowLoyalty_NeutralMood_Aligned_RollBelowProbability_Overrides()
        {
            var state = new RulerState { Mood = 50, Loyalty = 10, Agenda = RulerState.AgendaType.Expansionist };
            var allocation = new ResourceAllocation(50, 30, 20); // Army 50 >= 40 -> aligned

            var result = OverrideEvaluator.Evaluate(state, allocation, roll: 0.30);

            Assert.IsTrue(result.Overridden);
        }

        [Test]
        public void HighLoyalty_AlignedAgenda_LowRoll_DoesNotOverride()
        {
            var state = new RulerState { Mood = 50, Loyalty = 80, Agenda = RulerState.AgendaType.Mercantile };
            var allocation = new ResourceAllocation(20, 60, 20);

            var result = OverrideEvaluator.Evaluate(state, allocation, roll: 0.50);

            Assert.IsFalse(result.Overridden);
        }

        [Test]
        public void NeutralLoyalty_MisalignedAgenda_MidRoll_Overrides()
        {
            var state = new RulerState { Mood = 50, Loyalty = 50, Agenda = RulerState.AgendaType.Pious };
            var allocation = new ResourceAllocation(50, 30, 20); // Religion 20 < 40 threshold -> misaligned

            var result = OverrideEvaluator.Evaluate(state, allocation, roll: 0.25);

            Assert.IsTrue(result.Overridden);
        }

        [Test]
        public void NotOverridden_AppliesPositiveDeltas()
        {
            var state = new RulerState { Mood = 50, Loyalty = 80, Agenda = RulerState.AgendaType.Mercantile };
            var allocation = new ResourceAllocation(20, 60, 20);

            var result = OverrideEvaluator.Evaluate(state, allocation, roll: 0.99);

            Assert.AreEqual(5, result.MoodDelta);
            Assert.AreEqual(3, result.LoyaltyDelta);
        }

        [Test]
        public void Overridden_AppliesNegativeDeltas()
        {
            var state = new RulerState { Mood = 50, Loyalty = 10, Agenda = RulerState.AgendaType.Expansionist };
            var allocation = new ResourceAllocation(50, 30, 20);

            var result = OverrideEvaluator.Evaluate(state, allocation, roll: 0.30);

            Assert.AreEqual(-10, result.MoodDelta);
            Assert.AreEqual(-5, result.LoyaltyDelta);
        }

        [Test]
        public void HighLoyalty_NeutralMood_Aligned_ProbabilityIsLow()
        {
            var state = new RulerState { Mood = 50, Loyalty = 80, Agenda = RulerState.AgendaType.Mercantile };
            var allocation = new ResourceAllocation(20, 60, 20); // aligned

            double probability = OverrideEvaluator.OverrideProbability(state, allocation);

            Assert.LessOrEqual(probability, 0.10);
        }

        [Test]
        public void LowLoyalty_NeutralMood_ProbabilityIsHigh()
        {
            var state = new RulerState { Mood = 50, Loyalty = 10, Agenda = RulerState.AgendaType.Expansionist };
            var allocation = new ResourceAllocation(50, 30, 20); // aligned

            double probability = OverrideEvaluator.OverrideProbability(state, allocation);

            Assert.GreaterOrEqual(probability, 0.50);
        }

        [Test]
        public void LowLoyalty_LoweringMoodFurther_IncreasesProbabilityFurther()
        {
            var allocation = new ResourceAllocation(50, 30, 20); // aligned for Expansionist
            var neutralMoodState = new RulerState { Mood = 50, Loyalty = 10, Agenda = RulerState.AgendaType.Expansionist };
            var lowMoodState = new RulerState { Mood = 10, Loyalty = 10, Agenda = RulerState.AgendaType.Expansionist };

            double neutralMoodProbability = OverrideEvaluator.OverrideProbability(neutralMoodState, allocation);
            double lowMoodProbability = OverrideEvaluator.OverrideProbability(lowMoodState, allocation);

            Assert.Greater(lowMoodProbability, neutralMoodProbability);
        }

        [Test]
        public void MisalignedAgenda_AddsBumpOnTopOfLoyaltyAndMood()
        {
            var state = new RulerState { Mood = 50, Loyalty = 50, Agenda = RulerState.AgendaType.Pious };
            var alignedAllocation = new ResourceAllocation(20, 20, 60); // Religion 60 >= 40 -> aligned
            var misalignedAllocation = new ResourceAllocation(50, 30, 20); // Religion 20 < 40 -> misaligned

            double alignedProbability = OverrideEvaluator.OverrideProbability(state, alignedAllocation);
            double misalignedProbability = OverrideEvaluator.OverrideProbability(state, misalignedAllocation);

            Assert.Greater(misalignedProbability, alignedProbability);
        }

        [Test]
        public void ExtremeWorstCase_ClampsToMaxProbability()
        {
            var state = new RulerState { Mood = 0, Loyalty = 0, Agenda = RulerState.AgendaType.Pious };
            var allocation = new ResourceAllocation(50, 30, 20); // Religion 20 < 40 -> misaligned

            double probability = OverrideEvaluator.OverrideProbability(state, allocation);

            Assert.AreEqual(0.95, probability, 0.0001);
        }

        [Test]
        public void ExtremeBestCase_ClampsToMinProbability()
        {
            var state = new RulerState { Mood = 100, Loyalty = 100, Agenda = RulerState.AgendaType.Mercantile };
            var allocation = new ResourceAllocation(20, 60, 20); // aligned

            double probability = OverrideEvaluator.OverrideProbability(state, allocation);

            Assert.AreEqual(0.02, probability, 0.0001);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify the expected failures**

```bash
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" \
  -runTests -batchmode \
  -projectPath "C:\Users\rajes\understudy-kingdom" \
  -testResults "C:\Users\rajes\understudy-kingdom\TestResults\milestone2-task1-red.xml" \
  -testPlatform EditMode \
  -testFilter "UnderstudyKingdom.Tests.OverrideEvaluatorTests" \
  -logFile "C:\Users\rajes\understudy-kingdom\TestResults\milestone2-task1-red.log" \
  -nographics
```

Expected: non-zero exit code, but only 2 of the 11 tests actually fail — the old rule table's binary behavior (`loyalty < 20 → 0.95`, else `aligned ? 0.10 : 0.30`) coincidentally satisfies most of the new assertions too (e.g. old `loyalty=80,aligned` already gives exactly 0.10, satisfying `LessOrEqual(0.10)`; old `loyalty=50,misaligned` already gives 0.30, exceeding the aligned case's 0.10). The two that genuinely fail, because old code ignores mood entirely and its max-probability constant coincidentally matches but its baseline-vs-min doesn't:
- `LowLoyalty_LoweringMoodFurther_IncreasesProbabilityFurther` — old code gives `loyalty=10` a flat 0.95 regardless of mood, so `neutralMoodProbability` and `lowMoodProbability` are both `0.95`; `Assert.Greater(0.95, 0.95)` fails.
- `ExtremeBestCase_ClampsToMinProbability` — old code gives `loyalty=100,aligned` a probability of `0.10` (its own baseline), not `0.02`; `Assert.AreEqual(0.02, 0.10, 0.0001)` fails.

Confirm the log shows exactly these two test names failing (not a compile error, and not the other 9 — if more than these two fail, something else is different from what this plan assumes and is worth stopping to double-check before proceeding).

- [ ] **Step 3: Rewrite the implementation**

Replace the entire contents of `Assets/Scripts/NPC/OverrideEvaluator.cs` with:

```csharp
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Npc
{
    public readonly struct OverrideResult
    {
        public readonly bool Overridden;
        public readonly int MoodDelta;
        public readonly int LoyaltyDelta;

        public OverrideResult(bool overridden, int moodDelta, int loyaltyDelta)
        {
            Overridden = overridden;
            MoodDelta = moodDelta;
            LoyaltyDelta = loyaltyDelta;
        }
    }

    /// <summary>
    /// Weighted utility-AI override decision (design approach A in
    /// docs/superpowers/specs/2026-09-02-ruler-ai-depth-design.md,
    /// superseding the milestone #1 rule table). Pure function -- no
    /// UnityEngine dependency, no side effects. The caller supplies `roll`
    /// (a [0,1) random value) so this is deterministic and testable;
    /// DecisionCycleManager passes UnityEngine.Random.value at the real
    /// call site.
    /// </summary>
    public static class OverrideEvaluator
    {
        private const int Neutral = 50;
        private const double Baseline = 0.10;
        private const double LoyaltyWeight = 0.012;
        private const double MoodWeight = 0.005;
        private const double AgendaMisalignedBump = 0.25;
        private const double MinProbability = 0.02;
        private const double MaxProbability = 0.95;

        private const int AcceptedMoodDelta = 5;
        private const int AcceptedLoyaltyDelta = 3;
        private const int OverriddenMoodDelta = -10;
        private const int OverriddenLoyaltyDelta = -5;

        public static bool IsAligned(RulerState.AgendaType agenda, ResourceAllocation allocation)
        {
            switch (agenda)
            {
                case RulerState.AgendaType.Expansionist: return allocation.Army >= 40;
                case RulerState.AgendaType.Isolationist: return allocation.Army <= 20;
                case RulerState.AgendaType.Mercantile: return allocation.Trade >= 40;
                case RulerState.AgendaType.Pious: return allocation.Religion >= 40;
                default: return true;
            }
        }

        /// <summary>
        /// Weighted-sum utility score: Baseline plus a contribution each from
        /// loyalty, mood, and agenda-alignment (each measured relative to its
        /// neutral midpoint), clamped to [MinProbability, MaxProbability].
        /// LoyaltyWeight &gt; MoodWeight so loyalty stays the dominant factor.
        /// </summary>
        public static double OverrideProbability(RulerState state, ResourceAllocation allocation)
        {
            double probability = Baseline
                + (Neutral - state.Loyalty) * LoyaltyWeight
                + (Neutral - state.Mood) * MoodWeight
                + (IsAligned(state.Agenda, allocation) ? 0.0 : AgendaMisalignedBump);

            return Clamp(probability, MinProbability, MaxProbability);
        }

        public static OverrideResult Evaluate(RulerState state, ResourceAllocation allocation, double roll)
        {
            bool overridden = roll < OverrideProbability(state, allocation);

            return overridden
                ? new OverrideResult(true, OverriddenMoodDelta, OverriddenLoyaltyDelta)
                : new OverrideResult(false, AcceptedMoodDelta, AcceptedLoyaltyDelta);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run the same command as Step 2.
Expected: exit code 0. Read `TestResults/milestone2-task1-red.xml` (now green) and confirm `<test-run ... result="Passed" total="11" passed="11" failed="0" .../>`.

- [ ] **Step 5: Run the full EditMode suite to confirm no other regressions**

```bash
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" \
  -runTests -batchmode \
  -projectPath "C:\Users\rajes\understudy-kingdom" \
  -testResults "C:\Users\rajes\understudy-kingdom\TestResults\milestone2-task1-full.xml" \
  -testPlatform EditMode \
  -logFile "C:\Users\rajes\understudy-kingdom\TestResults\milestone2-task1-full.log" \
  -nographics
```

Expected: exit code 0. `DecisionCycleManagerTests` is unaffected functionally by the new formula (see Step 6's analysis: its two probability-dependent tests' existing roll values still produce the same accept/override outcomes under the new formula), so the full suite should already be green here, before Step 6's comment-only edit. Proceed to Step 6 regardless — stale comments describing the old formula would mislead the next reader even though no assertion needs to change.

- [ ] **Step 6: Update stale comments in `DecisionCycleManagerTests.cs`**

This file needs no assertion/value changes — recompute to confirm: its `SubmitRecommendation_Accepted_...` test uses `Loyalty = 80, Mood = 50, Mercantile`, aligned allocation `(20, 60, 20)`, `roll: 0.99`. Under the new formula: `OverrideProbability = 0.10 + (50-80)*0.012 + (50-50)*0.005 + 0 = 0.10 - 0.36 = -0.26`, clamped to `MinProbability` (0.02). Since `0.99 >= 0.02`, `Overridden` is still `false` — same outcome as before, so the existing `Assert.AreEqual(55, ...)`/`Assert.AreEqual(83, ...)` (using the unchanged `AcceptedMoodDelta`/`AcceptedLoyaltyDelta` constants) still hold. Its `SubmitRecommendation_Overridden_...` test sets `Loyalty = 10` (Mood stays 50 from `SetUp`, Mercantile), aligned allocation, `roll: 0.50`. New formula: `0.10 + (50-10)*0.012 + 0 + 0 = 0.58`. Since `0.50 < 0.58`, `Overridden` is still `true` — same outcome, `Assert.AreEqual(40, ruler.State.Mood)` (using unchanged `OverriddenMoodDelta`) still holds.

Only two comments are now inaccurate. In `Assets/Tests/EditMode/DecisionCycleManagerTests.cs`:

Change:
```csharp
            string narration = manager.SubmitRecommendation(allocation, roll: 0.99); // baseline 0.10, no override
```
to:
```csharp
            string narration = manager.SubmitRecommendation(allocation, roll: 0.99); // high loyalty + neutral mood -> low probability (clamped), no override
```

Change:
```csharp
            ruler.State.Loyalty = 10; // forces near-certain override
```
to:
```csharp
            ruler.State.Loyalty = 10; // low loyalty alone -> probability 0.58, comfortably above roll 0.50
```

- [ ] **Step 7: Run the full EditMode suite once more to confirm the comment-only change didn't break anything**

Run the same command as Step 5.
Expected: exit code 0. Read `TestResults/milestone2-task1-full.xml` and confirm the `UnderstudyKingdom.EditModeTests.dll` assembly shows `total="35" passed="35" failed="0"` (29 existing + 6 net-new in `OverrideEvaluatorTests.cs`: 11 tests now vs. 5 before = +6).

- [ ] **Step 8: Commit**

```bash
cd "C:\Users\rajes\understudy-kingdom"
git add Assets/Scripts/NPC/OverrideEvaluator.cs Assets/Tests/EditMode/OverrideEvaluatorTests.cs Assets/Tests/EditMode/DecisionCycleManagerTests.cs
git commit -m "feat: weight ruler override decisions by loyalty, mood, and agenda-alignment

Replaces OverrideEvaluator's 3-rule if/else cascade with a weighted
utility-AI formula, making mood a real factor in the accept/override
decision for the first time (previously tracked and narrated but
ignored by the decision logic). Loyalty stays the dominant factor.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_017JNi1ThZutdGUt6toaGBmq"
```

(Check `git status --short` for any newly-generated `.meta` files and add those too — none are expected since no files were created, only modified.)
