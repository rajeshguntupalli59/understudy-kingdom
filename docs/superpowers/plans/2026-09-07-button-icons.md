# Button Icons Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the Submit Recommendation, Challenge a Rival Kingdom, View History, and Council buttons a small painted icon next to their existing text label.

**Architecture:** Each icon is a static `Sprite` (no theme/state variation) assigned once by `CoreLoopSceneBuilder.Build()` to a new 32x32 child `Image` on the button, whose reference is passed into that button's owning controller's `Initialize(...)` and stored in a new field -- purely for future extensibility, no runtime logic reads it this pass.

**Tech Stack:** Unity 6000.3.23f1, C#, NUnit PlayMode tests, Unity batch-mode CLI for scene rebuild/verify/test runs.

## Global Constraints

- Only 4 of the originally-planned 15 buttons get icons this pass: Submit Recommendation, Challenge a Rival Kingdom, View History, Council -- the other 11 are deferred (Hugging Face credit cap hit mid-generation). No code shape changes needed to add the rest later, just repeat this pass's pattern once more art exists.
- Icon art already committed at `Assets/Art/ButtonIcons/<name>.png` (4 files: `submit.png`, `challenge.png`, `history.png`, `council.png`), all 512x512 -- see `docs/superpowers/specs/2026-09-07-button-icons-design.md`.
- Icon size is fixed at 32x32, inset 8px from the button's left edge; the button's existing text label shifts right by 40px (32px icon + 8px gap) to make room -- no icon-size design token, no per-button size variation.
- Each icon `Image` field is owned by exactly one controller (the one that already constructs that button) -- `CoreLoopScreenController` (submitIcon), `DuelButtonController` (challengeIcon), `HistoryPanelController` (viewHistoryIcon), `CouncilPanelController` (councilIcon). No cross-controller duplication.
- No theming/dynamic logic reads these fields this pass -- `Initialize(...)` stores the reference and nothing else touches it.

---

### Task 1: Wire icon Image fields into the 4 owning controllers

**Files:**
- Modify: `Assets/Scripts/UI/CoreLoopScreenController.cs`
- Modify: `Assets/Scripts/UI/DuelButtonController.cs`
- Modify: `Assets/Scripts/UI/HistoryPanelController.cs`
- Modify: `Assets/Scripts/UI/CouncilPanelController.cs`
- Modify: `Assets/Tests/PlayMode/CoreLoopScreenControllerTests.cs`
- Modify: `Assets/Tests/PlayMode/DuelButtonControllerTests.cs`
- Modify: `Assets/Tests/PlayMode/HistoryPanelControllerTests.cs`
- Modify: `Assets/Tests/PlayMode/CouncilPanelControllerTests.cs`

**Interfaces:**
- Consumes: nothing new from outside this task.
- Produces: 4 `Initialize(...)` signature changes, each gaining exactly one new trailing `Image` parameter, in this exact order (appended after each method's current final parameter):
  - `CoreLoopScreenController.Initialize(..., Sprite[] rulerPortraits, Image submitIcon)`
  - `DuelButtonController.Initialize(..., DuelModalGate gate, Image challengeIcon)`
  - `HistoryPanelController.Initialize(..., DuelModalGate gate, Image viewHistoryIcon)`
  - `CouncilPanelController.Initialize(..., DuelModalGate gate, Image councilIcon)`
  Task 2's scene builder call sites must pass an `Image` argument last, in this order, for each.

This task only touches hand-built-object PlayMode tests (each of the 4 test files constructs its own GameObjects and calls `Initialize` directly -- none load the real scene). The real `CoreLoop.unity` scene is not rebuilt until Task 2; until then the old serialized scene simply doesn't have these new fields wired (they deserialize as null), which is harmless here since nothing dereferences an icon field this pass (unlike `GetBackgroundSprite`, there's no code path that would NRE on a null icon reference).

- [ ] **Step 1: Add the field, Initialize parameter, and assignment to CoreLoopScreenController**

Open `Assets/Scripts/UI/CoreLoopScreenController.cs`. Add a new field right after the existing `[SerializeField] private Sprite[] rulerPortraits;` (line 27):

```csharp
        [SerializeField] private Sprite[] rulerPortraits;
        [SerializeField] private Image submitIcon;
```

Update the `Initialize` method signature (currently ending `Sprite[] rulerPortraits)`) to:

```csharp
        public void Initialize(
            DecisionCycleManager manager,
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            TextMeshProUGUI moodLabel,
            TextMeshProUGUI loyaltyLabel,
            TextMeshProUGUI agendaLabel,
            TextMeshProUGUI narrationText,
            Button submitButton,
            Image rulerPortraitImage,
            Sprite[] rulerPortraits,
            Image submitIcon)
        {
            this.manager = manager;
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.moodLabel = moodLabel;
            this.loyaltyLabel = loyaltyLabel;
            this.agendaLabel = agendaLabel;
            this.narrationText = narrationText;
            this.submitButton = submitButton;
            this.rulerPortraitImage = rulerPortraitImage;
            this.rulerPortraits = rulerPortraits;
            this.submitIcon = submitIcon;

            Bind();
        }
```

- [ ] **Step 2: Add the field, Initialize parameter, and assignment to DuelButtonController**

Open `Assets/Scripts/UI/DuelButtonController.cs`. Add a new field right after the existing `[SerializeField] private DuelModalGate gate;` (line 25):

```csharp
        [SerializeField] private DuelModalGate gate;
        [SerializeField] private Image challengeIcon;
```

Update the `Initialize` method signature (currently ending `DuelModalGate gate)`) to:

```csharp
        public void Initialize(
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button challengeButton,
            TextMeshProUGUI resultText,
            BackendSyncCoordinator coordinator,
            DuelModalGate gate,
            Image challengeIcon)
        {
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.challengeButton = challengeButton;
            this.resultText = resultText;
            this.coordinator = coordinator;
            this.gate = gate;
            this.challengeIcon = challengeIcon;

            Bind();
        }
```

Note: `Image` is not yet imported in this file's `using` list -- add `using UnityEngine.UI;` if it isn't already present (check the top of the file; it should already be there since `Button`/`Slider` are used).

- [ ] **Step 3: Add the field, Initialize parameter, and assignment to HistoryPanelController**

Open `Assets/Scripts/UI/HistoryPanelController.cs`. Add a new field right after the existing `[SerializeField] private DuelModalGate gate;` (line 33):

```csharp
        [SerializeField] private DuelModalGate gate;
        [SerializeField] private Image viewHistoryIcon;
```

Update the `Initialize` method signature (currently ending `DuelModalGate gate)`) to:

```csharp
        public void Initialize(
            Button viewHistoryButton,
            GameObject panelRoot,
            Button closeButton,
            TextMeshProUGUI[] rowTexts,
            BackendSyncCoordinator coordinator,
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button submitButton,
            Button challengeButton,
            Button councilButton,
            Button eventsButton,
            Button customizeButton,
            DuelModalGate gate,
            Image viewHistoryIcon)
        {
            this.viewHistoryButton = viewHistoryButton;
            this.panelRoot = panelRoot;
            this.closeButton = closeButton;
            this.rowTexts = rowTexts;
            this.coordinator = coordinator;
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.submitButton = submitButton;
            this.challengeButton = challengeButton;
            this.councilButton = councilButton;
            this.eventsButton = eventsButton;
            this.customizeButton = customizeButton;
            this.gate = gate;
            this.viewHistoryIcon = viewHistoryIcon;

            Bind();
        }
```

- [ ] **Step 4: Add the field, Initialize parameter, and assignment to CouncilPanelController**

Open `Assets/Scripts/UI/CouncilPanelController.cs`. Add a new field right after the existing `[SerializeField] private DuelModalGate gate;` (line 51):

```csharp
        [SerializeField] private DuelModalGate gate;
        [SerializeField] private Image councilIcon;
```

