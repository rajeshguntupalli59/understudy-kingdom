# Ruler Portrait System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the ruler NPC a painted portrait that visibly reacts to Mood and Loyalty, using the 15 AI-generated images already committed at `Assets/Art/RulerPortraits/`.

**Architecture:** Two pure `private static` tier-mapping functions map Mood/Loyalty (0-100) to a 5x3 matrix index; `CoreLoopScreenController` (which already owns the single Mood/Loyalty refresh point) swaps an `Image.sprite` using that index every time `RefreshStatusLabels()` runs; `CoreLoopSceneBuilder.Build()` wires the 15 sprites into the real scene, fixing their texture import type from Default to Sprite along the way since they were added to the project as raw files, not through Unity's asset pipeline.

**Tech Stack:** Unity 6000.3.23f1 (C#), NUnit (EditMode + PlayMode tests), existing `UnderstudyKingdom.Runtime` / `.EditModeTests` / `.PlayModeTests` asmdef split.

## Global Constraints

- Match this project's established `Initialize(...)`-args dependency-injection convention (trailing parameters, no constructor injection, no `[Inject]` attributes).
- Private members stay private; cross-assembly EditMode tests reach them via reflection (`BindingFlags.NonPublic`), matching the existing `EventPanelControllerTests.InvokeHandleResult` precedent -- do not add `internal`/`InternalsVisibleTo` as a shortcut.
- No placeholder/TBD content anywhere -- every step below is real, complete code.
- The 15 source images already exist at `Assets/Art/RulerPortraits/ruler_<mood>_<loyalty>.png` (mood in `furious|displeased|neutral|pleased|delighted`, loyalty in `low|medium|high`) and are already committed. This plan does not create or modify the images themselves.
- Server (`server/`) is untouched by this plan -- purely a Unity/C# change.
- Full regression after every task: `server/` tests are unaffected so skip them; run Unity EditMode and PlayMode suites via `-runTests`.

---

### Task 1: Mood/Loyalty tier-mapping logic

**Files:**
- Modify: `Assets/Scripts/UI/CoreLoopScreenController.cs`
- Create: `Assets/Tests/EditMode/RulerPortraitMappingTests.cs`

**Interfaces:**
- Produces: `CoreLoopScreenController.GetMoodTier(int mood) -> int` (private static, returns 0-4), `CoreLoopScreenController.GetLoyaltyTier(int loyalty) -> int` (private static, returns 0-2). Task 2 combines them as `GetMoodTier(mood) * 3 + GetLoyaltyTier(loyalty)` to index a 15-element mood-major array (index 0 = Furious/Low ... index 7 = Neutral/Medium ... index 14 = Delighted/High).

- [ ] **Step 1: Write the failing EditMode test**

Create `Assets/Tests/EditMode/RulerPortraitMappingTests.cs`:

```csharp
using System.Reflection;
using NUnit.Framework;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    public class RulerPortraitMappingTests
    {
        private static int InvokeGetMoodTier(int mood)
        {
            MethodInfo method = typeof(CoreLoopScreenController).GetMethod(
                "GetMoodTier", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "GetMoodTier method not found -- CoreLoopScreenController internals changed");
            return (int)method.Invoke(null, new object[] { mood });
        }

        private static int InvokeGetLoyaltyTier(int loyalty)
        {
            MethodInfo method = typeof(CoreLoopScreenController).GetMethod(
                "GetLoyaltyTier", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "GetLoyaltyTier method not found -- CoreLoopScreenController internals changed");
            return (int)method.Invoke(null, new object[] { loyalty });
        }

        [Test]
        public void GetMoodTier_BoundaryValues_MapToCorrectTier()
        {
            Assert.AreEqual(0, InvokeGetMoodTier(0));
            Assert.AreEqual(0, InvokeGetMoodTier(20));
            Assert.AreEqual(1, InvokeGetMoodTier(21));
            Assert.AreEqual(1, InvokeGetMoodTier(40));
            Assert.AreEqual(2, InvokeGetMoodTier(41));
            Assert.AreEqual(2, InvokeGetMoodTier(60));
            Assert.AreEqual(3, InvokeGetMoodTier(61));
            Assert.AreEqual(3, InvokeGetMoodTier(80));
            Assert.AreEqual(4, InvokeGetMoodTier(81));
            Assert.AreEqual(4, InvokeGetMoodTier(100));
        }

        [Test]
        public void GetLoyaltyTier_BoundaryValues_MapToCorrectTier()
        {
            Assert.AreEqual(0, InvokeGetLoyaltyTier(0));
            Assert.AreEqual(0, InvokeGetLoyaltyTier(33));
            Assert.AreEqual(1, InvokeGetLoyaltyTier(34));
            Assert.AreEqual(1, InvokeGetLoyaltyTier(66));
            Assert.AreEqual(2, InvokeGetLoyaltyTier(67));
            Assert.AreEqual(2, InvokeGetLoyaltyTier(100));
        }

        [Test]
        public void CombinedIndex_MoodTierTimesThreePlusLoyaltyTier_MatchesMoodMajorOrder()
        {
            // index 0 = Furious/Low, index 7 = Neutral/Medium, index 14 = Delighted/High
            Assert.AreEqual(0, InvokeGetMoodTier(0) * 3 + InvokeGetLoyaltyTier(0));
            Assert.AreEqual(7, InvokeGetMoodTier(50) * 3 + InvokeGetLoyaltyTier(50));
            Assert.AreEqual(14, InvokeGetMoodTier(100) * 3 + InvokeGetLoyaltyTier(100));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-task1.xml" -logFile "C:\Users\rajes\understudy-kingdom\unity-task1.log"
```
Expected: FAIL. `grep -i "error CS" unity-task1.log` shows `CS0117` or similar ("does not contain a definition for 'GetMoodTier'") from the reflection call's `Assert.IsNotNull` firing at runtime, or a null-reference on the `Invoke` call -- either way, `RulerPortraitMappingTests` fails, nothing else does.

- [ ] **Step 3: Implement the tier-mapping functions**

In `Assets/Scripts/UI/CoreLoopScreenController.cs`, add these two methods immediately after the closing brace of `RefreshStatusLabels()` (currently the last method in the class, ending at line 119):

```csharp
        private static int GetMoodTier(int mood)
        {
            if (mood <= 20) return 0; // Furious
            if (mood <= 40) return 1; // Displeased
            if (mood <= 60) return 2; // Neutral
            if (mood <= 80) return 3; // Pleased
            return 4;                 // Delighted
        }

        private static int GetLoyaltyTier(int loyalty)
        {
            if (loyalty <= 33) return 0; // Low
            if (loyalty <= 66) return 1; // Medium
            return 2;                    // High
        }
```

- [ ] **Step 4: Run the test to verify it passes**

Run the same command as Step 2 (or delete `test-results-task1.xml`/`unity-task1.log` first, they're gitignored). Expected: PASS, `testcasecount` for `RulerPortraitMappingTests` shows 3 passed, 0 failed. Also confirm nothing else regressed: `grep -o '<test-run[^>]*total="[0-9]*"[^>]*passed="[0-9]*"[^>]*failed="[0-9]*"' test-results-task1.xml` shows `failed="0"` for the whole suite.

- [ ] **Step 5: Clean up and commit**

```bash
cd /c/Users/rajes/understudy-kingdom
rm -f test-results-task1.xml unity-task1.log
git add Assets/Scripts/UI/CoreLoopScreenController.cs Assets/Tests/EditMode/RulerPortraitMappingTests.cs Assets/Tests/EditMode/RulerPortraitMappingTests.cs.meta
git commit -m "$(cat <<'EOF'
feat: add ruler portrait mood/loyalty tier-mapping logic

Pure GetMoodTier/GetLoyaltyTier functions on CoreLoopScreenController,
covered by boundary-value EditMode tests via the existing
reflection-based private-method testing convention. Not wired into
anything yet -- see docs/superpowers/plans/2026-09-06-ruler-portrait-system.md
Task 2 for the actual portrait swap.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01QMEErAfieqNAbk8ZRAFBwT
EOF
)"
```

(Note: `Assets/Tests/EditMode/RulerPortraitMappingTests.cs.meta` is auto-generated by Unity the next time the Editor imports assets -- if it doesn't exist yet when you run `git add`, run a quick `Unity.exe -batchmode -projectPath ... -quit` first to force import, then retry the `git add`.)

---

### Task 2: Wire the portrait into CoreLoopScreenController and every call site

This task changes `CoreLoopScreenController.Initialize(...)`'s signature, which is a compile-atomic change across 6 files (Unity compiles the whole project together, so there is no way to change the signature and run tests against only some updated call sites). All edits in this task must land together before the project compiles again.

**Files:**
- Modify: `Assets/Scripts/UI/CoreLoopScreenController.cs`
- Modify: `Assets/Editor/CoreLoopSceneBuilder.cs:90-93` (call site + new placeholder Image; real sprite loading is Task 3)
- Modify: `Assets/Tests/PlayMode/CoreLoopScreenControllerTests.cs` (call site + new portrait tests)
- Modify: `Assets/Tests/PlayMode/CouncilPanelControllerTests.cs:76-79` (call site only)
- Modify: `Assets/Tests/PlayMode/CouncilPanelControllerRealDataTests.cs:99-102` (call site only)
- Modify: `Assets/Tests/PlayMode/EventPanelControllerTests.cs:76-78` (call site only)
- Modify: `Assets/Tests/PlayMode/EventPanelControllerRealDataTests.cs:99-102` (call site only)

**Interfaces:**
- Consumes: `GetMoodTier(int) -> int`, `GetLoyaltyTier(int) -> int` from Task 1 (same class, no signature change to those).
- Produces: `CoreLoopScreenController.Initialize(..., Image rulerPortraitImage, Sprite[] rulerPortraits)` -- the two new trailing parameters every caller must supply. `rulerPortraits` must be a 15-element array in mood-major order (index = `GetMoodTier(mood) * 3 + GetLoyaltyTier(loyalty)`); a null element at the selected index falls back to index 7 (Neutral/Medium).

- [ ] **Step 1: Update `CoreLoopScreenController.cs`'s fields, `Initialize`, and `RefreshStatusLabels`**

In `Assets/Scripts/UI/CoreLoopScreenController.cs`, change:

```csharp
        [SerializeField] private Button submitButton;

        private bool rebalancing;
```

to:

```csharp
        [SerializeField] private Button submitButton;
        [SerializeField] private Image rulerPortraitImage;
        [SerializeField] private Sprite[] rulerPortraits;

        private bool rebalancing;
```

Change:

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
            Button submitButton)
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

            Bind();
        }
```

to:

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
            Sprite[] rulerPortraits)
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

            Bind();
        }
```

Change:

```csharp
        public void RefreshStatusLabels()
        {
            moodLabel.text = $"Mood: {manager.Ruler.State.Mood}";
            loyaltyLabel.text = $"Loyalty: {manager.Ruler.State.Loyalty}";
            agendaLabel.text = $"Agenda: {manager.Ruler.State.Agenda}";
        }
```

to:

```csharp
        public void RefreshStatusLabels()
        {
            moodLabel.text = $"Mood: {manager.Ruler.State.Mood}";
            loyaltyLabel.text = $"Loyalty: {manager.Ruler.State.Loyalty}";
            agendaLabel.text = $"Agenda: {manager.Ruler.State.Agenda}";

            // Neutral/Medium (index 7) is the fallback for a missing slot --
            // matches CosmeticsPanelController.GetThemeColor's fallback to the
            // Default theme rather than leaving a UI element blank/unset.
            int portraitIndex = GetMoodTier(manager.Ruler.State.Mood) * 3 + GetLoyaltyTier(manager.Ruler.State.Loyalty);
            rulerPortraitImage.sprite = rulerPortraits[portraitIndex] != null ? rulerPortraits[portraitIndex] : rulerPortraits[7];
        }
```

- [ ] **Step 2: Update the scene builder's call site with a real (but art-less) portrait Image**

In `Assets/Editor/CoreLoopSceneBuilder.cs`, change:

```csharp
            TextMeshProUGUI moodLabel = CreateLabel(canvasObject.transform, "MoodLabel", 200f, "Mood: 50");
            TextMeshProUGUI loyaltyLabel = CreateLabel(canvasObject.transform, "LoyaltyLabel", 240f, "Loyalty: 50");
            TextMeshProUGUI agendaLabel = CreateLabel(canvasObject.transform, "AgendaLabel", 280f, "Agenda: Expansionist");
            TextMeshProUGUI narrationText = CreateLabel(canvasObject.transform, "NarrationText", 340f, string.Empty);
```

to:

```csharp
            TextMeshProUGUI moodLabel = CreateLabel(canvasObject.transform, "MoodLabel", 200f, "Mood: 50");
            TextMeshProUGUI loyaltyLabel = CreateLabel(canvasObject.transform, "LoyaltyLabel", 240f, "Loyalty: 50");
            TextMeshProUGUI agendaLabel = CreateLabel(canvasObject.transform, "AgendaLabel", 280f, "Agenda: Expansionist");
            TextMeshProUGUI narrationText = CreateLabel(canvasObject.transform, "NarrationText", 340f, string.Empty);

            var rulerPortraitObject = new GameObject("RulerPortraitImage", typeof(Image));
            rulerPortraitObject.transform.SetParent(canvasObject.transform, false);
            var rulerPortraitRect = rulerPortraitObject.GetComponent<RectTransform>();
            rulerPortraitRect.anchoredPosition = new Vector2(-250f, -170f);
            rulerPortraitRect.sizeDelta = new Vector2(200f, 260f);
            var rulerPortraitImage = rulerPortraitObject.GetComponent<Image>();
            rulerPortraitImage.preserveAspect = true;
            // Real sprites are wired in Task 3 (LoadRulerPortraits()) -- this
            // task only proves the field/signature plumbing compiles and
            // RefreshStatusLabels() doesn't throw on a real scene load.
            Sprite[] rulerPortraits = new Sprite[15];
```

Then change:

```csharp
            controller.Initialize(manager, armySlider, tradeSlider, religionSlider,
                moodLabel, loyaltyLabel, agendaLabel, narrationText, button);
```

to:

```csharp
            controller.Initialize(manager, armySlider, tradeSlider, religionSlider,
                moodLabel, loyaltyLabel, agendaLabel, narrationText, button,
                rulerPortraitImage, rulerPortraits);
```

- [ ] **Step 3: Update `CoreLoopScreenControllerTests.cs`'s call site, add a stored `controller` field, and add portrait tests**

In `Assets/Tests/PlayMode/CoreLoopScreenControllerTests.cs`, change:

```csharp
        private GameObject rulerObject;
        private GameObject managerObject;
        private GameObject controllerObject;
        private GameObject canvasObject;

        private DecisionCycleManager manager;
        private Slider armySlider;
        private Slider tradeSlider;
        private Slider religionSlider;
        private TextMeshProUGUI moodLabel;
        private TextMeshProUGUI loyaltyLabel;
        private TextMeshProUGUI agendaLabel;
        private TextMeshProUGUI narrationText;
        private Button submitButton;
```

to:

```csharp
        private GameObject rulerObject;
        private GameObject managerObject;
        private GameObject controllerObject;
        private GameObject canvasObject;

        private DecisionCycleManager manager;
        private Slider armySlider;
        private Slider tradeSlider;
        private Slider religionSlider;
        private TextMeshProUGUI moodLabel;
        private TextMeshProUGUI loyaltyLabel;
        private TextMeshProUGUI agendaLabel;
        private TextMeshProUGUI narrationText;
        private Button submitButton;
        private Image rulerPortraitImage;
        private Sprite[] rulerPortraits;
        private CoreLoopScreenController controller;
```

Change:

```csharp
            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<CoreLoopScreenController>();
            controller.Initialize(manager, armySlider, tradeSlider, religionSlider,
                moodLabel, loyaltyLabel, agendaLabel, narrationText, submitButton);
        }
```

to:

```csharp
            var rulerPortraitObject = new GameObject("RulerPortraitImage", typeof(Image));
            rulerPortraitObject.transform.SetParent(canvasObject.transform, false);
            rulerPortraitImage = rulerPortraitObject.GetComponent<Image>();

            rulerPortraits = new Sprite[15];
            for (int i = 0; i < rulerPortraits.Length; i++)
            {
                rulerPortraits[i] = CreateDummySprite();
            }

            controllerObject = new GameObject("Controller");
            controller = controllerObject.AddComponent<CoreLoopScreenController>();
            controller.Initialize(manager, armySlider, tradeSlider, religionSlider,
                moodLabel, loyaltyLabel, agendaLabel, narrationText, submitButton,
                rulerPortraitImage, rulerPortraits);
        }
```

Change:

```csharp
        private TextMeshProUGUI CreateLabel(string name)
        {
            var labelObject = new GameObject(name, typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(canvasObject.transform, false);
            return labelObject.GetComponent<TextMeshProUGUI>();
        }

        [Test]
        public void ChangingOneSlider_RebalancesOtherTwoToKeepSumAt100()
```

to:

```csharp
        private TextMeshProUGUI CreateLabel(string name)
        {
            var labelObject = new GameObject(name, typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(canvasObject.transform, false);
            return labelObject.GetComponent<TextMeshProUGUI>();
        }

        private static Sprite CreateDummySprite()
        {
            var texture = new Texture2D(1, 1);
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        }

        [Test]
        public void ChangingOneSlider_RebalancesOtherTwoToKeepSumAt100()
```

Then change:

```csharp
        [Test]
        public void Initialize_RendersInitialStatusLabelsImmediately()
        {
            Assert.AreEqual($"Mood: {manager.Ruler.State.Mood}", moodLabel.text);
            Assert.AreEqual($"Loyalty: {manager.Ruler.State.Loyalty}", loyaltyLabel.text);
            Assert.AreEqual($"Agenda: {manager.Ruler.State.Agenda}", agendaLabel.text);
        }
    }
}
```

to:

```csharp
        [Test]
        public void Initialize_RendersInitialStatusLabelsImmediately()
        {
            Assert.AreEqual($"Mood: {manager.Ruler.State.Mood}", moodLabel.text);
            Assert.AreEqual($"Loyalty: {manager.Ruler.State.Loyalty}", loyaltyLabel.text);
            Assert.AreEqual($"Agenda: {manager.Ruler.State.Agenda}", agendaLabel.text);
        }

        [Test]
        public void RefreshStatusLabels_SelectsPortraitMatchingMoodAndLoyaltyTier()
        {
            manager.Ruler.State.Mood = 90;
            manager.Ruler.State.Loyalty = 10;

            controller.RefreshStatusLabels();

            // Delighted (tier 4) x Low (tier 0) = index 4*3+0 = 12
            Assert.AreSame(rulerPortraits[12], rulerPortraitImage.sprite);
        }

        [Test]
        public void RefreshStatusLabels_WithMissingSelectedPortrait_FallsBackToNeutralMediumPortrait()
        {
            manager.Ruler.State.Mood = 90;
            manager.Ruler.State.Loyalty = 10;
            rulerPortraits[12] = null;

            controller.RefreshStatusLabels();

            Assert.AreSame(rulerPortraits[7], rulerPortraitImage.sprite);
        }
    }
}
```

- [ ] **Step 4: Update the four pass-through call sites**

`CouncilPanelControllerTests.cs`, `CouncilPanelControllerRealDataTests.cs`, `EventPanelControllerTests.cs`, and `EventPanelControllerRealDataTests.cs` each construct a `screenController` purely to hand to the panel controller under test -- they don't test portrait behavior, so a minimal non-null `Image` and an all-null `Sprite[15]` is correct here (the fallback-to-null in `RefreshStatusLabels` means `rulerPortraitImage.sprite` just ends up `null`, which is safe).

In **each** of these 4 files, change:

```csharp
            screenControllerObject = new GameObject("ScreenController");
            var screenController = screenControllerObject.AddComponent<CoreLoopScreenController>();
            screenController.Initialize(manager, armySlider, tradeSlider, religionSlider,
                moodLabel, loyaltyLabel, agendaLabel, narrationText, submitButton);
```

to:

```csharp
            screenControllerObject = new GameObject("ScreenController");
            var screenController = screenControllerObject.AddComponent<CoreLoopScreenController>();

            var rulerPortraitObject = new GameObject("RulerPortraitImage", typeof(Image));
            rulerPortraitObject.transform.SetParent(canvasObject.transform, false);
            var rulerPortraitImage = rulerPortraitObject.GetComponent<Image>();

            screenController.Initialize(manager, armySlider, tradeSlider, religionSlider,
                moodLabel, loyaltyLabel, agendaLabel, narrationText, submitButton,
                rulerPortraitImage, new Sprite[15]);
```

(All 4 files share this exact surrounding text today, confirmed by direct inspection -- apply the same edit to each independently.)

- [ ] **Step 5: Run the full Unity test suite**

```bash
cd /c/Users/rajes/understudy-kingdom
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-task2-edit.xml" -logFile "C:\Users\rajes\understudy-kingdom\unity-task2-edit.log"
grep -o '<test-run[^>]*total="[0-9]*"[^>]*passed="[0-9]*"[^>]*failed="[0-9]*"' test-results-task2-edit.xml
```
Expected: `failed="0"`, one more test than before Task 1 (RulerPortraitMappingTests already counted) -- EditMode count unaffected by Task 2 itself since these are all PlayMode changes.

Then start the local server (needed for any real-data PlayMode test) and run PlayMode:
```bash
nohup npm --prefix server run dev > /dev/null 2>&1 &
disown
sleep 3
curl -s http://localhost:3000/health
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-task2-play.xml" -logFile "C:\Users\rajes\understudy-kingdom\unity-task2-play.log"
grep -o '<test-run[^>]*total="[0-9]*"[^>]*passed="[0-9]*"[^>]*failed="[0-9]*"' test-results-task2-play.xml
```
Expected: `failed="0"`, total is 2 more than before Task 2 (the two new `CoreLoopScreenControllerTests` cases). If anything else fails, check whether the local dev server started in time (`curl http://localhost:3000/health` should print `{"status":"ok"}` before the test run starts).

- [ ] **Step 6: Clean up and commit**

```bash
cd /c/Users/rajes/understudy-kingdom
rm -f test-results-task2-*.xml unity-task2-*.log
git add Assets/Scripts/UI/CoreLoopScreenController.cs Assets/Editor/CoreLoopSceneBuilder.cs \
  Assets/Tests/PlayMode/CoreLoopScreenControllerTests.cs \
  Assets/Tests/PlayMode/CouncilPanelControllerTests.cs \
  Assets/Tests/PlayMode/CouncilPanelControllerRealDataTests.cs \
  Assets/Tests/PlayMode/EventPanelControllerTests.cs \
  Assets/Tests/PlayMode/EventPanelControllerRealDataTests.cs
git commit -m "$(cat <<'EOF'
feat: wire ruler portrait Image into CoreLoopScreenController

Initialize(...) gains trailing Image/Sprite[] parameters, matching this
project's established DI convention. RefreshStatusLabels() now swaps the
portrait sprite using Task 1's tier-mapping functions, falling back to
the Neutral/Medium slot (index 7) if the selected one is missing --
mirrors CosmeticsPanelController.GetThemeColor's fallback pattern.

Real scene wiring uses a placeholder empty Sprite[15] for now (portrait
renders blank in the actual built scene) -- Task 3 loads the real 15
committed portraits. This task is scoped to proving the field/signature
plumbing is correct and doesn't crash any existing real-scene test, per
the exact class of bug (null SerializeField surviving scene
deserialization) milestone #9's C-1 already hit once.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01QMEErAfieqNAbk8ZRAFBwT
EOF
)"
```

---

### Task 3: Load the real portraits into the scene

**Files:**
- Modify: `Assets/Editor/CoreLoopSceneBuilder.cs`

**Interfaces:**
- Consumes: `CoreLoopScreenController.Initialize(..., Image, Sprite[])` from Task 2 (same call site, only the array's *value* changes).
- Produces: nothing new for later tasks -- this is the terminal task of the plan.

- [ ] **Step 1: Add the sprite-loading helpers**

In `Assets/Editor/CoreLoopSceneBuilder.cs`, add these two methods next to `CreateLabel`/`CreateInputField` (after `CreateLabel`, which ends around line 725):

```csharp
        // The 15 portrait PNGs were added to the project as raw files (not
        // through Unity's asset-creation menu), so their TextureImporter
        // defaults to Default (Texture2D), not Sprite -- AssetDatabase.
        // LoadAssetAtPath<Sprite> silently returns null for a Default-typed
        // texture. This fixes the import type once (idempotent -- skips the
        // reimport if it's already correct) before loading.
        private static Sprite LoadPortraitSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Mood-major order matches CoreLoopScreenController.RefreshStatusLabels()'s
        // GetMoodTier(mood) * 3 + GetLoyaltyTier(loyalty) indexing exactly.
        private static Sprite[] LoadRulerPortraits()
        {
            string[] moods = { "furious", "displeased", "neutral", "pleased", "delighted" };
            string[] loyalties = { "low", "medium", "high" };
            var portraits = new Sprite[15];
            int i = 0;
            foreach (string mood in moods)
            {
                foreach (string loyalty in loyalties)
                {
                    string path = $"Assets/Art/RulerPortraits/ruler_{mood}_{loyalty}.png";
                    portraits[i] = LoadPortraitSprite(path);
                    i++;
                }
            }
            return portraits;
        }
```

- [ ] **Step 2: Use the real loader in `Build()`**

Change:

```csharp
            // Real sprites are wired in Task 3 (LoadRulerPortraits()) -- this
            // task only proves the field/signature plumbing compiles and
            // RefreshStatusLabels() doesn't throw on a real scene load.
            Sprite[] rulerPortraits = new Sprite[15];
```

to:

```csharp
            Sprite[] rulerPortraits = LoadRulerPortraits();
```

- [ ] **Step 3: Rebuild and verify the scene**

```bash
cd /c/Users/rajes/understudy-kingdom
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Build -logFile "C:\Users\rajes\understudy-kingdom\build-scene-task3.log" -quit
tail -20 build-scene-task3.log
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Verify -logFile "C:\Users\rajes\understudy-kingdom\verify-scene-task3.log" -quit
grep -i "error\|Verify:" verify-scene-task3.log
```
Expected: no `error CS` lines in either log; `verify-scene-task3.log` contains `CoreLoopSceneBuilder.Verify: scene opened and controller found successfully.`; `git diff --stat -- Assets/Scenes/CoreLoop.unity` shows the scene file changed (confirms the rebuild actually re-serialized it, not a no-op).

Also confirm all 15 PNGs actually got reimported as Sprites (not silently skipped):
```bash
grep -l "textureType: 8" Assets/Art/RulerPortraits/*.png.meta | wc -l
```
Expected: `15`.

- [ ] **Step 4: Run the full regression suite one more time**

```bash
cd /c/Users/rajes/understudy-kingdom
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-task3-edit.xml" -logFile "C:\Users\rajes\understudy-kingdom\unity-task3-edit.log"
grep -o '<test-run[^>]*total="[0-9]*"[^>]*passed="[0-9]*"[^>]*failed="[0-9]*"' test-results-task3-edit.xml

curl -s http://localhost:3000/health || (nohup npm --prefix server run dev > /dev/null 2>&1 & disown; sleep 3; curl -s http://localhost:3000/health)
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-task3-play.xml" -logFile "C:\Users\rajes\understudy-kingdom\unity-task3-play.log"
grep -o '<test-run[^>]*total="[0-9]*"[^>]*passed="[0-9]*"[^>]*failed="[0-9]*"' test-results-task3-play.xml
```
Expected: `failed="0"` on both, same counts as Task 2's Step 5 (this task doesn't add tests, it makes the existing ones exercise real art instead of a placeholder array).

- [ ] **Step 5: Clean up and commit**

```bash
cd /c/Users/rajes/understudy-kingdom
rm -f test-results-task3-*.xml unity-task3-*.log build-scene-task3.log verify-scene-task3.log
git add Assets/Editor/CoreLoopSceneBuilder.cs Assets/Scenes/CoreLoop.unity Assets/Art/RulerPortraits/*.meta
git commit -m "$(cat <<'EOF'
feat: load the 15 real ruler portraits into the built scene

CoreLoopSceneBuilder now fixes the portrait PNGs' texture import type
(they were added as raw files, so Unity defaulted them to Texture2D
instead of Sprite -- LoadAssetAtPath<Sprite> would have silently
returned null for all 15) and loads them in mood-major order matching
CoreLoopScreenController's GetMoodTier*3+GetLoyaltyTier indexing.

Scene rebuilt via CoreLoopSceneBuilder.Build() and verified via
CoreLoopSceneBuilder.Verify(). Full regression green: Unity EditMode
and PlayMode both 0 failures.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01QMEErAfieqNAbk8ZRAFBwT
EOF
)"
```
