# Homestead Extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up a brand-new standalone Unity project, "Homestead", at `C:\Users\rajes\Homestead`, containing only the Estate farming economy hand-carried and cleaned up from `understudy-kingdom`, verified building and testing green on its own.

**Architecture:** A fresh Unity 2D project (same version as `understudy-kingdom`, `6000.3.23f1`) with a trimmed package manifest, hand-carried Estate-only scripts/tests/art, a cleaned-up `EstatePanelController` (dropping every reference to `understudy-kingdom`'s ruler/advisor systems), and a brand-new `HomesteadSceneBuilder.cs` that builds Estate as a full-screen root rather than a modal panel opened by a button.

**Tech Stack:** Unity 6000.3.23f1, C#, NUnit EditMode/PlayMode tests, the same `EditorApplication.Exit()`-on-success `Build()`/`Verify()` batch-mode pattern already proven in `understudy-kingdom`.

## Global Constraints

- Every reference to `understudy-kingdom`'s ruler/advisor systems (`DuelModalGate`, `SetCoreLoopControlsInteractable`, `armySlider`/`tradeSlider`/`religionSlider`/`submitButton`/`challengeButton`/`viewHistoryButton`/`councilButton`/`eventsButton`/`customizeButton`) is dropped during the port, not carried forward disabled/unused.
- `understudy-kingdom` itself is never modified, deleted from, or pushed to during this work.
- Same Unity version as `understudy-kingdom`: `6000.3.23f1`, installed at `C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe`.
- `-runTests` batch-mode calls must NEVER be combined with `-quit`. `-executeMethod Build`/`Verify` calls must NOT include `-quit` (both call `EditorApplication.Exit()` on their own success/failure paths, ported forward from `understudy-kingdom`'s own hard-won lesson).
- No comments explaining WHAT code does -- only WHY, and only when genuinely non-obvious, matching `understudy-kingdom`'s established convention. Preserve existing WHY-comments from ported code verbatim; don't add new WHAT-comments.
- The new GitHub repo (`rajeshguntupalli59/Homestead`) is created but never pushed to during this plan, per the user's explicit "don't push unless asked" convention.
- Every new `.cs`/`.unity` file gets its Unity-auto-generated `.meta` companion committed alongside it (this repo's established convention, confirmed by the 57 already-tracked `.meta` files from Task 1's bootstrap) -- before committing, run `git status --short` and confirm no `??` `.meta` file is left behind uncommitted, even if a task's own `git add` line doesn't happen to list one explicitly.

---

### Task 1: Bootstrap the Homestead Unity project

**Files:**
- Create: `C:\Users\rajes\Homestead\` (new Unity project root)

**Interfaces:**
- Consumes: nothing from `understudy-kingdom` yet -- this task only stands up an empty, buildable project.
- Produces: a git-initialized Unity project with TMP Essentials, the Unity Test Framework package, and the folder structure every later task writes into.

- [ ] **Step 1: Create the empty Unity project via CLI**

```bash
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -createProject "C:\Users\rajes\Homestead" -quit -logFile "C:\Users\rajes\Homestead-create.log"
```

(`-quit` IS correct here, unlike every other batch-mode call in this plan -- `-createProject` is Unity's own built-in operation with no custom exit call, so it needs the flag to actually terminate.) Expected: the command exits 0, and `C:\Users\rajes\Homestead\Assets`, `C:\Users\rajes\Homestead\Packages`, `C:\Users\rajes\Homestead\ProjectSettings` all exist.

- [ ] **Step 2: Trim the package manifest**

Read `C:\Users\rajes\understudy-kingdom\Packages\manifest.json` (reference only, don't copy verbatim -- Estate doesn't use `com.unity.inputsystem` or `com.unity.addressables`, confirmed by grepping the Estate-owned source for any reference to either -- there are none). Overwrite `C:\Users\rajes\Homestead\Packages\manifest.json` with:

```json
{
  "dependencies": {
    "com.unity.textmeshpro": "3.0.9",
    "com.unity.test-framework": "1.4.5",
    "com.unity.modules.animation": "1.0.0",
    "com.unity.modules.audio": "1.0.0",
    "com.unity.modules.ui": "1.0.0"
  }
}
```

- [ ] **Step 3: Copy TMP Essential Resources**

TMP's Essential Resources (fonts/shaders/sprites needed for any `TextMeshProUGUI` to render at all) are already correctly imported in `understudy-kingdom` -- copying the folder wholesale is more reliable than scripting Unity's interactive "Import TMP Essentials" wizard in batch mode:

```bash
cp -r "C:\Users\rajes\understudy-kingdom\Assets\TextMesh Pro" "C:\Users\rajes\Homestead\Assets\TextMesh Pro"
```

- [ ] **Step 4: Create the folder structure**

```bash
mkdir -p "C:\Users\rajes\Homestead\Assets\Scripts\Core"
mkdir -p "C:\Users\rajes\Homestead\Assets\Scripts\UI"
mkdir -p "C:\Users\rajes\Homestead\Assets\Editor"
mkdir -p "C:\Users\rajes\Homestead\Assets\Art"
mkdir -p "C:\Users\rajes\Homestead\Assets\Tests\EditMode"
mkdir -p "C:\Users\rajes\Homestead\Assets\Tests\PlayMode"
mkdir -p "C:\Users\rajes\Homestead\Assets\Scenes"
```

- [ ] **Step 5: Copy the real, proven assembly definitions**

`understudy-kingdom` DOES use explicit `.asmdef` files (confirmed by reading the real files, not assumed) -- one for `Assets/Scripts/` (`UnderstudyKingdom.Runtime.asmdef`, referencing `UnityEngine.UI`/`Unity.TextMeshPro`) and one each for `Assets/Tests/EditMode/` and `Assets/Tests/PlayMode/`, both referencing `UnderstudyKingdom.Runtime` by name. Since this plan keeps the same `UnderstudyKingdom`/`UnderstudyKingdom.Tests` namespaces (Task 4 Step 0), copy these three files verbatim rather than writing new ones from scratch -- they have zero project-specific content beyond Unity's own by-name assembly references, which resolve identically in any project using the same namespace:

```bash
cp "C:\Users\rajes\understudy-kingdom\Assets\Scripts\UnderstudyKingdom.Runtime.asmdef" "C:\Users\rajes\Homestead\Assets\Scripts\UnderstudyKingdom.Runtime.asmdef"
cp "C:\Users\rajes\understudy-kingdom\Assets\Tests\EditMode\UnderstudyKingdom.EditModeTests.asmdef" "C:\Users\rajes\Homestead\Assets\Tests\EditMode\UnderstudyKingdom.EditModeTests.asmdef"
cp "C:\Users\rajes\understudy-kingdom\Assets\Tests\PlayMode\UnderstudyKingdom.PlayModeTests.asmdef" "C:\Users\rajes\Homestead\Assets\Tests\PlayMode\UnderstudyKingdom.PlayModeTests.asmdef"
```

(No `.asmdef` for `Assets/Editor/` -- `understudy-kingdom` doesn't have one either, confirmed by the same check; Editor scripts there compile against the implicit default Editor assembly, which already sees `UnderstudyKingdom.Runtime` since it's `autoReferenced: true`.)

- [ ] **Step 6: Verify the empty project opens and compiles cleanly**

```bash
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\Homestead" -quit -logFile "C:\Users\rajes\Homestead-verify-empty.log"
```

Expected: exits 0, no compile errors in the log (grep the log for "error CS" -- should be zero matches).

- [ ] **Step 7: git init and first commit**

```bash
cd "C:\Users\rajes\Homestead"
git init
```

Create `.gitignore` matching `understudy-kingdom`'s (read `C:\Users\rajes\understudy-kingdom\.gitignore` and copy it verbatim -- it's a generic Unity `.gitignore`, not project-specific).

```bash
git add -A
git commit -m "$(cat <<'EOF'
chore: bootstrap empty Homestead Unity project

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01QMEErAfieqNAbk8ZRAFBwT
EOF
)"
```

---

### Task 2: Port the pure Estate scripts and their EditMode tests

**Files:**
- Create: `Assets/Scripts/Core/CropCatalog.cs`, `ShopCatalog.cs`, `ProductCatalog.cs`, `GoodsCatalog.cs`, `EstateState.cs`, `EstateSaveData.cs` (all under `C:\Users\rajes\Homestead\`)
- Create: `Assets/Tests/EditMode/ProductCatalogTests.cs`, `ShopCatalogTests.cs`, `GoodsCatalogTests.cs`, `EstateStateTests.cs` (all under `C:\Users\rajes\Homestead\`)

**Interfaces:**
- Consumes: nothing new -- these files have zero dependency on any ruler/advisor system already (confirmed: none of them `using` anything outside `System`/`UnityEngine`/each other).
- Produces: `EstateState`, `CropCatalog`, `ShopCatalog`, `ProductCatalog`, `GoodsCatalog`, `EstateSaveData` -- consumed by Task 3 (`SaveService`) and Task 4 (`EstatePanelController`).

- [ ] **Step 1: Copy the 6 source files verbatim**

```bash
cp "C:\Users\rajes\understudy-kingdom\Assets\Scripts\Core\CropCatalog.cs" "C:\Users\rajes\Homestead\Assets\Scripts\Core\CropCatalog.cs"
cp "C:\Users\rajes\understudy-kingdom\Assets\Scripts\Core\ShopCatalog.cs" "C:\Users\rajes\Homestead\Assets\Scripts\Core\ShopCatalog.cs"
cp "C:\Users\rajes\understudy-kingdom\Assets\Scripts\Core\ProductCatalog.cs" "C:\Users\rajes\Homestead\Assets\Scripts\Core\ProductCatalog.cs"
cp "C:\Users\rajes\understudy-kingdom\Assets\Scripts\Core\GoodsCatalog.cs" "C:\Users\rajes\Homestead\Assets\Scripts\Core\GoodsCatalog.cs"
cp "C:\Users\rajes\understudy-kingdom\Assets\Scripts\Core\EstateState.cs" "C:\Users\rajes\Homestead\Assets\Scripts\Core\EstateState.cs"
cp "C:\Users\rajes\understudy-kingdom\Assets\Scripts\Core\EstateSaveData.cs" "C:\Users\rajes\Homestead\Assets\Scripts\Core\EstateSaveData.cs"
```

No content changes -- these files are already fully self-contained within `UnderstudyKingdom.Core`. **Do not rename the namespace in this task** -- Task 4 needs to decide the namespace question once, for the whole codebase, not file-by-file (see Task 4 Step 0).

- [ ] **Step 2: Copy the 4 corresponding EditMode test files verbatim**

```bash
cp "C:\Users\rajes\understudy-kingdom\Assets\Tests\EditMode\ProductCatalogTests.cs" "C:\Users\rajes\Homestead\Assets\Tests\EditMode\ProductCatalogTests.cs"
cp "C:\Users\rajes\understudy-kingdom\Assets\Tests\EditMode\ShopCatalogTests.cs" "C:\Users\rajes\Homestead\Assets\Tests\EditMode\ShopCatalogTests.cs"
cp "C:\Users\rajes\understudy-kingdom\Assets\Tests\EditMode\GoodsCatalogTests.cs" "C:\Users\rajes\Homestead\Assets\Tests\EditMode\GoodsCatalogTests.cs"
cp "C:\Users\rajes\understudy-kingdom\Assets\Tests\EditMode\EstateStateTests.cs" "C:\Users\rajes\Homestead\Assets\Tests\EditMode\EstateStateTests.cs"
```

- [ ] **Step 3: Run the EditMode suite**

```bash
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\Homestead" -runTests -testPlatform EditMode -testResults "C:\Users\rajes\Homestead-task2-editmode-results.xml" -logFile "C:\Users\rajes\Homestead-task2-editmode.log"
```

Expected: all tests from the 4 copied test files pass, 0 failures. Read the actual XML `testcasecount`/`passed`/`failed` attributes -- this is the first real signal the ported code compiles and behaves identically in a fresh project.

- [ ] **Step 4: Commit**

```bash
cd "C:\Users\rajes\Homestead"
git add Assets/Scripts/Core/CropCatalog.cs Assets/Scripts/Core/CropCatalog.cs.meta Assets/Scripts/Core/ShopCatalog.cs Assets/Scripts/Core/ShopCatalog.cs.meta Assets/Scripts/Core/ProductCatalog.cs Assets/Scripts/Core/ProductCatalog.cs.meta Assets/Scripts/Core/GoodsCatalog.cs Assets/Scripts/Core/GoodsCatalog.cs.meta Assets/Scripts/Core/EstateState.cs Assets/Scripts/Core/EstateState.cs.meta Assets/Scripts/Core/EstateSaveData.cs Assets/Scripts/Core/EstateSaveData.cs.meta Assets/Tests/EditMode/ProductCatalogTests.cs Assets/Tests/EditMode/ProductCatalogTests.cs.meta Assets/Tests/EditMode/ShopCatalogTests.cs Assets/Tests/EditMode/ShopCatalogTests.cs.meta Assets/Tests/EditMode/GoodsCatalogTests.cs Assets/Tests/EditMode/GoodsCatalogTests.cs.meta Assets/Tests/EditMode/EstateStateTests.cs Assets/Tests/EditMode/EstateStateTests.cs.meta
git commit -m "$(cat <<'EOF'
feat: port pure Estate catalog/state scripts from understudy-kingdom

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01QMEErAfieqNAbk8ZRAFBwT
EOF
)"
```

---

### Task 3: New, Estate-only SaveService

**Files:**
- Create: `Assets/Scripts/Core/SaveService.cs` (under `C:\Users\rajes\Homestead\`)
- Create: `Assets/Tests/EditMode/SaveServiceEstateTests.cs` (under `C:\Users\rajes\Homestead\`)

**Interfaces:**
- Consumes: `EstateState`, `EstateSaveData`, `GoodsCatalog`, `ShopCatalog` (Task 2).
- Produces: `SaveService.SaveEstate(EstateState)`, `SaveService.LoadEstate() -> EstateState`, `SaveService.EstateSavePath` -- consumed by Task 4 (`EstatePanelController`).

- [ ] **Step 1: Read the real current `understudy-kingdom` `SaveService.cs`**

Read `C:\Users\rajes\understudy-kingdom\Assets\Scripts\Core\SaveService.cs` in full to get its exact current `SaveEstate`/`LoadEstate`/`EstateSavePath` implementation and the per-field `Inventory`/`Shops` migration logic verbatim -- don't guess at it, the real file has the exact working code including the Phase 3 per-field migration fix.

- [ ] **Step 2: Write the failing test**

Copy `C:\Users\rajes\understudy-kingdom\Assets\Tests\EditMode\SaveServiceEstateTests.cs` verbatim to `C:\Users\rajes\Homestead\Assets\Tests\EditMode\SaveServiceEstateTests.cs` -- it only exercises `SaveEstate`/`LoadEstate`, no `RulerState` dependency, so it ports unchanged.

- [ ] **Step 3: Run to verify it fails**

```bash
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\Homestead" -runTests -testPlatform EditMode -testResults "C:\Users\rajes\Homestead-task3-red-results.xml" -logFile "C:\Users\rajes\Homestead-task3-red.log"
```

Expected: FAIL with a compile error (`SaveService` doesn't exist yet).

- [ ] **Step 4: Write `SaveService.cs`**

Create `C:\Users\rajes\Homestead\Assets\Scripts\Core\SaveService.cs` containing ONLY the real `SaveEstate`/`LoadEstate`/`EstateSavePath` members copied verbatim from `understudy-kingdom`'s `SaveService.cs` (found in Step 1) -- same namespace (`UnderstudyKingdom.Core`, see Task 4 Step 0 for why the namespace itself isn't renamed in this plan), same `using` directives it actually needs (`System.IO`, `UnityEngine` for `Application.persistentDataPath`), same class name `SaveService`. Do NOT include any `RulerState`/`RulerSaveData`/`Save`/`Load` (non-Estate) members -- this is the one substantive content change from the source file, everything else is a verbatim copy of the Estate-related code.

- [ ] **Step 5: Run to verify it passes**

Same command as Step 3. Expected: all `SaveServiceEstateTests` pass, 0 failures.

- [ ] **Step 6: Commit**

```bash
cd "C:\Users\rajes\Homestead"
git add Assets/Scripts/Core/SaveService.cs Assets/Scripts/Core/SaveService.cs.meta Assets/Tests/EditMode/SaveServiceEstateTests.cs Assets/Tests/EditMode/SaveServiceEstateTests.cs.meta
git commit -m "$(cat <<'EOF'
feat: add Estate-only SaveService (no RulerState save path)

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01QMEErAfieqNAbk8ZRAFBwT
EOF
)"
```

---

### Task 4: Port and clean up EstatePanelController

**Files:**
- Create: `Assets/Scripts/UI/EstatePanelController.cs` (under `C:\Users\rajes\Homestead\`)
- Create: `Assets/Tests/PlayMode/EstatePanelControllerTests.cs` (under `C:\Users\rajes\Homestead\`)

**Interfaces:**
- Consumes: `EstateState`, `SaveService`, `CropCatalog`, `GoodsCatalog`, `ShopCatalog` (Tasks 2-3).
- Produces: `EstatePanelController` with a trimmed `Initialize(...)` signature (drops `estateButton`, `closeButton`, `armySlider`, `tradeSlider`, `religionSlider`, `submitButton`, `challengeButton`, `viewHistoryButton`, `councilButton`, `eventsButton`, `customizeButton`, `gate` -- 12 parameters removed) -- consumed by Task 6 (`HomesteadSceneBuilder`).

**Step 0 (read first, no file changes): namespace decision.** Keep the `UnderstudyKingdom.Core`/`UnderstudyKingdom.UI` namespaces exactly as they are in every file ported so far and in this task -- renaming to `Homestead.Core`/`Homestead.UI` is pure churn with real risk (every `using` statement, every fully-qualified reference in test files, every namespace declaration would need a matching, error-prone edit) for zero functional benefit. This is a private, single-project codebase; the namespace name carries no meaning outside it.

- [ ] **Step 1: Copy the source file verbatim, then apply exact deletions**

```bash
cp "C:\Users\rajes\understudy-kingdom\Assets\Scripts\UI\EstatePanelController.cs" "C:\Users\rajes\Homestead\Assets\Scripts\UI\EstatePanelController.cs"
```

In the new `C:\Users\rajes\Homestead\Assets\Scripts\UI\EstatePanelController.cs`, make these exact changes (every other line in the 792-line file stays untouched -- `Update()`, `CheckStagePop`, `OnPlotTapped`, `OnSeedPicked`, `RefreshPlots`, `OnSellStack`, `OnShopActionTapped`, `RefreshInventoryAndShops`, `SeedDrop`, `WaterDroplet`, `HarvestFly`, `ScaleBounce`, `ColorFlash`, `SetActiveTab`, `GetStageSprite`, `GoodsIdForIndex` are all 100% unchanged):

**Field removals** (delete these 10 lines from the `[SerializeField]` block):
```csharp
        [SerializeField] private Button estateButton;
