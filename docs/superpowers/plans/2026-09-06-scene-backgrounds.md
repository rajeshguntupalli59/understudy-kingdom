# Themed Scene Backgrounds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the CoreLoop screen a full-screen painted throne-room background that changes with the player's selected Cosmetics theme (Default/Council Chamber/Harvest Hall), using the 3 AI-generated images already committed at `Assets/Art/Backgrounds/`.

**Architecture:** `CosmeticsPanelController` already owns theme-switching (`ApplyTheme(themeId)` recolors the 3 panel interiors); it gains one more field pair (`Image sceneBackgroundImage`, `Sprite[] backgroundSprites`) and one more line in `ApplyTheme` that swaps the background sprite using the same theme-id lookup `GetThemeColor` already uses. `CoreLoopSceneBuilder.Build()` creates a full-screen `Image` as the scene's first canvas child (so it renders behind everything else) and loads the 3 sprites.

**Tech Stack:** Unity 6000.3.23f1 (C#), NUnit (PlayMode tests), existing `UnderstudyKingdom.Runtime` / `.PlayModeTests` asmdef split.

## Global Constraints

- Match this project's established `Initialize(...)`-args dependency-injection convention (trailing parameters).
- Reuse the existing `Themes` array (`CosmeticsPanelController.cs`) as the lookup key for backgrounds -- index-aligned `Sprite[3]`, same order as `Themes` (index 0 = Default, 1 = Council, 2 = Event). No new tier-mapping scheme.
- No placeholder/TBD content anywhere -- every step below is real, complete code.
- The 3 source images already exist and are committed at
  `Assets/Art/Backgrounds/background_default.png`,
  `Assets/Art/Backgrounds/background_council.png`,
  `Assets/Art/Backgrounds/background_event.png`. This plan does not
  create or modify the images themselves.
- Server (`server/`) is untouched by this plan.
- Learn from the ruler-portrait feature's final review (which found a
  silently-failing sprite loader with no error logging and a `Verify()`
  that didn't check the loaded array): this plan bakes the error logging,
  `Verify()` check, and regression test in from the start rather than as
  a post-review fix.
- Full regression after every task: run Unity EditMode and PlayMode
  suites via `-runTests`. Server tests are unaffected by this plan, skip
  them.

---

### Task 1: Wire the background into CosmeticsPanelController and every call site

This task changes `CosmeticsPanelController.Initialize(...)`'s signature,
which is compile-atomic across the 3 files that call it (Unity compiles
the whole project together). All edits in this task land together
before the project compiles again.

**Files:**
- Modify: `Assets/Scripts/UI/CosmeticsPanelController.cs`
- Modify: `Assets/Editor/CoreLoopSceneBuilder.cs:63-67` (new full-screen
  Image inserted before the sliders; call site updated; placeholder
  `new Sprite[3]` array -- real loading is Task 2)
- Modify: `Assets/Tests/PlayMode/CosmeticsPanelControllerTests.cs` (both
  call sites, a new `CreateDummySprite` helper, and new assertions in
  the two existing tests that already check panel colors)

**Interfaces:**
- Produces: `CosmeticsPanelController.Initialize(..., Image
  sceneBackgroundImage, Sprite[] backgroundSprites)` -- the two new
  trailing parameters every caller must supply. `backgroundSprites` must
  be a 3-element array in the same order as the existing `Themes` array
  (index 0 = Default, 1 = Council, 2 = Event); a null element at the
  selected index falls back to index 0 (Default).

- [ ] **Step 1: Update `CosmeticsPanelController.cs`'s fields, `Initialize`, and `ApplyTheme`**

Change:

```csharp
        [SerializeField] private Button eventsButton;
        [SerializeField] private DuelModalGate gate;

        private void Start()
```

to:

```csharp
        [SerializeField] private Button eventsButton;
        [SerializeField] private DuelModalGate gate;
        [SerializeField] private Image sceneBackgroundImage;
        [SerializeField] private Sprite[] backgroundSprites;

        private void Start()
```

Change:

```csharp
        public void Initialize(
            Button customizeButton,
            GameObject panelRoot,
            Button closeButton,
            TextMeshProUGUI[] statusLabels,
            Button[] applyButtons,
            Image eventPanelImage,
            Image councilPanelImage,
            Image historyPanelImage,
            DecisionCycleManager manager,
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button submitButton,
            Button challengeButton,
            Button viewHistoryButton,
            Button councilButton,
            Button eventsButton,
            DuelModalGate gate)
        {
            this.customizeButton = customizeButton;
            this.panelRoot = panelRoot;
            this.closeButton = closeButton;
            this.statusLabels = statusLabels;
            this.applyButtons = applyButtons;
            this.eventPanelImage = eventPanelImage;
            this.councilPanelImage = councilPanelImage;
            this.historyPanelImage = historyPanelImage;
            this.manager = manager;
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.submitButton = submitButton;
            this.challengeButton = challengeButton;
            this.viewHistoryButton = viewHistoryButton;
            this.councilButton = councilButton;
            this.eventsButton = eventsButton;
            this.gate = gate;

            Bind();
        }
```

to:

```csharp
        public void Initialize(
            Button customizeButton,
            GameObject panelRoot,
            Button closeButton,
            TextMeshProUGUI[] statusLabels,
            Button[] applyButtons,
            Image eventPanelImage,
            Image councilPanelImage,
            Image historyPanelImage,
            DecisionCycleManager manager,
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button submitButton,
            Button challengeButton,
            Button viewHistoryButton,
            Button councilButton,
            Button eventsButton,
            DuelModalGate gate,
            Image sceneBackgroundImage,
            Sprite[] backgroundSprites)
        {
            this.customizeButton = customizeButton;
            this.panelRoot = panelRoot;
            this.closeButton = closeButton;
            this.statusLabels = statusLabels;
            this.applyButtons = applyButtons;
            this.eventPanelImage = eventPanelImage;
            this.councilPanelImage = councilPanelImage;
            this.historyPanelImage = historyPanelImage;
            this.manager = manager;
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.submitButton = submitButton;
            this.challengeButton = challengeButton;
            this.viewHistoryButton = viewHistoryButton;
            this.councilButton = councilButton;
            this.eventsButton = eventsButton;
            this.gate = gate;
            this.sceneBackgroundImage = sceneBackgroundImage;
            this.backgroundSprites = backgroundSprites;

            Bind();
        }
```

Change:

```csharp
        private void ApplyTheme(string themeId)
        {
            Color color = GetThemeColor(themeId);
            eventPanelImage.color = color;
            councilPanelImage.color = color;
            historyPanelImage.color = color;
        }

        // Unrecognized ids (e.g. a future save-file edge case) resolve to
        // Default's color rather than throwing or leaving panels uncolored.
        private static Color GetThemeColor(string themeId)
        {
            foreach (ThemeDefinition theme in Themes)
            {
                if (theme.Id == themeId)
                {
                    return theme.PanelColor;
                }
            }

            return Themes[0].PanelColor;
        }
```

to:

```csharp
        private void ApplyTheme(string themeId)
        {
            Color color = GetThemeColor(themeId);
            eventPanelImage.color = color;
            councilPanelImage.color = color;
            historyPanelImage.color = color;
            sceneBackgroundImage.sprite = GetBackgroundSprite(themeId, backgroundSprites);
        }

        // Unrecognized ids (e.g. a future save-file edge case) resolve to
        // Default's color rather than throwing or leaving panels uncolored.
        private static Color GetThemeColor(string themeId)
        {
            foreach (ThemeDefinition theme in Themes)
            {
                if (theme.Id == themeId)
                {
                    return theme.PanelColor;
                }
            }

            return Themes[0].PanelColor;
        }

        // Same fallback shape as GetThemeColor -- a missing/null sprite for
        // an unrecognized or not-yet-loaded theme resolves to Default's
        // background (index 0) rather than leaving the background blank.
        private static Sprite GetBackgroundSprite(string themeId, Sprite[] sprites)
        {
            for (int i = 0; i < Themes.Length; i++)
            {
                if (Themes[i].Id == themeId)
                {
                    return sprites[i] != null ? sprites[i] : sprites[0];
                }
            }

            return sprites[0];
        }
```

- [ ] **Step 2: Update the scene builder: add the full-screen background Image and update the call site**

In `Assets/Editor/CoreLoopSceneBuilder.cs`, change:

```csharp
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            Slider armySlider = CreateSlider(canvasObject.transform, "ArmySlider", 40f, 40f);
```

to:

```csharp
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // Created before every other canvas child so it renders behind
            // all of them (Unity draws uGUI siblings in child order).
            var sceneBackgroundObject = new GameObject("SceneBackground", typeof(Image));
            sceneBackgroundObject.transform.SetParent(canvasObject.transform, false);
            var sceneBackgroundRect = sceneBackgroundObject.GetComponent<RectTransform>();
            sceneBackgroundRect.anchorMin = Vector2.zero;
            sceneBackgroundRect.anchorMax = Vector2.one;
            sceneBackgroundRect.offsetMin = Vector2.zero;
            sceneBackgroundRect.offsetMax = Vector2.zero;
            var sceneBackgroundImage = sceneBackgroundObject.GetComponent<Image>();
            sceneBackgroundImage.preserveAspect = false;
            // Real sprites are wired in Task 2 (LoadBackgroundSprites()) --
            // this task only proves the field/signature plumbing compiles.
            Sprite[] backgroundSprites = new Sprite[3];

            Slider armySlider = CreateSlider(canvasObject.transform, "ArmySlider", 40f, 40f);
```

Then change:

```csharp
            cosmeticsController.Initialize(customizeButton, cosmeticsPanelRootObject, cosmeticsCloseButton,
                themeStatusLabels, themeApplyButtons,
                eventPanelRootObject.GetComponent<Image>(), councilPanelRootObject.GetComponent<Image>(), panelRootObject.GetComponent<Image>(),
                manager, armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton, councilButton, eventsButton, duelModalGate);
```

to:

```csharp
            cosmeticsController.Initialize(customizeButton, cosmeticsPanelRootObject, cosmeticsCloseButton,
                themeStatusLabels, themeApplyButtons,
                eventPanelRootObject.GetComponent<Image>(), councilPanelRootObject.GetComponent<Image>(), panelRootObject.GetComponent<Image>(),
                manager, armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton, councilButton, eventsButton, duelModalGate,
                sceneBackgroundImage, backgroundSprites);
```

- [ ] **Step 3: Update `CosmeticsPanelControllerTests.cs`'s two call sites, add a dummy-sprite helper, and extend the two color-assertion tests**

Change:

```csharp
        private Image eventPanelImage;
        private Image councilPanelImage;
        private Image historyPanelImage;

        [SetUp]
```

to:

```csharp
        private Image eventPanelImage;
        private Image councilPanelImage;
        private Image historyPanelImage;
        private Image sceneBackgroundImage;
        private Sprite[] backgroundSprites;

        [SetUp]
```

Change:

```csharp
            gateObject = new GameObject("DuelModalGate");
            gate = gateObject.AddComponent<DuelModalGate>();

            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<CosmeticsPanelController>();
            controller.Initialize(customizeButton, panelRootObject, closeButton, statusLabels, applyButtons,
                eventPanelImage, councilPanelImage, historyPanelImage, manager,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, councilButton, eventsButton, gate);
        }
```

to:

```csharp
            gateObject = new GameObject("DuelModalGate");
            gate = gateObject.AddComponent<DuelModalGate>();

            var sceneBackgroundObject = new GameObject("SceneBackground", typeof(Image));
            sceneBackgroundObject.transform.SetParent(canvasObject.transform, false);
            sceneBackgroundImage = sceneBackgroundObject.GetComponent<Image>();

            backgroundSprites = new Sprite[3];
            for (int i = 0; i < backgroundSprites.Length; i++)
            {
                backgroundSprites[i] = CreateDummySprite();
            }

            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<CosmeticsPanelController>();
            controller.Initialize(customizeButton, panelRootObject, closeButton, statusLabels, applyButtons,
                eventPanelImage, councilPanelImage, historyPanelImage, manager,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, councilButton, eventsButton, gate,
                sceneBackgroundImage, backgroundSprites);
        }

        private static Sprite CreateDummySprite()
        {
            var texture = new Texture2D(1, 1);
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        }
```

Change:

```csharp
        [Test]
        public void ApplyTheme_Unlocked_RecolorsAllThreePanelsAndPersists()
        {
            ruler.State.CouncilRewardApplied = true;
            customizeButton.onClick.Invoke();

            applyButtons[1].onClick.Invoke();

            Color expected = new Color(0.22f, 0.08f, 0.16f, 0.95f);
            Assert.AreEqual(expected, eventPanelImage.color);
            Assert.AreEqual(expected, councilPanelImage.color);
            Assert.AreEqual(expected, historyPanelImage.color);
            Assert.AreEqual("Council", ruler.State.SelectedTheme);

            RulerState persisted = SaveService.Load();
            Assert.AreEqual("Council", persisted.SelectedTheme);
        }
```

to:

```csharp
        [Test]
        public void ApplyTheme_Unlocked_RecolorsAllThreePanelsAndPersists()
        {
            ruler.State.CouncilRewardApplied = true;
            customizeButton.onClick.Invoke();

            applyButtons[1].onClick.Invoke();

            Color expected = new Color(0.22f, 0.08f, 0.16f, 0.95f);
            Assert.AreEqual(expected, eventPanelImage.color);
            Assert.AreEqual(expected, councilPanelImage.color);
            Assert.AreEqual(expected, historyPanelImage.color);
            Assert.AreSame(backgroundSprites[1], sceneBackgroundImage.sprite);
            Assert.AreEqual("Council", ruler.State.SelectedTheme);

            RulerState persisted = SaveService.Load();
            Assert.AreEqual("Council", persisted.SelectedTheme);
        }
```

Change:

```csharp
            var freshControllerObject = new GameObject("FreshController");
            var freshController = freshControllerObject.AddComponent<CosmeticsPanelController>();
            freshController.Initialize(customizeButton, panelRootObject, closeButton, statusLabels, applyButtons,
                eventPanelImage, councilPanelImage, historyPanelImage,
                managerObject.GetComponent<DecisionCycleManager>(),
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, councilButton, eventsButton, gate);

            Color expected = new Color(0.22f, 0.08f, 0.16f, 0.95f);
            Assert.AreEqual(expected, eventPanelImage.color);
            Assert.AreEqual(expected, councilPanelImage.color);
            Assert.AreEqual(expected, historyPanelImage.color);

            Object.DestroyImmediate(freshControllerObject);
        }
```

to:

```csharp
            var freshControllerObject = new GameObject("FreshController");
            var freshController = freshControllerObject.AddComponent<CosmeticsPanelController>();
            freshController.Initialize(customizeButton, panelRootObject, closeButton, statusLabels, applyButtons,
                eventPanelImage, councilPanelImage, historyPanelImage,
                managerObject.GetComponent<DecisionCycleManager>(),
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, councilButton, eventsButton, gate,
                sceneBackgroundImage, backgroundSprites);

            Color expected = new Color(0.22f, 0.08f, 0.16f, 0.95f);
            Assert.AreEqual(expected, eventPanelImage.color);
            Assert.AreEqual(expected, councilPanelImage.color);
            Assert.AreEqual(expected, historyPanelImage.color);
            Assert.AreSame(backgroundSprites[1], sceneBackgroundImage.sprite);

            Object.DestroyImmediate(freshControllerObject);
        }
```

- [ ] **Step 4: Run the full Unity test suite**

```bash
cd /c/Users/rajes/understudy-kingdom
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-task1-edit.xml" -logFile "C:\Users\rajes\understudy-kingdom\unity-task1-edit.log"
grep -o '<test-run[^>]*total="[0-9]*"[^>]*passed="[0-9]*"[^>]*failed="[0-9]*"' test-results-task1-edit.xml
```
Expected: `failed="0"`, same EditMode count as before this task (this task touches no EditMode tests).

Then start the local server (only needed for real-data PlayMode tests elsewhere in the suite -- this task's own tests don't need it, but the full suite includes them) and run PlayMode:
```bash
curl -s http://localhost:3000/health || (nohup npm --prefix server run dev > /dev/null 2>&1 & disown; sleep 3; curl -s http://localhost:3000/health)
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-task1-play.xml" -logFile "C:\Users\rajes\understudy-kingdom\unity-task1-play.log"
grep -o '<test-run[^>]*total="[0-9]*"[^>]*passed="[0-9]*"[^>]*failed="[0-9]*"' test-results-task1-play.xml
```
Expected: `failed="0"`, same PlayMode count as before this task (no new tests added yet, only new assertions in existing tests).

- [ ] **Step 5: Clean up and commit**

```bash
cd /c/Users/rajes/understudy-kingdom
rm -f test-results-task1-*.xml unity-task1-*.log
git add Assets/Scripts/UI/CosmeticsPanelController.cs Assets/Editor/CoreLoopSceneBuilder.cs Assets/Tests/PlayMode/CosmeticsPanelControllerTests.cs
git commit -m "$(cat <<'EOF'
feat: wire scene background Image into CosmeticsPanelController

Initialize(...) gains trailing Image/Sprite[] parameters, matching this
project's established DI convention. ApplyTheme() now also swaps the
scene background sprite using the same theme-id lookup GetThemeColor
already uses, falling back to Default (index 0) for a missing/null
sprite. Real scene wiring uses a placeholder empty Sprite[3] for now
(background renders blank in the actual built scene) -- Task 2 loads
the real 3 committed backgrounds.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01QMEErAfieqNAbk8ZRAFBwT
EOF
)"
```

---

### Task 2: Load the real backgrounds into the scene

**Files:**
- Modify: `Assets/Editor/CoreLoopSceneBuilder.cs`
- Modify: `Assets/Tests/PlayMode/CoreLoopSceneTests.cs`

**Interfaces:**
- Consumes: `CosmeticsPanelController.Initialize(..., Image, Sprite[])`
  from Task 1 (same call site, only the array's *value* changes).
- Produces: nothing new for later tasks -- this is the terminal task of
  the plan.

- [ ] **Step 1: Add the sprite-loading helpers**

In `Assets/Editor/CoreLoopSceneBuilder.cs`, add these two methods next
to `LoadPortraitSprite`/`LoadRulerPortraits` (search for them -- they
were added by the ruler-portrait feature and this mirrors their shape
exactly, including the same texture-import-type fix, since these images
too were added as raw files):

```csharp
        private static Sprite LoadBackgroundSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Order matches the existing Themes array exactly (Default, Council,
        // Event) -- CosmeticsPanelController.GetBackgroundSprite indexes into
        // this array using the same Themes array as its lookup key.
        private static Sprite[] LoadBackgroundSprites()
        {
            string[] themeIds = { "default", "council", "event" };
            var sprites = new Sprite[3];
            for (int i = 0; i < themeIds.Length; i++)
            {
                string path = $"Assets/Art/Backgrounds/background_{themeIds[i]}.png";
                sprites[i] = LoadBackgroundSprite(path);
            }
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null)
                {
                    Debug.LogError($"CoreLoopSceneBuilder.LoadBackgroundSprites: failed to load background sprite at index {i}");
                }
            }
            return sprites;
        }
```

- [ ] **Step 2: Use the real loader in `Build()`**

Change:

```csharp
            // Real sprites are wired in Task 2 (LoadBackgroundSprites()) --
            // this task only proves the field/signature plumbing compiles.
            Sprite[] backgroundSprites = new Sprite[3];
```

to:

```csharp
            Sprite[] backgroundSprites = LoadBackgroundSprites();
```

- [ ] **Step 3: Add a `Verify()` check for the background array**

In `Assets/Editor/CoreLoopSceneBuilder.cs`'s `Verify()` method, find the
existing `cosmeticsController` check:

```csharp
            var cosmeticsController = Object.FindFirstObjectByType<CosmeticsPanelController>();
            if (cosmeticsController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no CosmeticsPanelController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            Debug.Log("CoreLoopSceneBuilder.Verify: scene opened and controller found successfully.");
        }
```

Replace it with (adds the background-array check right after the
existing null-controller guard, before the final success log):

```csharp
            var cosmeticsController = Object.FindFirstObjectByType<CosmeticsPanelController>();
            if (cosmeticsController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no CosmeticsPanelController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            FieldInfo backgroundSpritesField = typeof(CosmeticsPanelController).GetField("backgroundSprites", BindingFlags.NonPublic | BindingFlags.Instance);
            if (backgroundSpritesField == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: CosmeticsPanelController has no private backgroundSprites field (renamed?).");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            var backgroundSprites = (Sprite[])backgroundSpritesField.GetValue(cosmeticsController);
            if (backgroundSprites == null || backgroundSprites.Length != 3)
            {
                Debug.LogError($"CoreLoopSceneBuilder.Verify: expected a 3-element backgroundSprites array, found {(backgroundSprites == null ? "null" : backgroundSprites.Length.ToString())}.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            for (int j = 0; j < backgroundSprites.Length; j++)
            {
                if (backgroundSprites[j] == null)
                {
                    Debug.LogError($"CoreLoopSceneBuilder.Verify: backgroundSprites[{j}] is null.");
                    if (Application.isBatchMode)
                    {
                        EditorApplication.Exit(1);
                    }
                    return;
                }
            }

            Debug.Log("CoreLoopSceneBuilder.Verify: scene opened and controller found successfully.");
        }
```

(This mirrors the existing `rulerPortraits` check earlier in the same
method exactly, including guarding `GetField`'s result against null
before using it -- a gap the ruler-portrait feature's own equivalent
check had and was flagged as a Minor finding in its final review.)

- [ ] **Step 4: Add a PlayMode regression test**

In `Assets/Tests/PlayMode/CoreLoopSceneTests.cs`, add this test right
after `LoadedCoreLoopScene_Portrait_HasNonNullSpriteAfterSubmit` (uses
the file's existing `FindChildByName` helper):

```csharp
        /// <summary>
        /// Regression guard mirroring
        /// LoadedCoreLoopScene_Portrait_HasNonNullSpriteAfterSubmit above: a
        /// fully-non-null-array-but-all-elements-null backgroundSprites state
        /// wouldn't throw on its own, so this loads the real scene and
        /// asserts the scene background actually has a sprite.
        /// CosmeticsPanelController.ApplyTheme() runs from Bind() on scene
        /// load, so no click is needed to observe it.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadedCoreLoopScene_SceneBackground_HasNonNullSpriteOnLoad()
        {
            yield return SceneManager.LoadSceneAsync("CoreLoop");
            yield return null;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Canvas not found in the loaded CoreLoop scene.");

            GameObject backgroundObject = FindChildByName(canvas.transform, "SceneBackground");
            Assert.IsNotNull(backgroundObject, "SceneBackground not found in the loaded CoreLoop scene.");
            Image backgroundImage = backgroundObject.GetComponent<Image>();
            Assert.IsNotNull(backgroundImage, "SceneBackground has no Image component.");

            Assert.IsNotNull(backgroundImage.sprite,
                "Expected the scene background to have a non-null sprite after scene load.");
        }
```

- [ ] **Step 5: Rebuild and verify the scene**

```bash
cd /c/Users/rajes/understudy-kingdom
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Build -logFile "C:\Users\rajes\understudy-kingdom\build-scene-task2.log" -quit
tail -20 build-scene-task2.log
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Verify -logFile "C:\Users\rajes\understudy-kingdom\verify-scene-task2.log" -quit
grep -i "error\|Verify:" verify-scene-task2.log
```
Expected: no `error CS` lines in either log; `verify-scene-task2.log`
contains `CoreLoopSceneBuilder.Verify: scene opened and controller found
successfully.`; `git diff --stat -- Assets/Scenes/CoreLoop.unity` shows
the scene file changed.

Also confirm all 3 PNGs got reimported as Sprites:
```bash
grep -l "textureType: 8" Assets/Art/Backgrounds/*.png.meta | wc -l
```
Expected: `3`.

- [ ] **Step 6: Run the full regression suite one more time**

```bash
cd /c/Users/rajes/understudy-kingdom
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-task2-edit.xml" -logFile "C:\Users\rajes\understudy-kingdom\unity-task2-edit.log"
grep -o '<test-run[^>]*total="[0-9]*"[^>]*passed="[0-9]*"[^>]*failed="[0-9]*"' test-results-task2-edit.xml

curl -s http://localhost:3000/health || (nohup npm --prefix server run dev > /dev/null 2>&1 & disown; sleep 3; curl -s http://localhost:3000/health)
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-task2-play.xml" -logFile "C:\Users\rajes\understudy-kingdom\unity-task2-play.log"
grep -o '<test-run[^>]*total="[0-9]*"[^>]*passed="[0-9]*"[^>]*failed="[0-9]*"' test-results-task2-play.xml
```
Expected: `failed="0"` on both; PlayMode total is 1 higher than Task 1's
(the new `LoadedCoreLoopScene_SceneBackground_HasNonNullSpriteOnLoad`
test).

- [ ] **Step 7: Clean up and commit**

```bash
cd /c/Users/rajes/understudy-kingdom
rm -f test-results-task2-*.xml unity-task2-*.log build-scene-task2.log verify-scene-task2.log
git add Assets/Editor/CoreLoopSceneBuilder.cs Assets/Scenes/CoreLoop.unity Assets/Tests/PlayMode/CoreLoopSceneTests.cs Assets/Art/Backgrounds/*.meta
git commit -m "$(cat <<'EOF'
feat: load the 3 real scene backgrounds into the built scene

CoreLoopSceneBuilder now fixes the background PNGs' texture import type
(same fix as the ruler-portrait feature -- raw files default to
Texture2D, not Sprite) and loads them in Themes-array order. Verify()
gains a check for the loaded array (3 non-null entries), and a new
PlayMode test confirms the real scene's background has a sprite after
load. Both additions apply the exact lessons from the ruler-portrait
feature's final review (silent loader failures, an under-checked
Verify()) from the start instead of as a follow-up fix.

Full regression green: Unity EditMode and PlayMode both 0 failures.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01QMEErAfieqNAbk8ZRAFBwT
EOF
)"
```
