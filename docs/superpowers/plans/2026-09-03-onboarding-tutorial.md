# Onboarding Tutorial Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show a brand-new player a short, dismissible 4-step overlay explaining the core prep→ruler-decision loop on first launch, and never again once completed.

**Architecture:** A fourth overlay controller (`TutorialOverlayController`) alongside Duel/History/Council, but self-triggering (shows on `Start()` based on persisted state, not a button) rather than player-triggered. Persistence mirrors milestone #7's `CouncilRewardApplied` pattern exactly — one new bool threaded through the existing `SaveService.Save`/`Load` round trip.

**Tech Stack:** Unity 6000.3.23f1 (C#), Unity Test Framework (EditMode + PlayMode). No server changes.

## Global Constraints

- Trigger is the persisted flag itself — `!manager.Ruler.State.TutorialCompleted` — checked once `DecisionCycleManager.Awake()` has loaded state (Unity runs all `Awake()` calls in a scene before any `Start()`, so this ordering is guaranteed). Never `SaveService.HasSave()` directly.
- No visual pointer/spotlight highlighting live UI elements this pass — text-only callouts.
- No replay-from-settings-menu (none exists), no localization, no monetization-prompt gating (none exists yet).
- Buttons sized 220×44, matching every other button already in this scene.
- Skip is visible and reachable on every step (never buried behind Next). A step indicator ("Step N of 4") is always shown.
- Exact step title/body text (copied below) must be used verbatim.
- Never pass `-quit` alongside `-runTests` in any Unity batch-mode command (confirmed multiple times in this project: the combination exits the Editor before the test runner ever executes, silently producing no results file while still exiting code 0). `-quit` is still correct and required for `-executeMethod` (scene `Build()`/`Verify()`) invocations, a different code path.

---

## File Structure

- `Assets/Scripts/NPC/RulerState.cs` — modify: add `TutorialCompleted` field.
- `Assets/Scripts/Core/RulerSaveData.cs` — modify: add `TutorialCompleted` field.
- `Assets/Scripts/Core/SaveService.cs` — modify: thread `TutorialCompleted` through `Save`/`Load`.
- `Assets/Tests/EditMode/SaveServiceTests.cs` — modify: two new round-trip tests.
- `Assets/Scripts/UI/TutorialOverlayController.cs` — new: the overlay controller.
- `Assets/Tests/PlayMode/TutorialOverlayControllerTests.cs` — new: deterministic tests (no network dependency — this feature never touches the backend).
- `Assets/Editor/CoreLoopSceneBuilder.cs` — modify: build the full-screen overlay + callout box, wire `TutorialOverlayController.Initialize(...)`, extend `Verify()`.

---

### Task 1: Persisted `TutorialCompleted` flag

**Files:**
- Modify: `Assets/Scripts/NPC/RulerState.cs`
- Modify: `Assets/Scripts/Core/RulerSaveData.cs`
- Modify: `Assets/Scripts/Core/SaveService.cs`
- Modify: `Assets/Tests/EditMode/SaveServiceTests.cs`

**Interfaces:**
- Produces: `RulerState.TutorialCompleted` (bool, default `false`), persisted through `SaveService.Save(RulerState)`/`SaveService.Load()` — for Task 2 (`TutorialOverlayController`) to read and set.

- [ ] **Step 1: Add the field to `RulerState`**

In `Assets/Scripts/NPC/RulerState.cs`, change:

```csharp
        // True once the one-time council-milestone mood/loyalty reward has
        // been applied to THIS player's ruler -- prevents re-applying it on
        // every subsequent council-panel open. See
        // docs/superpowers/specs/2026-09-03-council-social-design.md.
        public bool CouncilRewardApplied = false;
```

to:

```csharp
        // True once the one-time council-milestone mood/loyalty reward has
        // been applied to THIS player's ruler -- prevents re-applying it on
        // every subsequent council-panel open. See
        // docs/superpowers/specs/2026-09-03-council-social-design.md.
        public bool CouncilRewardApplied = false;

        // True once the player has dismissed (via Skip or completing all
        // steps) the first-launch onboarding tutorial -- prevents it from
        // showing again on every subsequent launch. See
        // docs/superpowers/specs/2026-09-03-onboarding-tutorial-design.md.
        public bool TutorialCompleted = false;
```

- [ ] **Step 2: Add the field to `RulerSaveData`**

In `Assets/Scripts/Core/RulerSaveData.cs`, change:

```csharp
        public int Mood;
        public int Loyalty;
        public int Agenda;
        public bool CouncilRewardApplied;
```

to:

```csharp
        public int Mood;
        public int Loyalty;
        public int Agenda;
        public bool CouncilRewardApplied;
        public bool TutorialCompleted;
```

- [ ] **Step 3: Thread it through `SaveService.Save`/`Load`**

In `Assets/Scripts/Core/SaveService.cs`, change:

```csharp
            var data = new RulerSaveData
            {
                Mood = state.Mood,
                Loyalty = state.Loyalty,
                Agenda = (int)state.Agenda,
                CouncilRewardApplied = state.CouncilRewardApplied
            };
```

to:

```csharp
            var data = new RulerSaveData
            {
                Mood = state.Mood,
                Loyalty = state.Loyalty,
                Agenda = (int)state.Agenda,
                CouncilRewardApplied = state.CouncilRewardApplied,
                TutorialCompleted = state.TutorialCompleted
            };
```

and change:

```csharp
                var loaded = new RulerState
                {
                    Mood = data.Mood,
                    Loyalty = data.Loyalty,
                    Agenda = agenda,
                    CouncilRewardApplied = data.CouncilRewardApplied
                };
```

to:

```csharp
                var loaded = new RulerState
                {
                    Mood = data.Mood,
                    Loyalty = data.Loyalty,
                    Agenda = agenda,
                    CouncilRewardApplied = data.CouncilRewardApplied,
                    TutorialCompleted = data.TutorialCompleted
                };
```

- [ ] **Step 4: Add round-trip tests to `Assets/Tests/EditMode/SaveServiceTests.cs`**

Add these two tests inside the existing `SaveServiceTests` class, after `Load_NoSaveFile_CouncilRewardAppliedDefaultsFalse`:

```csharp
        [Test]
        public void SaveThenLoad_RoundTripsTutorialCompleted()
        {
            var original = new RulerState { Mood = 65, Loyalty = 65, Agenda = RulerState.AgendaType.Pious, TutorialCompleted = true };

            SaveService.Save(original);
            var loaded = SaveService.Load();

            Assert.IsTrue(loaded.TutorialCompleted);
        }

        [Test]
        public void Load_NoSaveFile_TutorialCompletedDefaultsFalse()
        {
            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }

            var state = SaveService.Load();

            Assert.IsFalse(state.TutorialCompleted);
        }
```

- [ ] **Step 5: Run the EditMode tests**

Run (no `-quit`):
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testFilter SaveServiceTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-tutorial-savedata-editmode.xml"
```
Expected: XML shows all `SaveServiceTests` tests passing (prior 7 + 2 new = 9/9), 0 failed.

- [ ] **Step 6: Run the full EditMode + PlayMode suite**

Expected: zero failures, count grows by this task's 2 new tests.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/NPC/RulerState.cs Assets/Scripts/Core/RulerSaveData.cs Assets/Scripts/Core/SaveService.cs Assets/Tests/EditMode/SaveServiceTests.cs
git commit -m "feat: persist TutorialCompleted on RulerState"
```

---

### Task 2: `TutorialOverlayController`

**Files:**
- Create: `Assets/Scripts/UI/TutorialOverlayController.cs`
- Create: `Assets/Tests/PlayMode/TutorialOverlayControllerTests.cs`

**Interfaces:**
- Consumes: `RulerState.TutorialCompleted` (Task 1); `DecisionCycleManager.Ruler.State`; `SaveService.Save`.
- Produces: `TutorialOverlayController.Initialize(GameObject panelRoot, TextMeshProUGUI stepIndicatorLabel, TextMeshProUGUI titleLabel, TextMeshProUGUI bodyLabel, Button nextButton, TextMeshProUGUI nextButtonLabel, Button skipButton, DecisionCycleManager manager, Slider armySlider, Slider tradeSlider, Slider religionSlider, Button submitButton, Button challengeButton, Button viewHistoryButton, Button councilButton)` — for Task 3 (`CoreLoopSceneBuilder`) to call with real scene objects.

**Structural note:** unlike `HistoryPanelController`/`CouncilPanelController` (button-triggered modals that disable each other's trigger button too), this overlay is nobody's target — it auto-shows on `Start()`, so it disables the 7 shared controls but nothing needs a reference to *it* to disable it in return, since by construction those 7 controls are the only things a player could otherwise interact with.

- [ ] **Step 1: Write `Assets/Scripts/UI/TutorialOverlayController.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Fourth overlay in this scene alongside Duel/History/Council, but
    /// unlike those it is not player-triggered -- it shows itself once on
    /// Start() based on persisted state, and nothing else needs a reference
    /// to it or a way to disable it in return, since by construction nothing
    /// else is interactable while it is up. See
    /// docs/superpowers/specs/2026-09-03-onboarding-tutorial-design.md.
    /// </summary>
    public class TutorialOverlayController : MonoBehaviour
    {
        private static readonly (string Title, string Body)[] Steps =
        {
            ("Your Resources",
                "These three sliders control your recommendation: Army, Trade, and Religion. " +
                "They always add up to 100 -- adjust one and the others rebalance automatically."),
            ("Submit Your Recommendation",
                "Once you're happy with your allocation, tap Submit Recommendation. Your ruler " +
                "will accept or override it based on their mood, loyalty, and agenda."),
            ("Reading Your Ruler",
                "Mood, Loyalty, and Agenda (top of screen) describe your ruler's state -- they " +
                "shift based on how well your recommendations match what your ruler actually wants."),
            ("Beyond the Basics",
                "Once you're comfortable with the core loop, you can Challenge rival kingdoms, " +
                "view your History, or join a Council with other players.")
        };

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TextMeshProUGUI stepIndicatorLabel;
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI bodyLabel;
        [SerializeField] private Button nextButton;
        [SerializeField] private TextMeshProUGUI nextButtonLabel;
        [SerializeField] private Button skipButton;
        [SerializeField] private DecisionCycleManager manager;
        [SerializeField] private Slider armySlider;
        [SerializeField] private Slider tradeSlider;
        [SerializeField] private Slider religionSlider;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button viewHistoryButton;
        [SerializeField] private Button councilButton;

        private int currentStep;

        private void Start()
        {
            Bind();
        }

        /// <summary>
        /// Mirrors HistoryPanelController/CouncilPanelController's Initialize
        /// pattern -- called by Start() in the real scene, and callable
        /// directly by tests to bypass Unity lifecycle timing.
        /// </summary>
        public void Initialize(
            GameObject panelRoot,
            TextMeshProUGUI stepIndicatorLabel,
            TextMeshProUGUI titleLabel,
            TextMeshProUGUI bodyLabel,
            Button nextButton,
            TextMeshProUGUI nextButtonLabel,
            Button skipButton,
            DecisionCycleManager manager,
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button submitButton,
            Button challengeButton,
            Button viewHistoryButton,
            Button councilButton)
        {
            this.panelRoot = panelRoot;
            this.stepIndicatorLabel = stepIndicatorLabel;
            this.titleLabel = titleLabel;
            this.bodyLabel = bodyLabel;
            this.nextButton = nextButton;
            this.nextButtonLabel = nextButtonLabel;
            this.skipButton = skipButton;
            this.manager = manager;
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.submitButton = submitButton;
            this.challengeButton = challengeButton;
            this.viewHistoryButton = viewHistoryButton;
            this.councilButton = councilButton;

            Bind();
        }

        private void Bind()
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(OnNext);
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(OnSkip);

            if (manager.Ruler.State.TutorialCompleted)
            {
                panelRoot.SetActive(false);
                SetCoreLoopControlsInteractable(true);
                return;
            }

            currentStep = 0;
            SetCoreLoopControlsInteractable(false);
            panelRoot.SetActive(true);
            RenderCurrentStep();
        }

        private void OnNext()
        {
            if (currentStep >= Steps.Length - 1)
            {
                Complete();
                return;
            }

            currentStep++;
            RenderCurrentStep();
        }

        private void OnSkip()
        {
            Complete();
        }

        private void Complete()
        {
            manager.Ruler.State.TutorialCompleted = true;
            SaveService.Save(manager.Ruler.State);
            panelRoot.SetActive(false);
            SetCoreLoopControlsInteractable(true);
        }

        private void RenderCurrentStep()
        {
            var step = Steps[currentStep];
            titleLabel.text = step.Title;
            bodyLabel.text = step.Body;
            stepIndicatorLabel.text = $"Step {currentStep + 1} of {Steps.Length}";
            nextButtonLabel.text = currentStep == Steps.Length - 1 ? "Done" : "Next";
        }

        private void SetCoreLoopControlsInteractable(bool interactable)
        {
            armySlider.interactable = interactable;
            tradeSlider.interactable = interactable;
            religionSlider.interactable = interactable;
            submitButton.interactable = interactable;
            challengeButton.interactable = interactable;
            viewHistoryButton.interactable = interactable;
            councilButton.interactable = interactable;
        }
    }
}
```

- [ ] **Step 2: Write `Assets/Tests/PlayMode/TutorialOverlayControllerTests.cs`**

```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    public class TutorialOverlayControllerTests
    {
        private GameObject rulerObject;
        private GameObject managerObject;
        private GameObject controllerObject;
        private GameObject canvasObject;
        private GameObject panelRootObject;
        private Slider armySlider;
        private Slider tradeSlider;
        private Slider religionSlider;
        private Button submitButton;
        private Button challengeButton;
        private Button viewHistoryButton;
        private Button councilButton;
        private Button nextButton;
        private TextMeshProUGUI nextButtonLabel;
        private Button skipButton;
        private TextMeshProUGUI stepIndicatorLabel;
        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI bodyLabel;
        private RulerNpcController ruler;
        private DecisionCycleManager manager;

        private void BuildScene()
        {
            rulerObject = new GameObject("Ruler");
            ruler = rulerObject.AddComponent<RulerNpcController>();

            managerObject = new GameObject("Manager");
            manager = managerObject.AddComponent<DecisionCycleManager>();
            manager.Ruler = ruler;

            canvasObject = new GameObject("Canvas", typeof(Canvas));

            armySlider = CreateSlider("ArmySlider");
            tradeSlider = CreateSlider("TradeSlider");
            religionSlider = CreateSlider("ReligionSlider");

            submitButton = CreateButton("SubmitButton");
            challengeButton = CreateButton("ChallengeButton");
            viewHistoryButton = CreateButton("ViewHistoryButton");
            councilButton = CreateButton("CouncilButton");

            panelRootObject = new GameObject("PanelRoot");
            panelRootObject.transform.SetParent(canvasObject.transform, false);

            stepIndicatorLabel = CreateLabel("StepIndicatorLabel", panelRootObject.transform);
            titleLabel = CreateLabel("TitleLabel", panelRootObject.transform);
            bodyLabel = CreateLabel("BodyLabel", panelRootObject.transform);

            nextButton = CreateButton("NextButton", panelRootObject.transform);
            nextButtonLabel = CreateLabel("NextButtonLabel", nextButton.transform);
            skipButton = CreateButton("SkipButton", panelRootObject.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(rulerObject);

            if (System.IO.File.Exists(SaveService.SavePath))
            {
                System.IO.File.Delete(SaveService.SavePath);
            }
        }

        private Slider CreateSlider(string name)
        {
            var sliderObject = new GameObject(name, typeof(Slider));
            sliderObject.transform.SetParent(canvasObject.transform, false);
            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            return slider;
        }

        private Button CreateButton(string name, Transform parent = null)
        {
            var buttonObject = new GameObject(name, typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent != null ? parent : canvasObject.transform, false);
            return buttonObject.GetComponent<Button>();
        }

        private TextMeshProUGUI CreateLabel(string name, Transform parent)
        {
            var labelObject = new GameObject(name, typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);
            return labelObject.GetComponent<TextMeshProUGUI>();
        }

        private TutorialOverlayController Initialize()
        {
            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<TutorialOverlayController>();
            controller.Initialize(panelRootObject, stepIndicatorLabel, titleLabel, bodyLabel,
                nextButton, nextButtonLabel, skipButton, manager,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, councilButton);
            return controller;
        }

        [Test]
        public void TutorialNotCompleted_ShowsStepOneAndDisablesControls()
        {
            BuildScene();
            Initialize();

            Assert.IsTrue(panelRootObject.activeSelf);
            Assert.IsFalse(armySlider.interactable);
            Assert.IsFalse(tradeSlider.interactable);
            Assert.IsFalse(religionSlider.interactable);
            Assert.IsFalse(submitButton.interactable);
            Assert.IsFalse(challengeButton.interactable);
            Assert.IsFalse(viewHistoryButton.interactable);
            Assert.IsFalse(councilButton.interactable);
            Assert.AreEqual("Your Resources", titleLabel.text);
            Assert.AreEqual("Step 1 of 4", stepIndicatorLabel.text);
            Assert.AreEqual("Next", nextButtonLabel.text);
        }

        [Test]
        public void TutorialAlreadyCompleted_ReenablesControlsThatWereDisabledInTheScene()
        {
            BuildScene();
            ruler.State.TutorialCompleted = true;

            // Mirrors the committed CoreLoop scene: CoreLoopSceneBuilder.Build()
            // calls Initialize() at edit time against a fresh RulerState, which
            // disables these controls and serializes that disabled state into
            // the scene asset. Establish that same precondition here so this
            // test actually proves the completed-path re-enables them at
            // runtime, instead of trivially passing because Unity UI controls
            // default to interactable == true.
            armySlider.interactable = false;
            tradeSlider.interactable = false;
            religionSlider.interactable = false;
            submitButton.interactable = false;
            challengeButton.interactable = false;
            viewHistoryButton.interactable = false;
            councilButton.interactable = false;

            Initialize();

            Assert.IsFalse(panelRootObject.activeSelf);
            Assert.IsTrue(armySlider.interactable);
            Assert.IsTrue(tradeSlider.interactable);
            Assert.IsTrue(religionSlider.interactable);
            Assert.IsTrue(submitButton.interactable);
            Assert.IsTrue(challengeButton.interactable);
            Assert.IsTrue(viewHistoryButton.interactable);
            Assert.IsTrue(councilButton.interactable);
        }

        [Test]
        public void Next_AdvancesThroughAllFourSteps_LabelBecomesDoneOnLastStep()
        {
            BuildScene();
            Initialize();

            nextButton.onClick.Invoke();
            Assert.AreEqual("Submit Your Recommendation", titleLabel.text);
            Assert.AreEqual("Step 2 of 4", stepIndicatorLabel.text);
            Assert.AreEqual("Next", nextButtonLabel.text);

            nextButton.onClick.Invoke();
            Assert.AreEqual("Reading Your Ruler", titleLabel.text);
            Assert.AreEqual("Step 3 of 4", stepIndicatorLabel.text);

            nextButton.onClick.Invoke();
            Assert.AreEqual("Beyond the Basics", titleLabel.text);
            Assert.AreEqual("Step 4 of 4", stepIndicatorLabel.text);
            Assert.AreEqual("Done", nextButtonLabel.text);
        }

        [Test]
        public void Skip_OnFirstStep_CompletesTutorialPersistsAndReenablesControls()
        {
            BuildScene();
            Initialize();

            skipButton.onClick.Invoke();

            Assert.IsTrue(ruler.State.TutorialCompleted);
            Assert.IsFalse(panelRootObject.activeSelf);
            Assert.IsTrue(armySlider.interactable);
            Assert.IsTrue(councilButton.interactable);

            RulerState persisted = SaveService.Load();
            Assert.IsTrue(persisted.TutorialCompleted);
        }

        [Test]
        public void Done_OnLastStep_CompletesTutorial()
        {
            BuildScene();
            Initialize();

            nextButton.onClick.Invoke();
            nextButton.onClick.Invoke();
            nextButton.onClick.Invoke();
            Assert.AreEqual("Done", nextButtonLabel.text);

            nextButton.onClick.Invoke();

            Assert.IsTrue(ruler.State.TutorialCompleted);
            Assert.IsFalse(panelRootObject.activeSelf);
            Assert.IsTrue(submitButton.interactable);
        }
    }
}
```

- [ ] **Step 3: Confirm no interactive Unity Editor GUI window is open**

Run (PowerShell): `Get-Process -Name Unity -ErrorAction SilentlyContinue`
Expected: no output (no running process). If one is found, ask the user to close it — never force-close.

- [ ] **Step 4: Run the new PlayMode tests**

Run (no `-quit`):
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter TutorialOverlayControllerTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-tutorial-overlay-playmode.xml"
```
Expected: XML shows 5/5 passed, 0 failed.

- [ ] **Step 5: Run the full EditMode + PlayMode suite**

Expected: zero failures, count grows by this task's 5 new tests.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/UI/TutorialOverlayController.cs Assets/Tests/PlayMode/TutorialOverlayControllerTests.cs
git commit -m "feat: add TutorialOverlayController"
```

---

### Task 3: Scene wiring, full regression, manual verification

**Files:**
- Modify: `Assets/Editor/CoreLoopSceneBuilder.cs`

**Interfaces:**
- Consumes: everything from Tasks 1-2.
- Produces: the real, playable scene — no further consumers this milestone.

- [ ] **Step 1: Add the full-screen overlay + callout box to `CoreLoopSceneBuilder.Build()`**

Insert the following block right after the existing `historyController.Initialize(...)` line and before `canvasObject.GetComponent<RectTransform>().localScale = Vector3.one;`:

```csharp
            var tutorialOverlayObject = new GameObject("TutorialOverlay", typeof(Image));
            tutorialOverlayObject.transform.SetParent(canvasObject.transform, false);
            var tutorialOverlayRect = tutorialOverlayObject.GetComponent<RectTransform>();
            tutorialOverlayRect.anchorMin = Vector2.zero;
            tutorialOverlayRect.anchorMax = Vector2.one;
            tutorialOverlayRect.offsetMin = Vector2.zero;
            tutorialOverlayRect.offsetMax = Vector2.zero;
            tutorialOverlayObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var tutorialBoxObject = new GameObject("CalloutBox", typeof(Image));
            tutorialBoxObject.transform.SetParent(tutorialOverlayObject.transform, false);
            var tutorialBoxRect = tutorialBoxObject.GetComponent<RectTransform>();
            tutorialBoxRect.anchoredPosition = Vector2.zero;
            tutorialBoxRect.sizeDelta = new Vector2(600f, 500f);
            tutorialBoxObject.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 1f);

            TextMeshProUGUI tutorialStepIndicatorLabel = CreateLabel(tutorialBoxObject.transform, "StepIndicatorLabel", 0f, "Step 1 of 4");
            tutorialStepIndicatorLabel.fontSize = 20f;
            tutorialStepIndicatorLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 210f);

            TextMeshProUGUI tutorialTitleLabel = CreateLabel(tutorialBoxObject.transform, "TitleLabel", 0f, string.Empty);
            tutorialTitleLabel.fontSize = 28f;
            tutorialTitleLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 160f);

            TextMeshProUGUI tutorialBodyLabel = CreateLabel(tutorialBoxObject.transform, "BodyLabel", 0f, string.Empty);
            var tutorialBodyLabelRect = tutorialBodyLabel.GetComponent<RectTransform>();
            tutorialBodyLabelRect.anchoredPosition = new Vector2(0f, 20f);
            tutorialBodyLabelRect.sizeDelta = new Vector2(520f, 200f);

            var tutorialSkipButtonObject = new GameObject("SkipButton", typeof(Image), typeof(Button));
            tutorialSkipButtonObject.transform.SetParent(tutorialBoxObject.transform, false);
            var tutorialSkipButtonRect = tutorialSkipButtonObject.GetComponent<RectTransform>();
            tutorialSkipButtonRect.anchoredPosition = new Vector2(-140f, -190f);
            tutorialSkipButtonRect.sizeDelta = new Vector2(220f, 44f);
            tutorialSkipButtonObject.GetComponent<Image>().color = new Color(0.4f, 0.4f, 0.4f, 1f);
            var tutorialSkipButton = tutorialSkipButtonObject.GetComponent<Button>();
            TextMeshProUGUI tutorialSkipButtonLabel = CreateLabel(tutorialSkipButtonObject.transform, "Text", 0f, "Skip");
            var tutorialSkipButtonLabelRect = tutorialSkipButtonLabel.GetComponent<RectTransform>();
            tutorialSkipButtonLabelRect.anchorMin = Vector2.zero;
            tutorialSkipButtonLabelRect.anchorMax = Vector2.one;
            tutorialSkipButtonLabelRect.sizeDelta = Vector2.zero;
            tutorialSkipButtonLabelRect.anchoredPosition = Vector2.zero;

            var tutorialNextButtonObject = new GameObject("NextButton", typeof(Image), typeof(Button));
            tutorialNextButtonObject.transform.SetParent(tutorialBoxObject.transform, false);
            var tutorialNextButtonRect = tutorialNextButtonObject.GetComponent<RectTransform>();
            tutorialNextButtonRect.anchoredPosition = new Vector2(140f, -190f);
            tutorialNextButtonRect.sizeDelta = new Vector2(220f, 44f);
            tutorialNextButtonObject.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.7f, 1f);
            var tutorialNextButton = tutorialNextButtonObject.GetComponent<Button>();
            TextMeshProUGUI tutorialNextButtonLabel = CreateLabel(tutorialNextButtonObject.transform, "Text", 0f, "Next");
            var tutorialNextButtonLabelRect = tutorialNextButtonLabel.GetComponent<RectTransform>();
            tutorialNextButtonLabelRect.anchorMin = Vector2.zero;
            tutorialNextButtonLabelRect.anchorMax = Vector2.one;
            tutorialNextButtonLabelRect.sizeDelta = Vector2.zero;
            tutorialNextButtonLabelRect.anchoredPosition = Vector2.zero;

            var tutorialControllerObject = new GameObject("TutorialOverlayController");
            var tutorialController = tutorialControllerObject.AddComponent<TutorialOverlayController>();
            tutorialController.Initialize(tutorialOverlayObject, tutorialStepIndicatorLabel, tutorialTitleLabel, tutorialBodyLabel,
                tutorialNextButton, tutorialNextButtonLabel, tutorialSkipButton, manager,
                armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton, councilButton);

```

- [ ] **Step 2: Extend `Verify()`**

Change:

```csharp
            var councilController = Object.FindFirstObjectByType<CouncilPanelController>();
            if (councilController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no CouncilPanelController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            Debug.Log("CoreLoopSceneBuilder.Verify: scene opened and controller found successfully.");
```

to:

```csharp
            var councilController = Object.FindFirstObjectByType<CouncilPanelController>();
            if (councilController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no CouncilPanelController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            var tutorialController = Object.FindFirstObjectByType<TutorialOverlayController>();
            if (tutorialController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no TutorialOverlayController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            Debug.Log("CoreLoopSceneBuilder.Verify: scene opened and controller found successfully.");
```

- [ ] **Step 3: Confirm no interactive Unity Editor GUI window is open**

Run (PowerShell): `Get-Process -Name Unity -ErrorAction SilentlyContinue`
Expected: no output. If one is found, ask the user to close it.

- [ ] **Step 4: Rebuild the scene**

Run (uses `-quit`, correct here — this is `-executeMethod`, not `-runTests`):
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Build
```
Expected: log line `CoreLoopSceneBuilder: saved scene to Assets/Scenes/CoreLoop.unity`, exit code 0.

- [ ] **Step 5: Verify the rebuilt scene**

Run (uses `-quit`, correct for `-executeMethod`):
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Verify
```
Expected: log line `CoreLoopSceneBuilder.Verify: scene opened and controller found successfully.`, exit code 0.

- [ ] **Step 6: Run the full EditMode + PlayMode suite (no `-quit`, per Global Constraints)**

Run both:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-tutorial-final-editmode.xml"
```
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-tutorial-final-playmode.xml"
```
Expected: both XML files show zero failures.

- [ ] **Step 7: Commit**

```bash
git add Assets/Editor/CoreLoopSceneBuilder.cs Assets/Scenes/CoreLoop.unity
git commit -m "feat: wire TutorialOverlayController into the CoreLoop scene"
```

- [ ] **Step 8: Manual Play Mode verification (human)**

This step cannot be scripted — it is the one thing exercising the real, rendered scene layout. Ask the user to:
1. Confirm no other Unity Editor GUI window is open, then delete any existing save file if one is present at their `Application.persistentDataPath` (or simply confirm this will be a genuinely fresh save) so the tutorial actually triggers — if a prior manual checkpoint from an earlier milestone already created a save file with `TutorialCompleted` absent (pre-this-milestone saves), that's fine too since `TutorialCompleted` will deserialize to `false` by default; only a save file where a *later* run of this same build already completed the tutorial would suppress it.
2. Open `Assets/Scenes/CoreLoop.unity` in the Unity Editor and press Play.
3. Confirm the tutorial overlay appears immediately, covering the screen, with all 7 background controls visibly greyed out/non-interactive.
4. Click through all 4 steps via Next, confirming the step indicator and title/body text update each time, and the button reads "Done" on step 4.
5. Click Done — confirm the overlay disappears and all 7 controls become interactive again.
6. Stop Play Mode, press Play again — confirm the tutorial does NOT reappear (since `TutorialCompleted` was persisted).
7. Stop Play Mode, confirm no Console errors were logged.
8. Optionally: delete the save file again, press Play, and this time click Skip on step 1 — confirm the same dismiss behavior (overlay hides, controls re-enable, doesn't reappear on next launch).

If any step reveals a real bug, fix it directly, re-verify the full suite, and ask the user to retest before proceeding to `finishing-a-development-branch`.
