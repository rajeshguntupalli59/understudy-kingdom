# Estate Crop Animation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the existing Estate plant -> water -> grow -> harvest loop feel tactile: a real plant animation, a real water animation, a live growth progress bar with a stage-change pop, and a harvest-fly animation that goes to the correct place (inventory, not the stale coins label).

**Architecture:** Presentation-layer only, built entirely on the existing coroutine-tween pattern already in `EstatePanelController.cs` (`ScaleBounce`/`ColorFlash`/`HarvestFly`). One new pure helper on `EstateState` (`GrowthProgress01`) drives a per-plot progress bar via a new `Update()` loop, gated on panel visibility. Two new generic sprites (seed, water droplet) are optional at every layer -- every new animation degrades gracefully (state still updates correctly, no exception) if its sprite hasn't been generated yet, matching this project's established "missing art never blocks gameplay" precedent.

**Tech Stack:** Unity 6000.3.23f1, C#, NUnit EditMode/PlayMode tests, existing `CoreLoopSceneBuilder` scene-authoring pattern, Hugging Face FLUX.1-schnell art pipeline (token in memory, `size: "512x512"` for these small icon-scale assets).

## Global Constraints

- No new animation engine, rigging tool, or frame-sequence pipeline -- procedural coroutine tweens on static sprites only (per spec's Non-Goals).
- No change to `GrowthStage`, save format, growth timing, crop values, `Coins`/`Plots`/`Inventory`/`Shops` -- presentation layer only.
- Exactly 2 new sprites this pass, both generic (not per-crop): `seed.png`, `water_droplet.png`. If HF credits run out before both land, ship whichever completed; the other stays null and every consumer already degrades gracefully -- no task blocks on art completing.
- `EstateState.cs` stays zero-UnityEngine-dependency beyond plain `System` types (existing file-level constraint, stated in its own doc comment) -- `GrowthProgress01` must not use `UnityEngine.Mathf` or any Unity type.
- No comments explaining WHAT code does -- only WHY, and only when genuinely non-obvious, matching this project's established convention.
- Every Unity batch-mode test run: `-runTests` NEVER combined with `-quit` (races the runner, produces empty/incomplete results). `-executeMethod Build`/`Verify` do NOT need `-quit` (both already call `EditorApplication.Exit()` on their own success paths).
- Test-observable side effects for coroutine-triggered behavior use this project's established reflection idiom: `typeof(EstatePanelController).GetField("fieldName", BindingFlags.NonPublic | BindingFlags.Instance)` -- not new public test-only API surface.
- Visual tween *appearance* (easing curves, exact pixel motion, animation completion timing) is not meaningfully unit-testable and is not tested here, matching how `ScaleBounce`/`ColorFlash`/`HarvestFly` are already (not) tested today.

---

### Task 1: `EstateState.GrowthProgress01`

**Files:**
- Modify: `Assets/Scripts/Core/EstateState.cs` (add a new static method near `GrowthStage`, currently at line 60)
- Test: `Assets/Tests/EditMode/EstateStateTests.cs` (add tests near the existing `GrowthStage_*` tests, lines 25-90)

**Interfaces:**
- Consumes: `LandPlot` (`WateredAtUnixSeconds`), `CropDefinition` (`GrowDurationSeconds`) -- both already exist, unchanged.
- Produces: `EstateState.GrowthProgress01(LandPlot plot, CropDefinition crop, long nowUnixSeconds) -> float`, used by Task 2's `Update()` loop.

- [ ] **Step 1: Write the failing tests**

Add to `Assets/Tests/EditMode/EstateStateTests.cs`, after the existing `GrowthStage_OddGrowDuration_DoesNotTransitionEarly` test (line 90):

```csharp
        [Test]
        public void GrowthProgress01_NeverWatered_IsZero()
        {
            var plot = new LandPlot { CropId = "wheat", WateredAtUnixSeconds = 0 };
            var wheat = new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12);

            float progress = EstateState.GrowthProgress01(plot, wheat, nowUnixSeconds: 999999);

            Assert.AreEqual(0f, progress);
        }

        [Test]
        public void GrowthProgress01_JustWatered_IsZero()
        {
            var plot = new LandPlot { CropId = "wheat", WateredAtUnixSeconds = 1000 };
            var wheat = new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12);

            float progress = EstateState.GrowthProgress01(plot, wheat, nowUnixSeconds: 1000);

            Assert.AreEqual(0f, progress);
        }

        [Test]
        public void GrowthProgress01_AtHalfDuration_IsAboutHalf()
        {
            var plot = new LandPlot { CropId = "wheat", WateredAtUnixSeconds = 1000 };
            var wheat = new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12);

            float progress = EstateState.GrowthProgress01(plot, wheat, nowUnixSeconds: 1015); // 15s of 30s

            Assert.AreEqual(0.5f, progress, 0.001f);
        }

        [Test]
        public void GrowthProgress01_AtFullDuration_IsOne()
        {
            var plot = new LandPlot { CropId = "wheat", WateredAtUnixSeconds = 1000 };
            var wheat = new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12);

            float progress = EstateState.GrowthProgress01(plot, wheat, nowUnixSeconds: 1030);

            Assert.AreEqual(1f, progress, 0.001f);
        }

        [Test]
        public void GrowthProgress01_PastFullDuration_StaysClampedAtOne()
        {
            var plot = new LandPlot { CropId = "wheat", WateredAtUnixSeconds = 1000 };
            var wheat = new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12);

            float progress = EstateState.GrowthProgress01(plot, wheat, nowUnixSeconds: 1000 + 999999);

            Assert.AreEqual(1f, progress, 0.001f);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

```
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testResults <path> -logFile <path>
```
Expected: FAIL with "GrowthProgress01 not defined" (compile error) or NUnit "method not found."

- [ ] **Step 3: Implement**

In `Assets/Scripts/Core/EstateState.cs`, immediately after the `GrowthStage` method (ends at line 81), add:

```csharp
        /// <summary>
        /// Continuous 0..1 fraction toward the next growth transition, for
        /// driving a live progress-bar fillAmount -- GrowthStage itself
        /// stays the discrete 0/1/2 the rest of the codebase already
        /// depends on. Same WateredAtUnixSeconds-based timing: 0 before
        /// watering (nothing to show), clamped to 1 at/after
        /// GrowDurationSeconds. Plain System.Math clamp, not
        /// UnityEngine.Mathf -- this file stays free of the UnityEngine
        /// dependency (see the class doc comment above).
        /// </summary>
        public static float GrowthProgress01(LandPlot plot, CropDefinition crop, long nowUnixSeconds)
        {
            if (plot.WateredAtUnixSeconds == 0)
            {
                return 0f;
            }
            long elapsed = nowUnixSeconds - plot.WateredAtUnixSeconds;
            float fraction = elapsed / (float)crop.GrowDurationSeconds;
            if (fraction < 0f)
            {
                return 0f;
            }
            return fraction > 1f ? 1f : fraction;
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Same command as Step 2. Expected: PASS, all 5 new tests plus the full existing `EstateStateTests` suite green.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/EstateState.cs Assets/Tests/EditMode/EstateStateTests.cs
git commit -m "feat: add EstateState.GrowthProgress01 for live growth-progress UI"
```

---

### Task 2: Live growth feedback (progress bar + stage-change pop)

**Files:**
- Modify: `Assets/Scripts/UI/EstatePanelController.cs`
- Modify: `Assets/Tests/PlayMode/EstatePanelControllerTests.cs`

**Interfaces:**
- Consumes: `EstateState.GrowthProgress01` (Task 1), `EstateState.GrowthStage` (existing), `PlotView` (existing).
- Produces: `PlotView.progressBarImage` (new public field, null-safe everywhere it's read), private `CheckStagePop(int plotIndex, int newStage)` method (invoked by tests via reflection to avoid needing real wall-clock stage-boundary crossings), private `lastPoppedPlotIndex`/`lastKnownStage` fields (test-observable via reflection). No `Initialize(...)` signature change -- `PlotView` gains a field, not a new top-level parameter.

- [ ] **Step 1: Write the failing tests**

In `Assets/Tests/PlayMode/EstatePanelControllerTests.cs`, add `using System.Reflection;` to the top imports (alongside the existing `using System.IO;`).

Modify the existing `plotViews` construction loop in `SetUp` (currently lines 59-74) to also create a progress-bar `Image` per plot:

```csharp
            plotViews = new EstatePanelController.PlotView[EstateState.PlotCount];
            for (int i = 0; i < plotViews.Length; i++)
            {
                var stageObject = new GameObject($"Plot{i}Stage", typeof(Image));
                stageObject.transform.SetParent(canvasObject.transform, false);
                var lockedOverlay = new GameObject($"Plot{i}Locked");
                lockedOverlay.transform.SetParent(canvasObject.transform, false);
                var progressBarObject = new GameObject($"Plot{i}ProgressBar", typeof(Image));
                progressBarObject.transform.SetParent(canvasObject.transform, false);
                var progressBarImage = progressBarObject.GetComponent<Image>();
                progressBarImage.type = Image.Type.Filled;

                plotViews[i] = new EstatePanelController.PlotView
                {
                    stageImage = stageObject.GetComponent<Image>(),
                    tapButton = CreateButton($"Plot{i}Button"),
                    lockedOverlay = lockedOverlay,
                    lockCostLabel = CreateLabel($"Plot{i}LockCost"),
                    progressBarImage = progressBarImage
                };
            }
```

Add these tests after the existing `TapGrowingNotMaturePlot_DoesNothing` test (ends around line 293):

```csharp
        [UnityTest]
        public IEnumerator PanelOpen_GrowingPlot_ProgressBarReflectsElapsedFraction()
        {
            var seeded = new EstateState { Coins = 200 };
            seeded.Plots[0].CropId = "wheat";
            seeded.Plots[0].PlantedAtUnixSeconds = 1;
            seeded.Plots[0].WateredAtUnixSeconds = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 15; // half of wheat's 30s
            SaveService.SaveEstate(seeded);

            estateButton.onClick.Invoke();
            yield return null; // let Update() run at least one tick

            Assert.IsTrue(plotViews[0].progressBarImage.gameObject.activeSelf);
            Assert.AreEqual(0.5f, plotViews[0].progressBarImage.fillAmount, 0.05f);
        }

        [Test]
        public void EmptyPlot_ProgressBarStaysHidden()
        {
            estateButton.onClick.Invoke();

            Assert.IsFalse(plotViews[0].progressBarImage.gameObject.activeSelf);
        }

        [Test]
        public void CheckStagePop_StageIncreased_SetsLastPoppedIndexAndUpdatesCache()
        {
            estateButton.onClick.Invoke(); // constructs the controller, syncs lastKnownStage via RefreshPlots

            MethodInfo checkStagePop = typeof(EstatePanelController).GetMethod("CheckStagePop", BindingFlags.NonPublic | BindingFlags.Instance);
            checkStagePop.Invoke(controller, new object[] { 0, 2 }); // plot 0 was stage 0 (empty), now claim stage 2

            FieldInfo lastPoppedField = typeof(EstatePanelController).GetField("lastPoppedPlotIndex", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo lastKnownField = typeof(EstatePanelController).GetField("lastKnownStage", BindingFlags.NonPublic | BindingFlags.Instance);
            var lastKnownStage = (int[])lastKnownField.GetValue(controller);

            Assert.AreEqual(0, (int)lastPoppedField.GetValue(controller));
            Assert.AreEqual(2, lastKnownStage[0]);
        }

        [Test]
        public void CheckStagePop_StageUnchanged_DoesNotSetLastPoppedIndex()
        {
            estateButton.onClick.Invoke();

            MethodInfo checkStagePop = typeof(EstatePanelController).GetMethod("CheckStagePop", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo lastPoppedField = typeof(EstatePanelController).GetField("lastPoppedPlotIndex", BindingFlags.NonPublic | BindingFlags.Instance);

            checkStagePop.Invoke(controller, new object[] { 1, 0 }); // plot 1 already at stage 0, claim stage 0 again

            Assert.AreEqual(-1, (int)lastPoppedField.GetValue(controller)); // default, never set
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Same PlayMode command as prior tasks (no `-quit`). Expected: FAIL -- `progressBarImage` not defined on `PlotView`, `CheckStagePop`/`lastPoppedPlotIndex`/`lastKnownStage` not found.

- [ ] **Step 3: Implement**

In `Assets/Scripts/UI/EstatePanelController.cs`, add `progressBarImage` to the `PlotView` class (currently lines 20-26):

```csharp
        [Serializable]
        public class PlotView
        {
            public Image stageImage;
            public Button tapButton;
            public GameObject lockedOverlay;
            public TextMeshProUGUI lockCostLabel;
            public Image progressBarImage;
        }
```

Add two new private fields near `pendingPlantPlotIndex` (currently line 53):

```csharp
        private int pendingPlantPlotIndex = -1;
        private readonly int[] lastKnownStage = new int[EstateState.PlotCount];
        private int lastPoppedPlotIndex = -1;
```

Add a new `Update()` method and `CheckStagePop` right after `Start()` (currently lines 55-58):

```csharp
        private void Update()
        {
            if (!panelRoot.activeSelf)
            {
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            for (int i = 0; i < EstateState.PlotCount; i++)
            {
                LandPlot plot = state.Plots[i];
                Image bar = plotViews[i].progressBarImage;

                if (!plot.Unlocked || string.IsNullOrEmpty(plot.CropId))
                {
                    if (bar != null)
                    {
                        bar.gameObject.SetActive(false);
                    }
                    continue;
                }

                CropDefinition? crop = CropCatalog.Find(plot.CropId);
                if (crop == null)
                {
                    continue;
                }

                int stage = EstateState.GrowthStage(plot, crop.Value, now);
                CheckStagePop(i, stage);

                if (bar != null)
                {
                    bool showBar = plot.WateredAtUnixSeconds != 0 && stage < 2;
                    bar.gameObject.SetActive(showBar);
                    if (showBar)
                    {
                        bar.fillAmount = EstateState.GrowthProgress01(plot, crop.Value, now);
                    }
                }
            }
        }

        // Extracted so tests can drive it directly with a controlled stage
        // value via reflection, instead of needing real wall-clock time to
        // cross a growth-stage boundary mid-test (crop durations are tens
        // of real seconds -- too slow to wait out in a PlayMode test).
        private void CheckStagePop(int plotIndex, int newStage)
        {
            if (newStage > lastKnownStage[plotIndex])
            {
                lastPoppedPlotIndex = plotIndex;
                StartCoroutine(ScaleBounce(plotViews[plotIndex].stageImage.transform, 1.3f, 0.15f));
            }
            lastKnownStage[plotIndex] = newStage;
        }
```

Modify `RefreshPlots()` (currently lines 327-369) to sync `lastKnownStage` directly (not via `CheckStagePop`, so opening the panel or any discrete action never itself fires a pop for growth that happened while the panel was closed -- only genuine in-panel `Update()` transitions do):

```csharp
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
                    lastKnownStage[i] = 0;
                    continue;
                }

                view.lockedOverlay.SetActive(false);
                view.stageImage.gameObject.SetActive(true);

                if (string.IsNullOrEmpty(plot.CropId))
                {
                    view.stageImage.sprite = null;
                    lastKnownStage[i] = 0;
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
                lastKnownStage[i] = stage;
            }

            if (seedPickerRoot.activeSelf)
            {
                RefreshSeedPickerAffordability();
            }
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Same command. Expected: PASS, all 4 new tests plus the full existing suite.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/EstatePanelController.cs Assets/Tests/PlayMode/EstatePanelControllerTests.cs
git commit -m "feat: live growth progress bar + stage-change pop animation"
```

---

### Task 3: Plant and water animations (seed-drop, water-droplet)

**Files:**
- Modify: `Assets/Scripts/UI/EstatePanelController.cs`
- Modify: `Assets/Tests/PlayMode/EstatePanelControllerTests.cs`
- Modify: `Assets/Editor/CoreLoopSceneBuilder.cs` (Initialize call-site update only -- real sprite loading comes in Task 6)

**Interfaces:**
- Consumes: nothing new from earlier tasks.
- Produces: `Initialize(...)` gains 2 new trailing `Sprite` parameters (`seedSprite, waterDropletSprite`) after the existing final parameter `shopRows` -- consumed by Task 6's scene wiring. Both null-safe everywhere.

- [ ] **Step 1: Write the failing tests**

In `Assets/Tests/PlayMode/EstatePanelControllerTests.cs`, update the `controller.Initialize(...)` call in `SetUp` (currently lines 133-137) to append 2 trailing `null` arguments:

```csharp
            controller.Initialize(estateButton, panelRootObject, closeButton, coinsLabel,
                plotViews, seedPickerRoot, seedButtons, seedCostLabels, new Sprite[9],
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton,
                viewHistoryButton, councilButton, eventsButton, customizeButton, gate,
                landTabButton, shopsTabButton, landTabRoot, shopsTabRoot, inventoryRows, shopRows,
                null, null);
```

Add this test after the `CheckStagePop_StageUnchanged_DoesNotSetLastPoppedIndex` test added in Task 2:

```csharp
        [Test]
        public void PlantAndWaterPlot_WithNoSeedOrDropletArtGenerated_StillUpdatesStateWithoutThrowing()
        {
            // seedSprite/waterDropletSprite are null in this shared SetUp
            // (matches a real project state before Assets/Art/Estate/
            // {seed,water_droplet}.png are generated) -- the new SeedDrop/
            // WaterDroplet coroutines must both no-op their visual and let
            // the underlying plant/water state changes through untouched.
            estateButton.onClick.Invoke();
            plotViews[0].tapButton.onClick.Invoke();

            Assert.DoesNotThrow(() => seedButtons[0].onClick.Invoke()); // plant wheat
            Assert.DoesNotThrow(() => plotViews[0].tapButton.onClick.Invoke()); // water it

            closeButton.onClick.Invoke();
            var saved = SaveService.LoadEstate();
            Assert.AreEqual("wheat", saved.Plots[0].CropId);
            Assert.AreNotEqual(0, saved.Plots[0].WateredAtUnixSeconds);
        }
```

In `Assets/Editor/CoreLoopSceneBuilder.cs`, update the `estateController.Initialize(...)` call (currently lines 597-602) to append 2 trailing `null` arguments (real sprites wired in Task 6):

```csharp
            estateController.Initialize(estateButton, estatePanelRootObject, estateCloseButton, estateCoinsLabel,
                estatePlotViews, estateSeedPickerObject, estateSeedButtons, estateSeedCostLabels, estateCropStageSprites,
                armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton, councilButton,
                eventsButton, customizeButton, duelModalGate,
                estateLandTabButton, estateShopsTabButton, estateLandTabRootObject, estateShopsTabRootObject,
                estateInventoryRows, estateShopRows,
                null, null); // seed/water-droplet sprites wired once art is generated -- see Task 6
```

- [ ] **Step 2: Run tests to verify they fail**

Same PlayMode command. Expected: FAIL with a compile error (`Initialize` doesn't accept 2 extra arguments yet).

- [ ] **Step 3: Implement**

In `Assets/Scripts/UI/EstatePanelController.cs`, add 2 new `[SerializeField]` fields after `[SerializeField] private ShopRowView[] shopRows;` (the last field from Phase 3):

```csharp
        [SerializeField] private Sprite seedSprite;
        [SerializeField] private Sprite waterDropletSprite;
```

Extend `Initialize(...)` (currently lines 87-141) with the 2 new trailing parameters and their assignments -- every other line stays exactly as it already is:

```csharp
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
            DuelModalGate gate,
            Button landTabButton,
            Button shopsTabButton,
            GameObject landTabRoot,
            GameObject shopsTabRoot,
            InventoryRowView[] inventoryRows,
            ShopRowView[] shopRows,
            Sprite seedSprite,
            Sprite waterDropletSprite)
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
            this.landTabButton = landTabButton;
            this.shopsTabButton = shopsTabButton;
            this.landTabRoot = landTabRoot;
            this.shopsTabRoot = shopsTabRoot;
            this.inventoryRows = inventoryRows;
            this.shopRows = shopRows;
            this.seedSprite = seedSprite;
            this.waterDropletSprite = waterDropletSprite;

            Bind();
        }
```

Add two new coroutines near `HarvestFly` (after it, currently ending at line 483):

```csharp
        // Spawns a temporary seed sprite over the plot that scales down to
        // nothing ("settling into the soil") before the existing sprout
        // ScaleBounce plays -- degrades gracefully (no visual, no error)
        // if seed art hasn't been generated yet, matching GetStageSprite's
        // own null-tolerant precedent.
        private IEnumerator SeedDrop(Transform slotTransform)
        {
            if (seedSprite == null)
            {
                yield break;
            }

            var seed = new GameObject("SeedDrop", typeof(Image));
            seed.transform.SetParent(slotTransform, false);
            var seedImage = seed.GetComponent<Image>();
            seedImage.sprite = seedSprite;
            seedImage.raycastTarget = false;
            var seedRect = seed.GetComponent<RectTransform>();
            seedRect.anchoredPosition = Vector2.zero;
            seedRect.sizeDelta = new Vector2(60f, 60f);

            const float duration = 0.25f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float scale = 1f - (t / duration);
                seed.transform.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            Destroy(seed);
        }

        // Same spawn-temp-object-then-destroy technique as HarvestFly/
        // SeedDrop -- falls from just above the plot onto it, fading out.
        private IEnumerator WaterDroplet(Transform slotTransform)
        {
            if (waterDropletSprite == null)
            {
                yield break;
            }

            var droplet = new GameObject("WaterDroplet", typeof(Image));
            droplet.transform.SetParent(slotTransform, false);
            var dropletImage = droplet.GetComponent<Image>();
            dropletImage.sprite = waterDropletSprite;
            dropletImage.raycastTarget = false;
            var dropletRect = droplet.GetComponent<RectTransform>();
            dropletRect.sizeDelta = new Vector2(40f, 40f);
            Vector2 start = new Vector2(0f, 60f);
            Vector2 end = Vector2.zero;

            const float duration = 0.35f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float progress = t / duration;
                dropletRect.anchoredPosition = Vector2.Lerp(start, end, progress);
                dropletImage.color = new Color(1f, 1f, 1f, 1f - progress);
                yield return null;
            }

            Destroy(droplet);
        }
```

In `OnSeedPicked` (currently lines 300-325), add the seed-drop call right before the existing `ScaleBounce` line:

```csharp
            int plantedPlotIndex = pendingPlantPlotIndex;
            seedPickerRoot.SetActive(false);
            pendingPlantPlotIndex = -1;
            RefreshPlots();
            StartCoroutine(SeedDrop(plotViews[plantedPlotIndex].stageImage.transform.parent));
            StartCoroutine(ScaleBounce(plotViews[plantedPlotIndex].stageImage.transform, 1.3f, 0.15f));
            SaveService.SaveEstate(state);
```

In `OnPlotTapped`'s water branch (currently lines 248-255), replace the `ColorFlash` call:

```csharp
            if (plot.WateredAtUnixSeconds == 0)
            {
                plot.WateredAtUnixSeconds = now;
                StartCoroutine(WaterDroplet(plotViews[plotIndex].stageImage.transform.parent));
                RefreshPlots();
                SaveService.SaveEstate(state);
                return;
            }
```

Leave the existing `ColorFlash` method itself in the file, untouched -- it becomes unused by this change but stays as a general-purpose helper (removing a small, still-correct, reusable coroutine isn't this task's job).

- [ ] **Step 4: Run tests to verify they pass**

Same command. Expected: PASS, the new test plus the full existing suite (including Task 2's new tests).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/EstatePanelController.cs Assets/Tests/PlayMode/EstatePanelControllerTests.cs Assets/Editor/CoreLoopSceneBuilder.cs
git commit -m "feat: add plant (seed-drop) and water (droplet) animations"
```

---

### Task 4: Fix `HarvestFly`'s destination (flies to inventory, not the stale coins label)

**Files:**
- Modify: `Assets/Scripts/UI/EstatePanelController.cs`

**Interfaces:**
- Consumes: `GoodsCatalog.IndexOf` (existing), `InventoryRowView` (existing, from Phase 3).
- Produces: `HarvestFly(Image sourceImage, Vector3 targetPosition)` -- signature changes from single-argument to explicit target, consumed only by `OnPlotTapped` in this same file.

- [ ] **Step 1: Write the failing test**

Add to `Assets/Tests/PlayMode/EstatePanelControllerTests.cs`, after the test added in Task 3:

```csharp
        [Test]
        public void HarvestMaturePlot_AddsToCorrectInventoryRowBeforeFlightStarts()
        {
            // The fix under test is HarvestFly's destination -- this can't be
            // observed by watching the animation itself (not meaningfully
            // testable, see this plan's Global Constraints), so it's pinned
            // via the state-side guarantee the fix's own call-site ordering
            // depends on: Inventory must already be incremented, and the
            // target row must already be showing, BEFORE HarvestFly starts,
            // for the fly-to-inventory-row target to resolve to a visible
            // destination at all.
            var seeded = new EstateState { Coins = 200 };
            seeded.Plots[0].CropId = "wheat";
            seeded.Plots[0].PlantedAtUnixSeconds = 1;
            seeded.Plots[0].WateredAtUnixSeconds = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3600;
            SaveService.SaveEstate(seeded);

            estateButton.onClick.Invoke();
            plotViews[0].tapButton.onClick.Invoke(); // harvest

            int wheatIndex = GoodsCatalog.IndexOf("wheat");
            Assert.IsTrue(inventoryRows[wheatIndex].root.activeSelf, "Inventory row must already be visible by the time HarvestFly's target is resolved.");
        }
```

- [ ] **Step 2: Run test to verify it fails or passes trivially**

Same PlayMode command. This specific assertion may already pass today (Phase 3's `RefreshInventoryAndShops()` already runs before `HarvestFly` is invoked) -- if so, this step confirms the pre-condition the fix depends on, and Step 3 proceeds regardless (the actual regression this task fixes is the *destination value* `HarvestFly` uses internally, not this pre-condition).

- [ ] **Step 3: Implement**

In `Assets/Scripts/UI/EstatePanelController.cs`, change `HarvestFly`'s signature (currently lines 454-483) to accept an explicit target instead of reading `coinsLabel` internally:

```csharp
        // Spawns a short-lived copy of the harvested plot's sprite that
        // arcs to targetPosition then is destroyed -- both live under the
        // same Canvas so a straight world-position lerp is a valid arc
        // target, no coroutine-owning-object lifetime issue since this
        // MonoBehaviour outlives the tween. targetPosition is resolved by
        // the caller (OnPlotTapped) before the plot's CropId is cleared,
        // since resolving it requires the still-known harvested crop id.
        private IEnumerator HarvestFly(Image sourceImage, Vector3 targetPosition)
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
            Vector3 end = targetPosition;
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
```

In `OnPlotTapped`'s harvest branch (currently lines 269-276), resolve the goods index once and pass the real target:

```csharp
            int goodsIndex = GoodsCatalog.IndexOf(plot.CropId);
            state.Inventory[goodsIndex] += 1;
            Vector3 harvestFlyTarget = inventoryRows[goodsIndex].root.transform.position;
            plot.CropId = null;
            plot.PlantedAtUnixSeconds = 0;
            plot.WateredAtUnixSeconds = 0;
            StartCoroutine(HarvestFly(plotViews[plotIndex].stageImage, harvestFlyTarget));
            RefreshPlots();
            RefreshInventoryAndShops();
            SaveService.SaveEstate(state);
```

- [ ] **Step 4: Run tests to verify they pass**

Same command. Expected: PASS, the new test plus the full existing suite -- in particular confirm no other call site of `HarvestFly` exists that would break from the signature change (there is exactly one, in `OnPlotTapped`).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/EstatePanelController.cs Assets/Tests/PlayMode/EstatePanelControllerTests.cs
git commit -m "fix: HarvestFly now flies to the correct inventory row, not the stale coins label"
```

---

### Task 5: Generate seed and water-droplet art

**Files:**
- Create: `Assets/Art/Estate/seed.png`, `Assets/Art/Estate/water_droplet.png` (if HF credits allow both; ship whichever completes if not)

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: the 2 files Task 6's scene wiring loads via `LoadIconSprite`. No code in this task -- purely an asset-generation task, same shape as every prior art-generation pass in this project (crop sprites, panel art, button icons).

- [ ] **Step 1: Generate the sprites**

Using the established Hugging Face pipeline (token in memory, `POST https://router.huggingface.co/nscale/v1/images/generations`, model `black-forest-labs/FLUX.1-schnell`, `size: "512x512"` -- matches the existing crop-sprite size precedent):

- `seed.png`: a single small seed/sprout icon, simple flat game-art style matching the existing crop sprites' visual tone, transparent background, no text.
- `water_droplet.png`: a single water droplet icon, same style/background constraints.

Test one cheap generation call first to confirm the session's credit budget isn't already exhausted (established practice from every prior art pass) before generating both. If only one lands this session, that's a complete, valid partial batch -- do not block on the second.

- [ ] **Step 2: Save and verify**

Save each successful generation to `Assets/Art/Estate/<name>.png` (create the `Assets/Art/Estate/` directory if it doesn't exist). Confirm the files are valid PNGs with a transparent background by opening them.

- [ ] **Step 3: Commit**

```bash
git add Assets/Art/Estate/
git commit -m "art: add seed and water-droplet sprites for crop animation"
```

(If only one file was generated, commit just that one -- the other stays absent until a follow-up art pass, exactly like `carrot_mature.png`/`pumpkin_*.png` from Phase 1.)

---

### Task 6: Final scene wiring (progress bars, real seed/droplet sprites, rebuild + full suite)

**Files:**
- Modify: `Assets/Editor/CoreLoopSceneBuilder.cs`
- Modify: `Assets/Scenes/CoreLoop.unity` (regenerated by `Build`, not hand-edited)

**Interfaces:**
- Consumes: `PlotView.progressBarImage` (Task 2), `Initialize(...)`'s 2 new trailing sprite params (Task 3), `Assets/Art/Estate/*.png` (Task 5, may be partial or absent).

- [ ] **Step 1: Add a progress-bar Image per plot tile**

In `Assets/Editor/CoreLoopSceneBuilder.cs`, inside the 8-plot loop (currently lines 350-412), add a progress-bar `Image` as a sibling of the existing `Stage`/`TapButton`/`LockedOverlay` children, positioned along the tile's bottom edge (tile is 160x160 local, centered; a bar at `y=-72` with `sizeDelta(140,10)` sits clear of the existing 120x120 centered stage sprite, which spans `y:[-60,60]`, by 7 units):

```csharp
                var progressBarObject = new GameObject("ProgressBar", typeof(Image));
                progressBarObject.transform.SetParent(slotBackgroundObject.transform, false);
                var progressBarRect = progressBarObject.GetComponent<RectTransform>();
                progressBarRect.anchoredPosition = new Vector2(0f, -72f);
                progressBarRect.sizeDelta = new Vector2(140f, 10f);
                var progressBarImage = progressBarObject.GetComponent<Image>();
                progressBarImage.type = Image.Type.Filled;
                progressBarImage.fillMethod = Image.FillMethod.Horizontal;
                progressBarImage.color = new Color(0.4f, 0.8f, 0.4f, 1f);
                progressBarObject.SetActive(false);
```

Add `progressBarImage = progressBarImage,` to the `estatePlotViews[i] = new EstatePanelController.PlotView { ... }` object initializer (currently lines 405-411).

- [ ] **Step 2: Load seed/water-droplet sprites, gracefully degrading if absent**

Add this alongside the existing crop-stage-sprite loading block (currently lines 537-565), after it:

```csharp
            // seed.png/water_droplet.png are a separate, later-generated art
            // pass (same established pattern as Assets/Art/Crops) -- guard
            // the directory the same way, to avoid a noisy per-file
            // LoadIconSprite error when the folder itself doesn't exist yet.
            Sprite estateSeedSprite = null;
            Sprite estateWaterDropletSprite = null;
            if (!System.IO.Directory.Exists("Assets/Art/Estate"))
            {
                Debug.LogWarning("CoreLoopSceneBuilder: Assets/Art/Estate does not exist yet -- seed/water-droplet animation sprites will be null until art is generated (see docs/superpowers/specs/2026-09-08-estate-crop-animation-design.md).");
            }
            else
            {
                if (System.IO.File.Exists("Assets/Art/Estate/seed.png"))
                {
                    estateSeedSprite = LoadIconSprite("Assets/Art/Estate/seed.png");
                }
                if (System.IO.File.Exists("Assets/Art/Estate/water_droplet.png"))
                {
                    estateWaterDropletSprite = LoadIconSprite("Assets/Art/Estate/water_droplet.png");
                }
            }
```

- [ ] **Step 3: Update the `Initialize(...)` call site**

Replace the `null, null` placeholders added in Task 3 (in the `estateController.Initialize(...)` call) with the real loaded sprites:

```csharp
            estateController.Initialize(estateButton, estatePanelRootObject, estateCloseButton, estateCoinsLabel,
                estatePlotViews, estateSeedPickerObject, estateSeedButtons, estateSeedCostLabels, estateCropStageSprites,
                armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton, councilButton,
                eventsButton, customizeButton, duelModalGate,
                estateLandTabButton, estateShopsTabButton, estateLandTabRootObject, estateShopsTabRootObject,
                estateInventoryRows, estateShopRows,
                estateSeedSprite, estateWaterDropletSprite);
```

- [ ] **Step 4: Rebuild and verify the scene**

```
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Build -logFile <path>.log
```
(no `-quit` -- `Build()` calls `EditorApplication.Exit()` on its own success path). Expected: `CoreLoopSceneBuilder: saved scene to Assets/Scenes/CoreLoop.unity`, plus the pre-existing crop-art warnings and, if Task 5 didn't complete both sprites, the new `Assets/Art/Estate` warning too -- both expected, not failures.

```
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Verify -logFile <path>.log
```
Expected: `CoreLoopSceneBuilder.Verify: scene opened and controller found successfully.`

- [ ] **Step 5: Run the full test suite once**

EditMode: `-testPlatform EditMode`. Expected: 134 tests pass, 0 failures -- 129 baseline (post-Phase-3) + 5 (Task 1, `GrowthProgress01`).
PlayMode: `-testPlatform PlayMode`. Expected: 111 tests pass, 0 failures -- 105 baseline + 4 (Task 2) + 1 (Task 3) + 1 (Task 4). Task 7 (not yet run at this point) adds the 112th.

- [ ] **Step 6: Commit**

```bash
git add Assets/Editor/CoreLoopSceneBuilder.cs Assets/Scenes/CoreLoop.unity
git commit -m "feat: wire progress bars and seed/water-droplet sprites into CoreLoop scene"
```

---

### Task 7: Scene-level regression test

**Files:**
- Modify: `Assets/Tests/PlayMode/CoreLoopSceneTests.cs`

**Interfaces:**
- Consumes: the built `CoreLoop.unity` scene (Task 6).

- [ ] **Step 1: Write the failing test**

Add to `Assets/Tests/PlayMode/CoreLoopSceneTests.cs`, following the existing `LoadedCoreLoopScene_...` naming and `FindButton`/`FindChildByName` helper pattern:

```csharp
        [UnityTest]
        public IEnumerator LoadedCoreLoopScene_EstatePlots_HaveProgressBarChild()
        {
            yield return SceneManager.LoadSceneAsync("CoreLoop");
            yield return null;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Canvas not found in the loaded CoreLoop scene.");

            Button estateButton = FindButton(canvas, "EstateButton");
            Assert.IsNotNull(estateButton, "EstateButton not found in the loaded CoreLoop scene.");
            estateButton.onClick.Invoke();

            for (int i = 0; i < 4; i++) // the 4 starting-unlocked plots
            {
                GameObject plot = FindChildByName(canvas.transform, $"Plot{i}");
                Assert.IsNotNull(plot, $"Plot{i} not found in the loaded CoreLoop scene.");

                GameObject progressBar = FindChildByName(plot.transform, "ProgressBar");
                Assert.IsNotNull(progressBar, $"Plot{i} is missing its ProgressBar child.");
            }
        }
```

- [ ] **Step 2: Run test to verify it fails**

Same PlayMode command as Task 6 (no `-quit`). Expected: FAIL -- `ProgressBar` not found (this test runs against the committed scene, so it fails until Task 6's `Build` step has actually run and been committed; if Task 6 is already done, this should instead PASS immediately -- if so, skip to Step 4).

- [ ] **Step 3: N/A**

No implementation step -- Task 6 already built the scene. This task only adds the regression test proving it stays correct on future scene rebuilds.

- [ ] **Step 4: Run test to verify it passes**

Same command. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Tests/PlayMode/CoreLoopSceneTests.cs
git commit -m "test: add scene-level regression test for Estate plot progress bars"
```

---

## Final Verification (after all 7 tasks)

Run the full suite once more:

```
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testResults <path>.xml -logFile <path>.log
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testResults <path>.xml -logFile <path>.log
```

Expected: 134 EditMode / 112 PlayMode, 100% pass. Then follow `understudy-kingdom:finishing-a-development-branch` to merge.

**Given this project's real history of final-review-only-caught bugs (Estate Phase 1's off-canvas button, Estate Phase 3's tab-button/plot overlap)**, the final whole-branch review for this plan should specifically hand-verify: (1) the new progress-bar `Image` per plot tile doesn't visually or spatially collide with the existing `TapButton` (which is full-tile, `anchorMin/Max 0/1`) -- confirm the progress bar sits *behind* or is excluded from the tap button's raycast in a way that doesn't block plot taps (the `TapButton` covers the whole 160x160 tile including the new bar's `y=-72` region -- since `TapButton` is still the last-added interactive sibling, it should still win raycasts, but this needs independent confirmation, not just assumption from a "the code compiles" check); (2) `Update()`'s per-frame work across 8 plots is cheap enough to not be a concern on a mobile device (8 plots x a handful of comparisons/field reads per frame, no allocations in the steady-state path -- worth a quick read-through, not a profiling pass); (3) the seed/water-droplet coroutines' temp `GameObject`s are reliably destroyed even if the panel is closed mid-animation (unlike `HarvestFly`, which was already accepted as "fine" under this exact condition -- confirm `SeedDrop`/`WaterDroplet` have the same acceptable behavior, not a leaked-GameObject regression).

## Explicitly Out of Scope for This Plan

- Shops-on-land, land expansion mechanic, animal care animations -- see `2026-09-08-estate-real-feel-vision.md`; each gets its own future spec.
- Per-crop-type seed/droplet art (wheat seed vs. carrot seed vs. pumpkin seed) -- generic assets only, this pass.
- Any change to `GrowthStage`, save format, growth timing, or crop values -- presentation layer only.
- Sound effects / haptics.
- Any APK rebuild/on-device verification -- follow the same build-and-visually-verify process already established for prior phases, as its own follow-up step once this plan's code is merged.
