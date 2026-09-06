# Cosmetics Customization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship FR-12 (cosmetics customization) as a 3-theme color picker recoloring the game's existing modal panel backgrounds, unlocked via `RulerState` flags the game already tracks, with zero new art and zero server changes.

**Architecture:** A 6th modal panel (`CosmeticsPanelController`, zero network calls) lets the player pick from Default/Council/Event themes and recolors the History/Council/Events panel backgrounds live. Unlock state is derived entirely from `RulerState.CouncilRewardApplied` and `RulerState.ClaimedEventWeekId`, already persisted by milestones #7 and #10. The chosen theme persists in a new `RulerState.SelectedTheme` field and re-applies on every scene load.

**Tech Stack:** Unity 6000.3.23f1 / C#, Unity Test Framework EditMode/PlayMode.

## Global Constraints

- Zero new art assets, zero `server/` changes, zero currency system.
- Individual action buttons (Submit/Challenge/History/Council/Events/Customize) keep their existing distinct colors — only the 3 modal panel backgrounds (History/Council/Events) are themeable.
- `RulerState.SelectedTheme` defaults to `"Default"`, never null — same string-not-null discipline as `ClaimedEventWeekId`.
- An unrecognized `SelectedTheme` value must resolve to Default's color, never throw or leave a panel uncolored.
- `CosmeticsPanelController` is explicitly NOT `DuelModalGate`-aware this pass (blocked on milestone #9 merging) — do not reference or import `DuelModalGate`.
- The authoritative, complete list of every file that calls `Initialize()` on `HistoryPanelController`, `CouncilPanelController`, `TutorialOverlayController`, or `EventPanelController` is exactly these 8 files (grepped directly against the current repo, not assumed) — all 8 must be updated together in Task 3 or the project won't compile:
  1. `Assets/Editor/CoreLoopSceneBuilder.cs`
  2. `Assets/Tests/PlayMode/HistoryPanelControllerTests.cs`
  3. `Assets/Tests/PlayMode/HistoryPanelControllerRealDataTests.cs`
  4. `Assets/Tests/PlayMode/CouncilPanelControllerTests.cs`
  5. `Assets/Tests/PlayMode/CouncilPanelControllerRealDataTests.cs`
  6. `Assets/Tests/PlayMode/TutorialOverlayControllerTests.cs`
  7. `Assets/Tests/PlayMode/EventPanelControllerTests.cs`
  8. `Assets/Tests/PlayMode/EventPanelControllerRealDataTests.cs`
- New UI labels 24pt minimum (28pt titles only); new interactive buttons 44pt minimum tall.
- Never pass `-quit` alongside `-runTests` in any Unity batch-mode command.
- `server/` must be running locally for `CouncilPanelControllerRealDataTests` and `EventPanelControllerRealDataTests` (Task 3 touches their `Initialize()` call sites, not their real-network bodies, but running them to confirm no regression still needs `server/` up).

---

## Task 1: `RulerState.SelectedTheme` persistence

**Files:**
- Modify: `Assets/Scripts/NPC/RulerState.cs`
- Modify: `Assets/Scripts/Core/RulerSaveData.cs`
- Modify: `Assets/Scripts/Core/SaveService.cs`
- Modify: `Assets/Tests/EditMode/SaveServiceTests.cs`

**Interfaces:**
- Produces: `RulerState.SelectedTheme` (`public string`, defaults `"Default"`, never null) — consumed by Task 2's `CosmeticsPanelController`.

- [ ] **Step 1: Write the failing tests**

In `Assets/Tests/EditMode/SaveServiceTests.cs`, add after `Load_SaveFileMissingClaimedEventWeekId_DefaultsToEmptyStringNotNull` (after line 138, before `Load_CorruptFile_ReturnsDefaultState`):

```csharp
        [Test]
        public void SaveThenLoad_RoundTripsSelectedTheme()
        {
            var original = new RulerState { Mood = 55, Loyalty = 55, Agenda = RulerState.AgendaType.Expansionist, SelectedTheme = "Council" };

            SaveService.Save(original);
            var loaded = SaveService.Load();

            Assert.AreEqual("Council", loaded.SelectedTheme);
        }

        [Test]
        public void Load_NoSaveFile_SelectedThemeDefaultsToDefault()
        {
            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }

            var state = SaveService.Load();

            Assert.AreEqual("Default", state.SelectedTheme);
        }

        [Test]
        public void Load_SaveFileMissingSelectedTheme_DefaultsToDefaultNotNull()
        {
            // Simulates a save file written before this milestone shipped --
            // literal JSON with no "SelectedTheme" key at all. As established
            // by ClaimedEventWeekId's identical test (see the comment there),
            // building this via JsonUtility.ToJson on a RulerSaveData with the
            // field left at its C# default (null) does NOT reproduce this --
            // Unity's JsonUtility serializes a null string field as an empty
            // string VALUE with the key still present, never a genuinely
            // missing key. Writing the JSON text directly guarantees the key
            // is absent, matching a real pre-milestone-11 save.
            System.IO.File.WriteAllText(SaveService.SavePath, "{\"Mood\":50,\"Loyalty\":50,\"Agenda\":0}");

            var state = SaveService.Load();

            Assert.AreEqual("Default", state.SelectedTheme);
            Assert.IsNotNull(state.SelectedTheme);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testFilter SaveServiceTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-cosmetics-savedata-editmode.xml"`
Expected: compile error — `RulerState.SelectedTheme` doesn't exist yet.

- [ ] **Step 3: Add the field to `Assets/Scripts/NPC/RulerState.cs`**

Add after `ClaimedEventWeekId` (after line 45, before `ApplyDelta`):

```csharp
        // Id of the currently-selected cosmetic panel-background theme
        // ("Default", "Council", or "Event"). Empty/unrecognized values
        // resolve to Default's color at application time -- see
        // CosmeticsPanelController.GetThemeColor. Never null (same
        // rationale as ClaimedEventWeekId). See
        // docs/superpowers/specs/2026-09-04-cosmetics-customization-design.md.
        public string SelectedTheme = "Default";
```

- [ ] **Step 4: Add the field to `Assets/Scripts/Core/RulerSaveData.cs`**

Add after `ClaimedEventWeekId` (after line 26, before the closing brace):

```csharp
        public string SelectedTheme;
```

- [ ] **Step 5: Thread the field through `Assets/Scripts/Core/SaveService.cs`**

In `Save` (lines 27-39), add `SelectedTheme = state.SelectedTheme` to the `RulerSaveData` object literal (after `ClaimedEventWeekId = state.ClaimedEventWeekId`):

```csharp
        public static void Save(RulerState state)
        {
            var data = new RulerSaveData
            {
                Mood = state.Mood,
                Loyalty = state.Loyalty,
                Agenda = (int)state.Agenda,
                CouncilRewardApplied = state.CouncilRewardApplied,
                TutorialCompleted = state.TutorialCompleted,
                ClaimedEventWeekId = state.ClaimedEventWeekId,
                SelectedTheme = state.SelectedTheme
            };
            File.WriteAllText(SavePath, JsonUtility.ToJson(data));
        }
```

In `Load` (lines 41-89), add `SelectedTheme = data.SelectedTheme ?? "Default"` to the `RulerState` object literal (after `ClaimedEventWeekId = data.ClaimedEventWeekId ?? string.Empty`):

```csharp
                var loaded = new RulerState
                {
                    Mood = data.Mood,
                    Loyalty = data.Loyalty,
                    Agenda = agenda,
                    CouncilRewardApplied = data.CouncilRewardApplied,
                    TutorialCompleted = data.TutorialCompleted,
                    ClaimedEventWeekId = data.ClaimedEventWeekId ?? string.Empty,
                    SelectedTheme = data.SelectedTheme ?? "Default"
                };
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testFilter SaveServiceTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-cosmetics-savedata-editmode.xml"`
Expected: XML shows all `SaveServiceTests` passing (prior 12 + 3 new = 15/15), 0 failed.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/NPC/RulerState.cs Assets/Scripts/Core/RulerSaveData.cs Assets/Scripts/Core/SaveService.cs Assets/Tests/EditMode/SaveServiceTests.cs
git commit -m "feat: persist RulerState.SelectedTheme"
```

---

## Task 2: `CosmeticsPanelController`

**Files:**
- Create: `Assets/Scripts/UI/CosmeticsPanelController.cs`
- Test: `Assets/Tests/PlayMode/CosmeticsPanelControllerTests.cs`

**Interfaces:**
- Consumes: `RulerState.SelectedTheme`/`CouncilRewardApplied`/`ClaimedEventWeekId` (Task 1); `DecisionCycleManager.Ruler.State` (existing); `SaveService.Save` (existing).
- Produces: `CosmeticsPanelController.Initialize(Button customizeButton, GameObject panelRoot, Button closeButton, TextMeshProUGUI[] statusLabels, Button[] applyButtons, Image eventPanelImage, Image councilPanelImage, Image historyPanelImage, DecisionCycleManager manager, Slider armySlider, Slider tradeSlider, Slider religionSlider, Button submitButton, Button challengeButton, Button viewHistoryButton, Button councilButton, Button eventsButton)` — consumed by Task 4's `CoreLoopSceneBuilder`.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/PlayMode/CosmeticsPanelControllerTests.cs`:

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
    public class CosmeticsPanelControllerTests
    {
        private GameObject managerObject;
        private GameObject rulerObject;
        private GameObject controllerObject;
        private GameObject canvasObject;
        private GameObject panelRootObject;
        private RulerNpcController ruler;
        private Slider armySlider;
        private Slider tradeSlider;
        private Slider religionSlider;
        private Button submitButton;
        private Button challengeButton;
        private Button viewHistoryButton;
        private Button councilButton;
        private Button eventsButton;
        private Button customizeButton;
        private Button closeButton;
        private TextMeshProUGUI[] statusLabels;
        private Button[] applyButtons;
        private Image eventPanelImage;
        private Image councilPanelImage;
        private Image historyPanelImage;

        [SetUp]
        public void SetUp()
        {
            rulerObject = new GameObject("Ruler");
            ruler = rulerObject.AddComponent<RulerNpcController>();

            managerObject = new GameObject("Manager");
            var manager = managerObject.AddComponent<DecisionCycleManager>();
            manager.Ruler = ruler;

            canvasObject = new GameObject("Canvas", typeof(Canvas));

            armySlider = CreateSlider("ArmySlider", 40);
            tradeSlider = CreateSlider("TradeSlider", 30);
            religionSlider = CreateSlider("ReligionSlider", 30);

            var submitButtonObject = new GameObject("SubmitButton", typeof(Image), typeof(Button));
            submitButtonObject.transform.SetParent(canvasObject.transform, false);
            submitButton = submitButtonObject.GetComponent<Button>();

            var challengeButtonObject = new GameObject("ChallengeButton", typeof(Image), typeof(Button));
            challengeButtonObject.transform.SetParent(canvasObject.transform, false);
            challengeButton = challengeButtonObject.GetComponent<Button>();

            var viewHistoryButtonObject = new GameObject("ViewHistoryButton", typeof(Image), typeof(Button));
            viewHistoryButtonObject.transform.SetParent(canvasObject.transform, false);
            viewHistoryButton = viewHistoryButtonObject.GetComponent<Button>();

            var councilButtonObject = new GameObject("CouncilButton", typeof(Image), typeof(Button));
            councilButtonObject.transform.SetParent(canvasObject.transform, false);
            councilButton = councilButtonObject.GetComponent<Button>();

            var eventsButtonObject = new GameObject("EventsButton", typeof(Image), typeof(Button));
            eventsButtonObject.transform.SetParent(canvasObject.transform, false);
            eventsButton = eventsButtonObject.GetComponent<Button>();

            var customizeButtonObject = new GameObject("CustomizeButton", typeof(Image), typeof(Button));
            customizeButtonObject.transform.SetParent(canvasObject.transform, false);
            customizeButton = customizeButtonObject.GetComponent<Button>();

            var eventPanelObject = new GameObject("EventPanel", typeof(Image));
            eventPanelObject.transform.SetParent(canvasObject.transform, false);
            eventPanelImage = eventPanelObject.GetComponent<Image>();

            var councilPanelObject = new GameObject("CouncilPanel", typeof(Image));
            councilPanelObject.transform.SetParent(canvasObject.transform, false);
            councilPanelImage = councilPanelObject.GetComponent<Image>();

            var historyPanelObject = new GameObject("HistoryPanel", typeof(Image));
            historyPanelObject.transform.SetParent(canvasObject.transform, false);
            historyPanelImage = historyPanelObject.GetComponent<Image>();

            panelRootObject = new GameObject("CosmeticsPanel");
            panelRootObject.transform.SetParent(canvasObject.transform, false);

            var closeButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            closeButtonObject.transform.SetParent(panelRootObject.transform, false);
            closeButton = closeButtonObject.GetComponent<Button>();

            statusLabels = new TextMeshProUGUI[3];
            applyButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                statusLabels[i] = CreateLabel($"ThemeStatusLabel{i}", panelRootObject.transform);

                var applyButtonObject = new GameObject($"ApplyButton{i}", typeof(Image), typeof(Button));
                applyButtonObject.transform.SetParent(panelRootObject.transform, false);
                applyButtons[i] = applyButtonObject.GetComponent<Button>();
            }

            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<CosmeticsPanelController>();
            controller.Initialize(customizeButton, panelRootObject, closeButton, statusLabels, applyButtons,
                eventPanelImage, councilPanelImage, historyPanelImage, manager,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, councilButton, eventsButton);
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

        private Slider CreateSlider(string name, float initialValue)
        {
            var sliderObject = new GameObject(name, typeof(Slider));
            sliderObject.transform.SetParent(canvasObject.transform, false);
            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(initialValue);
            return slider;
        }

        private TextMeshProUGUI CreateLabel(string name, Transform parent)
        {
            var labelObject = new GameObject(name, typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);
            return labelObject.GetComponent<TextMeshProUGUI>();
        }

        [Test]
        public void CustomizeButton_Clicked_DisablesControlsAndShowsPanel()
        {
            customizeButton.onClick.Invoke();

            Assert.IsFalse(customizeButton.interactable);
            Assert.IsFalse(viewHistoryButton.interactable);
            Assert.IsFalse(councilButton.interactable);
            Assert.IsFalse(eventsButton.interactable);
            Assert.IsFalse(armySlider.interactable);
            Assert.IsFalse(tradeSlider.interactable);
            Assert.IsFalse(religionSlider.interactable);
            Assert.IsFalse(submitButton.interactable);
            Assert.IsFalse(challengeButton.interactable);
            Assert.IsTrue(panelRootObject.activeSelf);
        }

        [Test]
        public void Close_ReEnablesControlsAndHidesPanel()
        {
            customizeButton.onClick.Invoke();
            closeButton.onClick.Invoke();

            Assert.IsTrue(customizeButton.interactable);
            Assert.IsTrue(viewHistoryButton.interactable);
            Assert.IsTrue(councilButton.interactable);
            Assert.IsTrue(eventsButton.interactable);
            Assert.IsTrue(armySlider.interactable);
            Assert.IsTrue(tradeSlider.interactable);
            Assert.IsTrue(religionSlider.interactable);
            Assert.IsTrue(submitButton.interactable);
            Assert.IsTrue(challengeButton.interactable);
            Assert.IsFalse(panelRootObject.activeSelf);
        }

        [Test]
        public void CustomizeButton_Clicked_DefaultShowsSelectedAndOthersLocked()
        {
            customizeButton.onClick.Invoke();

            // Index 0 = Default (always unlocked, already selected on a
            // fresh RulerState), 1 = Council (locked), 2 = Event (locked).
            Assert.IsFalse(applyButtons[0].interactable, "Default is already selected -- Apply should be a no-op");
            Assert.IsFalse(applyButtons[1].interactable, "Council should be locked on a fresh RulerState");
            Assert.IsFalse(applyButtons[2].interactable, "Event should be locked on a fresh RulerState");
            StringAssert.Contains("Default", statusLabels[0].text);
            StringAssert.Contains("Council", statusLabels[1].text);
            StringAssert.Contains("Event", statusLabels[2].text);
        }

        [Test]
        public void CustomizeButton_Clicked_CouncilUnlockedAfterRewardApplied_ShowsSelectable()
        {
            ruler.State.CouncilRewardApplied = true;

            customizeButton.onClick.Invoke();

            Assert.IsTrue(applyButtons[1].interactable, "Council should be selectable once CouncilRewardApplied is true");
        }

        [Test]
        public void CustomizeButton_Clicked_EventUnlockedAfterClaim_ShowsSelectable()
        {
            ruler.State.ClaimedEventWeekId = "W2026-37";

            customizeButton.onClick.Invoke();

            Assert.IsTrue(applyButtons[2].interactable, "Event should be selectable once ClaimedEventWeekId is non-empty");
        }

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

        [Test]
        public void ApplyTheme_Locked_ClickingApplyDirectlyIsNoOp()
        {
            // Council is locked (CouncilRewardApplied stays false). Exercise
            // the guard directly via onClick.Invoke() -- Unity's Button
            // still permits a direct onClick.Invoke() regardless of
            // interactable state, so this proves OnApplyTheme's own guard,
            // not just the UI-level disable.
            customizeButton.onClick.Invoke();

            applyButtons[1].onClick.Invoke();

            Assert.AreEqual("Default", ruler.State.SelectedTheme, "A locked theme must never become selected");
        }

        [Test]
        public void ApplyTheme_NeverChangesMoodLoyaltyOrAgenda()
        {
            ruler.State.CouncilRewardApplied = true;
            int moodBefore = ruler.State.Mood;
            int loyaltyBefore = ruler.State.Loyalty;
            RulerState.AgendaType agendaBefore = ruler.State.Agenda;

            customizeButton.onClick.Invoke();
            applyButtons[1].onClick.Invoke();

            Assert.AreEqual(moodBefore, ruler.State.Mood, "Cosmetics must never affect Mood");
            Assert.AreEqual(loyaltyBefore, ruler.State.Loyalty, "Cosmetics must never affect Loyalty");
            Assert.AreEqual(agendaBefore, ruler.State.Agenda, "Cosmetics must never affect Agenda");
        }

        [Test]
        public void Initialize_WithPreviouslySelectedTheme_ReappliesItImmediately()
        {
            // Simulates a relaunch: SelectedTheme is already "Council" in the
            // RulerState the controller is constructed against (mirroring
            // how a real relaunch loads the save before Initialize/Start
            // runs), and the panel is never opened.
            ruler.State.CouncilRewardApplied = true;
            ruler.State.SelectedTheme = "Council";

            var freshControllerObject = new GameObject("FreshController");
            var freshController = freshControllerObject.AddComponent<CosmeticsPanelController>();
            freshController.Initialize(customizeButton, panelRootObject, closeButton, statusLabels, applyButtons,
                eventPanelImage, councilPanelImage, historyPanelImage,
                managerObject.GetComponent<DecisionCycleManager>(),
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, councilButton, eventsButton);

            Color expected = new Color(0.22f, 0.08f, 0.16f, 0.95f);
            Assert.AreEqual(expected, eventPanelImage.color);
            Assert.AreEqual(expected, councilPanelImage.color);
            Assert.AreEqual(expected, historyPanelImage.color);

            Object.DestroyImmediate(freshControllerObject);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter CosmeticsPanelControllerTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-cosmetics-panel-playmode.xml"`
Expected: compile error — `CosmeticsPanelController` doesn't exist yet.

- [ ] **Step 3: Implement `Assets/Scripts/UI/CosmeticsPanelController.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Sixth modal panel alongside Duel/History/Council/Tutorial/Events.
    /// Makes zero network calls -- every theme's unlock condition is a
    /// RulerState flag milestones #7 (CouncilRewardApplied) and #10
    /// (ClaimedEventWeekId) already persist. Recolors the History/Council/
    /// Events panel backgrounds only; individual action buttons keep their
    /// existing distinct colors. NOT DuelModalGate-aware this pass -- see
    /// docs/superpowers/specs/2026-09-04-cosmetics-customization-design.md.
    /// </summary>
    public class CosmeticsPanelController : MonoBehaviour
    {
        private struct ThemeDefinition
        {
            public string Id;
            public string DisplayName;
            public Color PanelColor;
            public string LockedDescription;
        }

        // Panel colors are distinct from each other, from Default's
        // existing (0.1, 0.1, 0.15) navy, and from every button color
        // already used in CoreLoopSceneBuilder.cs.
        private static readonly ThemeDefinition[] Themes =
        {
            new ThemeDefinition
            {
                Id = "Default",
                DisplayName = "Default",
                PanelColor = new Color(0.1f, 0.1f, 0.15f, 0.95f),
                LockedDescription = null
            },
            new ThemeDefinition
            {
                Id = "Council",
                DisplayName = "Council Chamber",
                PanelColor = new Color(0.22f, 0.08f, 0.16f, 0.95f),
                LockedDescription = "Unlocks once your council reaches its milestone"
            },
            new ThemeDefinition
            {
                Id = "Event",
                DisplayName = "Harvest Hall",
                PanelColor = new Color(0.16f, 0.13f, 0.04f, 0.95f),
                LockedDescription = "Unlocks once you claim a live-ops event reward"
            }
        };

        [SerializeField] private Button customizeButton;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI[] statusLabels;
        [SerializeField] private Button[] applyButtons;
        [SerializeField] private Image eventPanelImage;
        [SerializeField] private Image councilPanelImage;
        [SerializeField] private Image historyPanelImage;
        [SerializeField] private DecisionCycleManager manager;
        [SerializeField] private Slider armySlider;
        [SerializeField] private Slider tradeSlider;
        [SerializeField] private Slider religionSlider;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button viewHistoryButton;
        [SerializeField] private Button councilButton;
        [SerializeField] private Button eventsButton;

        private void Start()
        {
            Bind();
        }

        /// <summary>
        /// Mirrors EventPanelController/CouncilPanelController's Initialize
        /// pattern -- called by Start() in the real scene, and callable
        /// directly by tests to bypass Unity lifecycle timing.
        /// </summary>
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
            Button eventsButton)
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

            Bind();
        }

        private void Bind()
        {
            customizeButton.onClick.RemoveAllListeners();
            customizeButton.onClick.AddListener(OnCustomizeButtonClicked);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnClose);

            for (int i = 0; i < applyButtons.Length; i++)
            {
                int themeIndex = i; // capture by value, not the loop variable
                applyButtons[i].onClick.RemoveAllListeners();
                applyButtons[i].onClick.AddListener(() => OnApplyTheme(themeIndex));
            }

            panelRoot.SetActive(false);

            // Re-applies the saved theme on every scene load (relaunch),
            // not just when the panel is explicitly opened.
            ApplyTheme(manager.Ruler.State.SelectedTheme);
        }

        private void OnCustomizeButtonClicked()
        {
            SetCoreLoopControlsInteractable(false);
            panelRoot.SetActive(true);
            RenderThemeRows();
        }

        private void RenderThemeRows()
        {
            for (int i = 0; i < Themes.Length; i++)
            {
                ThemeDefinition theme = Themes[i];
                bool unlocked = IsUnlocked(theme.Id);
                bool isSelected = manager.Ruler.State.SelectedTheme == theme.Id;

                statusLabels[i].text = unlocked
                    ? (isSelected ? $"{theme.DisplayName} (Selected)" : theme.DisplayName)
                    : $"{theme.DisplayName} -- {theme.LockedDescription}";
                applyButtons[i].interactable = unlocked && !isSelected;
            }
        }

        private void OnApplyTheme(int themeIndex)
        {
            ThemeDefinition theme = Themes[themeIndex];
            if (!IsUnlocked(theme.Id))
            {
                return;
            }

            manager.Ruler.State.SelectedTheme = theme.Id;
            SaveService.Save(manager.Ruler.State);
            ApplyTheme(theme.Id);
            RenderThemeRows();
        }

        private void OnClose()
        {
            panelRoot.SetActive(false);
            SetCoreLoopControlsInteractable(true);
        }

        private bool IsUnlocked(string themeId)
        {
            switch (themeId)
            {
                case "Default":
                    return true;
                case "Council":
                    return manager.Ruler.State.CouncilRewardApplied;
                case "Event":
                    return !string.IsNullOrEmpty(manager.Ruler.State.ClaimedEventWeekId);
                default:
                    return false;
            }
        }

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

        private void SetCoreLoopControlsInteractable(bool interactable)
        {
            customizeButton.interactable = interactable;
            viewHistoryButton.interactable = interactable;
            councilButton.interactable = interactable;
            eventsButton.interactable = interactable;
            armySlider.interactable = interactable;
            tradeSlider.interactable = interactable;
            religionSlider.interactable = interactable;
            submitButton.interactable = interactable;
            challengeButton.interactable = interactable;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter CosmeticsPanelControllerTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-cosmetics-panel-playmode.xml"`
Expected: XML shows 10/10 passed, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/CosmeticsPanelController.cs Assets/Tests/PlayMode/CosmeticsPanelControllerTests.cs
git commit -m "feat: add CosmeticsPanelController"
```

---

## Task 3: Wire `customizeButton` into History/Council/Tutorial/Events

**Files:**
- Modify: `Assets/Scripts/UI/HistoryPanelController.cs`
- Modify: `Assets/Scripts/UI/CouncilPanelController.cs`
- Modify: `Assets/Scripts/UI/TutorialOverlayController.cs`
- Modify: `Assets/Scripts/UI/EventPanelController.cs`
- Modify: `Assets/Editor/CoreLoopSceneBuilder.cs` (constructs the `customizeButton` GameObject only this task — the full `CosmeticsPanel` + `CosmeticsPanelController` construction is Task 4)
- Modify: `Assets/Tests/PlayMode/HistoryPanelControllerTests.cs`
- Modify: `Assets/Tests/PlayMode/HistoryPanelControllerRealDataTests.cs`
- Modify: `Assets/Tests/PlayMode/CouncilPanelControllerTests.cs`
- Modify: `Assets/Tests/PlayMode/CouncilPanelControllerRealDataTests.cs`
- Modify: `Assets/Tests/PlayMode/TutorialOverlayControllerTests.cs`
- Modify: `Assets/Tests/PlayMode/EventPanelControllerTests.cs`
- Modify: `Assets/Tests/PlayMode/EventPanelControllerRealDataTests.cs`

**Interfaces:**
- Produces: `HistoryPanelController.Initialize(..., Button eventsButton, Button customizeButton)`, `CouncilPanelController.Initialize(..., Button eventsButton, Button customizeButton)`, `TutorialOverlayController.Initialize(..., Button eventsButton, Button customizeButton)`, `EventPanelController.Initialize(..., Button councilButton, Button customizeButton)` (all gain `customizeButton` as a new trailing parameter) — consumed by Task 4's full `CosmeticsPanel` wiring, which reuses the same `customizeButton` local variable `CoreLoopSceneBuilder.cs` constructs in this task.

This task mirrors exactly how `eventsButton` was threaded into these same four files in milestone #10 — every file in the Global Constraints' authoritative 8-file list must be updated together, or the project won't compile.

- [ ] **Step 1: Update `Assets/Scripts/UI/HistoryPanelController.cs`**

Add a field (after `[SerializeField] private Button eventsButton;` on line 31):

```csharp
        [SerializeField] private Button customizeButton;
```

Add a parameter to `Initialize` (after `Button eventsButton` on line 55) and the matching assignment (after `this.eventsButton = eventsButton;` on line 68):

```csharp
            Button eventsButton,
            Button customizeButton)
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
```

Add to `SetCoreLoopControlsInteractable` (after `eventsButton.interactable = interactable;` on line 162):

```csharp
            customizeButton.interactable = interactable;
```

- [ ] **Step 2: Update `Assets/Scripts/UI/CouncilPanelController.cs`**

Add a field (after `[SerializeField] private Button eventsButton;` on line 49):

```csharp
        [SerializeField] private Button customizeButton;
```

Add a parameter to `Initialize` (after `Button eventsButton` on line 86) and the matching assignment (after `this.eventsButton = eventsButton;` on line 112):

```csharp
            Button eventsButton,
            Button customizeButton)
        {
            this.councilButton = councilButton;
            this.panelRoot = panelRoot;
            this.closeButton = closeButton;
            this.notInCouncilView = notInCouncilView;
            this.inCouncilView = inCouncilView;
            this.nameInputField = nameInputField;
            this.createButton = createButton;
            this.joinCodeInputField = joinCodeInputField;
            this.joinButton = joinButton;
            this.statusMessageText = statusMessageText;
            this.nameLabel = nameLabel;
            this.joinCodeLabel = joinCodeLabel;
            this.memberCountLabel = memberCountLabel;
            this.progressLabel = progressLabel;
            this.rewardStatusLabel = rewardStatusLabel;
            this.coordinator = coordinator;
            this.manager = manager;
            this.screenController = screenController;
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.submitButton = submitButton;
            this.challengeButton = challengeButton;
            this.viewHistoryButton = viewHistoryButton;
            this.eventsButton = eventsButton;
            this.customizeButton = customizeButton;
```

Add to `SetCoreLoopControlsInteractable` (after `eventsButton.interactable = interactable;` on line 252):

```csharp
            customizeButton.interactable = interactable;
```

- [ ] **Step 3: Update `Assets/Scripts/UI/TutorialOverlayController.cs`**

Add a field (after `[SerializeField] private Button eventsButton;` on line 49):

```csharp
        [SerializeField] private Button customizeButton;
```

Add a parameter to `Initialize` (after `Button eventsButton` on line 79) and the matching assignment (after `this.eventsButton = eventsButton;` on line 96):

```csharp
            Button eventsButton,
            Button customizeButton)
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
            this.eventsButton = eventsButton;
            this.customizeButton = customizeButton;
```

Add to `SetCoreLoopControlsInteractable` (after `eventsButton.interactable = interactable;` on line 164):

```csharp
            customizeButton.interactable = interactable;
```

- [ ] **Step 4: Update `Assets/Scripts/UI/EventPanelController.cs`**

Add a field (after `[SerializeField] private Button councilButton;` on line 41):

```csharp
        [SerializeField] private Button customizeButton;
```

Add a parameter to `Initialize` (after `Button councilButton` on line 73) and the matching assignment (after `this.councilButton = councilButton;` on line 92):

```csharp
            Button councilButton,
            Button customizeButton)
        {
            this.eventsButton = eventsButton;
            this.panelRoot = panelRoot;
            this.closeButton = closeButton;
            this.nameLabel = nameLabel;
            this.narrationLabel = narrationLabel;
            this.progressLabel = progressLabel;
            this.statusMessageText = statusMessageText;
            this.claimButton = claimButton;
            this.coordinator = coordinator;
            this.manager = manager;
            this.screenController = screenController;
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.submitButton = submitButton;
            this.challengeButton = challengeButton;
            this.viewHistoryButton = viewHistoryButton;
            this.councilButton = councilButton;
            this.customizeButton = customizeButton;
```

Add to `SetCoreLoopControlsInteractable` (after `challengeButton.interactable = interactable;` on line 175):

```csharp
            customizeButton.interactable = interactable;
```

- [ ] **Step 5: Construct `customizeButton` in `Assets/Editor/CoreLoopSceneBuilder.cs` and update the four existing `Initialize()` calls**

Insert after `eventsButtonLabelRect.anchoredPosition = Vector2.zero;` (after line 155) and before the `eventPanelRootObject` construction (line 157):

```csharp
            var customizeButtonObject = new GameObject("CustomizeButton", typeof(Image), typeof(Button));
            customizeButtonObject.transform.SetParent(canvasObject.transform, false);
            var customizeButtonRect = customizeButtonObject.GetComponent<RectTransform>();
            customizeButtonRect.anchoredPosition = new Vector2(0f, -780f);
            customizeButtonRect.sizeDelta = new Vector2(220f, 44f);
            customizeButtonObject.GetComponent<Image>().color = new Color(0.45f, 0.45f, 0.5f, 1f);
            var customizeButton = customizeButtonObject.GetComponent<Button>();
            TextMeshProUGUI customizeButtonLabel = CreateLabel(customizeButtonObject.transform, "Text", 0f, "Customize");
            var customizeButtonLabelRect = customizeButtonLabel.GetComponent<RectTransform>();
            customizeButtonLabelRect.anchorMin = Vector2.zero;
            customizeButtonLabelRect.anchorMax = Vector2.one;
            customizeButtonLabelRect.sizeDelta = Vector2.zero;
            customizeButtonLabelRect.anchoredPosition = Vector2.zero;
```

`customizeButton` is constructed here (not later, alongside its own panel in Task 4) specifically so it already exists by the time the four `Initialize()` calls below run — the panel it opens doesn't exist yet, and won't until Task 4, but the button itself only needs to exist and be referenceable.

Update the `eventController.Initialize(...)` call (lines 219-221) to add `customizeButton` as the new trailing argument:

```csharp
            eventController.Initialize(eventsButton, eventPanelRootObject, eventCloseButton, eventNameLabel, eventNarrationLabel,
                eventProgressLabel, eventStatusMessageText, claimButton, backendCoordinator, manager, controller,
                armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton, councilButton, customizeButton);
```

Update the `councilController.Initialize(...)` call (lines 329-333) to add `customizeButton` as the new trailing argument:

```csharp
            councilController.Initialize(councilButton, councilPanelRootObject, councilCloseButton, notInCouncilViewObject, inCouncilViewObject,
                nameInputField, createButton, joinCodeInputField, joinButton, councilStatusMessageText,
                councilNameLabel, councilJoinCodeLabel, councilMemberCountLabel, councilProgressLabel, councilRewardStatusLabel,
                backendCoordinator, manager, controller,
                armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton, eventsButton, customizeButton);
```

Update the `historyController.Initialize(...)` call (lines 378-379) to add `customizeButton` as the new trailing argument:

```csharp
            historyController.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, backendCoordinator,
                armySlider, tradeSlider, religionSlider, button, duelButton, councilButton, eventsButton, customizeButton);
