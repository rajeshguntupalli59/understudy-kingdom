# Estate Phase 1 (Land, Coins & Crops) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `Coins` currency, an 8-plot land grid, and a
plant->water->grow->harvest crop loop as a new "Estate" panel in the
existing `CoreLoop` scene, fully client-authoritative and locally
persisted, with staged sprite-swap growth and short procedural UI-tween
animations for plant/water/harvest.

**Architecture:** New plain-C# state classes (`EstateState`, `LandPlot`,
`CropDefinition`/`CropCatalog`) with zero `UnityEngine` dependency beyond
`Mathf`/`Math`, fully EditMode-testable. A new `SaveService.SaveEstate`/
`LoadEstate` pair writes a second local JSON file, independent of the
existing `RulerState` save. A new `EstatePanelController` (mirrors
`CouncilPanelController`/`CosmeticsPanelController`'s
Initialize-for-scene-and-tests pattern) owns the panel UI and all
plant/water/harvest/unlock interactions. `CoreLoopSceneBuilder.cs` wires
everything into the single existing scene, matching every prior panel.

**Tech Stack:** Unity 6000.3.23f1, C#, NUnit EditMode/PlayMode tests. No
new packages, no third-party animation library (plain `IEnumerator`
coroutines), no server/network code this phase.

## Global Constraints