```
```csharp
        [SerializeField] private Button closeButton;
```
```csharp
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
```

**`Initialize(...)` signature and body** -- replace the entire method (currently the full method shown below on the left is what's in the file; replace it with the trimmed version on the right):

Old signature (delete):
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

New signature (replace with this):
```csharp
        public void Initialize(
            GameObject panelRoot,
            TextMeshProUGUI coinsLabel,
            PlotView[] plotViews,
            GameObject seedPickerRoot,
            Button[] seedButtons,
            TextMeshProUGUI[] seedCostLabels,
            Sprite[] cropStageSprites,
            Button landTabButton,
            Button shopsTabButton,
            GameObject landTabRoot,
            GameObject shopsTabRoot,
            InventoryRowView[] inventoryRows,
            ShopRowView[] shopRows,
            Sprite seedSprite,
            Sprite waterDropletSprite)
        {
            this.panelRoot = panelRoot;
            this.coinsLabel = coinsLabel;
            this.plotViews = plotViews;
            this.seedPickerRoot = seedPickerRoot;
            this.seedButtons = seedButtons;
            this.seedCostLabels = seedCostLabels;
            this.cropStageSprites = cropStageSprites;
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

**`Bind()`** -- delete these 4 lines from the start of the method:
```csharp
            estateButton.onClick.RemoveAllListeners();
            estateButton.onClick.AddListener(OnEstateButtonClicked);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnClose);
```

At the END of `Bind()`, change:
```csharp
            panelRoot.SetActive(false);
            seedPickerRoot.SetActive(false);
```
to just:
```csharp
            seedPickerRoot.SetActive(false);
```
(`panelRoot` stays active by default -- there's no "closed" state anymore, Estate is always the visible content.)

**`Start()`** -- replace the entire method:
```csharp
        private void Start()
        {
            Bind();
        }
```
with:
```csharp
        private void Start()
        {
            Bind();
            state = SaveService.LoadEstate();
            SetActiveTab(true);
            RefreshPlots();
            RefreshInventoryAndShops();
        }
```
(This is the load-and-refresh half of the old `OnEstateButtonClicked` -- Estate initializes itself once at scene load instead of waiting for a button tap.)

**Delete `OnEstateButtonClicked` and `OnClose` entirely** (both full methods):
```csharp
        private void OnEstateButtonClicked()
        {
            gate.IsModalOpen = true;
            SetCoreLoopControlsInteractable(false);
            state = SaveService.LoadEstate();
            panelRoot.SetActive(true);
            SetActiveTab(true);
            RefreshPlots();
            RefreshInventoryAndShops();
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
```
(The `SaveService.SaveEstate(state)` call in the old `OnClose` is not replaced with anything -- every state-mutating method in this file, `OnPlotTapped`/`OnSeedPicked`/`OnSellStack`/`OnShopActionTapped`, already calls `SaveService.SaveEstate(state)` itself on every single mutation, per this project's established per-mutation-save convention. There is no unsaved-state window this deletion introduces.)

**Delete `SetCoreLoopControlsInteractable` entirely** (its whole body only ever touched fields this task already removed):
```csharp
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
            // DuelModalGate's own duel-in-flight lock) -- see
            // docs/superpowers/specs/2026-09-03-duel-modal-gate-design.md.
            if (interactable && gate.IsDuelInFlight)
            {
                return;
            }
            challengeButton.interactable = interactable;
        }
```

Confirm no other reference to `estateButton`, `closeButton`, `gate`, `armySlider`, `tradeSlider`, `religionSlider`, `submitButton`, `challengeButton`, `viewHistoryButton`, `councilButton`, `eventsButton`, `customizeButton`, or `SetCoreLoopControlsInteractable` remains anywhere in the file (`grep -n` for each name after editing -- should return zero matches).

- [ ] **Step 2: Port and adjust the test file**

```bash
cp "C:\Users\rajes\understudy-kingdom\Assets\Tests\PlayMode\EstatePanelControllerTests.cs" "C:\Users\rajes\Homestead\Assets\Tests\PlayMode\EstatePanelControllerTests.cs"
```

In the new file, adjust `SetUp` to match the trimmed `Initialize(...)` signature and the new always-active-panel/no-open-button lifecycle:

- Delete the `estateButton = CreateButton("EstateButton");` line and the `closeButton = CreateButton("CloseButton");` line, and every corresponding private field declaration (`estateButton`, `closeButton`) at the top of the class.
- Delete `armySlider`/`tradeSlider`/`religionSlider`/`submitButton`/`challengeButton`/`viewHistoryButton`/`councilButton`/`eventsButton`/`customizeButton`/`gateObject`/`gate` field declarations and their `SetUp` construction lines (the `CreateSlider(...)`/`CreateButton(...)` calls for each, and the `gateObject = new GameObject("DuelModalGate"); gate = gateObject.AddComponent<DuelModalGate>();` lines). Delete the `DuelModalGate` reference in `TearDown` (`Object.DestroyImmediate(gateObject);`) too.
- Update the `controller.Initialize(...)` call in `SetUp` to match the new 15-parameter signature (drop the corresponding 12 removed arguments, keep the rest in the same relative order: `panelRootObject, coinsLabel, plotViews, seedPickerRoot, seedButtons, seedCostLabels, new Sprite[9], landTabButton, shopsTabButton, landTabRoot, shopsTabRoot, inventoryRows, shopRows, null, null`).
- **Every test that currently calls `estateButton.onClick.Invoke();` before interacting with the panel** must delete that line -- the panel is now active from `Start()`, no tap needed to open it. Grep the test file for `estateButton.onClick.Invoke()` and delete each occurrence (there is no replacement call needed -- `SetUp` already triggers `Start()`'s equivalent init logic via the `Initialize(...)` call, which now itself loads state and refreshes, matching the old post-open state).
- **Delete every test whose entire purpose was verifying open/close/disable behavior that no longer exists**: any test asserting `panelRootObject.activeSelf` toggling, `armySlider.interactable`/`submitButton.interactable`/etc. being disabled while "open", or close-button behavior (e.g. a test like `EstateButton_OnFirstOpen_ShowsPanelWithStartingCoinsAndPlots`, `EstateButton_OnOpen_DisablesSharedControls`, `Close_ReEnablesSharedControlsAndHidesPanel` -- read the real current file to find the exact names and confirm each one's actual assertions reference only the now-removed open/close/shared-controls behavior before deleting; keep any test whose assertions are about Estate's own state, e.g. locked-plot-overlay-on-load, even if its name mentions "OnOpen").

- [ ] **Step 3: Run the PlayMode suite**

```bash
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\Homestead" -runTests -testPlatform PlayMode -testResults "C:\Users\rajes\Homestead-task4-playmode-results.xml" -logFile "C:\Users\rajes\Homestead-task4-playmode.log"
```

Expected: all remaining tests pass, 0 failures. Report the real before/after test count (how many tests existed in the source file vs. how many remain after deleting the open/close/shared-control tests) -- don't guess this number in advance, count what's actually there.

- [ ] **Step 4: Commit**

```bash
cd "C:\Users\rajes\Homestead"
git add Assets/Scripts/UI/EstatePanelController.cs Assets/Scripts/UI/EstatePanelController.cs.meta Assets/Tests/PlayMode/EstatePanelControllerTests.cs Assets/Tests/PlayMode/EstatePanelControllerTests.cs.meta
git commit -m "$(cat <<'EOF'
feat: port EstatePanelController, dropping ruler/advisor coupling

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01QMEErAfieqNAbk8ZRAFBwT
EOF
)"
```

---

### Task 5: Port art assets

**Files:**
- Create: `Assets/Art/Crops/*` (under `C:\Users\rajes\Homestead\`)
- Create: `Assets/Art/Estate/*` (under `C:\Users\rajes\Homestead\`)

**Interfaces:**
- Consumes: nothing.
- Produces: the PNG (+ `.meta`) files Task 6's scene builder loads.

- [ ] **Step 1: Copy both art folders wholesale, including `.meta` files**

```bash
cp -r "C:\Users\rajes\understudy-kingdom\Assets\Art\Crops" "C:\Users\rajes\Homestead\Assets\Art\Crops"
cp -r "C:\Users\rajes\understudy-kingdom\Assets\Art\Estate" "C:\Users\rajes\Homestead\Assets\Art\Estate"
cp "C:\Users\rajes\understudy-kingdom\Assets\Art\Crops.meta" "C:\Users\rajes\Homestead\Assets\Art\Crops.meta"
cp "C:\Users\rajes\understudy-kingdom\Assets\Art\Estate.meta" "C:\Users\rajes\Homestead\Assets\Art\Estate.meta"
```

Copying the `.meta` files along with the PNGs preserves their existing GUIDs -- this matters less here than it did porting between commits of the *same* project (nothing in Homestead references these GUIDs yet), but it's free and avoids Unity re-generating fresh ones on first import for no reason. Note the two extra `cp` lines: `cp -r SourceDir DestDir` copies a folder's *contents* (including each file's own `.meta` sitting inside it) but NOT the source folder's own sibling `.meta` file (e.g. `Assets/Art/Crops.meta`, which describes the `Crops` folder itself and lives next to it, not inside it) -- that needs its own explicit copy or Unity will auto-generate a fresh one on next import, leaving it untracked until caught by `git status`.

- [ ] **Step 2: Verify the files landed correctly**

```bash
ls "C:\Users\rajes\Homestead\Assets\Art\Crops\"
ls "C:\Users\rajes\Homestead\Assets\Art\Estate\"
```

Expected: `wheat_sprout.png`, `wheat_growing.png`, `wheat_mature.png`, `carrot_sprout.png`, `carrot_growing.png` (5 crop PNGs -- `carrot_mature.png` and all 3 `pumpkin_*.png` remain the known, already-accepted gap from `understudy-kingdom`, not something this task fixes), and `seed.png`, `water_droplet.png` in Estate.

- [ ] **Step 3: Commit**

```bash
cd "C:\Users\rajes\Homestead"
git add Assets/Art/Crops Assets/Art/Estate Assets/Art/Crops.meta Assets/Art/Estate.meta
git commit -m "$(cat <<'EOF'
art: port Estate and Crops art assets from understudy-kingdom

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01QMEErAfieqNAbk8ZRAFBwT
EOF
)"
```

---

### Task 6: New HomesteadSceneBuilder.cs

**Files:**
- Create: `Assets/Editor/HomesteadSceneBuilder.cs` (under `C:\Users\rajes\Homestead\`)

**Interfaces:**
- Consumes: `EstatePanelController`'s new 15-parameter `Initialize(...)` (Task 4), `GoodsCatalog.Count`, `ShopCatalog.All.Length` (Task 2).
- Produces: `Assets/Scenes/Homestead.unity`, `HomesteadSceneBuilder.Build()`/`.Verify()`.

- [ ] **Step 1: Write the new scene builder**

This reuses `understudy-kingdom`'s `CoreLoopSceneBuilder.cs` Estate-building block (the `estatePanelRootObject` through `estateController.Initialize(...)` section) almost verbatim -- none of that block ever referenced ruler/advisor systems at the GameObject-construction level, only at the final `Initialize(...)` call site. The real changes: no `EstateButton` (nothing opens Estate, it's the whole game), `estatePanelRootObject` is full-screen sized instead of a 700x800 modal, and the `Initialize(...)` call passes the trimmed 15-argument list.

Create `C:\Users\rajes\Homestead\Assets\Editor\HomesteadSceneBuilder.cs`:

```csharp
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/Homestead.unity programmatically -- same
    /// reproducible-scene-via-script approach as understudy-kingdom's
    /// CoreLoopSceneBuilder, which this Estate-building block is ported
    /// from almost verbatim (it never depended on the ruler/advisor
    /// systems at the GameObject-construction level, only at the old
    /// Initialize(...) call site). See
    /// docs/superpowers/specs/2026-09-08-homestead-extraction-design.md.
    /// </summary>
    public static class HomesteadSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Homestead.unity";

        [MenuItem("Homestead/Build Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";

            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(800f, 1600f);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 1f;

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // Full-screen root -- Estate is the whole game now, not a
            // 700x800 modal panel opened by a button. sizeDelta(800,1600)
            // matches the CanvasScaler's own referenceResolution exactly,
            // so this root always exactly covers the canvas regardless of
            // device aspect (same fixed-height-pinned-by-matchWidthOrHeight
            // behavior CoreLoopSceneBuilder's canvas already relied on).
            var estatePanelRootObject = new GameObject("EstateRoot", typeof(Image));
            estatePanelRootObject.transform.SetParent(canvasObject.transform, false);
            var estatePanelRect = estatePanelRootObject.GetComponent<RectTransform>();
            estatePanelRect.anchoredPosition = Vector2.zero;
            estatePanelRect.sizeDelta = new Vector2(800f, 1600f);
            estatePanelRootObject.GetComponent<Image>().color = new Color(0.1f, 0.15f, 0.1f, 1f);

            TextMeshProUGUI estateTitleLabel = CreateLabel(estatePanelRootObject.transform, "Title", 0f, "Homestead");
            estateTitleLabel.fontSize = 28f;
            estateTitleLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 740f);

            TextMeshProUGUI estateCoinsLabel = CreateLabel(estatePanelRootObject.transform, "CoinsLabel", 0f, "Coins: 200");
            estateCoinsLabel.fontSize = 24f;
            estateCoinsLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 695f);

            var estateLandTabRootObject = new GameObject("LandTabRoot");
            estateLandTabRootObject.transform.SetParent(estatePanelRootObject.transform, false);

            var estatePlotViews = new EstatePanelController.PlotView[8];
            for (int i = 0; i < 8; i++)
            {
                int column = i % 4;
                int row = i / 4;
                float plotX = -255f + column * 170f;
                float plotY = 550f - row * 220f;

                var slotBackgroundObject = new GameObject($"Plot{i}", typeof(Image));
                slotBackgroundObject.transform.SetParent(estateLandTabRootObject.transform, false);
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

                var progressBarObject = new GameObject("ProgressBar", typeof(Image));
                progressBarObject.transform.SetParent(slotBackgroundObject.transform, false);
                var progressBarRect = progressBarObject.GetComponent<RectTransform>();
                progressBarRect.anchoredPosition = new Vector2(0f, -72f);
                progressBarRect.sizeDelta = new Vector2(140f, 10f);
                var progressBarImage = progressBarObject.GetComponent<Image>();
                progressBarImage.type = Image.Type.Filled;
                progressBarImage.fillMethod = Image.FillMethod.Horizontal;
                // Image.Type.Filled only renders fillAmount when it has a
                // sprite -- a null sprite falls through to a plain
                // full-rect quad regardless of fillAmount (real bug this
                // project already hit once, in understudy-kingdom's Estate
                // Crop Animation final review -- not repeating it here).
                progressBarImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                progressBarImage.color = new Color(0.4f, 0.8f, 0.4f, 1f);
                progressBarImage.raycastTarget = false;
                progressBarObject.SetActive(false);

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

                estatePlotViews[i] = new EstatePanelController.PlotView
                {
                    stageImage = stageImage,
                    tapButton = tapButton,
                    lockedOverlay = lockedOverlayObject,
                    lockCostLabel = lockCostLabel,
                    progressBarImage = progressBarImage
                };
            }

            var estateSeedPickerObject = new GameObject("SeedPicker", typeof(Image));
            estateSeedPickerObject.transform.SetParent(estateLandTabRootObject.transform, false);
            var estateSeedPickerRect = estateSeedPickerObject.GetComponent<RectTransform>();
            estateSeedPickerRect.anchoredPosition = new Vector2(0f, 120f);
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

            var estateShopsTabRootObject = new GameObject("ShopsTabRoot");
            estateShopsTabRootObject.transform.SetParent(estatePanelRootObject.transform, false);
            estateShopsTabRootObject.SetActive(false);

            var estateShopRows = new EstatePanelController.ShopRowView[3];
            string[] estateShopIds = { "bakery", "kitchen", "pie_house" };
            for (int i = 0; i < 3; i++)
            {
                float rowY = 540f - i * 120f;

                var rowBackgroundObject = new GameObject($"ShopRow_{estateShopIds[i]}", typeof(Image));
                rowBackgroundObject.transform.SetParent(estateShopsTabRootObject.transform, false);
                var rowBackgroundRect = rowBackgroundObject.GetComponent<RectTransform>();
                rowBackgroundRect.anchoredPosition = new Vector2(0f, rowY);
                rowBackgroundRect.sizeDelta = new Vector2(640f, 100f);
                rowBackgroundObject.GetComponent<Image>().color = new Color(0.15f, 0.2f, 0.15f, 1f);

                TextMeshProUGUI statusLabel = CreateLabel(rowBackgroundObject.transform, "StatusLabel", 0f, string.Empty);
                statusLabel.fontSize = 20f;
                var statusLabelRect = statusLabel.GetComponent<RectTransform>();
                statusLabelRect.anchoredPosition = new Vector2(-80f, 15f);
                statusLabelRect.sizeDelta = new Vector2(460f, 50f);

                var actionButtonObject = new GameObject("ActionButton", typeof(Image), typeof(Button));
                actionButtonObject.transform.SetParent(rowBackgroundObject.transform, false);
                var actionButtonRect = actionButtonObject.GetComponent<RectTransform>();
                actionButtonRect.anchoredPosition = new Vector2(250f, -15f);
                actionButtonRect.sizeDelta = new Vector2(120f, 44f);
                actionButtonObject.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.7f, 1f);
                var actionButton = actionButtonObject.GetComponent<Button>();
                TextMeshProUGUI actionButtonLabel = CreateLabel(actionButtonObject.transform, "Text", 0f, string.Empty);
                var actionButtonLabelRect = actionButtonLabel.GetComponent<RectTransform>();
                actionButtonLabelRect.anchorMin = Vector2.zero;
                actionButtonLabelRect.anchorMax = Vector2.one;
                actionButtonLabelRect.sizeDelta = Vector2.zero;
                actionButtonLabelRect.anchoredPosition = Vector2.zero;

                estateShopRows[i] = new EstatePanelController.ShopRowView
                {
                    statusLabel = statusLabel,
                    actionButton = actionButton,
                    actionButtonLabel = actionButtonLabel
                };
            }

            var estateInventoryRows = new EstatePanelController.InventoryRowView[GoodsCatalog.Count];
            for (int i = 0; i < estateInventoryRows.Length; i++)
            {
                float rowX = -250f + i * 100f;

                var invRowObject = new GameObject($"InventoryRow{i}", typeof(Image));
                invRowObject.transform.SetParent(estatePanelRootObject.transform, false);
                var invRowRect = invRowObject.GetComponent<RectTransform>();
                invRowRect.anchoredPosition = new Vector2(rowX, 40f);
                invRowRect.sizeDelta = new Vector2(110f, 50f);
                invRowObject.GetComponent<Image>().color = new Color(0.2f, 0.25f, 0.15f, 1f);
                invRowObject.SetActive(false);

                TextMeshProUGUI invLabel = CreateLabel(invRowObject.transform, "Label", 0f, string.Empty);
                invLabel.fontSize = 14f;
                var invLabelRect = invLabel.GetComponent<RectTransform>();
                invLabelRect.anchoredPosition = new Vector2(0f, 10f);
                invLabelRect.sizeDelta = new Vector2(105f, 24f);

                var sellButtonObject = new GameObject("SellButton", typeof(Image), typeof(Button));
                sellButtonObject.transform.SetParent(invRowObject.transform, false);
                var sellButtonRect = sellButtonObject.GetComponent<RectTransform>();
                sellButtonRect.anchoredPosition = new Vector2(0f, -12f);
                sellButtonRect.sizeDelta = new Vector2(90f, 22f);
                sellButtonObject.GetComponent<Image>().color = new Color(0.6f, 0.3f, 0.3f, 1f);
                var sellButton = sellButtonObject.GetComponent<Button>();
                TextMeshProUGUI sellLabel = CreateLabel(sellButtonObject.transform, "Text", 0f, "Sell");
                sellLabel.fontSize = 14f;
                var sellLabelRect = sellLabel.GetComponent<RectTransform>();
                sellLabelRect.anchorMin = Vector2.zero;
                sellLabelRect.anchorMax = Vector2.one;
                sellLabelRect.sizeDelta = Vector2.zero;
                sellLabelRect.anchoredPosition = Vector2.zero;

                estateInventoryRows[i] = new EstatePanelController.InventoryRowView
                {
                    root = invRowObject,
                    label = invLabel,
                    sellButton = sellButton
                };
            }

            var estateCropStageSprites = new Sprite[9];
            string[] estateCropIds = { "wheat", "carrot", "pumpkin" };
            string[] estateStageNames = { "sprout", "growing", "mature" };
            if (!Directory.Exists("Assets/Art/Crops"))
            {
                Debug.LogWarning("HomesteadSceneBuilder: Assets/Art/Crops does not exist -- crop stage sprites will be null.");
            }
            else
            {
                for (int cropIndex = 0; cropIndex < estateCropIds.Length; cropIndex++)
                {
                    for (int stage = 0; stage < estateStageNames.Length; stage++)
                    {
                        string path = $"Assets/Art/Crops/{estateCropIds[cropIndex]}_{estateStageNames[stage]}.png";
                        estateCropStageSprites[cropIndex * 3 + stage] = LoadIconSprite(path);
                    }
                }
            }

            Sprite estateSeedSprite = null;
            Sprite estateWaterDropletSprite = null;
            if (!Directory.Exists("Assets/Art/Estate"))
            {
                Debug.LogWarning("HomesteadSceneBuilder: Assets/Art/Estate does not exist -- seed/water-droplet sprites will be null.");
            }
            else
            {
                if (File.Exists("Assets/Art/Estate/seed.png"))
                {
                    estateSeedSprite = LoadIconSprite("Assets/Art/Estate/seed.png");
                }
                if (File.Exists("Assets/Art/Estate/water_droplet.png"))
                {
                    estateWaterDropletSprite = LoadIconSprite("Assets/Art/Estate/water_droplet.png");
                }
            }

            var estateLandTabButtonObject = new GameObject("LandTabButton", typeof(Image), typeof(Button));
            estateLandTabButtonObject.transform.SetParent(estatePanelRootObject.transform, false);
            var estateLandTabButtonRect = estateLandTabButtonObject.GetComponent<RectTransform>();
            estateLandTabButtonRect.anchoredPosition = new Vector2(-80f, 655f);
            estateLandTabButtonRect.sizeDelta = new Vector2(140f, 40f);
            estateLandTabButtonObject.GetComponent<Image>().color = new Color(0.35f, 0.55f, 0.3f, 1f);
            var estateLandTabButton = estateLandTabButtonObject.GetComponent<Button>();
            TextMeshProUGUI estateLandTabLabel = CreateLabel(estateLandTabButtonObject.transform, "Text", 0f, "Land");
            var estateLandTabLabelRect = estateLandTabLabel.GetComponent<RectTransform>();
            estateLandTabLabelRect.anchorMin = Vector2.zero;
            estateLandTabLabelRect.anchorMax = Vector2.one;
            estateLandTabLabelRect.sizeDelta = Vector2.zero;
            estateLandTabLabelRect.anchoredPosition = Vector2.zero;

            var estateShopsTabButtonObject = new GameObject("ShopsTabButton", typeof(Image), typeof(Button));
            estateShopsTabButtonObject.transform.SetParent(estatePanelRootObject.transform, false);
            var estateShopsTabButtonRect = estateShopsTabButtonObject.GetComponent<RectTransform>();
            estateShopsTabButtonRect.anchoredPosition = new Vector2(80f, 655f);
            estateShopsTabButtonRect.sizeDelta = new Vector2(140f, 40f);
            estateShopsTabButtonObject.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.7f, 1f);
            var estateShopsTabButton = estateShopsTabButtonObject.GetComponent<Button>();
            TextMeshProUGUI estateShopsTabLabel = CreateLabel(estateShopsTabButtonObject.transform, "Text", 0f, "Shops");
            var estateShopsTabLabelRect = estateShopsTabLabel.GetComponent<RectTransform>();
            estateShopsTabLabelRect.anchorMin = Vector2.zero;
            estateShopsTabLabelRect.anchorMax = Vector2.one;
            estateShopsTabLabelRect.sizeDelta = Vector2.zero;
            estateShopsTabLabelRect.anchoredPosition = Vector2.zero;

            var estateControllerObject = new GameObject("EstatePanelController");
            var estateController = estateControllerObject.AddComponent<EstatePanelController>();
            estateController.Initialize(
                estatePanelRootObject, estateCoinsLabel,
                estatePlotViews, estateSeedPickerObject, estateSeedButtons, estateSeedCostLabels, estateCropStageSprites,
                estateLandTabButton, estateShopsTabButton, estateLandTabRootObject, estateShopsTabRootObject,
                estateInventoryRows, estateShopRows,
                estateSeedSprite, estateWaterDropletSprite);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            // Registers the scene in Build Settings so
            // SceneManager.LoadSceneAsync("Homestead") can find it by name
            // at runtime and in PlayMode tests (Task 7) -- a saved .unity
            // file on disk alone isn't enough, Unity only loads scenes by
            // name that are listed here.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"HomesteadSceneBuilder: saved scene to {ScenePath}");
            EditorApplication.Exit(0);
        }

        [MenuItem("Homestead/Verify Scene")]
        public static void Verify()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var controller = Object.FindFirstObjectByType<EstatePanelController>();
            if (controller == null)
            {
                Debug.LogError("HomesteadSceneBuilder.Verify: EstatePanelController not found in the scene.");
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log("HomesteadSceneBuilder.Verify: scene opened and controller found successfully.");
            EditorApplication.Exit(0);
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, float yOffset, string text)
        {
            var labelObject = new GameObject(name, typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);
            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0f, -yOffset);
            rect.sizeDelta = new Vector2(420f, 32f);

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 24f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            return label;
        }

        private static Sprite LoadIconSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError($"HomesteadSceneBuilder.LoadIconSprite: failed to load icon sprite at {path}");
            }
            return sprite;
        }
    }
}
```

Note the two real layout changes from the source: (1) the whole content block shifted up by a uniform +400 on every `y` value (e.g. plot row 0's original `150f` becomes `550f`, the seed picker's `-280f` becomes `120f`, etc. -- verify each one you transcribe: original-y + 400 = new-y, no exceptions) since the new root is 1600 tall centered at 0 instead of 800 tall centered at 0. A *uniform* shift is deliberate and load-bearing for correctness: every spacing relationship between elements in the source (tab-buttons-to-plot-grid clearance, coins-label-to-tab-buttons clearance, the accepted 5-unit seed-picker/inventory-row overlap) was already independently verified during `understudy-kingdom`'s own final reviews -- translating every element by the same constant preserves all of those relationships exactly, while translating by inconsistent amounts would silently reintroduce exactly the class of geometry bug this project has already shipped three times (Estate Phase 1's off-canvas button, Phase 3's tab/plot overlap, Crop Animation's progress-bar raycast/render bugs). (2) No `estateButton`/`CloseButton` GameObjects at all, and `Initialize(...)` is called with the trimmed 15-argument list from Task 4.

**Known, deliberate simplification -- not a bug:** this uniform shift only fills the *upper* portion of the new 1600-tall root (content spans roughly `y=[15,756]`, leaving the entire bottom half, `y=[-800,0]`, empty). That's an accepted consequence of the "preserve exact relative arrangement, change nothing else" approach this task takes -- a real visual layout redesign to use the full screen is explicitly out of scope for this extraction (it's a structural port, not a redesign) and would naturally happen whenever Homestead's own Shops-on-Land/land-expansion work reshapes this screen anyway.

- [ ] **Step 2: Rebuild and verify the scene**

```bash
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\Homestead" -executeMethod UnderstudyKingdom.EditorTools.HomesteadSceneBuilder.Build -logFile "C:\Users\rajes\Homestead-task6-build.log"
```
(no `-quit` -- `Build()` calls `EditorApplication.Exit()` itself). Expected: `HomesteadSceneBuilder: saved scene to Assets/Scenes/Homestead.unity`, no compile errors, only the expected `carrot_mature.png`/`pumpkin_*.png`-style missing-file warnings if any (there should be none new, since Task 5 confirmed which files exist).

```bash
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\Homestead" -executeMethod UnderstudyKingdom.EditorTools.HomesteadSceneBuilder.Verify -logFile "C:\Users\rajes\Homestead-task6-verify.log"
```
Expected: `HomesteadSceneBuilder.Verify: scene opened and controller found successfully.`

- [ ] **Step 3: Run the full test suite**

```bash
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\Homestead" -runTests -testPlatform EditMode -testResults "C:\Users\rajes\Homestead-task6-editmode-results.xml" -logFile "C:\Users\rajes\Homestead-task6-editmode.log"
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\Homestead" -runTests -testPlatform PlayMode -testResults "C:\Users\rajes\Homestead-task6-playmode-results.xml" -logFile "C:\Users\rajes\Homestead-task6-playmode.log"
```

Expected: both green, 0 failures, matching the counts already established at the end of Tasks 2-4.

- [ ] **Step 4: Commit**

```bash
cd "C:\Users\rajes\Homestead"
git add Assets/Editor/HomesteadSceneBuilder.cs Assets/Editor/HomesteadSceneBuilder.cs.meta Assets/Scenes/Homestead.unity Assets/Scenes/Homestead.unity.meta
git commit -m "$(cat <<'EOF'
feat: add HomesteadSceneBuilder, build full-screen Estate scene

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01QMEErAfieqNAbk8ZRAFBwT
EOF
)"
```

---

### Task 7: Scene-level regression test

**Files:**
- Create: `Assets/Tests/PlayMode/HomesteadSceneTests.cs` (under `C:\Users\rajes\Homestead\`)

**Interfaces:**
- Consumes: the built `Homestead.unity` scene (Task 6).

- [ ] **Step 1: Write the failing test**

Create `C:\Users\rajes\Homestead\Assets\Tests\PlayMode\HomesteadSceneTests.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace UnderstudyKingdom.Tests
{
    public class HomesteadSceneTests
    {
        [UnityTest]
        public IEnumerator LoadedHomesteadScene_HasEstateController()
        {
            yield return SceneManager.LoadSceneAsync("Homestead");
            yield return null;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Canvas not found in the loaded Homestead scene.");

            var controller = Object.FindFirstObjectByType<UnderstudyKingdom.UI.EstatePanelController>();
            Assert.IsNotNull(controller, "EstatePanelController not found in the loaded Homestead scene.");
        }

        [UnityTest]
        public IEnumerator LoadedHomesteadScene_AllPlotsHaveProgressBarChild()
        {
            yield return SceneManager.LoadSceneAsync("Homestead");
            yield return null;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Canvas not found in the loaded Homestead scene.");

            for (int i = 0; i < 4; i++) // the 4 starting-unlocked plots
            {
                GameObject plot = FindChildByName(canvas.transform, $"Plot{i}");
                Assert.IsNotNull(plot, $"Plot{i} not found in the loaded Homestead scene.");

                GameObject progressBar = FindChildByName(plot.transform, "ProgressBar");
                Assert.IsNotNull(progressBar, $"Plot{i} is missing its ProgressBar child.");

                Image progressBarImage = progressBar.GetComponent<Image>();
                Assert.IsNotNull(progressBarImage.sprite, $"Plot{i}'s ProgressBar has no sprite assigned.");
                Assert.IsFalse(progressBarImage.raycastTarget, $"Plot{i}'s ProgressBar should not block raycasts.");
            }
        }

        private static GameObject FindChildByName(Transform parent, string name)
        {
            var stack = new Stack<Transform>();
            stack.Push(parent);
            while (stack.Count > 0)
            {
                Transform current = stack.Pop();
                if (current.name == name)
                {
                    return current.gameObject;
                }
                foreach (Transform child in current)
                {
                    stack.Push(child);
                }
            }
            Assert.Fail($"Could not find child named '{name}' under '{parent.name}'.");
            return null;
        }
    }
}
```

Task 6's `Build()` already registers the scene in `EditorBuildSettings.scenes`, so `SceneManager.LoadSceneAsync("Homestead")` can find it by name -- no additional setup needed here.

- [ ] **Step 2: Run test to verify it fails or passes trivially**

```bash
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\Homestead" -runTests -testPlatform PlayMode -testResults "C:\Users\rajes\Homestead-task7-red-results.xml" -logFile "C:\Users\rajes\Homestead-task7-red.log"
```

Since Task 6 already built and committed the scene, this may PASS immediately rather than fail first -- if so, that's expected (same as every other scene-level regression test added after its scene already exists in this project's history), skip to Step 4.

- [ ] **Step 3: N/A**

No implementation step -- the scene already exists from Task 6.

- [ ] **Step 4: Run test to verify it passes**

Same command. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
cd "C:\Users\rajes\Homestead"
git add Assets/Tests/PlayMode/HomesteadSceneTests.cs Assets/Tests/PlayMode/HomesteadSceneTests.cs.meta Assets/Editor/HomesteadSceneBuilder.cs Assets/Scenes/Homestead.unity
git commit -m "$(cat <<'EOF'
test: add scene-level regression test for Homestead

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01QMEErAfieqNAbk8ZRAFBwT
EOF
)"
```

---

### Task 8: Create the GitHub repo, final verification

**Files:** none (no code changes).

- [ ] **Step 1: Create the private GitHub repo, no push**

```bash
cd "C:\Users\rajes\Homestead"
gh repo create rajeshguntupalli59/Homestead --private --source=. --remote=origin
```

Do NOT run `git push` -- per this plan's Global Constraints, the repo is created but not pushed to until the user explicitly asks.

- [ ] **Step 2: Run the full suite one final time**

```bash
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\Homestead" -runTests -testPlatform EditMode -testResults "C:\Users\rajes\Homestead-final-editmode-results.xml" -logFile "C:\Users\rajes\Homestead-final-editmode.log"
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\Homestead" -runTests -testPlatform PlayMode -testResults "C:\Users\rajes\Homestead-final-playmode-results.xml" -logFile "C:\Users\rajes\Homestead-final-playmode.log"
```

Expected: both green, 0 failures.

- [ ] **Step 3: Report the manual Play Mode check as a pending human step**

This plan's automated tasks end here. Per the design spec's Verification Plan, a manual Play Mode check (open the game, confirm the UI renders as a full-screen game with no dead modal-panel space, plant/water/harvest a crop, unlock/use a shop) is the human's own step, not something a subagent can do -- report it as the next action for the user, the same way every prior milestone in `understudy-kingdom` has deferred its own manual Play Mode checkpoint to the user.

**Once the user has confirmed the manual Play Mode check passes**, the extraction is complete. Per the design spec, archiving the `understudy-kingdom` GitHub repo is explicitly OUT of this plan's scope -- ask the user for that go-ahead separately, at that point, rather than folding it into this task.

---

## Final Verification (after all 8 tasks)

Both EditMode and PlayMode green, `Homestead.unity` builds and opens via `Verify()`, the private `rajeshguntupalli59/Homestead` GitHub repo exists (unpushed), and `understudy-kingdom` has zero uncommitted or modified files (confirm with `git -C C:\Users\rajes\understudy-kingdom status --short` -- should be empty, proving nothing there was touched).

## Explicitly Out of Scope for This Plan

- Any new gameplay feature (Shops-on-Land, land expansion, construction animation, animals) -- resumes as its own brainstorm inside Homestead once this extraction is verified working, per the design spec.
- Archiving the `understudy-kingdom` GitHub repo -- a separate, explicitly-confirmed action after the user's own manual verification, not a task in this plan.
- Pushing the `Homestead` repo to GitHub.
- Any change to `understudy-kingdom` itself.