```

Update the `tutorialController.Initialize(...)` call (lines 444-446) to add `customizeButton` as the new trailing argument:

```csharp
            tutorialController.Initialize(tutorialOverlayObject, tutorialStepIndicatorLabel, tutorialTitleLabel, tutorialBodyLabel,
                tutorialNextButton, tutorialNextButtonLabel, tutorialSkipButton, manager,
                armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton, councilButton, eventsButton, customizeButton);
```

- [ ] **Step 6: Update `Assets/Tests/PlayMode/HistoryPanelControllerTests.cs`**

Add a field (after `private Button eventsButton;` on line 23):

```csharp
        private Button customizeButton;
```

In `SetUp`, add after the `eventsButton` construction (after line 60, before `viewHistoryButtonObject` construction):

```csharp
            var customizeButtonObject = new GameObject("CustomizeButton", typeof(Image), typeof(Button));
            customizeButtonObject.transform.SetParent(canvasObject.transform, false);
            customizeButton = customizeButtonObject.GetComponent<Button>();
```

Update the `Initialize` call (lines 83-84) to add `customizeButton` as the new trailing argument:

```csharp
            controller.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, coordinator,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, councilButton, eventsButton, customizeButton);
```

Add `Assert.IsFalse(customizeButton.interactable);` after `Assert.IsFalse(eventsButton.interactable);` (line 119) in `ViewHistory_WithNoSessionYet_DisablesControlsAndShowsMessage`, and `Assert.IsTrue(customizeButton.interactable);` after `Assert.IsTrue(eventsButton.interactable);` (line 137) in `Close_ReEnablesControlsAndHidesPanel`.

- [ ] **Step 7: Update `Assets/Tests/PlayMode/HistoryPanelControllerRealDataTests.cs`**

`eventsButton` here is a LOCAL variable inside `UnitySetUp` (confirmed by reading the file — unlike `HistoryPanelControllerTests.cs`, this file has no class field for it, only `councilButton`/`viewHistoryButton`/etc. among its declared `private Button` fields at the top of the class). `customizeButton` needs the same treatment: a local variable, no new field.

In `UnitySetUp`, add after the `eventsButton` construction (after line 92, `var eventsButton = eventsButtonObject.GetComponent<Button>();`, before the `Initialize` call):

```csharp
            var customizeButtonObject = new GameObject("CustomizeButton", typeof(Image), typeof(Button));
            customizeButtonObject.transform.SetParent(canvasObject.transform, false);
            var customizeButton = customizeButtonObject.GetComponent<Button>();