Update the `Initialize` method signature (currently ending `DuelModalGate gate)`) to add `Image councilIcon` as the new final parameter, and add `this.councilIcon = councilIcon;` right after the existing `this.gate = gate;` assignment. The full signature (all other parameters unchanged from the file's current state) is:

```csharp
        public void Initialize(
            Button councilButton,
            GameObject panelRoot,
            Button closeButton,
            GameObject notInCouncilView,
            GameObject inCouncilView,
            TMP_InputField nameInputField,
            Button createButton,
            TMP_InputField joinCodeInputField,
            Button joinButton,
            TextMeshProUGUI statusMessageText,
            TextMeshProUGUI nameLabel,
            TextMeshProUGUI joinCodeLabel,
            TextMeshProUGUI memberCountLabel,
            TextMeshProUGUI progressLabel,
            TextMeshProUGUI rewardStatusLabel,
            BackendSyncCoordinator coordinator,
            DecisionCycleManager manager,
            CoreLoopScreenController screenController,
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button submitButton,
            Button challengeButton,
            Button viewHistoryButton,
            Button eventsButton,
            Button customizeButton,
            DuelModalGate gate,
            Image councilIcon)
```

Add `this.councilIcon = councilIcon;` immediately after the existing `this.gate = gate;` line inside the method body (leave every other assignment in this large method exactly as it already is).

- [ ] **Step 5: Add a reflection-based test to CoreLoopScreenControllerTests.cs**

Open `Assets/Tests/PlayMode/CoreLoopScreenControllerTests.cs`. Add `using System.Reflection;` as the first line if not already present.

Add a new private field near the other private fields (next to `private Sprite[] rulerPortraits;`):

```csharp
        private Sprite[] rulerPortraits;
        private Image submitIcon;
```

In `SetUp`, right after the existing `rulerPortraits` population loop and before the `controllerObject`/`controller` creation, add:

```csharp
            var submitIconObject = new GameObject("SubmitIcon", typeof(Image));
            submitIconObject.transform.SetParent(canvasObject.transform, false);
            submitIcon = submitIconObject.GetComponent<Image>();
```

Update the `controller.Initialize(...)` call (currently ending `rulerPortraitImage, rulerPortraits);`) to:

```csharp
            controller.Initialize(manager, armySlider, tradeSlider, religionSlider,
                moodLabel, loyaltyLabel, agendaLabel, narrationText, submitButton,
                rulerPortraitImage, rulerPortraits, submitIcon);
```

Add a new test method (anywhere among the other `[Test]` methods in this file):

```csharp
        [Test]
        public void Initialize_StoresSubmitIconReference()
        {
            FieldInfo iconField = typeof(CoreLoopScreenController).GetField("submitIcon", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.AreSame(submitIcon, iconField.GetValue(controller));
        }
```

- [ ] **Step 6: Add a reflection-based test to DuelButtonControllerTests.cs**

Open `Assets/Tests/PlayMode/DuelButtonControllerTests.cs`. Add `using System.Reflection;` as the first line.

Add a new private field near the other private fields (next to `private Button challengeButton;`):

```csharp
        private Button challengeButton;
        private Image challengeIcon;
```

Also change the test file's `controller` field (find where `var controller = controllerObject.AddComponent<DuelButtonController>();` is declared in `SetUp`) to be a private field of the test class instead of a local `var`, so the new test method can reach it -- check whether it's already a field or a local `var` first: if it's currently `var controller = ...` inside `SetUp`, add `private DuelButtonController controller;` to the class's field list and change that line to `controller = controllerObject.AddComponent<DuelButtonController>();` (drop the `var`).

In `SetUp`, right after the existing `challengeButton = buttonObject.GetComponent<Button>();` line, add:

```csharp
            var challengeIconObject = new GameObject("ChallengeIcon", typeof(Image));
            challengeIconObject.transform.SetParent(canvasObject.transform, false);
            challengeIcon = challengeIconObject.GetComponent<Image>();
```

Update the `controller.Initialize(...)` call (currently `controller.Initialize(armySlider, tradeSlider, religionSlider, challengeButton, resultText, coordinator, gate);`) to:

```csharp
            controller.Initialize(armySlider, tradeSlider, religionSlider, challengeButton, resultText, coordinator, gate, challengeIcon);
```

Add a new test method:

```csharp
        [Test]
        public void Initialize_StoresChallengeIconReference()
        {
            FieldInfo iconField = typeof(DuelButtonController).GetField("challengeIcon", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.AreSame(challengeIcon, iconField.GetValue(controller));
        }
```

- [ ] **Step 7: Add a reflection-based test to HistoryPanelControllerTests.cs**

Open `Assets/Tests/PlayMode/HistoryPanelControllerTests.cs` (`using System.Reflection;` is already present in this file).

Add a new private field near the other private fields (next to `private Button viewHistoryButton;` or similar):

```csharp
        private Image viewHistoryIcon;
```

Check whether this file's `controller` (the `HistoryPanelController` created in `SetUp`) is already a class field or a local `var`; if local, promote it to a private class field the same way as Step 6, so the new test method can reach it.

In `SetUp`, right after the existing `viewHistoryButton = viewHistoryButtonObject.GetComponent<Button>();` line, add:

```csharp
            var viewHistoryIconObject = new GameObject("ViewHistoryIcon", typeof(Image));
            viewHistoryIconObject.transform.SetParent(canvasObject.transform, false);
            viewHistoryIcon = viewHistoryIconObject.GetComponent<Image>();
```

Update the `controller.Initialize(...)` call (currently ending `..., customizeButton, gate);`) to:

```csharp
            controller.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, coordinator,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, councilButton, eventsButton, customizeButton, gate, viewHistoryIcon);
```

Add a new test method:

```csharp
        [Test]
        public void Initialize_StoresViewHistoryIconReference()
        {
            FieldInfo iconField = typeof(HistoryPanelController).GetField("viewHistoryIcon", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.AreSame(viewHistoryIcon, iconField.GetValue(controller));
        }
```

- [ ] **Step 8: Add a reflection-based test to CouncilPanelControllerTests.cs**

Open `Assets/Tests/PlayMode/CouncilPanelControllerTests.cs`. Add `using System.Reflection;` as the first line.

Add a new private field near the other private fields (next to `private Button councilButton;` or similar):

```csharp
        private Image councilIcon;
```

Check whether this file's `controller` (the `CouncilPanelController` created in `SetUp`) is already a class field or a local `var`; if local, promote it to a private class field the same way as Step 6.

In `SetUp`, right after the existing `councilButton = councilButtonObject.GetComponent<Button>();` line, add:

```csharp
            var councilIconObject = new GameObject("CouncilIcon", typeof(Image));
            councilIconObject.transform.SetParent(canvasObject.transform, false);
            councilIcon = councilIconObject.GetComponent<Image>();
```

Update the `controller.Initialize(...)` call to append `, councilIcon` as the final argument (find the call -- it currently ends with `..., customizeButton, gate);` across several wrapped lines -- and change the final `gate);` to `gate, councilIcon);`).

Add a new test method:

```csharp
        [Test]
        public void Initialize_StoresCouncilIconReference()
        {
            FieldInfo iconField = typeof(CouncilPanelController).GetField("councilIcon", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.AreSame(councilIcon, iconField.GetValue(controller));
        }
```

Steps 1-4 (controller changes) and 5-8 (matching test changes) were written
together as one coherent change rather than test-first-then-implementation:
every assertion here is a simple reflection check confirming a field holds
the reference it was given, so there's no meaningful RED state to observe
separately from a plain compile error. Proceed straight to running the
suite.

- [ ] **Step 9: Run PlayMode tests to confirm they pass**

```bash
cd /c/Users/rajes/understudy-kingdom
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-icons-task1-play.xml" -logFile "C:\Users\rajes\understudy-kingdom\unity-icons-task1-play.log"
grep -o '<test-run[^>]*total="[0-9]*"[^>]*passed="[0-9]*"[^>]*failed="[0-9]*"' test-results-icons-task1-play.xml
```

Expected: `failed="0"`, `total` matching the full PlayMode suite count plus 4 (the new tests). If the project fails to compile because `CoreLoopSceneBuilder.cs`'s calls to these 4 controllers' `Initialize(...)` no longer match the new signatures, that is expected and must ALSO be fixed as part of this task (add one placeholder `null` argument at each of the 4 call sites in `Assets/Editor/CoreLoopSceneBuilder.cs` -- e.g. `duelController.Initialize(armySlider, tradeSlider, religionSlider, duelButton, duelResultText, backendCoordinator, duelModalGate, null);` -- purely to keep the project compiling; Task 2 replaces every one of these placeholders with the real wiring). This mirrors the established pattern from the panel-art milestone's Task 1, which needed the same kind of minimal placeholder fix at its own scene-builder call site.

- [ ] **Step 10: Run EditMode tests too**

```bash
cd /c/Users/rajes/understudy-kingdom
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-icons-task1-edit.xml" -logFile "C:\Users\rajes\understudy-kingdom\unity-icons-task1-edit.log"
grep -o '<test-run[^>]*total="[0-9]*"[^>]*passed="[0-9]*"[^>]*failed="[0-9]*"' test-results-icons-task1-edit.xml
```

Expected: `failed="0"`.

- [ ] **Step 11: Commit**

```bash
git add Assets/Scripts/UI/CoreLoopScreenController.cs Assets/Scripts/UI/DuelButtonController.cs Assets/Scripts/UI/HistoryPanelController.cs Assets/Scripts/UI/CouncilPanelController.cs Assets/Tests/PlayMode/CoreLoopScreenControllerTests.cs Assets/Tests/PlayMode/DuelButtonControllerTests.cs Assets/Tests/PlayMode/HistoryPanelControllerTests.cs Assets/Tests/PlayMode/CouncilPanelControllerTests.cs Assets/Editor/CoreLoopSceneBuilder.cs
git commit -m "feat: wire icon Image fields into the 4 owning controllers"
```

---

### Task 2: Load the 4 real icons into the built scene

**Files:**
- Modify: `Assets/Editor/CoreLoopSceneBuilder.cs`
- Modify: `Assets/Tests/PlayMode/CoreLoopSceneTests.cs`

**Interfaces:**
- Consumes: the 4 `Initialize(...)` signatures from Task 1, each now taking one trailing `Image` argument (in the exact order shown in Task 1's Interfaces block).
- Produces: `CoreLoopSceneBuilder.LoadIconSprite(string path)` (static, `Sprite`) -- not consumed by any later task in this plan, but is the loader any future icon-shaped asset should call.

- [ ] **Step 1: Add the new regression test to CoreLoopSceneTests.cs -- this will fail until Step 2-5 wire the real icons**

Open `Assets/Tests/PlayMode/CoreLoopSceneTests.cs`. Add this new test method anywhere among the other `[UnityTest]` methods:

```csharp
        [UnityTest]
        public IEnumerator LoadedCoreLoopScene_ButtonIcons_HaveNonNullSpritesOnLoad()
        {
            yield return SceneManager.LoadSceneAsync("CoreLoop");
            yield return null;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Canvas not found in the loaded CoreLoop scene.");

            GameObject submitButton = FindChildByName(canvas.transform, "SubmitButton");
            GameObject challengeButton = FindChildByName(canvas.transform, "ChallengeButton");
            GameObject viewHistoryButton = FindChildByName(canvas.transform, "ViewHistoryButton");
            GameObject councilButton = FindChildByName(canvas.transform, "CouncilButton");

            GameObject submitIcon = FindChildByName(submitButton.transform, "Icon");
            GameObject challengeIcon = FindChildByName(challengeButton.transform, "Icon");
            GameObject viewHistoryIcon = FindChildByName(viewHistoryButton.transform, "Icon");
            GameObject councilIcon = FindChildByName(councilButton.transform, "Icon");

            Assert.IsNotNull(submitIcon.GetComponent<Image>().sprite, "Expected SubmitButton's Icon to have a non-null sprite.");
            Assert.IsNotNull(challengeIcon.GetComponent<Image>().sprite, "Expected ChallengeButton's Icon to have a non-null sprite.");
            Assert.IsNotNull(viewHistoryIcon.GetComponent<Image>().sprite, "Expected ViewHistoryButton's Icon to have a non-null sprite.");
            Assert.IsNotNull(councilIcon.GetComponent<Image>().sprite, "Expected CouncilButton's Icon to have a non-null sprite.");
        }
```

- [ ] **Step 2: Run PlayMode tests to confirm it fails**

```bash
cd /c/Users/rajes/understudy-kingdom
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-icons-task2-red.xml" -logFile "C:\Users\rajes\understudy-kingdom\unity-icons-task2-red.log"
grep -o '<test-run[^>]*total="[0-9]*"[^>]*passed="[0-9]*"[^>]*failed="[0-9]*"' test-results-icons-task2-red.xml
```

Expected: `failed="1"` (the new test fails -- no `Icon` child GameObject exists on any of the 4 buttons in the currently-committed scene).

- [ ] **Step 3: Add the LoadIconSprite loader to CoreLoopSceneBuilder.cs**

Open `Assets/Editor/CoreLoopSceneBuilder.cs`. Add this new method right after the existing `LoadThemedSprites` method (which itself sits after `LoadThemedSprite`):

```csharp
        // Icons are static (no theme/state variation), so this is a single-file
        // loader, not the array-based LoadThemedSprites shape -- mirrors how
        // LoadPortraitSprite/LoadRulerPortraits coexist as their own shape for
        // their own differently-keyed asset set.
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
                Debug.LogError($"CoreLoopSceneBuilder.LoadIconSprite: failed to load icon sprite at {path}");
            }
            return sprite;
        }
```

- [ ] **Step 4: Wire the icon + label repositioning into the 4 button-creation blocks and update their Initialize call sites**

Still in `Assets/Editor/CoreLoopSceneBuilder.cs`, make the following four changes.

**SubmitButton** (find the block starting `var buttonObject = new GameObject("SubmitButton", typeof(Image), typeof(Button));`): change the label rect block from:

```csharp
            TextMeshProUGUI buttonLabel = CreateLabel(buttonObject.transform, "Text", 0f, "Submit Recommendation");
            var buttonLabelRect = buttonLabel.GetComponent<RectTransform>();
            buttonLabelRect.anchorMin = Vector2.zero;
            buttonLabelRect.anchorMax = Vector2.one;
            buttonLabelRect.sizeDelta = Vector2.zero;
            buttonLabelRect.anchoredPosition = Vector2.zero;
```

to:

```csharp
            TextMeshProUGUI buttonLabel = CreateLabel(buttonObject.transform, "Text", 0f, "Submit Recommendation");
            var buttonLabelRect = buttonLabel.GetComponent<RectTransform>();
            buttonLabelRect.anchorMin = Vector2.zero;
            buttonLabelRect.anchorMax = Vector2.one;
            buttonLabelRect.offsetMin = new Vector2(40f, 0f);
            buttonLabelRect.offsetMax = Vector2.zero;

            var submitIconObject = new GameObject("Icon", typeof(Image));
            submitIconObject.transform.SetParent(buttonObject.transform, false);
            var submitIconRect = submitIconObject.GetComponent<RectTransform>();
            submitIconRect.anchorMin = new Vector2(0f, 0.5f);
            submitIconRect.anchorMax = new Vector2(0f, 0.5f);
            submitIconRect.pivot = new Vector2(0f, 0.5f);
            submitIconRect.anchoredPosition = new Vector2(8f, 0f);
            submitIconRect.sizeDelta = new Vector2(32f, 32f);
            var submitIconImage = submitIconObject.GetComponent<Image>();
            submitIconImage.sprite = LoadIconSprite("Assets/Art/ButtonIcons/submit.png");
            submitIconImage.raycastTarget = false;
```

Then update the `controller.Initialize(...)` call right below it (currently ending `rulerPortraitImage, rulerPortraits);`) to:

```csharp
            controller.Initialize(manager, armySlider, tradeSlider, religionSlider,
                moodLabel, loyaltyLabel, agendaLabel, narrationText, button,
                rulerPortraitImage, rulerPortraits, submitIconImage);
```

**ChallengeButton** (find the block starting `var duelButtonObject = new GameObject("ChallengeButton", typeof(Image), typeof(Button));`): change the label rect block from:

```csharp
            TextMeshProUGUI duelButtonLabel = CreateLabel(duelButtonObject.transform, "Text", 0f, "Challenge a Rival Kingdom");
            var duelButtonLabelRect = duelButtonLabel.GetComponent<RectTransform>();
            duelButtonLabelRect.anchorMin = Vector2.zero;
            duelButtonLabelRect.anchorMax = Vector2.one;
            duelButtonLabelRect.sizeDelta = Vector2.zero;
            duelButtonLabelRect.anchoredPosition = Vector2.zero;
```

to:

```csharp
            TextMeshProUGUI duelButtonLabel = CreateLabel(duelButtonObject.transform, "Text", 0f, "Challenge a Rival Kingdom");
            var duelButtonLabelRect = duelButtonLabel.GetComponent<RectTransform>();
            duelButtonLabelRect.anchorMin = Vector2.zero;
            duelButtonLabelRect.anchorMax = Vector2.one;
            duelButtonLabelRect.offsetMin = new Vector2(40f, 0f);
            duelButtonLabelRect.offsetMax = Vector2.zero;

            var challengeIconObject = new GameObject("Icon", typeof(Image));
            challengeIconObject.transform.SetParent(duelButtonObject.transform, false);
            var challengeIconRect = challengeIconObject.GetComponent<RectTransform>();
            challengeIconRect.anchorMin = new Vector2(0f, 0.5f);
            challengeIconRect.anchorMax = new Vector2(0f, 0.5f);
            challengeIconRect.pivot = new Vector2(0f, 0.5f);
            challengeIconRect.anchoredPosition = new Vector2(8f, 0f);
            challengeIconRect.sizeDelta = new Vector2(32f, 32f);
            var challengeIconImage = challengeIconObject.GetComponent<Image>();
            challengeIconImage.sprite = LoadIconSprite("Assets/Art/ButtonIcons/challenge.png");
            challengeIconImage.raycastTarget = false;
```

Then update the `duelController.Initialize(...)` call right below it to:

```csharp
            duelController.Initialize(armySlider, tradeSlider, religionSlider, duelButton, duelResultText, backendCoordinator, duelModalGate, challengeIconImage);
```

**ViewHistoryButton** (find the block starting `var viewHistoryButtonObject = new GameObject("ViewHistoryButton", typeof(Image), typeof(Button));`): change the label rect block from:

```csharp
            TextMeshProUGUI viewHistoryLabel = CreateLabel(viewHistoryButtonObject.transform, "Text", 0f, "View History");
            var viewHistoryLabelRect = viewHistoryLabel.GetComponent<RectTransform>();
            viewHistoryLabelRect.anchorMin = Vector2.zero;
            viewHistoryLabelRect.anchorMax = Vector2.one;
            viewHistoryLabelRect.sizeDelta = Vector2.zero;
            viewHistoryLabelRect.anchoredPosition = Vector2.zero;
```

to:

```csharp
            TextMeshProUGUI viewHistoryLabel = CreateLabel(viewHistoryButtonObject.transform, "Text", 0f, "View History");
            var viewHistoryLabelRect = viewHistoryLabel.GetComponent<RectTransform>();
            viewHistoryLabelRect.anchorMin = Vector2.zero;
            viewHistoryLabelRect.anchorMax = Vector2.one;
            viewHistoryLabelRect.offsetMin = new Vector2(40f, 0f);
            viewHistoryLabelRect.offsetMax = Vector2.zero;

            var viewHistoryIconObject = new GameObject("Icon", typeof(Image));
            viewHistoryIconObject.transform.SetParent(viewHistoryButtonObject.transform, false);
            var viewHistoryIconRect = viewHistoryIconObject.GetComponent<RectTransform>();
            viewHistoryIconRect.anchorMin = new Vector2(0f, 0.5f);
            viewHistoryIconRect.anchorMax = new Vector2(0f, 0.5f);
            viewHistoryIconRect.pivot = new Vector2(0f, 0.5f);
            viewHistoryIconRect.anchoredPosition = new Vector2(8f, 0f);
            viewHistoryIconRect.sizeDelta = new Vector2(32f, 32f);
            var viewHistoryIconImage = viewHistoryIconObject.GetComponent<Image>();
            viewHistoryIconImage.sprite = LoadIconSprite("Assets/Art/ButtonIcons/history.png");
            viewHistoryIconImage.raycastTarget = false;
```

This button's owning controller (`HistoryPanelController`) is not constructed until much later in `Build()` (its `historyController.Initialize(...)` call sits near the History panel construction, not immediately after this button block) -- leave `viewHistoryIconImage` as a local variable here; it stays in scope for the rest of `Build()` the same way `viewHistoryButton` itself already does. Find the existing call (currently ending `..., customizeButton, duelModalGate);`) and update it to:

```csharp
            historyController.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, backendCoordinator,
                armySlider, tradeSlider, religionSlider, button, duelButton, councilButton, eventsButton, customizeButton, duelModalGate, viewHistoryIconImage);
```

**CouncilButton** (find the block starting `var councilButtonObject = new GameObject("CouncilButton", typeof(Image), typeof(Button));`): change the label rect block from:

```csharp
            TextMeshProUGUI councilButtonLabel = CreateLabel(councilButtonObject.transform, "Text", 0f, "Council");
            var councilButtonLabelRect = councilButtonLabel.GetComponent<RectTransform>();
            councilButtonLabelRect.anchorMin = Vector2.zero;
            councilButtonLabelRect.anchorMax = Vector2.one;
            councilButtonLabelRect.sizeDelta = Vector2.zero;
            councilButtonLabelRect.anchoredPosition = Vector2.zero;
```

to:

```csharp
            TextMeshProUGUI councilButtonLabel = CreateLabel(councilButtonObject.transform, "Text", 0f, "Council");
            var councilButtonLabelRect = councilButtonLabel.GetComponent<RectTransform>();
            councilButtonLabelRect.anchorMin = Vector2.zero;
            councilButtonLabelRect.anchorMax = Vector2.one;
            councilButtonLabelRect.offsetMin = new Vector2(40f, 0f);
            councilButtonLabelRect.offsetMax = Vector2.zero;

            var councilIconObject = new GameObject("Icon", typeof(Image));
            councilIconObject.transform.SetParent(councilButtonObject.transform, false);
            var councilIconRect = councilIconObject.GetComponent<RectTransform>();
            councilIconRect.anchorMin = new Vector2(0f, 0.5f);
            councilIconRect.anchorMax = new Vector2(0f, 0.5f);
            councilIconRect.pivot = new Vector2(0f, 0.5f);
            councilIconRect.anchoredPosition = new Vector2(8f, 0f);
            councilIconRect.sizeDelta = new Vector2(32f, 32f);
            var councilIconImage = councilIconObject.GetComponent<Image>();
            councilIconImage.sprite = LoadIconSprite("Assets/Art/ButtonIcons/council.png");
            councilIconImage.raycastTarget = false;
```

This button's owning controller (`CouncilPanelController`) is likewise constructed later in `Build()`. Find the existing `councilController.Initialize(...)` call (currently ending `..., customizeButton, duelModalGate);`) and update its final two arguments from `duelModalGate);` to `duelModalGate, councilIconImage);`.

- [ ] **Step 5: Remove the 4 placeholder `null` arguments from Task 1**

If Task 1's Step 10 added placeholder `null` arguments at these same 4 `Initialize(...)` call sites to keep the project compiling before this task existed, Step 4 above already replaced each of those calls in full -- confirm no `, null);` remains at any of these 4 call sites (`grep -n ", null);" Assets/Editor/CoreLoopSceneBuilder.cs` should show no matches introduced by Task 1).

- [ ] **Step 6: Extend Verify() with non-null checks for the 4 new icon Image references**

Still in `Assets/Editor/CoreLoopSceneBuilder.cs`, in the `Verify()` method, insert this block right after the existing `CosmeticsPanelController`-related checks (after the last `eventPanelSprites[1] == null` guard block from the panel-art milestone, right before the final `Debug.Log("CoreLoopSceneBuilder.Verify: scene opened and controller found successfully.");` line):

```csharp
            var coreLoopController = Object.FindFirstObjectByType<CoreLoopScreenController>();
            if (coreLoopController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no CoreLoopScreenController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            if (!VerifyIconField(coreLoopController, "submitIcon"))
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            var duelButtonController = Object.FindFirstObjectByType<DuelButtonController>();
            if (duelButtonController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no DuelButtonController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            if (!VerifyIconField(duelButtonController, "challengeIcon"))
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            var historyPanelController = Object.FindFirstObjectByType<HistoryPanelController>();
            if (historyPanelController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no HistoryPanelController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            if (!VerifyIconField(historyPanelController, "viewHistoryIcon"))
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            var councilPanelController = Object.FindFirstObjectByType<CouncilPanelController>();
            if (councilPanelController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no CouncilPanelController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            if (!VerifyIconField(councilPanelController, "councilIcon"))
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
```

Add this new private helper method near `TryGetThemedSpriteArray` (same file):

```csharp
        // Confirms an icon Image field is non-null AND its .sprite is non-null --
        // icons have no fallback/intentionally-missing slot (unlike the themed
        // panel art), so both checks apply here.
        private static bool VerifyIconField(Component controller, string fieldName)
        {
            FieldInfo field = controller.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogError($"CoreLoopSceneBuilder.Verify: {controller.GetType().Name} has no private {fieldName} field (renamed?).");
                return false;
            }
            var image = (Image)field.GetValue(controller);
            if (image == null)
            {
                Debug.LogError($"CoreLoopSceneBuilder.Verify: {controller.GetType().Name}.{fieldName} is null.");
                return false;
            }
            if (image.sprite == null)
            {
                Debug.LogError($"CoreLoopSceneBuilder.Verify: {controller.GetType().Name}.{fieldName}'s Image has a null sprite.");
                return false;
            }
            return true;
        }
```

- [ ] **Step 7: Rebuild and verify the scene**

```bash
cd /c/Users/rajes/understudy-kingdom
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Build -logFile "C:\Users\rajes\understudy-kingdom\build-scene-icons.log" -quit
tail -20 build-scene-icons.log
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Verify -logFile "C:\Users\rajes\understudy-kingdom\verify-scene-icons.log" -quit
grep -i "error\|Verify:" verify-scene-icons.log
```

Expected: `Build` logs no new errors (the only pre-existing expected error is the unrelated `LoadThemedSprites: sprite at index 2 for event in Assets/Art/PanelArt` warning from milestone #14 -- confirm it's a warning, not an error, per that milestone's fix). `Verify` ends with `Verify: scene opened and controller found successfully.` and no errors at all.

- [ ] **Step 8: Run the full test suite (EditMode + PlayMode)**

```bash
cd /c/Users/rajes/understudy-kingdom
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-icons-task2-edit.xml" -logFile "C:\Users\rajes\understudy-kingdom\unity-icons-task2-edit.log"
grep -o '<test-run[^>]*total="[0-9]*"[^>]*passed="[0-9]*"[^>]*failed="[0-9]*"' test-results-icons-task2-edit.xml

curl -s http://localhost:3000/health || (nohup npm --prefix server run dev > /dev/null 2>&1 & disown; sleep 3; curl -s http://localhost:3000/health)
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-icons-task2-play.xml" -logFile "C:\Users\rajes\understudy-kingdom\unity-icons-task2-play.log"
grep -o '<test-run[^>]*total="[0-9]*"[^>]*passed="[0-9]*"[^>]*failed="[0-9]*"' test-results-icons-task2-play.xml
```

Run these as plain foreground Bash calls and let them block until they finish -- do NOT use `run_in_background` or a `Monitor`/polling loop for these commands; call them directly and wait for the result before proceeding to the next step.

Expected: `failed="0"` on both, including the new `LoadedCoreLoopScene_ButtonIcons_HaveNonNullSpritesOnLoad` test.

- [ ] **Step 9: Commit**

```bash
git add Assets/Editor/CoreLoopSceneBuilder.cs Assets/Tests/PlayMode/CoreLoopSceneTests.cs Assets/Scenes/CoreLoop.unity
git commit -m "feat: load the 4 real button icons into the built scene"
```

After committing, run `git status` and confirm nothing untracked/stray needs attention (e.g. `.meta` files Unity generated for `Assets/Art/ButtonIcons/*.png` on first import -- if any are untracked, stage and commit them in a follow-up `chore:` commit, matching exactly how the panel-art milestone's Task 2 handled the same situation).
