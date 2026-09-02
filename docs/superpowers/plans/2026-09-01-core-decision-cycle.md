# Core Decision Cycle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the resource-allocation-only decision cycle (FR-01–FR-04) end to end: player submits an allocation, the ruler NPC accepts or overrides it per a rule-based probability table, mood/loyalty persist to a local JSON save file, and the outcome is narrated via template.

**Architecture:** Decision logic (`ResourceAllocation`, `RulerState`, `OverrideEvaluator`) is plain C# with zero `UnityEngine` dependency, isolated in its own assembly (`UnderstudyKingdom.Runtime`) so it's unit-testable via Unity's EditMode Test Runner. `SaveService` is the one Unity-dependent piece (uses `Application.persistentDataPath` + `JsonUtility`), still testable in EditMode since EditMode tests run inside the Editor process. `DecisionCycleManager` (MonoBehaviour) stays a thin orchestrator with no decision logic of its own.

**Tech Stack:** Unity 6 LTS (6000.3.23f1), C#, Unity Test Framework (NUnit-based EditMode tests), `com.unity.test-framework` package.

## Global Constraints

- Unity version floor: **6000.3.23f1** (`ProjectSettings/ProjectVersion.txt`) — do not target an older editor.
- Namespace convention: `UnderstudyKingdom.<Area>` (established in PR #1's stubs — `Core`, `Npc`).
- No `UnityEngine` imports in `ResourceAllocation`, `RulerState`, or `OverrideEvaluator` — this is load-bearing for testability, not a style preference.
- **This plan was written in a sandbox with no Unity Editor or dotnet toolchain available.** Every "Run test" step below gives the exact command and expected output for whoever executes this plan with a real Unity install — it has NOT been run or confirmed passing by the plan's author. Do not report a task complete without actually running the command and observing the real output.
- Design reference: `docs/superpowers/specs/2026-09-01-core-decision-cycle-design.md` — every task below traces back to a section of that spec.

---

### Task 1: ResourceAllocation + test project scaffolding

**Files:**
- Create: `Assets/Scripts/UnderstudyKingdom.Runtime.asmdef`
- Create: `Assets/Scripts/Core/ResourceAllocation.cs`
- Create: `Assets/Tests/EditMode/UnderstudyKingdom.EditModeTests.asmdef`
- Create: `Assets/Tests/EditMode/ResourceAllocationTests.cs`
- Modify: `Packages/manifest.json`

**Interfaces:**
- Produces: `UnderstudyKingdom.Core.ResourceAllocation` — `readonly struct` with `int Army, int Trade, int Religion` fields (constructor-set) and `bool IsValid()`.

- [ ] **Step 1: Add the Test Framework package**

Edit `Packages/manifest.json` to add the dependency (alphabetical order among existing entries):

```json
{
  "dependencies": {
    "com.unity.textmeshpro": "3.0.9",
    "com.unity.inputsystem": "1.11.2",
    "com.unity.addressables": "2.2.2",
    "com.unity.test-framework": "1.4.5",
    "com.unity.modules.animation": "1.0.0",
    "com.unity.modules.audio": "1.0.0",
    "com.unity.modules.ui": "1.0.0"
  }
}
```

- [ ] **Step 2: Create the runtime assembly definition**

Create `Assets/Scripts/UnderstudyKingdom.Runtime.asmdef`:

```json
{
    "name": "UnderstudyKingdom.Runtime",
    "rootNamespace": "UnderstudyKingdom",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

This turns everything under `Assets/Scripts/` into one compiled assembly the test assembly (Step 3) can reference by name.

- [ ] **Step 3: Create the EditMode test assembly definition**

Create `Assets/Tests/EditMode/UnderstudyKingdom.EditModeTests.asmdef`:

```json
{
    "name": "UnderstudyKingdom.EditModeTests",
    "rootNamespace": "UnderstudyKingdom.Tests",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "UnderstudyKingdom.Runtime"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": true,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 4: Write the failing test**

Create `Assets/Tests/EditMode/ResourceAllocationTests.cs`:

```csharp
using NUnit.Framework;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Tests
{
    public class ResourceAllocationTests
    {
        [Test]
        public void SummingTo100_IsValid()
        {
            var allocation = new ResourceAllocation(40, 30, 30);

            Assert.IsTrue(allocation.IsValid());
        }

        [Test]
        public void NotSummingTo100_IsNotValid()
        {
            var allocation = new ResourceAllocation(40, 30, 20);

            Assert.IsFalse(allocation.IsValid());
        }

        [Test]
        public void NegativeValue_IsNotValid()
        {
            var allocation = new ResourceAllocation(-10, 60, 50);

            Assert.IsFalse(allocation.IsValid());
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they fail**

In the Unity Editor: **Window → General → Test Runner → EditMode tab → Run All**.

Expected: compile error — `ResourceAllocation` does not exist yet. (If Unity hasn't resolved the new `com.unity.test-framework` package yet, resolve packages first via Package Manager before running.)

- [ ] **Step 6: Write minimal implementation**

Create `Assets/Scripts/Core/ResourceAllocation.cs`:

```csharp
namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// The one recommendation type implemented in this pass (FR-01, scoped to
    /// resource allocation only per docs/superpowers/specs/2026-09-01-core-decision-cycle-design.md).
    /// </summary>
    public readonly struct ResourceAllocation
    {
        public readonly int Army;
        public readonly int Trade;
        public readonly int Religion;

        public ResourceAllocation(int army, int trade, int religion)
        {
            Army = army;
            Trade = trade;
            Religion = religion;
        }

        public bool IsValid()
        {
            return Army >= 0 && Trade >= 0 && Religion >= 0 && (Army + Trade + Religion) == 100;
        }
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

**Window → General → Test Runner → EditMode tab → Run All**.
Expected: 3 tests green (`SummingTo100_IsValid`, `NotSummingTo100_IsNotValid`, `NegativeValue_IsNotValid`).

- [ ] **Step 8: Commit**

```bash
git add Packages/manifest.json Assets/Scripts/UnderstudyKingdom.Runtime.asmdef \
  Assets/Scripts/Core/ResourceAllocation.cs \
  Assets/Tests/EditMode/UnderstudyKingdom.EditModeTests.asmdef \
  Assets/Tests/EditMode/ResourceAllocationTests.cs
git commit -m "feat: add ResourceAllocation and EditMode test scaffolding"
```

---

### Task 2: RulerState

**Files:**
- Create: `Assets/Scripts/NPC/RulerState.cs`
- Modify: `Assets/Scripts/NPC/RulerNpcController.cs` (replace inline `Agenda` enum and `mood`/`loyalty`/`agenda` fields with a `RulerState` reference)
- Create: `Assets/Tests/EditMode/RulerStateTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `UnderstudyKingdom.Npc.RulerState` — class with `int Mood`, `int Loyalty`, `AgendaType Agenda` (nested enum: `Expansionist, Isolationist, Mercantile, Pious`), `void ApplyDelta(int moodDelta, int loyaltyDelta)` (clamps both to [0, 100]). `RulerNpcController.State` (public field of type `RulerState`) is what later tasks read/write.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/RulerStateTests.cs`:

```csharp
using NUnit.Framework;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.Tests
{
    public class RulerStateTests
    {
        [Test]
        public void ApplyDelta_ClampsAtUpperBound()
        {
            var state = new RulerState { Mood = 95, Loyalty = 98 };

            state.ApplyDelta(moodDelta: 10, loyaltyDelta: 10);

            Assert.AreEqual(100, state.Mood);
            Assert.AreEqual(100, state.Loyalty);
        }

        [Test]
        public void ApplyDelta_ClampsAtLowerBound()
        {
            var state = new RulerState { Mood = 3, Loyalty = 2 };

            state.ApplyDelta(moodDelta: -10, loyaltyDelta: -10);

            Assert.AreEqual(0, state.Mood);
            Assert.AreEqual(0, state.Loyalty);
        }

        [Test]
        public void DefaultState_StartsAtFifty()
        {
            var state = new RulerState();

            Assert.AreEqual(50, state.Mood);
            Assert.AreEqual(50, state.Loyalty);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

**Test Runner → EditMode → Run All.** Expected: compile error — `RulerState` does not exist yet.

- [ ] **Step 3: Write minimal implementation**

Create `Assets/Scripts/NPC/RulerState.cs`:

```csharp
using System;

namespace UnderstudyKingdom.Npc
{
    /// <summary>
    /// Plain-C# ruler state (mood/loyalty/agenda) with zero UnityEngine dependency,
    /// so it's directly unit-testable. See
    /// docs/superpowers/specs/2026-09-01-core-decision-cycle-design.md.
    /// </summary>
    [Serializable]
    public class RulerState
    {
        public enum AgendaType
        {
            Expansionist,
            Isolationist,
            Mercantile,
            Pious
        }

        public int Mood = 50;
        public int Loyalty = 50;
        public AgendaType Agenda = AgendaType.Expansionist;

        public void ApplyDelta(int moodDelta, int loyaltyDelta)
        {
            Mood = Clamp(Mood + moodDelta);
            Loyalty = Clamp(Loyalty + loyaltyDelta);
        }

        private static int Clamp(int value)
        {
            if (value < 0) return 0;
            if (value > 100) return 100;
            return value;
        }
    }
}
```

Now replace `Assets/Scripts/NPC/RulerNpcController.cs` in full (removes the old inline `Agenda` enum and bare fields, replaces with a `RulerState` reference):

```csharp
using UnityEngine;

namespace UnderstudyKingdom.Npc
{
    /// <summary>
    /// Drives the ruler NPC's behavior via a lightweight utility-AI / behavior-tree
    /// over mood, loyalty, and agenda state, held in RulerState. Deliberately NOT a
    /// heavy on-device model -- see docs/NPC_PERFORMANCE_NOTES.md.
    /// See docs/PROJECT_PLAN.md FR-04.
    /// </summary>
    public class RulerNpcController : MonoBehaviour
    {
        public RulerState State = new RulerState();
    }
}
```

(The `EvaluateRecommendation`/`ApplyOutcome` methods from the PR #1 stub move into `OverrideEvaluator` as pure functions — Task 3 — and `DecisionCycleManager` — Task 6 — calls them directly against `ruler.State`. `RulerNpcController` becomes a thin state holder rather than owning the logic itself, matching the design spec's component breakdown.)

- [ ] **Step 4: Run test to verify it passes**

**Test Runner → EditMode → Run All.** Expected: 3 new tests green, no regressions in `ResourceAllocationTests`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/NPC/RulerState.cs Assets/Scripts/NPC/RulerNpcController.cs \
  Assets/Tests/EditMode/RulerStateTests.cs
git commit -m "feat: add RulerState, simplify RulerNpcController to hold it"
```

---

### Task 3: OverrideEvaluator

**Files:**
- Create: `Assets/Scripts/NPC/OverrideEvaluator.cs`
- Create: `Assets/Tests/EditMode/OverrideEvaluatorTests.cs`

**Interfaces:**
- Consumes: `RulerState` (Task 2), `ResourceAllocation` (Task 1).
- Produces: `UnderstudyKingdom.Npc.OverrideResult` (`readonly struct`: `bool Overridden, int MoodDelta, int LoyaltyDelta`) and `UnderstudyKingdom.Npc.OverrideEvaluator.Evaluate(RulerState state, ResourceAllocation allocation, double roll)` — pure function, `roll` is caller-supplied `[0,1)` so it's deterministic in tests. Task 6 supplies `UnityEngine.Random.value` as `roll` at the real call site.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/OverrideEvaluatorTests.cs`:

```csharp
using NUnit.Framework;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.Tests
{
    public class OverrideEvaluatorTests
    {
        [Test]
        public void LowLoyalty_AlwaysOverrides()
        {
            var state = new RulerState { Mood = 50, Loyalty = 10, Agenda = RulerState.AgendaType.Expansionist };
            var allocation = new ResourceAllocation(50, 30, 20);

            var result = OverrideEvaluator.Evaluate(state, allocation, roll: 0.90);

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
        public void HighLoyalty_MisalignedAgenda_MidRoll_Overrides()
        {
            var state = new RulerState { Mood = 50, Loyalty = 80, Agenda = RulerState.AgendaType.Pious };
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

            var result = OverrideEvaluator.Evaluate(state, allocation, roll: 0.90);

            Assert.AreEqual(-10, result.MoodDelta);
            Assert.AreEqual(-5, result.LoyaltyDelta);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

**Test Runner → EditMode → Run All.** Expected: compile error — `OverrideEvaluator`/`OverrideResult` do not exist yet.

- [ ] **Step 3: Write minimal implementation**

Create `Assets/Scripts/NPC/OverrideEvaluator.cs`:

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
    /// Rule-based override decision table (design approach B in
    /// docs/superpowers/specs/2026-09-01-core-decision-cycle-design.md).
    /// Pure function -- no UnityEngine dependency, no side effects. The caller
    /// supplies `roll` (a [0,1) random value) so this is deterministic and
    /// testable; DecisionCycleManager passes UnityEngine.Random.value at the
    /// real call site.
    /// </summary>
    public static class OverrideEvaluator
    {
        private const int LoyaltyOverrideThreshold = 20;
        private const double LoyaltyOverrideProbability = 0.95;
        private const double MisalignedOverrideProbability = 0.30;
        private const double BaselineOverrideProbability = 0.10;

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

        public static double OverrideProbability(RulerState state, ResourceAllocation allocation)
        {
            if (state.Loyalty < LoyaltyOverrideThreshold)
            {
                return LoyaltyOverrideProbability;
            }

            return IsAligned(state.Agenda, allocation)
                ? BaselineOverrideProbability
                : MisalignedOverrideProbability;
        }

        public static OverrideResult Evaluate(RulerState state, ResourceAllocation allocation, double roll)
        {
            bool overridden = roll < OverrideProbability(state, allocation);

            return overridden
                ? new OverrideResult(true, OverriddenMoodDelta, OverriddenLoyaltyDelta)
                : new OverrideResult(false, AcceptedMoodDelta, AcceptedLoyaltyDelta);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

**Test Runner → EditMode → Run All.** Expected: all 5 new tests green (`LowLoyalty_AlwaysOverrides`, `HighLoyalty_AlignedAgenda_LowRoll_DoesNotOverride`, `HighLoyalty_MisalignedAgenda_MidRoll_Overrides`, `NotOverridden_AppliesPositiveDeltas`, `Overridden_AppliesNegativeDeltas`), no regressions.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/NPC/OverrideEvaluator.cs Assets/Tests/EditMode/OverrideEvaluatorTests.cs
git commit -m "feat: add OverrideEvaluator rule-based decision table"
```

---

### Task 4: RulerSaveData + SaveService

**Files:**
- Create: `Assets/Scripts/Core/RulerSaveData.cs`
- Create: `Assets/Scripts/Core/SaveService.cs`
- Create: `Assets/Tests/EditMode/SaveServiceTests.cs`

**Interfaces:**
- Consumes: `RulerState` (Task 2).
- Produces: `UnderstudyKingdom.Core.SaveService.SavePath` (string property), `SaveService.Save(RulerState state)`, `SaveService.Load()` returning `RulerState` (fresh default on missing/corrupt file per design spec's Error Handling section).

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/SaveServiceTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.Tests
{
    public class SaveServiceTests
    {
        [TearDown]
        public void Cleanup()
        {
            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }
        }

        [Test]
        public void Load_NoSaveFile_ReturnsDefaultState()
        {
            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }

            var state = SaveService.Load();

            Assert.AreEqual(50, state.Mood);
            Assert.AreEqual(50, state.Loyalty);
        }

        [Test]
        public void SaveThenLoad_RoundTripsState()
        {
            var original = new RulerState { Mood = 70, Loyalty = 30, Agenda = RulerState.AgendaType.Isolationist };

            SaveService.Save(original);
            var loaded = SaveService.Load();

            Assert.AreEqual(70, loaded.Mood);
            Assert.AreEqual(30, loaded.Loyalty);
            Assert.AreEqual(RulerState.AgendaType.Isolationist, loaded.Agenda);
        }

        [Test]
        public void Load_CorruptFile_ReturnsDefaultState()
        {
            File.WriteAllText(SaveService.SavePath, "not valid json {{{");

            var state = SaveService.Load();

            Assert.AreEqual(50, state.Mood);
            Assert.AreEqual(50, state.Loyalty);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

**Test Runner → EditMode → Run All.** Expected: compile error — `SaveService` does not exist yet.

- [ ] **Step 3: Write minimal implementation**

Create `Assets/Scripts/Core/RulerSaveData.cs`:

```csharp
using System;

namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// JSON-serializable DTO mirroring RulerState, for JsonUtility (which cannot
    /// serialize nested enums directly as robustly across Unity versions, so the
    /// agenda is stored as its int ordinal).
    /// </summary>
    [Serializable]
    public class RulerSaveData
    {
        public int Mood;
        public int Loyalty;
        public int Agenda;
    }
}
```

Create `Assets/Scripts/Core/SaveService.cs`:

```csharp
using System;
using System.IO;
using UnityEngine;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// Local JSON persistence for RulerState (FR-03), ahead of the not-yet-designed
    /// backend. The one Unity-dependent piece in this feature (Application.persistentDataPath,
    /// JsonUtility) -- still testable in EditMode since EditMode tests run inside the
    /// Editor process. See docs/superpowers/specs/2026-09-01-core-decision-cycle-design.md
    /// Error Handling section for the missing/corrupt-file behavior below.
    /// </summary>
    public static class SaveService
    {
        private const string FileName = "ruler_save.json";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static void Save(RulerState state)
        {
            var data = new RulerSaveData
            {
                Mood = state.Mood,
                Loyalty = state.Loyalty,
                Agenda = (int)state.Agenda
            };
            File.WriteAllText(SavePath, JsonUtility.ToJson(data));
        }

        public static RulerState Load()
        {
            if (!File.Exists(SavePath))
            {
                return new RulerState();
            }

            try
            {
                var data = JsonUtility.FromJson<RulerSaveData>(File.ReadAllText(SavePath));
                return new RulerState
                {
                    Mood = data.Mood,
                    Loyalty = data.Loyalty,
                    Agenda = (RulerState.AgendaType)data.Agenda
                };
            }
            catch (Exception)
            {
                return new RulerState();
            }
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

**Test Runner → EditMode → Run All.** Expected: all 3 new tests green, no regressions.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/RulerSaveData.cs Assets/Scripts/Core/SaveService.cs \
  Assets/Tests/EditMode/SaveServiceTests.cs
git commit -m "feat: add RulerSaveData and SaveService for local JSON persistence"
```

---

### Task 5: DialogueTemplateEngine (minimal)

**Files:**
- Modify: `Assets/Scripts/NPC/DialogueTemplateEngine.cs` (full replacement — the PR #1 stub throws `NotImplementedException`)
- Create: `Assets/Tests/EditMode/DialogueTemplateEngineTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `UnderstudyKingdom.Npc.DialogueTemplateEngine.Resolve(string templateTag, IReadOnlyDictionary<string, string> variables)` returning `string`. Task 6 calls this with tags `"ruler_accept"` / `"ruler_override"` and variables `{"mood": ..., "loyalty": ...}`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/DialogueTemplateEngineTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.Tests
{
    public class DialogueTemplateEngineTests
    {
        [Test]
        public void Resolve_AcceptTemplate_SubstitutesVariables()
        {
            var variables = new Dictionary<string, string> { { "mood", "60" }, { "loyalty", "55" } };

            string result = DialogueTemplateEngine.Resolve("ruler_accept", variables);

            Assert.IsTrue(result.Contains("60"));
            Assert.IsTrue(result.Contains("55"));
        }

        [Test]
        public void Resolve_UnknownTag_ReturnsPlaceholderMarker()
        {
            string result = DialogueTemplateEngine.Resolve("not_a_real_tag", new Dictionary<string, string>());

            Assert.IsTrue(result.Contains("not_a_real_tag"));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

**Test Runner → EditMode → Run All.** Expected: `Resolve_AcceptTemplate_SubstitutesVariables` and `Resolve_UnknownTag_ReturnsPlaceholderMarker` fail — current stub throws `NotImplementedException`.

- [ ] **Step 3: Write minimal implementation**

Replace `Assets/Scripts/NPC/DialogueTemplateEngine.cs` in full:

```csharp
using System.Collections.Generic;

namespace UnderstudyKingdom.Npc
{
    /// <summary>
    /// Generates ruler dialogue from templated strings with variable slots keyed to
    /// current mood/history. Explicitly NOT backed by an on-device LLM -- see
    /// docs/NPC_PERFORMANCE_NOTES.md. See docs/PROJECT_PLAN.md FR-05.
    /// </summary>
    public static class DialogueTemplateEngine
    {
        private static readonly Dictionary<string, string> Templates = new Dictionary<string, string>
        {
            { "ruler_accept", "The ruler nods. \"A wise allocation.\" (mood {mood}, loyalty {loyalty})" },
            { "ruler_override", "The ruler waves a hand. \"I have other plans.\" (mood {mood}, loyalty {loyalty})" }
        };

        public static string Resolve(string templateTag, IReadOnlyDictionary<string, string> variables)
        {
            if (!Templates.TryGetValue(templateTag, out string template))
            {
                return $"[missing template: {templateTag}]";
            }

            string result = template;
            foreach (var pair in variables)
            {
                result = result.Replace("{" + pair.Key + "}", pair.Value);
            }
            return result;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

**Test Runner → EditMode → Run All.** Expected: both new tests green, no regressions.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/NPC/DialogueTemplateEngine.cs Assets/Tests/EditMode/DialogueTemplateEngineTests.cs
git commit -m "feat: implement minimal DialogueTemplateEngine with 2 templates"
```

---

### Task 6: Wire DecisionCycleManager end-to-end

**Files:**
- Modify: `Assets/Scripts/Core/DecisionCycleManager.cs` (full replacement — the PR #1 stub throws `NotImplementedException`)
- Create: `Assets/Tests/EditMode/DecisionCycleManagerTests.cs`

**Interfaces:**
- Consumes: `ResourceAllocation` (Task 1), `RulerState`/`RulerNpcController` (Task 2), `OverrideEvaluator` (Task 3), `SaveService` (Task 4), `DialogueTemplateEngine` (Task 5).
- Produces: `DecisionCycleManager.SubmitRecommendation(ResourceAllocation recommendation, double roll)` returning `string` (the narration). `roll` is exposed as a parameter (not hardcoded to `UnityEngine.Random.value` inside) specifically so this integration is still testable without Play Mode — a thin `MonoBehaviour`-level caller (future UI task, out of scope here) supplies `UnityEngine.Random.value` at the real call site.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/DecisionCycleManagerTests.cs`:

```csharp
using NUnit.Framework;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;
using UnityEngine;

namespace UnderstudyKingdom.Tests
{
    public class DecisionCycleManagerTests
    {
        private GameObject rulerObject;
        private GameObject managerObject;

        [SetUp]
        public void SetUp()
        {
            rulerObject = new GameObject("Ruler");
            var ruler = rulerObject.AddComponent<RulerNpcController>();
            ruler.State = new RulerState { Mood = 50, Loyalty = 80, Agenda = RulerState.AgendaType.Mercantile };

            managerObject = new GameObject("Manager");
            var manager = managerObject.AddComponent<DecisionCycleManager>();
            manager.Ruler = ruler;

            this.manager = manager;
            this.ruler = ruler;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(rulerObject);

            if (System.IO.File.Exists(SaveService.SavePath))
            {
                System.IO.File.Delete(SaveService.SavePath);
            }
        }

        private DecisionCycleManager manager;
        private RulerNpcController ruler;

        [Test]
        public void SubmitRecommendation_Accepted_ReturnsAcceptNarrationAndSaves()
        {
            var allocation = new ResourceAllocation(20, 60, 20); // aligned with Mercantile

            string narration = manager.SubmitRecommendation(allocation, roll: 0.99); // baseline 0.10, no override

            Assert.IsTrue(narration.Contains("wise allocation"));
            Assert.AreEqual(55, ruler.State.Mood);
            Assert.AreEqual(83, ruler.State.Loyalty);

            var reloaded = SaveService.Load();
            Assert.AreEqual(55, reloaded.Mood);
        }

        [Test]
        public void SubmitRecommendation_Overridden_ReturnsOverrideNarration()
        {
            ruler.State.Loyalty = 10; // forces near-certain override

            var allocation = new ResourceAllocation(20, 60, 20);
            string narration = manager.SubmitRecommendation(allocation, roll: 0.50);

            Assert.IsTrue(narration.Contains("other plans"));
            Assert.AreEqual(40, ruler.State.Mood);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

**Test Runner → EditMode → Run All.** Expected: compile error — `DecisionCycleManager` has no public `Ruler` field or `SubmitRecommendation(ResourceAllocation, double)` overload yet (current PR #1 stub only has `SubmitRecommendation(object)` and `ResolveCycle()`, both throwing).

- [ ] **Step 3: Write minimal implementation**

Replace `Assets/Scripts/Core/DecisionCycleManager.cs` in full:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// Thin orchestrator for the prep -> ruler-decision loop (FR-01, FR-02, FR-03).
    /// Holds no decision logic itself -- that lives in OverrideEvaluator (pure,
    /// testable) so this class stays a coordinator. See
    /// docs/superpowers/specs/2026-09-01-core-decision-cycle-design.md.
    /// </summary>
    public class DecisionCycleManager : MonoBehaviour
    {
        public RulerNpcController Ruler;

        private int currentCycleNumber;

        private void Awake()
        {
            if (Ruler != null)
            {
                Ruler.State = SaveService.Load();
            }
        }

        /// <summary>
        /// Submits a resource-allocation recommendation and resolves the cycle
        /// immediately. `roll` is caller-supplied (not read from UnityEngine.Random
        /// internally) so this method is testable without Play Mode; the real UI
        /// call site passes UnityEngine.Random.value.
        /// </summary>
        public string SubmitRecommendation(ResourceAllocation recommendation, double roll)
        {
            currentCycleNumber++;

            OverrideResult result = OverrideEvaluator.Evaluate(Ruler.State, recommendation, roll);
            Ruler.State.ApplyDelta(result.MoodDelta, result.LoyaltyDelta);
            SaveService.Save(Ruler.State);

            string templateTag = result.Overridden ? "ruler_override" : "ruler_accept";
            var variables = new Dictionary<string, string>
            {
                { "mood", Ruler.State.Mood.ToString() },
                { "loyalty", Ruler.State.Loyalty.ToString() }
            };

            return DialogueTemplateEngine.Resolve(templateTag, variables);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

**Test Runner → EditMode → Run All.** Expected: both new tests green (verify the exact delta math: Task 3's `AcceptedMoodDelta = 5`/`AcceptedLoyaltyDelta = 3` from a 50/80 start gives 55/83; `OverriddenMoodDelta = -10` from 50 gives 40), and the full suite (all tasks) green with no regressions.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/DecisionCycleManager.cs Assets/Tests/EditMode/DecisionCycleManagerTests.cs
git commit -m "feat: wire DecisionCycleManager end-to-end for resource-allocation cycle"
```

---

## Definition of Done

- [ ] All 6 tasks committed with their tests passing (verified in a real Unity Editor — see Global Constraints)
- [ ] Full EditMode suite green: `ResourceAllocationTests`, `RulerStateTests`, `OverrideEvaluatorTests`, `SaveServiceTests`, `DialogueTemplateEngineTests`, `DecisionCycleManagerTests`
- [ ] No `UnityEngine` import in `ResourceAllocation.cs`, `RulerState.cs`, or `OverrideEvaluator.cs` (grep to confirm)
- [ ] `docs/PROJECT_PLAN.md` FR-01, FR-02, FR-03, FR-04 TODO comments in the touched files are resolved (either removed or narrowed to the explicitly out-of-scope items: army/diplomatic recommendation types, backend integration)