```

Update the `Initialize` call (line 111-112) to:

```csharp
            controller.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, coordinator,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, councilButton, eventsButton, customizeButton);
```

This file has no per-button interactable assertions to extend (its one test only checks rendered row text), so the construction + call-site update is the complete change here.

- [ ] **Step 8: Update `Assets/Tests/PlayMode/CouncilPanelControllerTests.cs`**

Add a field for `customizeButton` (mirroring its existing `eventsButton` field). In `SetUp`, add the same `GameObject("CustomizeButton", typeof(Image), typeof(Button))` construction pattern used for `eventsButton`. Update the `Initialize` call (currently ending `armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, eventsButton);` at its own line 136) to:

```csharp
            controller.Initialize(councilButton, panelRootObject, closeButton, notInCouncilViewObject, inCouncilViewObject,
                nameInputField, createButton, joinCodeInputField, joinButton, statusMessageText,
                nameLabel, joinCodeLabel, memberCountLabel, progressLabel, rewardStatusLabel,
                coordinator, manager, screenController,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, eventsButton, customizeButton);
```

Add `Assert.IsFalse(customizeButton.interactable);` next to the existing `Assert.IsFalse(eventsButton.interactable);` (at its own line 176) in `CouncilButton_WithNoSessionYet_DisablesControlsAndShowsMessage`, and `Assert.IsTrue(customizeButton.interactable);` next to `Assert.IsTrue(eventsButton.interactable);` (at its own line 194) in `Close_ReEnablesControlsAndHidesPanel`.

- [ ] **Step 9: Update `Assets/Tests/PlayMode/CouncilPanelControllerRealDataTests.cs`**

`eventsButton` here is also a LOCAL variable inside `UnitySetUp` (confirmed by reading the file — only `councilButton` among the relevant buttons is a class field, since it's the only one referenced later in the test body). `customizeButton` gets the same local-variable treatment.

In `UnitySetUp`, add after the `eventsButton` construction (after line 112, `var eventsButton = eventsButtonObject.GetComponent<Button>();`, before the `Initialize` call):

```csharp
            var customizeButtonObject = new GameObject("CustomizeButton", typeof(Image), typeof(Button));
            customizeButtonObject.transform.SetParent(canvasObject.transform, false);
            var customizeButton = customizeButtonObject.GetComponent<Button>();