- Client-authoritative, locally persisted only -- no server/database work
  this phase (see spec's Scope Decisions).
- New, independent save file (`estate_save.json`) -- the existing
  `SaveService.Save(RulerState)` / `Load()` pair and `ruler_save.json` are
  never modified.
- 8 total land plots, indices 0-3 unlocked at start, 4-7 locked.
- Land-plot unlock cost: `round(100 * 1.6^(plotIndex - 4) / 10) * 10` --
  plot 4 = 100, plot 5 = 160, plot 6 = 260, plot 7 = 410.
- Starting balance: 200 coins.
- 3 starting crops (id, seed cost, grow duration seconds, sell value):
  `wheat` (5, 30, 12), `carrot` (15, 120, 40), `pumpkin` (40, 300, 110).
- Growth stages: 0 (sprout, unwatered or just watered), 1 (growing, at
  half `GrowDurationSeconds` elapsed since watering), 2 (mature, at full
  `GrowDurationSeconds` elapsed). An unwatered crop (`WateredAtUnixSeconds
  == 0`) stays at stage 0 forever -- no wilt, no death, no timer runs.
- "Animation" = staged sprite-swap (3 sprites per crop, matching the
  existing ruler-portrait tier-grid pattern) + short procedural
  `IEnumerator` UI tweens for plant/water/harvest -- no new animation
  tooling/package.
- Full spec: `docs/superpowers/specs/2026-09-07-estate-phase1-land-crops-design.md`.
  Roadmap context: `docs/superpowers/specs/2026-09-07-estate-economy-roadmap-design.md`.

---

### Task 1: EstateState, LandPlot, growth stage & unlock cost

**Files:**
- Create: `Assets/Scripts/Core/EstateState.cs`
- Test: `Assets/Tests/EditMode/EstateStateTests.cs`

**Interfaces:**
- Produces: `LandPlot` (`Unlocked bool`, `CropId string`,
  `PlantedAtUnixSeconds long`, `WateredAtUnixSeconds long`), `EstateState`
  (`Coins int = 200`, `Plots LandPlot[8]`, constructor initializes 8 plots
  with indices 0-3 `Unlocked = true`), `EstateState.GrowthStage(LandPlot
  plot, CropDefinition crop, long nowUnixSeconds) -> int`,
  `EstateState.UnlockCost(int plotIndex) -> int`. `CropDefinition` is
  produced by Task 2 but referenced here only by parameter type --
  `GrowthStage` reads `crop.GrowDurationSeconds`, so this task's tests use
  a locally-constructed `CropDefinition` (Task 2 not required first,
  `CropDefinition` is a small standalone struct declared in Task 2's file;
  if executed out of order, stub `CropDefinition` is NOT needed since this
  task's own test file can construct one once Task 2 exists -- execute
  Task 2 before Task 1's tests are run, or write both in the same session
  before testing either. **Recommended order: do Task 2 first, then Task
  1** -- renumber mentally if executing strictly in order; the two are
  small enough this rarely matters in practice with subagent-driven
  development since both land in the same review pass region.)

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/EstateStateTests.cs
using NUnit.Framework;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Tests
{
    public class EstateStateTests
    {
        [Test]
        public void NewEstateState_HasStartingCoinsAndFourUnlockedPlots()
        {
            var state = new EstateState();

            Assert.AreEqual(200, state.Coins);
            Assert.AreEqual(8, state.Plots.Length);
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(state.Plots[i].Unlocked, $"plot {i} should start unlocked");
            }
            for (int i = 4; i < 8; i++)
            {
                Assert.IsFalse(state.Plots[i].Unlocked, $"plot {i} should start locked");
            }
        }

        [Test]
        public void GrowthStage_NeverWatered_StaysStageZeroRegardlessOfElapsedTime()
        {
            var plot = new LandPlot { CropId = "wheat", PlantedAtUnixSeconds = 1000, WateredAtUnixSeconds = 0 };
            var wheat = new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12);

            int stage = EstateState.GrowthStage(plot, wheat, nowUnixSeconds: 1000 + 999999);

            Assert.AreEqual(0, stage);
        }

        [Test]
        public void GrowthStage_JustWatered_IsStageZero()
        {
            var plot = new LandPlot { CropId = "wheat", WateredAtUnixSeconds = 1000 };
            var wheat = new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12);

            int stage = EstateState.GrowthStage(plot, wheat, nowUnixSeconds: 1000);

            Assert.AreEqual(0, stage);
        }

        [Test]
        public void GrowthStage_AtHalfDuration_IsStageOne()
        {
            var plot = new LandPlot { CropId = "wheat", WateredAtUnixSeconds = 1000 };
            var wheat = new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12);

            int stage = EstateState.GrowthStage(plot, wheat, nowUnixSeconds: 1015); // 15s = half of 30s

            Assert.AreEqual(1, stage);
        }

        [Test]
        public void GrowthStage_AtFullDuration_IsStageTwo()
        {
            var plot = new LandPlot { CropId = "wheat", WateredAtUnixSeconds = 1000 };
            var wheat = new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12);

            int stage = EstateState.GrowthStage(plot, wheat, nowUnixSeconds: 1030);

            Assert.AreEqual(2, stage);
        }

        [Test]
        public void GrowthStage_PastFullDuration_ClampsAtStageTwo()
        {
            var plot = new LandPlot { CropId = "wheat", WateredAtUnixSeconds = 1000 };
            var wheat = new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12);

            int stage = EstateState.GrowthStage(plot, wheat, nowUnixSeconds: 1000 + 999999);

            Assert.AreEqual(2, stage);
        }

        [Test]
        public void UnlockCost_FirstLockedPlot_Is100()
        {
            Assert.AreEqual(100, EstateState.UnlockCost(4));
        }

        [Test]
        public void UnlockCost_LastLockedPlot_Is410()
        {
            Assert.AreEqual(410, EstateState.UnlockCost(7));
        }

        [Test]
        public void UnlockCost_MiddleLockedPlots_Match160And260()
        {
            Assert.AreEqual(160, EstateState.UnlockCost(5));
            Assert.AreEqual(260, EstateState.UnlockCost(6));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testResults <path>.xml -logFile <path>.log`
Expected: compile error (`EstateState`/`LandPlot`/`CropDefinition` do not exist) -- this is the expected "fails because it doesn't exist yet" state for a brand-new class. `CropDefinition` must exist too (Task 2) for this to compile; if running Task 1 in isolation first, add a minimal `CropDefinition` stub in this task's own scope is NOT correct -- instead do Task 2 before running Task 1's tests (see Interfaces note above).

- [ ] **Step 3: Write the implementation**

```csharp
// Assets/Scripts/Core/EstateState.cs
using System;

namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// Plain-C# land-plot state, zero UnityEngine dependency beyond Math,
    /// directly unit-testable -- same shape/role as RulerState. See
    /// docs/superpowers/specs/2026-09-07-estate-phase1-land-crops-design.md.
    /// </summary>
    [Serializable]
    public class LandPlot
    {
        public bool Unlocked;
        public string CropId;
        public long PlantedAtUnixSeconds;
        public long WateredAtUnixSeconds;
    }

    [Serializable]
    public class EstateState
    {
        public const int PlotCount = 8;
        public const int StartingUnlockedPlots = 4;

        public int Coins = 200;
        public LandPlot[] Plots;

        public EstateState()
        {
            Plots = new LandPlot[PlotCount];
            for (int i = 0; i < Plots.Length; i++)
            {
                Plots[i] = new LandPlot { Unlocked = i < StartingUnlockedPlots };
            }
        }

        /// <summary>
        /// 0 (sprout) while never watered -- growth never starts until the
        /// player waters the plot, no timer runs and nothing decays while
        /// waiting. Once watered, 1 (growing) at half GrowDurationSeconds
        /// elapsed, 2 (mature) at full GrowDurationSeconds elapsed, clamped
        /// at 2 past that (no further stages).
        /// </summary>
        public static int GrowthStage(LandPlot plot, CropDefinition crop, long nowUnixSeconds)
        {
            if (plot.WateredAtUnixSeconds == 0)
            {
                return 0;
            }

            long elapsed = nowUnixSeconds - plot.WateredAtUnixSeconds;
            long halfDuration = crop.GrowDurationSeconds / 2;
            if (halfDuration <= 0)
            {
                halfDuration = 1;
            }

            long stage = elapsed / halfDuration;
            if (stage > 2) return 2;
            if (stage < 0) return 0;
            return (int)stage;
        }

        /// <summary>
        /// Geometric cost curve for locked plots (index 4-7), rounded to
        /// the nearest 10 and always shown before purchase -- see the
        /// design doc's "opaque expansion cost" differentiation note.
        /// </summary>
        public static int UnlockCost(int plotIndex)
        {
            int lockedIndex = plotIndex - StartingUnlockedPlots;
            double raw = 100.0 * Math.Pow(1.6, lockedIndex);
            return (int)(Math.Round(raw / 10.0) * 10.0);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the same EditMode command as Step 2.
Expected: all `EstateStateTests` PASS (this task's tests only compile once
Task 2's `CropDefinition` also exists -- do Task 2 first, or in the same
batch, before this step).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/EstateState.cs Assets/Scripts/Core/EstateState.cs.meta Assets/Tests/EditMode/EstateStateTests.cs Assets/Tests/EditMode/EstateStateTests.cs.meta
git commit -m "feat: add EstateState, LandPlot, growth stage and unlock cost"
```

---

### Task 2: CropCatalog

**Files:**
- Create: `Assets/Scripts/Core/CropCatalog.cs`
- Test: `Assets/Tests/EditMode/CropCatalogTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `CropDefinition` (readonly struct: `Id`, `DisplayName`,
  `SeedCost`, `GrowDurationSeconds`, `SellValue`, all `readonly`, set via
  constructor `CropDefinition(string id, string displayName, int
  seedCost, int growDurationSeconds, int sellValue)`), `CropCatalog.All`
  (static `CropDefinition[]`, 3 entries: wheat/carrot/pumpkin, exact
  values in Global Constraints), `CropCatalog.Find(string id) ->
  CropDefinition?` (null if not found), `CropCatalog.IndexOf(string id)
  -> int` (-1 if not found, used by Task 4 to resolve which of the 9
  `cropStageSprites` slots a crop's stage sprite lives at).

**Note:** do this task before Task 1's tests are run (Task 1 references
`CropDefinition` in its own test file) -- see Task 1's Interfaces note.

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/CropCatalogTests.cs
using NUnit.Framework;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Tests
{
    public class CropCatalogTests
    {
        [Test]
        public void All_HasExactlyThreeCrops()
        {
            Assert.AreEqual(3, CropCatalog.All.Length);
        }

        [Test]
        public void All_EveryCropHasPositiveCostDurationAndValue()
        {
            foreach (CropDefinition crop in CropCatalog.All)
            {
                Assert.Greater(crop.SeedCost, 0, $"{crop.Id} SeedCost");
                Assert.Greater(crop.GrowDurationSeconds, 0, $"{crop.Id} GrowDurationSeconds");
                Assert.Greater(crop.SellValue, 0, $"{crop.Id} SellValue");
                Assert.IsFalse(string.IsNullOrEmpty(crop.DisplayName), $"{crop.Id} DisplayName");
            }
        }

        [Test]
        public void All_ExactValues_MatchDesignSpec()
        {
            CropDefinition wheat = CropCatalog.Find("wheat").Value;
            Assert.AreEqual(5, wheat.SeedCost);
            Assert.AreEqual(30, wheat.GrowDurationSeconds);
            Assert.AreEqual(12, wheat.SellValue);

            CropDefinition carrot = CropCatalog.Find("carrot").Value;
            Assert.AreEqual(15, carrot.SeedCost);
            Assert.AreEqual(120, carrot.GrowDurationSeconds);
            Assert.AreEqual(40, carrot.SellValue);

            CropDefinition pumpkin = CropCatalog.Find("pumpkin").Value;
            Assert.AreEqual(40, pumpkin.SeedCost);
            Assert.AreEqual(300, pumpkin.GrowDurationSeconds);
            Assert.AreEqual(110, pumpkin.SellValue);
        }

        [Test]
        public void Find_UnknownId_ReturnsNull()
        {
            Assert.IsNull(CropCatalog.Find("does-not-exist"));
        }

        [Test]
        public void IndexOf_KnownIds_ReturnsCatalogOrder()
        {
            Assert.AreEqual(0, CropCatalog.IndexOf("wheat"));
            Assert.AreEqual(1, CropCatalog.IndexOf("carrot"));
            Assert.AreEqual(2, CropCatalog.IndexOf("pumpkin"));
        }

        [Test]
        public void IndexOf_UnknownId_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, CropCatalog.IndexOf("does-not-exist"));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Same EditMode command as Task 1 Step 2.
Expected: FAIL with "CropCatalog does not exist" (compile error).

- [ ] **Step 3: Write the implementation**

```csharp
// Assets/Scripts/Core/CropCatalog.cs
namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// Static crop content, not save data -- same "static catalog vs. save
    /// data" split the ruler dialogue templates already use. See
    /// docs/superpowers/specs/2026-09-07-estate-phase1-land-crops-design.md.
    /// </summary>
    public readonly struct CropDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly int SeedCost;
        public readonly int GrowDurationSeconds;
        public readonly int SellValue;

        public CropDefinition(string id, string displayName, int seedCost, int growDurationSeconds, int sellValue)
        {
            Id = id;
            DisplayName = displayName;
            SeedCost = seedCost;
            GrowDurationSeconds = growDurationSeconds;
            SellValue = sellValue;
        }
    }

    public static class CropCatalog
    {
        public static readonly CropDefinition[] All =
        {
            new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12),
            new CropDefinition("carrot", "Carrot", seedCost: 15, growDurationSeconds: 120, sellValue: 40),
            new CropDefinition("pumpkin", "Pumpkin", seedCost: 40, growDurationSeconds: 300, sellValue: 110),
        };

        public static CropDefinition? Find(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return All[i];
                }
            }
            return null;
        }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Same command. Expected: all `CropCatalogTests` PASS, and (now that
`CropDefinition` exists) `EstateStateTests` from Task 1 PASS too.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/CropCatalog.cs Assets/Scripts/Core/CropCatalog.cs.meta Assets/Tests/EditMode/CropCatalogTests.cs Assets/Tests/EditMode/CropCatalogTests.cs.meta
git commit -m "feat: add CropCatalog static crop content"
```

---

### Task 3: SaveService.SaveEstate / LoadEstate

**Files:**
- Create: `Assets/Scripts/Core/EstateSaveData.cs`
- Modify: `Assets/Scripts/Core/SaveService.cs`
- Test: `Assets/Tests/EditMode/SaveServiceEstateTests.cs`

**Interfaces:**
- Consumes: `EstateState`/`LandPlot` (Task 1).
- Produces: `SaveService.EstateSavePath` (static string property),
  `SaveService.SaveEstate(EstateState state)`,
  `SaveService.LoadEstate() -> EstateState`. Existing `SaveService.Save`/
  `Load`/`SavePath` (for `RulerState`) are untouched -- this is strictly
  additive.

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/SaveServiceEstateTests.cs
using System.IO;
using NUnit.Framework;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Tests
{
    public class SaveServiceEstateTests
    {
        [TearDown]
        public void Cleanup()
        {
            if (File.Exists(SaveService.EstateSavePath))
            {
                File.Delete(SaveService.EstateSavePath);
            }
        }

        [Test]
        public void LoadEstate_NoSaveFile_ReturnsDefaultState()
        {
            if (File.Exists(SaveService.EstateSavePath))
            {
                File.Delete(SaveService.EstateSavePath);
            }

            var state = SaveService.LoadEstate();

            Assert.AreEqual(200, state.Coins);
            Assert.AreEqual(8, state.Plots.Length);
            Assert.IsTrue(state.Plots[0].Unlocked);
            Assert.IsFalse(state.Plots[4].Unlocked);
        }

        [Test]
        public void SaveEstateThenLoadEstate_RoundTripsCoinsAndPlots()
        {
            var original = new EstateState { Coins = 355 };
            original.Plots[0].CropId = "wheat";
            original.Plots[0].PlantedAtUnixSeconds = 1000;
            original.Plots[0].WateredAtUnixSeconds = 1005;
            original.Plots[4].Unlocked = true;

            SaveService.SaveEstate(original);
            var loaded = SaveService.LoadEstate();

            Assert.AreEqual(355, loaded.Coins);
            Assert.AreEqual("wheat", loaded.Plots[0].CropId);
            Assert.AreEqual(1000, loaded.Plots[0].PlantedAtUnixSeconds);
            Assert.AreEqual(1005, loaded.Plots[0].WateredAtUnixSeconds);
            Assert.IsTrue(loaded.Plots[4].Unlocked);
        }

        [Test]
        public void LoadEstate_CorruptFile_ReturnsDefaultState()
        {
            File.WriteAllText(SaveService.EstateSavePath, "not valid json {{{");

            var state = SaveService.LoadEstate();

            Assert.AreEqual(200, state.Coins);
        }

        [Test]
        public void LoadEstate_WrongPlotCount_ReturnsDefaultState()
        {
            // Simulates a hand-corrupted or foreign-format file -- a Plots
            // array of the wrong length must not be trusted as-is (every
            // caller indexes Plots[0..7] directly).
            var corrupted = new EstateSaveData { Coins = 50, Plots = new LandPlot[3] };
            System.IO.File.WriteAllText(SaveService.EstateSavePath, UnityEngine.JsonUtility.ToJson(corrupted));

            var state = SaveService.LoadEstate();

            Assert.AreEqual(200, state.Coins);
            Assert.AreEqual(8, state.Plots.Length);
        }

        [Test]
        public void SaveService_RulerSaveFile_UnaffectedByEstateSave()
        {
            // The two save files are fully independent -- saving Estate
            // state must never touch ruler_save.json.
            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }

            SaveService.SaveEstate(new EstateState { Coins = 999 });

            Assert.IsFalse(File.Exists(SaveService.SavePath));

            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Same EditMode command. Expected: FAIL with "EstateSavePath/SaveEstate/LoadEstate does not exist".

- [ ] **Step 3: Write the implementation**

```csharp
// Assets/Scripts/Core/EstateSaveData.cs
using System;

namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// JSON-serializable DTO for EstateState, mirroring RulerSaveData's
    /// role (decouples save format from runtime shape, carries a Version
    /// field for future migrations). LandPlot itself has no enum-encoding
    /// issue RulerState's Agenda has, so it's reused directly here rather
    /// than duplicated into a parallel DTO type.
    /// </summary>
    [Serializable]
    public class EstateSaveData
    {
        public int Version = 1;
        public int Coins;
        public LandPlot[] Plots;
    }
}
```

Edit `Assets/Scripts/Core/SaveService.cs` -- add alongside the existing
`FileName`/`SavePath`/`Save`/`Load` (do not modify any existing method):

```csharp
        private const string EstateFileName = "estate_save.json";

        public static string EstateSavePath => Path.Combine(Application.persistentDataPath, EstateFileName);

        public static void SaveEstate(EstateState state)
        {
            var data = new EstateSaveData
            {
                Coins = state.Coins,
                Plots = state.Plots
            };
            File.WriteAllText(EstateSavePath, JsonUtility.ToJson(data));
        }

        public static EstateState LoadEstate()
        {
            if (!File.Exists(EstateSavePath))
            {
                return new EstateState();
            }

            try
            {
                string raw = File.ReadAllText(EstateSavePath);
                string trimmed = raw.TrimStart();

                if (trimmed.Length == 0 || trimmed[0] != '{')
                {
                    return new EstateState();
                }

                var data = JsonUtility.FromJson<EstateSaveData>(raw);

                if (data.Plots == null || data.Plots.Length != EstateState.PlotCount)
                {
                    return new EstateState();
                }

                return new EstateState
                {
                    Coins = data.Coins,
                    Plots = data.Plots
                };
            }
            catch (Exception)
            {
                return new EstateState();
            }
        }
```

Insert this block after the existing `Load()` method's closing brace, still
inside the `SaveService` class. No existing line is modified.

- [ ] **Step 4: Run tests to verify they pass**

Same command. Expected: all `SaveServiceEstateTests` PASS, and every
pre-existing `SaveServiceTests` test still PASSES unmodified (confirms
zero impact on `RulerState` persistence).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/EstateSaveData.cs Assets/Scripts/Core/EstateSaveData.cs.meta Assets/Scripts/Core/SaveService.cs Assets/Tests/EditMode/SaveServiceEstateTests.cs Assets/Tests/EditMode/SaveServiceEstateTests.cs.meta
git commit -m "feat: add SaveService.SaveEstate/LoadEstate"
```

---

### Task 4: EstatePanelController (plant, water, harvest, unlock, animations)

**Files:**
- Create: `Assets/Scripts/UI/EstatePanelController.cs`
- Test: `Assets/Tests/PlayMode/EstatePanelControllerTests.cs`

**Interfaces:**
- Consumes: `EstateState`/`LandPlot` (Task 1), `CropCatalog`/
  `CropDefinition` (Task 2), `SaveService.SaveEstate`/`LoadEstate` (Task
  3), `DuelModalGate` (existing, `Assets/Scripts/UI/DuelModalGate.cs`).
- Produces: `EstatePanelController` (MonoBehaviour), nested public class
  `EstatePanelController.PlotView` (`Image stageImage`, `Button
  tapButton`, `GameObject lockedOverlay`, `TextMeshProUGUI
  lockCostLabel`), `Initialize(...)` (full parameter list in Step 3,
  consumed by Task 6's scene wiring and this task's own tests).

This task has the most steps -- each is its own small TDD cycle within the
same file/class, matching the existing multi-behavior controllers
(`CouncilPanelController` etc.) that were also built incrementally.

- [ ] **Step 1: Write the failing test file (all behaviors)**

```csharp
// Assets/Tests/PlayMode/EstatePanelControllerTests.cs
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    public class EstatePanelControllerTests
    {
        private GameObject canvasObject;
        private GameObject panelRootObject;
        private GameObject controllerObject;
        private EstatePanelController controller;
        private Button estateButton;
        private Button closeButton;
        private TextMeshProUGUI coinsLabel;
        private EstatePanelController.PlotView[] plotViews;
        private GameObject seedPickerRoot;
        private Button[] seedButtons;
        private TextMeshProUGUI[] seedCostLabels;
        private Slider armySlider;
        private Slider tradeSlider;
        private Slider religionSlider;
        private Button submitButton;
        private Button challengeButton;
        private Button viewHistoryButton;
        private Button councilButton;
        private Button eventsButton;
        private Button customizeButton;
        private GameObject gateObject;
        private DuelModalGate gate;

        [SetUp]
        public void SetUp()
        {
            if (File.Exists(SaveService.EstateSavePath))
            {
                File.Delete(SaveService.EstateSavePath);
            }

            canvasObject = new GameObject("Canvas", typeof(Canvas));

            estateButton = CreateButton("EstateButton");
            panelRootObject = new GameObject("EstatePanel");
            panelRootObject.transform.SetParent(canvasObject.transform, false);
            closeButton = CreateButton("CloseButton");
            coinsLabel = CreateLabel("CoinsLabel");

            plotViews = new EstatePanelController.PlotView[EstateState.PlotCount];
            for (int i = 0; i < plotViews.Length; i++)
            {
                var stageObject = new GameObject($"Plot{i}Stage", typeof(Image));
                stageObject.transform.SetParent(canvasObject.transform, false);
                var lockedOverlay = new GameObject($"Plot{i}Locked");
                lockedOverlay.transform.SetParent(canvasObject.transform, false);

                plotViews[i] = new EstatePanelController.PlotView
                {
                    stageImage = stageObject.GetComponent<Image>(),
                    tapButton = CreateButton($"Plot{i}Button"),
                    lockedOverlay = lockedOverlay,
                    lockCostLabel = CreateLabel($"Plot{i}LockCost")
                };
            }

            seedPickerRoot = new GameObject("SeedPicker");
            seedPickerRoot.transform.SetParent(canvasObject.transform, false);
            seedButtons = new Button[CropCatalog.All.Length];
            seedCostLabels = new TextMeshProUGUI[CropCatalog.All.Length];
            for (int i = 0; i < seedButtons.Length; i++)
            {
                seedButtons[i] = CreateButton($"SeedButton{i}");
                seedCostLabels[i] = CreateLabel($"SeedCostLabel{i}");
            }

            armySlider = CreateSlider("ArmySlider");
            tradeSlider = CreateSlider("TradeSlider");
            religionSlider = CreateSlider("ReligionSlider");
            submitButton = CreateButton("SubmitButton");
            challengeButton = CreateButton("ChallengeButton");
            viewHistoryButton = CreateButton("ViewHistoryButton");
            councilButton = CreateButton("CouncilButton");
            eventsButton = CreateButton("EventsButton");
            customizeButton = CreateButton("CustomizeButton");

            gateObject = new GameObject("DuelModalGate");
            gate = gateObject.AddComponent<DuelModalGate>();

            controllerObject = new GameObject("Controller");
            controller = controllerObject.AddComponent<EstatePanelController>();
            controller.Initialize(estateButton, panelRootObject, closeButton, coinsLabel,
                plotViews, seedPickerRoot, seedButtons, seedCostLabels, new Sprite[9],
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton,
                viewHistoryButton, councilButton, eventsButton, customizeButton, gate);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(gateObject);

            if (File.Exists(SaveService.EstateSavePath))
            {
                File.Delete(SaveService.EstateSavePath);
            }
        }

        private Slider CreateSlider(string name)
        {
            var sliderObject = new GameObject(name, typeof(Slider));
            sliderObject.transform.SetParent(canvasObject.transform, false);
            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 100f;
            return slider;
        }

        private Button CreateButton(string name)
        {
            var buttonObject = new GameObject(name, typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvasObject.transform, false);
            return buttonObject.GetComponent<Button>();
        }

        private TextMeshProUGUI CreateLabel(string name)
        {
            var labelObject = new GameObject(name, typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(canvasObject.transform, false);
            return labelObject.GetComponent<TextMeshProUGUI>();
        }

        [Test]
        public void EstateButton_OnFirstOpen_ShowsPanelWithStartingCoinsAndPlots()
        {
            estateButton.onClick.Invoke();

            Assert.IsTrue(panelRootObject.activeSelf);
            Assert.AreEqual("Coins: 200", coinsLabel.text);
        }

        [Test]
        public void EstateButton_OnOpen_LockedPlotsShowLockedOverlayWithCost()
        {
            estateButton.onClick.Invoke();

            Assert.IsTrue(plotViews[4].lockedOverlay.activeSelf);
            Assert.AreEqual("Unlock: 100", plotViews[4].lockCostLabel.text);
            Assert.IsFalse(plotViews[0].lockedOverlay.activeSelf);
        }

        [Test]
        public void EstateButton_OnOpen_DisablesSharedControls()
        {
            estateButton.onClick.Invoke();

            Assert.IsFalse(armySlider.interactable);
            Assert.IsFalse(submitButton.interactable);
            Assert.IsFalse(challengeButton.interactable);
            Assert.IsFalse(viewHistoryButton.interactable);
            Assert.IsFalse(councilButton.interactable);
            Assert.IsFalse(eventsButton.interactable);
            Assert.IsFalse(customizeButton.interactable);
        }

        [Test]
        public void Close_ReEnablesSharedControlsAndHidesPanel()
        {
            estateButton.onClick.Invoke();
            closeButton.onClick.Invoke();

            Assert.IsFalse(panelRootObject.activeSelf);
            Assert.IsTrue(armySlider.interactable);
            Assert.IsTrue(submitButton.interactable);
        }

        [Test]
        public void TapEmptyUnlockedPlot_ShowsSeedPickerWithAffordableButtonsEnabled()
        {
            estateButton.onClick.Invoke();

            plotViews[0].tapButton.onClick.Invoke();

            Assert.IsTrue(seedPickerRoot.activeSelf);
            Assert.IsTrue(seedButtons[0].interactable); // wheat, cost 5, affordable at 200 coins
            Assert.AreEqual("Wheat: 5", seedCostLabels[0].text);
        }

        [Test]
        public void PickSeed_DeductsCoinsAndPlantsOnPendingPlot()
        {
            estateButton.onClick.Invoke();
            plotViews[0].tapButton.onClick.Invoke();

            seedButtons[0].onClick.Invoke(); // wheat, cost 5

            Assert.AreEqual("Coins: 195", coinsLabel.text);
            Assert.IsFalse(seedPickerRoot.activeSelf);
        }

        [Test]
        public void TapPlantedUnwateredPlot_StartsGrowthByRecordingWaterTime()
        {
            estateButton.onClick.Invoke();
            plotViews[0].tapButton.onClick.Invoke();
            seedButtons[0].onClick.Invoke(); // plant wheat

            plotViews[0].tapButton.onClick.Invoke(); // water it

            // Reopen fresh to confirm the watered timestamp persisted into
            // the controller's in-memory state (checked indirectly via
            // save/reload below, which is the observable contract).
            closeButton.onClick.Invoke();
            var saved = SaveService.LoadEstate();
            Assert.AreNotEqual(0, saved.Plots[0].WateredAtUnixSeconds);
            Assert.AreEqual("wheat", saved.Plots[0].CropId);
        }

        [Test]
        public void TapMaturePlot_AwardsSellValueCoinsAndClearsPlot()
        {
            // Arrange a pre-existing save with plot 0 already mature (wheat,
            // 30s grow duration, watered far enough in the past).
            var seeded = new EstateState { Coins = 200 };
            seeded.Plots[0].CropId = "wheat";
            seeded.Plots[0].PlantedAtUnixSeconds = 1;
            seeded.Plots[0].WateredAtUnixSeconds = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3600;
            SaveService.SaveEstate(seeded);

            estateButton.onClick.Invoke(); // loads the seeded state
            plotViews[0].tapButton.onClick.Invoke(); // harvest (mature)

            Assert.AreEqual("Coins: 212", coinsLabel.text); // 200 + 12 (wheat SellValue)
        }

        [Test]
        public void TapMaturePlot_ClearsCropSoNextOpenShowsEmptyPlot()
        {
            var seeded = new EstateState { Coins = 200 };
            seeded.Plots[0].CropId = "wheat";
            seeded.Plots[0].PlantedAtUnixSeconds = 1;
            seeded.Plots[0].WateredAtUnixSeconds = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3600;
            SaveService.SaveEstate(seeded);

            estateButton.onClick.Invoke();
            plotViews[0].tapButton.onClick.Invoke();
            closeButton.onClick.Invoke();

            var saved = SaveService.LoadEstate();
            Assert.IsTrue(string.IsNullOrEmpty(saved.Plots[0].CropId));
        }

        [Test]
        public void TapGrowingNotMaturePlot_DoesNothing()
        {
            var seeded = new EstateState { Coins = 200 };
            seeded.Plots[0].CropId = "wheat";
            seeded.Plots[0].PlantedAtUnixSeconds = 1;
            seeded.Plots[0].WateredAtUnixSeconds = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // stage 0/1, not mature
            SaveService.SaveEstate(seeded);

            estateButton.onClick.Invoke();
            plotViews[0].tapButton.onClick.Invoke(); // should be a no-op harvest attempt

            Assert.AreEqual("Coins: 200", coinsLabel.text);
        }

        [Test]
        public void TapLockedPlot_WithEnoughCoins_UnlocksAndDeductsCost()
        {
            estateButton.onClick.Invoke(); // 200 coins, plot 4 costs 100

            plotViews[4].tapButton.onClick.Invoke();

            Assert.AreEqual("Coins: 100", coinsLabel.text);
            Assert.IsFalse(plotViews[4].lockedOverlay.activeSelf);
        }

        [Test]
        public void TapLockedPlot_WithoutEnoughCoins_DoesNothing()
        {
            var seeded = new EstateState { Coins = 50 }; // plot 4 costs 100
            SaveService.SaveEstate(seeded);

            estateButton.onClick.Invoke();
            plotViews[4].tapButton.onClick.Invoke();

            Assert.AreEqual("Coins: 50", coinsLabel.text);
            Assert.IsTrue(plotViews[4].lockedOverlay.activeSelf);
        }

        [Test]
        public void Close_PersistsStateForNextOpen()
        {
            estateButton.onClick.Invoke();
            plotViews[4].tapButton.onClick.Invoke(); // unlock plot 4, 200 -> 100
            closeButton.onClick.Invoke();

            var saved = SaveService.LoadEstate();
            Assert.AreEqual(100, saved.Coins);
            Assert.IsTrue(saved.Plots[4].Unlocked);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Same PlayMode command shape as prior sessions:
`-runTests -testPlatform PlayMode -testResults <path>.xml -logFile <path>.log`
(no `-quit`, per this project's established batchmode gotcha -- see
`docs/PROJECT_PLAN.md`'s build-tooling notes if unfamiliar).
Expected: FAIL with "EstatePanelController does not exist" (compile error).

- [ ] **Step 3: Write the implementation**

```csharp
// Assets/Scripts/UI/EstatePanelController.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Seventh modal panel alongside Duel/History/Council/Tutorial/Events/
    /// Cosmetics. Fully client-authoritative and locally persisted (no
    /// network calls) -- see
    /// docs/superpowers/specs/2026-09-07-estate-phase1-land-crops-design.md.
    /// </summary>
    public class EstatePanelController : MonoBehaviour
    {
        [Serializable]
        public class PlotView
        {
            public Image stageImage;
            public Button tapButton;
            public GameObject lockedOverlay;
            public TextMeshProUGUI lockCostLabel;
        }

        [SerializeField] private Button estateButton;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI coinsLabel;
        [SerializeField] private PlotView[] plotViews;
        [SerializeField] private GameObject seedPickerRoot;
        [SerializeField] private Button[] seedButtons;
        [SerializeField] private TextMeshProUGUI[] seedCostLabels;
        // Flat, index = CropCatalog.IndexOf(cropId) * 3 + stage. Loaded and
        // assigned once at scene-build time (CoreLoopSceneBuilder), same
        // pattern as CosmeticsPanelController's backgroundSprites arrays --
        // never loaded at runtime.
        [SerializeField] private Sprite[] cropStageSprites;
        [SerializeField] private Slider armySlider;
        [SerializeField] private Slider tradeSlider;
        [SerializeField] private Slider religionSlider;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button viewHistoryButton;
        [SerializeField] private Button councilButton;
        [SerializeField] private Button eventsButton;
        [SerializeField] private Button customizeButton;
        [SerializeField] private DuelModalGate gate;

        private EstateState state;
        private int pendingPlantPlotIndex = -1;

        private void Start()
        {
            Bind();
        }

        /// <summary>
        /// Mirrors CouncilPanelController/CosmeticsPanelController's
        /// Initialize pattern -- called by Start() in the real scene, and
        /// callable directly by tests to bypass Unity lifecycle timing.
        /// </summary>
        public void Initialize(
            Button estateButton,
            GameObject panelRoot,
            Button closeButton,
            TextMeshProUGUI coinsLabel,
            PlotView[] plotViews,
            GameObject seedPickerRoot,
            Button[] seedButtons,
            TextMeshProUGUI[] seedCostLabels,
            Sprite[] cropStageSprites,
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button submitButton,
            Button challengeButton,
            Button viewHistoryButton,
            Button councilButton,
            Button eventsButton,
            Button customizeButton,
            DuelModalGate gate)
        {
            this.estateButton = estateButton;
            this.panelRoot = panelRoot;
            this.closeButton = closeButton;
            this.coinsLabel = coinsLabel;
            this.plotViews = plotViews;
            this.seedPickerRoot = seedPickerRoot;
            this.seedButtons = seedButtons;
            this.seedCostLabels = seedCostLabels;
            this.cropStageSprites = cropStageSprites;
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.submitButton = submitButton;
            this.challengeButton = challengeButton;
            this.viewHistoryButton = viewHistoryButton;
            this.councilButton = councilButton;
            this.eventsButton = eventsButton;
            this.customizeButton = customizeButton;
            this.gate = gate;

            Bind();
        }

        private void Bind()
        {
            estateButton.onClick.RemoveAllListeners();
            estateButton.onClick.AddListener(OnEstateButtonClicked);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnClose);

            for (int i = 0; i < plotViews.Length; i++)
            {
                int plotIndex = i; // capture by value, not the loop variable
                plotViews[i].tapButton.onClick.RemoveAllListeners();
                plotViews[i].tapButton.onClick.AddListener(() => OnPlotTapped(plotIndex));
            }

            for (int i = 0; i < seedButtons.Length; i++)
            {
                int cropIndex = i;
                seedButtons[i].onClick.RemoveAllListeners();
                seedButtons[i].onClick.AddListener(() => OnSeedPicked(cropIndex));
            }

            panelRoot.SetActive(false);
            seedPickerRoot.SetActive(false);
        }

        private void OnEstateButtonClicked()
        {
            gate.IsModalOpen = true;
            SetCoreLoopControlsInteractable(false);
            state = SaveService.LoadEstate();
            panelRoot.SetActive(true);
            RefreshPlots();
        }

        private void OnClose()
        {
            SaveService.SaveEstate(state);
            seedPickerRoot.SetActive(false);
            pendingPlantPlotIndex = -1;
            panelRoot.SetActive(false);
            gate.IsModalOpen = false;
            SetCoreLoopControlsInteractable(true);
        }

        private void OnPlotTapped(int plotIndex)
        {
            LandPlot plot = state.Plots[plotIndex];

            if (!plot.Unlocked)
            {
                int cost = EstateState.UnlockCost(plotIndex);
                if (state.Coins < cost)
                {
                    return;
                }
                state.Coins -= cost;
                plot.Unlocked = true;
                RefreshPlots();
                return;
            }

            if (string.IsNullOrEmpty(plot.CropId))
            {
                pendingPlantPlotIndex = plotIndex;
                ShowSeedPicker();
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (plot.WateredAtUnixSeconds == 0)
            {
                plot.WateredAtUnixSeconds = now;
                StartCoroutine(ColorFlash(plotViews[plotIndex].stageImage, new Color(0.4f, 0.7f, 1f), 0.2f));
                RefreshPlots();
                return;
            }

            CropDefinition? crop = CropCatalog.Find(plot.CropId);
            if (crop == null)
            {
                return;
            }

            int stage = EstateState.GrowthStage(plot, crop.Value, now);
            if (stage < 2)
            {
                return;
            }

            state.Coins += crop.Value.SellValue;
            plot.CropId = null;
            plot.PlantedAtUnixSeconds = 0;
            plot.WateredAtUnixSeconds = 0;
            StartCoroutine(HarvestFly(plotViews[plotIndex].stageImage));
            RefreshPlots();
        }

        private void ShowSeedPicker()
        {
            for (int i = 0; i < CropCatalog.All.Length; i++)
            {
                CropDefinition crop = CropCatalog.All[i];
                seedCostLabels[i].text = $"{crop.DisplayName}: {crop.SeedCost}";
                seedButtons[i].interactable = state.Coins >= crop.SeedCost;
            }
            seedPickerRoot.SetActive(true);
        }

        private void OnSeedPicked(int cropIndex)
        {
            if (pendingPlantPlotIndex < 0)
            {
                return;
            }

            CropDefinition crop = CropCatalog.All[cropIndex];
            if (state.Coins < crop.SeedCost)
            {
                return;
            }

            state.Coins -= crop.SeedCost;
            LandPlot plot = state.Plots[pendingPlantPlotIndex];
            plot.CropId = crop.Id;
            plot.PlantedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            plot.WateredAtUnixSeconds = 0;

            int plantedPlotIndex = pendingPlantPlotIndex;
            seedPickerRoot.SetActive(false);
            pendingPlantPlotIndex = -1;
            RefreshPlots();
            StartCoroutine(ScaleBounce(plotViews[plantedPlotIndex].stageImage.transform, 1.3f, 0.15f));
        }

        private void RefreshPlots()
        {
            coinsLabel.text = $"Coins: {state.Coins}";
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            for (int i = 0; i < EstateState.PlotCount; i++)
            {
                LandPlot plot = state.Plots[i];
                PlotView view = plotViews[i];

                if (!plot.Unlocked)
                {
                    view.lockedOverlay.SetActive(true);
                    view.stageImage.gameObject.SetActive(false);
                    view.lockCostLabel.text = $"Unlock: {EstateState.UnlockCost(i)}";
                    continue;
                }

                view.lockedOverlay.SetActive(false);
                view.stageImage.gameObject.SetActive(true);

                if (string.IsNullOrEmpty(plot.CropId))
                {
                    view.stageImage.sprite = null;
                    continue;
                }

                int cropIndex = CropCatalog.IndexOf(plot.CropId);
                CropDefinition? crop = CropCatalog.Find(plot.CropId);
                if (crop == null || cropIndex < 0)
                {
                    continue;
                }

                int stage = EstateState.GrowthStage(plot, crop.Value, now);
                view.stageImage.sprite = GetStageSprite(cropIndex, stage);
            }
        }

        // Missing/not-yet-assigned sprites resolve to null (Image just
        // shows nothing) rather than throwing -- matches every other
        // panel's null/length-guarded sprite lookup precedent
        // (CosmeticsPanelController.GetBackgroundSprite).
        private Sprite GetStageSprite(int cropIndex, int stage)
        {
            if (cropStageSprites == null)
            {
                return null;
            }
            int index = cropIndex * 3 + stage;
            return index >= 0 && index < cropStageSprites.Length ? cropStageSprites[index] : null;
        }

        private void SetCoreLoopControlsInteractable(bool interactable)
        {
            estateButton.interactable = interactable;
            armySlider.interactable = interactable;
            tradeSlider.interactable = interactable;
            religionSlider.interactable = interactable;
            submitButton.interactable = interactable;
            viewHistoryButton.interactable = interactable;
            councilButton.interactable = interactable;
            eventsButton.interactable = interactable;
            customizeButton.interactable = interactable;

            // challengeButton has two independent disablers (this modal, and
            // Duel's own in-flight state) -- see
            // docs/superpowers/specs/2026-09-03-duel-modal-gate-design.md.
            if (interactable && gate.IsDuelInFlight)
            {
                return;
            }
            challengeButton.interactable = interactable;
        }

        private static IEnumerator ScaleBounce(Transform target, float peakScale, float duration)
        {
            Vector3 original = target.localScale;
            float half = duration / 2f;
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                target.localScale = Vector3.Lerp(original, original * peakScale, t / half);
                yield return null;
            }
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                target.localScale = Vector3.Lerp(original * peakScale, original, t / half);
                yield return null;
            }
            target.localScale = original;
        }

        private static IEnumerator ColorFlash(Image target, Color flashColor, float duration)
        {
            Color original = target.color;
            float half = duration / 2f;
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                target.color = Color.Lerp(original, flashColor, t / half);
                yield return null;
            }
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                target.color = Color.Lerp(flashColor, original, t / half);
                yield return null;
            }
            target.color = original;
        }

        // Spawns a short-lived copy of the harvested plot's sprite that
        // arcs to the coin label then is destroyed -- both live under the
        // same Canvas so a straight world-position lerp is a valid arc
        // target, no coroutine-owning-object lifetime issue since this
        // MonoBehaviour outlives the tween.
        private IEnumerator HarvestFly(Image sourceImage)
        {
            Sprite sprite = sourceImage.sprite;
            if (sprite == null)
            {
                yield break;
            }

            var flyer = new GameObject("HarvestFlyer", typeof(Image));
            flyer.transform.SetParent(sourceImage.transform.parent, false);
            var flyerImage = flyer.GetComponent<Image>();
            flyerImage.sprite = sprite;
            flyerImage.raycastTarget = false;
            var flyerRect = flyer.GetComponent<RectTransform>();
            flyerRect.position = sourceImage.transform.position;
            flyerRect.sizeDelta = sourceImage.rectTransform.sizeDelta;

            Vector3 start = flyerRect.position;
            Vector3 end = coinsLabel.transform.position;
            const float duration = 0.4f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                flyerRect.position = Vector3.Lerp(start, end, t / duration);
                yield return null;
            }

            Destroy(flyer);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Same PlayMode command as Step 2. Expected: all
`EstatePanelControllerTests` PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/EstatePanelController.cs Assets/Scripts/UI/EstatePanelController.cs.meta Assets/Tests/PlayMode/EstatePanelControllerTests.cs Assets/Tests/PlayMode/EstatePanelControllerTests.cs.meta
git commit -m "feat: add EstatePanelController (plant/water/harvest/unlock + tween animations)"
```

---

### Task 5: Wire the Estate button into every existing panel's shared-controls list

**Why this task exists:** every existing modal panel
(Council/History/Events/Cosmetics/Tutorial) directly toggles
`.interactable` on every *other* panel's open button while it's open, and
vice versa -- that's the entire mechanism preventing two modals from being
open at once (there's no separate "any modal open" flag; it's enforced by
every button being in every other controller's disable list). Skipping
this task means a player could open Council, then also tap Estate, with
both panels active simultaneously -- a real regression, not a
nice-to-have. This is a mechanical, repetitive change across 5 controller
files and their 8 test files.

**Files:**
- Modify: `Assets/Scripts/UI/CouncilPanelController.cs`
- Modify: `Assets/Scripts/UI/HistoryPanelController.cs`
- Modify: `Assets/Scripts/UI/EventPanelController.cs`
- Modify: `Assets/Scripts/UI/CosmeticsPanelController.cs`
- Modify: `Assets/Scripts/UI/TutorialOverlayController.cs`
- Modify: `Assets/Tests/PlayMode/CouncilPanelControllerTests.cs`
- Modify: `Assets/Tests/PlayMode/CouncilPanelControllerRealDataTests.cs`
- Modify: `Assets/Tests/PlayMode/HistoryPanelControllerTests.cs`
- Modify: `Assets/Tests/PlayMode/HistoryPanelControllerRealDataTests.cs`
- Modify: `Assets/Tests/PlayMode/EventPanelControllerTests.cs`
- Modify: `Assets/Tests/PlayMode/EventPanelControllerRealDataTests.cs`
- Modify: `Assets/Tests/PlayMode/CosmeticsPanelControllerTests.cs`
- Modify: `Assets/Tests/PlayMode/TutorialOverlayControllerTests.cs`

**Interfaces:**
- Consumes: nothing new -- this task only *adds a trailing parameter* to
  5 existing `Initialize(...)` signatures.
- Produces: every existing panel controller now takes one more trailing
  `Button estateButton` parameter and disables/re-enables it alongside
  its other shared controls. Task 6 (the real scene) is the only
  non-test call site for all five -- update it there, not before.

**The identical shape, applied 5 times.** For each of the 5 controller
files: (1) add `[SerializeField] private Button estateButton;` after the
existing `customizeButton`/last shared-button field, (2) add `Button
estateButton` as the new last parameter of `Initialize(...)` and `this.estateButton
= estateButton;` in its body, (3) add `estateButton.interactable =
interactable;` inside `SetCoreLoopControlsInteractable`. No test runs
this step in isolation -- compile only succeeds once every call site
(including the 8 test files) is updated too, so do all 13 file edits
before running tests once at the end.

- [ ] **Step 1: `CouncilPanelController.cs`**

Field (after `[SerializeField] private Image councilIcon;` /
`createIcon`/`joinIcon` -- add after whichever is currently last):
```csharp
        [SerializeField] private Button estateButton;
```
`Initialize(...)` -- add as the new final parameter, after `Image joinIcon`:
```csharp
            Image joinIcon,
            Button estateButton)
```
Body -- add after `this.joinIcon = joinIcon;`:
```csharp
            this.estateButton = estateButton;
```
`SetCoreLoopControlsInteractable` -- add after `customizeButton.interactable = interactable;`:
```csharp
            estateButton.interactable = interactable;
```

- [ ] **Step 2: `HistoryPanelController.cs`**

Field (after `[SerializeField] private Image viewHistoryIcon;`):
```csharp
        [SerializeField] private Button estateButton;
```
`Initialize(...)` -- add as the new final parameter, after `Image viewHistoryIcon`:
```csharp
            Image viewHistoryIcon,
            Button estateButton)
```
Body -- add after `this.viewHistoryIcon = viewHistoryIcon;`:
```csharp
            this.estateButton = estateButton;
```
`SetCoreLoopControlsInteractable` -- add after `customizeButton.interactable = interactable;`:
```csharp
            estateButton.interactable = interactable;
```

- [ ] **Step 3: `EventPanelController.cs`**

Field (after `[SerializeField] private Image claimIcon;`):
```csharp
        [SerializeField] private Button estateButton;
```
`Initialize(...)` -- add as the new final parameter, after `Image claimIcon`:
```csharp
            Image claimIcon,
            Button estateButton)
```
Body -- add after `this.claimIcon = claimIcon;`:
```csharp
            this.estateButton = estateButton;
```
`SetCoreLoopControlsInteractable` -- add after `customizeButton.interactable = interactable;`:
```csharp
            estateButton.interactable = interactable;
```

- [ ] **Step 4: `CosmeticsPanelController.cs`**

Field (after `[SerializeField] private Image customizeIcon;`):
```csharp
        [SerializeField] private Button estateButton;
```
`Initialize(...)` -- add as the new final parameter, after `Image customizeIcon`:
```csharp
            Image customizeIcon,
            Button estateButton)
```
Body -- add after `this.customizeIcon = customizeIcon;`:
```csharp
            this.estateButton = estateButton;
```
`SetCoreLoopControlsInteractable` -- add after `submitButton.interactable = interactable;` (this
controller's list ends with `submitButton` before the `challengeButton` gate check -- see its
existing body):
```csharp
            estateButton.interactable = interactable;
```

- [ ] **Step 5: `TutorialOverlayController.cs`**

Field (after `[SerializeField] private Image skipIcon;`):
```csharp
        [SerializeField] private Button estateButton;
```
`Initialize(...)` -- add as the new final parameter, after `Image skipIcon`:
```csharp
            Image skipIcon,
            Button estateButton)
```
Body -- add after `this.skipIcon = skipIcon;`:
```csharp
            this.estateButton = estateButton;
```
`SetCoreLoopControlsInteractable` -- add after `customizeButton.interactable = interactable;`:
```csharp
            estateButton.interactable = interactable;
```

- [ ] **Step 6: Update every test file's `Initialize(...)` call site**

Each of the 8 test files below constructs one dummy `Button` and passes it
as the new trailing argument (cannot pass `null` -- `SetCoreLoopControlsInteractable`
unconditionally dereferences it). Add this field + creation line to each
test's `SetUp`/setup method, then append it to the existing `Initialize(...)`
call's argument list:

```csharp
        private Button estateButton; // add as a field near the other Button fields
```
```csharp
            var estateButtonObject = new GameObject("EstateButton", typeof(Image), typeof(Button));
            estateButtonObject.transform.SetParent(canvasObject.transform, false);
            estateButton = estateButtonObject.GetComponent<Button>();
            // add this block right before the controller's Initialize(...) call
```
Then append `, estateButton)` to the existing `Initialize(...)` call's
closing arguments (replacing its current closing `);`) in:
`CouncilPanelControllerTests.cs`, `CouncilPanelControllerRealDataTests.cs`,
`HistoryPanelControllerTests.cs`, `HistoryPanelControllerRealDataTests.cs`,
`EventPanelControllerTests.cs`, `EventPanelControllerRealDataTests.cs`,
`CosmeticsPanelControllerTests.cs`, `TutorialOverlayControllerTests.cs`.

- [ ] **Step 7: Run the full test suite to verify everything still compiles and passes**

Run both EditMode and PlayMode commands (same shape as earlier tasks).
Expected: PASS -- same total test count as before this task plus zero new
tests (this task adds no new behavior, only a new required wiring point);
a compile error here means one of the 13 files' edits was missed or a
parameter was placed in the wrong position.

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/UI/CouncilPanelController.cs Assets/Scripts/UI/HistoryPanelController.cs Assets/Scripts/UI/EventPanelController.cs Assets/Scripts/UI/CosmeticsPanelController.cs Assets/Scripts/UI/TutorialOverlayController.cs Assets/Tests/PlayMode/CouncilPanelControllerTests.cs Assets/Tests/PlayMode/CouncilPanelControllerRealDataTests.cs Assets/Tests/PlayMode/HistoryPanelControllerTests.cs Assets/Tests/PlayMode/HistoryPanelControllerRealDataTests.cs Assets/Tests/PlayMode/EventPanelControllerTests.cs Assets/Tests/PlayMode/EventPanelControllerRealDataTests.cs Assets/Tests/PlayMode/CosmeticsPanelControllerTests.cs Assets/Tests/PlayMode/TutorialOverlayControllerTests.cs
git commit -m "feat: wire estateButton into every existing panel's modal-exclusivity gating"
```

---

### Task 6: CoreLoopSceneBuilder wiring

**Files:**
- Modify: `Assets/Editor/CoreLoopSceneBuilder.cs`

**Interfaces:**
- Consumes: `EstatePanelController.Initialize(...)` (Task 4), the
  5 updated `Initialize(...)` signatures (Task 5), `LoadIconSprite(string
  path)` (existing private helper), `CreateLabel(Transform, string,
  float, string)` (existing private helper).
- Produces: the real, playable Estate button + panel in the committed
  `CoreLoop.unity` scene (regenerated by running `Build`).

Follow the exact `HistoryPanel` construction block (panel root + close
button + title) already in this file as the template, extended with the
8-plot grid and seed picker. Insert this block after the existing
`CosmeticsPanel` construction block and before its own
`cosmeticsController.Initialize(...)` call site is easiest to locate via
search -- exact insertion point doesn't matter as long as `estateButton`
exists before every `Initialize(...)` call that now needs it (Task 5's
5 call sites, updated below).

- [ ] **Step 1: Add the Estate button (throne-room list) and panel construction**

Add immediately after the existing `customizeButton` creation block (the
last throne-room button in the existing list):

```csharp
            var estateButtonObject = new GameObject("EstateButton", typeof(Image), typeof(Button));
            estateButtonObject.transform.SetParent(canvasObject.transform, false);
            var estateButtonRect = estateButtonObject.GetComponent<RectTransform>();
            estateButtonRect.anchoredPosition = new Vector2(0f, -420f);
            estateButtonRect.sizeDelta = new Vector2(220f, 44f);
            estateButtonObject.GetComponent<Image>().color = new Color(0.35f, 0.55f, 0.3f, 1f);
            var estateButton = estateButtonObject.GetComponent<Button>();
            TextMeshProUGUI estateButtonLabel = CreateLabel(estateButtonObject.transform, "Text", 0f, "Estate");
            var estateButtonLabelRect = estateButtonLabel.GetComponent<RectTransform>();
            estateButtonLabelRect.anchorMin = Vector2.zero;
            estateButtonLabelRect.anchorMax = Vector2.one;
            estateButtonLabelRect.sizeDelta = Vector2.zero;
            estateButtonLabelRect.anchoredPosition = Vector2.zero;

            var estatePanelRootObject = new GameObject("EstatePanel", typeof(Image));
            estatePanelRootObject.transform.SetParent(canvasObject.transform, false);
            var estatePanelRect = estatePanelRootObject.GetComponent<RectTransform>();
            estatePanelRect.anchoredPosition = Vector2.zero;
            estatePanelRect.sizeDelta = new Vector2(700f, 800f);
            estatePanelRootObject.GetComponent<Image>().color = new Color(0.1f, 0.15f, 0.1f, 0.95f);

            var estateCloseButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            estateCloseButtonObject.transform.SetParent(estatePanelRootObject.transform, false);
            var estateCloseButtonRect = estateCloseButtonObject.GetComponent<RectTransform>();
            estateCloseButtonRect.anchoredPosition = new Vector2(310f, 360f);
            estateCloseButtonRect.sizeDelta = new Vector2(60f, 40f);
            estateCloseButtonObject.GetComponent<Image>().color = new Color(0.6f, 0.3f, 0.3f, 1f);
            var estateCloseButton = estateCloseButtonObject.GetComponent<Button>();
            TextMeshProUGUI estateCloseLabel = CreateLabel(estateCloseButtonObject.transform, "Text", 0f, "X");
            var estateCloseLabelRect = estateCloseLabel.GetComponent<RectTransform>();
            estateCloseLabelRect.anchorMin = Vector2.zero;
            estateCloseLabelRect.anchorMax = Vector2.one;
            estateCloseLabelRect.sizeDelta = Vector2.zero;
            estateCloseLabelRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI estateTitleLabel = CreateLabel(estatePanelRootObject.transform, "Title", 0f, "Your Estate");
            estateTitleLabel.fontSize = 28f;
            estateTitleLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 340f);

            TextMeshProUGUI estateCoinsLabel = CreateLabel(estatePanelRootObject.transform, "CoinsLabel", 0f, "Coins: 200");
            estateCoinsLabel.fontSize = 24f;
            estateCoinsLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 295f);
```

- [ ] **Step 2: Add the 8-plot grid (4x2, PlotView per slot)**

```csharp
            var estatePlotViews = new UnderstudyKingdom.UI.EstatePanelController.PlotView[8];
            for (int i = 0; i < 8; i++)
            {
                int column = i % 4;
                int row = i / 4;
                float plotX = -300f + column * 200f;
                float plotY = 180f - row * 220f;

                var slotBackgroundObject = new GameObject($"Plot{i}", typeof(Image));
                slotBackgroundObject.transform.SetParent(estatePanelRootObject.transform, false);
                var slotBackgroundRect = slotBackgroundObject.GetComponent<RectTransform>();
                slotBackgroundRect.anchoredPosition = new Vector2(plotX, plotY);
                slotBackgroundRect.sizeDelta = new Vector2(160f, 160f);
                slotBackgroundObject.GetComponent<Image>().color = new Color(0.25f, 0.2f, 0.1f, 1f);

                var stageObject = new GameObject("Stage", typeof(Image));
                stageObject.transform.SetParent(slotBackgroundObject.transform, false);
                var stageRect = stageObject.GetComponent<RectTransform>();
                stageRect.anchoredPosition = Vector2.zero;
                stageRect.sizeDelta = new Vector2(120f, 120f);
                var stageImage = stageObject.GetComponent<Image>();
                stageImage.raycastTarget = false;

                var tapButtonObject = new GameObject("TapButton", typeof(Image), typeof(Button));
                tapButtonObject.transform.SetParent(slotBackgroundObject.transform, false);
                var tapButtonRect = tapButtonObject.GetComponent<RectTransform>();
                tapButtonRect.anchorMin = Vector2.zero;
                tapButtonRect.anchorMax = Vector2.one;
                tapButtonRect.sizeDelta = Vector2.zero;
                tapButtonRect.anchoredPosition = Vector2.zero;
                tapButtonObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
                var tapButton = tapButtonObject.GetComponent<Button>();

                var lockedOverlayObject = new GameObject("LockedOverlay", typeof(Image));
                lockedOverlayObject.transform.SetParent(slotBackgroundObject.transform, false);
                var lockedOverlayRect = lockedOverlayObject.GetComponent<RectTransform>();
                lockedOverlayRect.anchorMin = Vector2.zero;
                lockedOverlayRect.anchorMax = Vector2.one;
                lockedOverlayRect.sizeDelta = Vector2.zero;
                lockedOverlayRect.anchoredPosition = Vector2.zero;
                lockedOverlayObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
                lockedOverlayObject.transform.SetAsLastSibling();

                TextMeshProUGUI lockCostLabel = CreateLabel(lockedOverlayObject.transform, "LockCost", 0f, "Unlock: 0");
                lockCostLabel.fontSize = 18f;

                estatePlotViews[i] = new UnderstudyKingdom.UI.EstatePanelController.PlotView
                {
                    stageImage = stageImage,
                    tapButton = tapButton,
                    lockedOverlay = lockedOverlayObject,
                    lockCostLabel = lockCostLabel
                };
            }
```

- [ ] **Step 3: Add the seed picker sub-view (3 crop buttons)**

```csharp
            var estateSeedPickerObject = new GameObject("SeedPicker", typeof(Image));
            estateSeedPickerObject.transform.SetParent(estatePanelRootObject.transform, false);
            var estateSeedPickerRect = estateSeedPickerObject.GetComponent<RectTransform>();
            estateSeedPickerRect.anchoredPosition = new Vector2(0f, -280f);
            estateSeedPickerRect.sizeDelta = new Vector2(640f, 120f);
            estateSeedPickerObject.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.05f, 0.98f);

            var estateSeedButtons = new Button[3];
            var estateSeedCostLabels = new TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                var seedButtonObject = new GameObject($"SeedButton{i}", typeof(Image), typeof(Button));
                seedButtonObject.transform.SetParent(estateSeedPickerObject.transform, false);
                var seedButtonRect = seedButtonObject.GetComponent<RectTransform>();
                seedButtonRect.anchoredPosition = new Vector2(-200f + i * 200f, 0f);
                seedButtonRect.sizeDelta = new Vector2(180f, 60f);
                seedButtonObject.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.3f, 1f);
                estateSeedButtons[i] = seedButtonObject.GetComponent<Button>();

                TextMeshProUGUI seedCostLabel = CreateLabel(seedButtonObject.transform, "Text", 0f, string.Empty);
                var seedCostLabelRect = seedCostLabel.GetComponent<RectTransform>();
                seedCostLabelRect.anchorMin = Vector2.zero;
                seedCostLabelRect.anchorMax = Vector2.one;
                seedCostLabelRect.sizeDelta = Vector2.zero;
                seedCostLabelRect.anchoredPosition = Vector2.zero;
                estateSeedCostLabels[i] = seedCostLabel;
            }
            estateSeedPickerObject.SetActive(false);
```

- [ ] **Step 4: Load crop sprites and construct the controller**

```csharp
            // 3 crops x 3 stages, flat -- index = cropIndex * 3 + stage,
            // matching EstatePanelController.GetStageSprite's indexing.
            var estateCropStageSprites = new Sprite[9];
            string[] estateCropIds = { "wheat", "carrot", "pumpkin" };
            string[] estateStageNames = { "sprout", "growing", "mature" };
            for (int cropIndex = 0; cropIndex < estateCropIds.Length; cropIndex++)
            {
                for (int stage = 0; stage < estateStageNames.Length; stage++)
                {
                    string path = $"Assets/Art/Crops/{estateCropIds[cropIndex]}_{estateStageNames[stage]}.png";
                    estateCropStageSprites[cropIndex * 3 + stage] = LoadIconSprite(path);
                }
            }

            var estateControllerObject = new GameObject("EstatePanelController");
            var estateController = estateControllerObject.AddComponent<UnderstudyKingdom.UI.EstatePanelController>();
            estateController.Initialize(estateButton, estatePanelRootObject, estateCloseButton, estateCoinsLabel,
                estatePlotViews, estateSeedPickerObject, estateSeedButtons, estateSeedCostLabels, estateCropStageSprites,
                armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton, councilButton,
                eventsButton, customizeButton, duelModalGate);
```

`LoadIconSprite` already logs an error and returns `null` for a missing
asset (existing behavior, unchanged) -- the crop PNG assets themselves are
art-generation work, explicitly out of scope for this coding task (same
split already established for every prior icon/portrait/background
milestone: code lands first, referencing a path; art is generated and
committed separately). Missing sprites render as blank Images, not a
build failure, matching `LoadIconSprite`'s existing null-safe contract.

- [ ] **Step 5: Update the 5 existing `Initialize(...)` call sites to pass `estateButton`**

For each of the 5 controllers touched in Task 5, find its existing
`Initialize(...)` call in this file and append `estateButton` as the new
final argument (matching the new parameter position added in Task 5):
`councilController.Initialize(...)`, `historyController.Initialize(...)`
(note: this file's local variable may be named differently -- search for
`HistoryPanelController` `Initialize` to find the exact call),
`eventController.Initialize(...)`, `cosmeticsController.Initialize(...)`,
`tutorialController.Initialize(...)`. Each call site's argument list
already ends with that controller's own last icon parameter (e.g.
`councilIconImage, createIconImage, joinIconImage` for Council) --
append `, estateButton` immediately after.

- [ ] **Step 6: Extend `Verify()`**

Add after the existing `councilPanelController` icon checks (mirroring
every prior panel-controller check in this method):

```csharp
            var estatePanelController = Object.FindFirstObjectByType<UnderstudyKingdom.UI.EstatePanelController>();
            if (estatePanelController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no EstatePanelController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
```

- [ ] **Step 7: Rebuild and verify the scene**

Run:
```
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Build -logFile <path>.log
```
Expected: `CoreLoopSceneBuilder: saved scene to Assets/Scenes/CoreLoop.unity`,
no errors (missing crop PNGs log warnings via `LoadIconSprite`, not build
failures -- expected until Task 8's art is generated).

Then:
```
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Verify -logFile <path>.log
```
Expected: `CoreLoopSceneBuilder.Verify: scene opened and controller found successfully.`

- [ ] **Step 8: Commit**

```bash
git add Assets/Editor/CoreLoopSceneBuilder.cs Assets/Scenes/CoreLoop.unity
git commit -m "feat: wire Estate button/panel/plot-grid/seed-picker into CoreLoop scene"
```

---

### Task 7: Scene-level regression test

**Files:**
- Modify: `Assets/Tests/PlayMode/CoreLoopSceneTests.cs`

**Interfaces:**
- Consumes: the built `CoreLoop.unity` scene (Task 6).

- [ ] **Step 1: Write the failing test**

Add to `Assets/Tests/PlayMode/CoreLoopSceneTests.cs` (same file/class as
every other scene-level test, following the existing
`LoadedCoreLoopScene_...` naming and `FindChildByName`/`FindButton`
helpers already in this file):

```csharp
        [UnityTest]
        public IEnumerator LoadedCoreLoopScene_EstateButton_OpensPanelWithFourUnlockedAndFourLockedPlots()
        {
            yield return SceneManager.LoadSceneAsync("CoreLoop");
            yield return null;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Canvas not found in the loaded CoreLoop scene.");

            Button estateButton = FindButton(canvas, "EstateButton");
            Assert.IsNotNull(estateButton, "EstateButton not found in the loaded CoreLoop scene.");

            GameObject estatePanel = FindChildByName(canvas.transform, "EstatePanel");
            Assert.IsNotNull(estatePanel, "EstatePanel not found in the loaded CoreLoop scene.");
            Assert.IsFalse(estatePanel.activeSelf, "Expected EstatePanel to start inactive.");

            estateButton.onClick.Invoke();

            Assert.IsTrue(estatePanel.activeSelf,
                "Expected EstatePanel to become active after EstateButton is clicked.");

            GameObject plot0Locked = FindChildByName(estatePanel.transform, "LockedOverlay");
            Assert.IsNotNull(plot0Locked, "Expected at least one Plot LockedOverlay in the Estate panel.");
        }
```

- [ ] **Step 2: Run test to verify it fails**

Same PlayMode command as Task 4. Expected: FAIL -- `EstateButton`/
`EstatePanel` not found (this test runs against the committed scene, so
it fails until Task 6's `Build` step has actually run and been
committed; if Task 6 is already done, this should instead PASS
immediately -- if so, skip to Step 4).

- [ ] **Step 3: N/A**

No implementation step -- Task 6 already built the scene. This task only
adds the regression test proving it stays correct on future scene
rebuilds.

- [ ] **Step 4: Run test to verify it passes**

Same command. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Tests/PlayMode/CoreLoopSceneTests.cs
git commit -m "test: add scene-level regression test for Estate panel"
```

---

## Final Verification (after all 7 tasks)

Run the full suite once more (per this project's established "batch it,
run once at the end" practice):

```
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testResults <path>.xml -logFile <path>.log
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testResults <path>.xml -logFile <path>.log
```

Expected: 100% pass, both platforms. Then follow
`understudy-kingdom:finishing-a-development-branch` to merge.

## Explicitly Out of Scope for This Plan

- Crop sprite art generation (`Assets/Art/Crops/*.png`, 9 files) -- code
  references the paths; art is a separate follow-up pass, same split as
  every prior milestone.
- Everything in Phases 2-5 of the roadmap (Animals, Shops, Trade, the
  recruitable Shop Seller, Integration with Council/Customize).
- Any APK rebuild/on-device verification -- follow the same
  build-and-visually-verify process already established once this plan's
  code is merged, as its own follow-up step.