```

Update the `Initialize` call (line 156-160) to:

```csharp
            controller.Initialize(councilButton, panelRootObject, closeButton, notInCouncilViewObject, inCouncilViewObject,
                nameInputField, createButton, joinCodeInputField, joinButton, statusMessageText,
                nameLabel, joinCodeLabel, memberCountLabel, progressLabel, rewardStatusLabel,
                coordinator, manager, screenController,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, eventsButton, customizeButton);
```

This file's one test only asserts reward state, not per-button interactable flags, so no assertion additions are needed here.

- [ ] **Step 10: Update `Assets/Tests/PlayMode/TutorialOverlayControllerTests.cs`**

Add a field (after `private Button eventsButton;` on line 25):

```csharp
        private Button customizeButton;
```

In `BuildScene()`, add after `eventsButton = CreateButton("EventsButton");` (after line 54):

```csharp
            customizeButton = CreateButton("CustomizeButton");
```

In the private `Initialize()` helper, update the `controller.Initialize(...)` call (lines 111-113) to:

```csharp
            controller.Initialize(panelRootObject, stepIndicatorLabel, titleLabel, bodyLabel,
                nextButton, nextButtonLabel, skipButton, manager,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, councilButton, eventsButton, customizeButton);
```

Add `Assert.IsFalse(customizeButton.interactable);` after `Assert.IsFalse(eventsButton.interactable);` (after line 131) in `TutorialNotCompleted_ShowsStepOneAndDisablesControls`.

In `TutorialAlreadyCompleted_ReenablesControlsThatWereDisabledInTheScene`, add `customizeButton.interactable = false;` after `eventsButton.interactable = false;` (after line 157) AND add `Assert.IsTrue(customizeButton.interactable);` after `Assert.IsTrue(eventsButton.interactable);` (after line 169).

In `Skip_OnFirstStep_CompletesTutorialPersistsAndReenablesControls`, add `Assert.IsTrue(customizeButton.interactable);` after `Assert.IsTrue(eventsButton.interactable);` (after line 205).

- [ ] **Step 11: Update `Assets/Tests/PlayMode/EventPanelControllerTests.cs`**

Add a field for `customizeButton` (mirroring its existing `councilButton` field). In `SetUp`, add the same `GameObject("CustomizeButton", typeof(Image), typeof(Button))` construction pattern used for `eventsButton`/`councilButton`. Update the `Initialize` call (lines 113-115) to:

```csharp
            controller.Initialize(eventsButton, panelRootObject, closeButton, nameLabel, narrationLabel,
                progressLabel, statusMessageText, claimButton, coordinator, manager, screenController,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, councilButton, customizeButton);
```

Add `Assert.IsFalse(customizeButton.interactable);` next to `Assert.IsFalse(councilButton.interactable);` (line 168) in `EventsButton_WithNoSessionYet_DisablesControlsAndShowsMessage`, and `Assert.IsTrue(customizeButton.interactable);` next to `Assert.IsTrue(councilButton.interactable);` (line 186) in `Close_ReEnablesControlsAndHidesPanel`.

- [ ] **Step 12: Update `Assets/Tests/PlayMode/EventPanelControllerRealDataTests.cs`**

`councilButton` here is a LOCAL variable inside its setup method (confirmed by reading the file — `eventsButton` and `claimButton` are the only relevant class fields, since they're the two referenced later in the test body). `customizeButton` gets the same local-variable treatment as `councilButton`.

Add after the `councilButton` construction (after line 114, `var councilButton = councilButtonObject.GetComponent<Button>();`, before the `Initialize` call):

```csharp
            var customizeButtonObject = new GameObject("CustomizeButton", typeof(Image), typeof(Button));
            customizeButtonObject.transform.SetParent(canvasObject.transform, false);
            var customizeButton = customizeButtonObject.GetComponent<Button>();
```

Update the `Initialize` call (line 138-140) to:

```csharp
            controller.Initialize(eventsButton, panelRootObject, closeButton, nameLabel, narrationLabel,
                progressLabel, statusMessageText, claimButton, coordinator, manager, screenController,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, councilButton, customizeButton);
```

This file's one test only asserts reward/progress state, not per-button interactable flags, so no assertion additions are needed here.

- [ ] **Step 13: Run the affected test suites to verify they pass**

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter HistoryPanelControllerTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-cosmetics-history-playmode.xml"`
Expected: XML shows all `HistoryPanelControllerTests` passing, 0 failed.

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter CouncilPanelControllerTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-cosmetics-council-playmode.xml"`
Expected: XML shows all `CouncilPanelControllerTests` passing, 0 failed.

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter TutorialOverlayControllerTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-cosmetics-tutorial-playmode.xml"`
Expected: XML shows all `TutorialOverlayControllerTests` passing, 0 failed.

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter EventPanelControllerTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-cosmetics-event-playmode.xml"`
Expected: XML shows all `EventPanelControllerTests` passing, 0 failed.

Ensure `server/` is running, then:

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter HistoryPanelControllerRealDataTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-cosmetics-historyreal-playmode.xml"`
Expected: XML shows all passing, 0 failed.

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter CouncilPanelControllerRealDataTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-cosmetics-councilreal-playmode.xml"`
Expected: XML shows all passing, 0 failed.

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter EventPanelControllerRealDataTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-cosmetics-eventreal-playmode.xml"`
Expected: XML shows all passing, 0 failed.

Note: `CoreLoopSceneBuilder.cs` now constructs a `customizeButton` that isn't yet wired to any panel/controller of its own (that's Task 4) — this is intentional and does not affect any of the above test suites, none of which load the real scene or reference this specific local variable.

- [ ] **Step 14: Commit**

```bash
git add Assets/Scripts/UI/HistoryPanelController.cs Assets/Scripts/UI/CouncilPanelController.cs Assets/Scripts/UI/TutorialOverlayController.cs Assets/Scripts/UI/EventPanelController.cs Assets/Editor/CoreLoopSceneBuilder.cs Assets/Tests/PlayMode/HistoryPanelControllerTests.cs Assets/Tests/PlayMode/HistoryPanelControllerRealDataTests.cs Assets/Tests/PlayMode/CouncilPanelControllerTests.cs Assets/Tests/PlayMode/CouncilPanelControllerRealDataTests.cs Assets/Tests/PlayMode/TutorialOverlayControllerTests.cs Assets/Tests/PlayMode/EventPanelControllerTests.cs Assets/Tests/PlayMode/EventPanelControllerRealDataTests.cs
git commit -m "feat: wire customizeButton into History/Council/Tutorial/Events's shared-control sets"
```

---

## Task 4: `CoreLoopSceneBuilder` wiring — the full `CosmeticsPanel`

**Files:**
- Modify: `Assets/Editor/CoreLoopSceneBuilder.cs`
- Modify: `Assets/Tests/PlayMode/CoreLoopSceneTests.cs`

**Interfaces:**
- Consumes: `CosmeticsPanelController` (Task 2); `customizeButton`, `eventPanelRootObject`, `councilPanelRootObject`, `panelRootObject` (History) — all already-existing local variables in `CoreLoopSceneBuilder.Build()` by this point (the panel-root `GameObject`s were constructed for Events/Council/History earlier in the same method; their `Image` components are fetched fresh via `.GetComponent<Image>()` here rather than threading new variables through the method).
- Produces: the real `Assets/Scenes/CoreLoop.unity` scene, regenerated via `Understudy Kingdom > Build Core Loop Scene`, now containing a `CosmeticsPanel` and `CosmeticsPanelController` — consumed by the milestone's manual Play Mode checkpoint.

- [ ] **Step 1: Write the failing scene smoke test**

In `Assets/Tests/PlayMode/CoreLoopSceneTests.cs`, add a new test after `LoadedCoreLoopScene_EventsButton_OpensPanelWithoutThrowing` (after line 109, before the `FindLabel` helper), reusing the existing `FindButton`/`FindChildByName` helpers:

```csharp
        [UnityTest]
        public IEnumerator LoadedCoreLoopScene_CustomizeButton_OpensPanelWithoutThrowing()
        {
            yield return SceneManager.LoadSceneAsync("CoreLoop");
            yield return null;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Canvas not found in the loaded CoreLoop scene.");

            Button customizeButton = FindButton(canvas, "CustomizeButton");
            Assert.IsNotNull(customizeButton, "CustomizeButton not found in the loaded CoreLoop scene.");

            GameObject cosmeticsPanel = FindChildByName(canvas.transform, "CosmeticsPanel");
            Assert.IsNotNull(cosmeticsPanel, "CosmeticsPanel not found in the loaded CoreLoop scene.");
            Assert.IsFalse(cosmeticsPanel.activeSelf, "Expected CosmeticsPanel to start inactive.");

            customizeButton.onClick.Invoke();

            Assert.IsTrue(cosmeticsPanel.activeSelf,
                "Expected CosmeticsPanel to become active after CustomizeButton is clicked.");
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter CoreLoopSceneTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-cosmetics-scene-playmode-before.xml"`
Expected: FAIL — `CosmeticsPanel not found in the loaded CoreLoop scene.` (the scene hasn't been rebuilt yet).

- [ ] **Step 3: Add the full `CosmeticsPanel` construction to `Assets/Editor/CoreLoopSceneBuilder.cs`**

Insert after the `tutorialController.Initialize(...)` call (after Task 3's updated version of that call, and before `canvasObject.GetComponent<RectTransform>().localScale = Vector3.one;`) — this must be the LAST panel constructed, since it needs `eventPanelRootObject`, `councilPanelRootObject`, and `panelRootObject` (History), all of which exist by this point in the method but none of which exist earlier:

```csharp
            var cosmeticsPanelRootObject = new GameObject("CosmeticsPanel", typeof(Image));
            cosmeticsPanelRootObject.transform.SetParent(canvasObject.transform, false);
            var cosmeticsPanelRect = cosmeticsPanelRootObject.GetComponent<RectTransform>();
            cosmeticsPanelRect.anchoredPosition = Vector2.zero;
            cosmeticsPanelRect.sizeDelta = new Vector2(700f, 800f);
            cosmeticsPanelRootObject.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            var cosmeticsCloseButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            cosmeticsCloseButtonObject.transform.SetParent(cosmeticsPanelRootObject.transform, false);
            var cosmeticsCloseButtonRect = cosmeticsCloseButtonObject.GetComponent<RectTransform>();
            cosmeticsCloseButtonRect.anchoredPosition = new Vector2(310f, 360f);
            cosmeticsCloseButtonRect.sizeDelta = new Vector2(60f, 44f);
            cosmeticsCloseButtonObject.GetComponent<Image>().color = new Color(0.6f, 0.3f, 0.3f, 1f);
            var cosmeticsCloseButton = cosmeticsCloseButtonObject.GetComponent<Button>();
            TextMeshProUGUI cosmeticsCloseLabel = CreateLabel(cosmeticsCloseButtonObject.transform, "Text", 0f, "X");
            var cosmeticsCloseLabelRect = cosmeticsCloseLabel.GetComponent<RectTransform>();
            cosmeticsCloseLabelRect.anchorMin = Vector2.zero;
            cosmeticsCloseLabelRect.anchorMax = Vector2.one;
            cosmeticsCloseLabelRect.sizeDelta = Vector2.zero;
            cosmeticsCloseLabelRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI cosmeticsTitleLabel = CreateLabel(cosmeticsPanelRootObject.transform, "Title", 0f, "Customize Your Court");
            cosmeticsTitleLabel.fontSize = 28f;
            cosmeticsTitleLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 340f);

            var themeStatusLabels = new TextMeshProUGUI[3];
            var themeApplyButtons = new Button[3];
            float[] themeRowY = { 240f, 140f, 40f };
            for (int i = 0; i < 3; i++)
            {
                TextMeshProUGUI statusLabel = CreateLabel(cosmeticsPanelRootObject.transform, $"ThemeStatusLabel{i}", 0f, string.Empty);
                statusLabel.alignment = TextAlignmentOptions.Left;
                var statusLabelRect = statusLabel.GetComponent<RectTransform>();
                statusLabelRect.sizeDelta = new Vector2(360f, 50f);
                statusLabelRect.anchoredPosition = new Vector2(-40f, themeRowY[i]);
                themeStatusLabels[i] = statusLabel;

                var applyButtonObject = new GameObject($"ApplyButton{i}", typeof(Image), typeof(Button));
                applyButtonObject.transform.SetParent(cosmeticsPanelRootObject.transform, false);
                var applyButtonRect = applyButtonObject.GetComponent<RectTransform>();
                applyButtonRect.anchoredPosition = new Vector2(270f, themeRowY[i]);
                applyButtonRect.sizeDelta = new Vector2(140f, 44f);
                applyButtonObject.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.7f, 1f);
                var applyButton = applyButtonObject.GetComponent<Button>();
                TextMeshProUGUI applyButtonLabel = CreateLabel(applyButtonObject.transform, "Text", 0f, "Apply");
                var applyButtonLabelRect = applyButtonLabel.GetComponent<RectTransform>();
                applyButtonLabelRect.anchorMin = Vector2.zero;
                applyButtonLabelRect.anchorMax = Vector2.one;
                applyButtonLabelRect.sizeDelta = Vector2.zero;
                applyButtonLabelRect.anchoredPosition = Vector2.zero;
                themeApplyButtons[i] = applyButton;
            }

            var cosmeticsControllerObject = new GameObject("CosmeticsPanelController");
            var cosmeticsController = cosmeticsControllerObject.AddComponent<CosmeticsPanelController>();
            cosmeticsController.Initialize(customizeButton, cosmeticsPanelRootObject, cosmeticsCloseButton,
                themeStatusLabels, themeApplyButtons,
                eventPanelRootObject.GetComponent<Image>(), councilPanelRootObject.GetComponent<Image>(), panelRootObject.GetComponent<Image>(),
                manager, armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton, councilButton, eventsButton);
```

- [ ] **Step 4: Add a `CosmeticsPanelController` check to `Verify()`**

In `Assets/Editor/CoreLoopSceneBuilder.cs`'s `Verify()` method, add after the `EventPanelController` check (after line 564, before the final `Debug.Log`):

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
```

- [ ] **Step 5: Regenerate the scene**

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Build -quit`
Expected: exits 0, log line `CoreLoopSceneBuilder: saved scene to Assets/Scenes/CoreLoop.unity`. (`-quit` is correct and required here — `-executeMethod` is a different code path than `-runTests`.)

Then verify it:

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Verify -quit`
Expected: exits 0, log line `CoreLoopSceneBuilder.Verify: scene opened and controller found successfully.`

- [ ] **Step 6: Run the scene smoke test to verify it passes**

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter CoreLoopSceneTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-cosmetics-scene-playmode-after.xml"`
Expected: XML shows all `CoreLoopSceneTests` passing (prior 2 + 1 new = 3/3), 0 failed.

- [ ] **Step 7: Run the full regression suite**

Run: `cd server && npm test && npm run typecheck` (no server-side changes this milestone, confirms zero regression)
Expected: all passing, clean typecheck.

Ensure `server/` is running, then:

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-cosmetics-full-editmode.xml"`
Expected: all passing (prior 71 + 3 new SaveServiceTests = 74/74).

Run: `"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-cosmetics-full-playmode.xml"`
Expected: all passing (prior 52 + 10 new CosmeticsPanelControllerTests + 1 new scene smoke test = 63/63).

- [ ] **Step 8: Commit**

```bash
git add Assets/Editor/CoreLoopSceneBuilder.cs Assets/Tests/PlayMode/CoreLoopSceneTests.cs Assets/Scenes/CoreLoop.unity
git commit -m "feat: wire CosmeticsPanelController into the CoreLoop scene"
```

---

## Definition of Done

- [ ] Full server suite passes: `cd server && npm test && npm run typecheck`
- [ ] Full Unity EditMode suite passes (74/74 expected)
- [ ] Full Unity PlayMode suite passes (63/63 expected, `server/` running)
- [ ] Manual Play Mode checkpoint (open the Editor, enter Play Mode on the real `CoreLoop` scene): Customize panel opens; Default is selectable/already-selected immediately on a fresh save; Council and Event themes show correctly locked with their unlock description before their respective milestones are reached, and become selectable immediately after (join a council and reach its milestone; claim a live-ops event reward); applying a theme visibly recolors the History/Council/Events panel backgrounds immediately; the choice persists across an Editor Stop/Play restart.
- [ ] Update `docs/PROJECT_PLAN.md`'s Implementation Status table (milestone #11: Done, covers FR-12) and "Known follow-up items" if anything surfaces during the manual checkpoint or final review.
