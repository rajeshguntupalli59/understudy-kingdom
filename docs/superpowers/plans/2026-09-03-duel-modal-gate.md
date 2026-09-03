# Duel/Modal Gate Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the Challenge-button race flagged in milestones #6 and #7's final reviews: a duel resolving while a modal is open, or a modal closing while a duel is still in flight, can each leave `challengeButton` in the wrong interactable state.

**Architecture:** A new plain C# class, `DuelModalGate`, with two public bool properties (`IsDuelInFlight`, `IsModalOpen`), shared by reference between `DuelButtonController`, `HistoryPanelController`, and `CouncilPanelController`. Only `challengeButton`'s enable/disable decisions change; the other 6 shared controls are untouched.

**Tech Stack:** Unity 6000.3.23f1 (C#), Unity Test Framework (EditMode + PlayMode). No server changes.

## Global Constraints

- `DuelModalGate` is a plain C# class, not a `MonoBehaviour` — no Unity component references, just two bool properties, no methods.
- Only `challengeButton`'s enable/disable decisions change in `HistoryPanelController`/`CouncilPanelController`; the other 6 shared controls (3 sliders, Submit, the other modal's trigger button, and the modal's own trigger button) keep their existing unconditional direct-set behavior.
- Deterministic tests only — no real network duel needed anywhere in this milestone's new tests.
- One new trailing `Initialize()` parameter on all 3 existing controllers (`DuelButtonController`, `HistoryPanelController`, `CouncilPanelController`) — every existing call site (production scene builder + all relevant test files) must be updated consistently in the same task that changes the signature, or the project won't compile.
- Never pass `-quit` alongside `-runTests` in any Unity batch-mode command (confirmed multiple times in this project: the combination exits the Editor before the test runner ever executes, silently producing no results file while still exiting code 0). `-quit` is still correct and required for `-executeMethod` (scene `Build()`/`Verify()`) invocations, a different code path.

---

## File Structure

- `Assets/Scripts/UI/DuelModalGate.cs` — new: the shared 2-flag state class.
- `Assets/Scripts/UI/DuelButtonController.cs` — modify: gate-aware `challengeButton` re-enable.
- `Assets/Tests/PlayMode/DuelButtonControllerTests.cs` — modify: gate construction + new isolated test.
- `Assets/Scripts/UI/HistoryPanelController.cs` — modify: gate-aware open/close + `challengeButton` guard.
- `Assets/Tests/PlayMode/HistoryPanelControllerTests.cs` — modify: gate construction + new isolated test.
- `Assets/Tests/PlayMode/HistoryPanelControllerRealDataTests.cs` — modify: gate construction (call-site fix only).
- `Assets/Scripts/UI/CouncilPanelController.cs` — modify: identical shape of change to History's.
- `Assets/Tests/PlayMode/CouncilPanelControllerTests.cs` — modify: gate construction + new isolated test.
- `Assets/Tests/PlayMode/CouncilPanelControllerRealDataTests.cs` — modify: gate construction (call-site fix only).
- `Assets/Editor/CoreLoopSceneBuilder.cs` — modify: construct one `DuelModalGate`, pass it into all 3 `Initialize()` calls.
- `Assets/Tests/PlayMode/DuelModalGateInterleavingTests.cs` — new: the real cross-controller composition proof.

---

### Task 1: `DuelModalGate` + `DuelButtonController`

**Files:**
- Create: `Assets/Scripts/UI/DuelModalGate.cs`
- Modify: `Assets/Scripts/UI/DuelButtonController.cs`
- Modify: `Assets/Tests/PlayMode/DuelButtonControllerTests.cs`
- Modify: `Assets/Editor/CoreLoopSceneBuilder.cs` (duel section only)

**Interfaces:**
- Produces: `DuelModalGate` (`IsDuelInFlight`, `IsModalOpen`, both `bool`, default `false`) — for Task 2 and Task 3 to share by reference. `DuelButtonController.Initialize(...)` gains a trailing `DuelModalGate gate` parameter.

- [ ] **Step 1: Write `Assets/Scripts/UI/DuelModalGate.cs`**

```csharp
namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Tracks the two independent things that can want the shared
    /// "Challenge a Rival Kingdom" button disabled at once: a duel actually
    /// in flight, and a modal panel (History or Council) currently open.
    /// Nothing else needs this -- the other 6 shared controls only ever have
    /// one thing wanting them disabled (whichever modal is open, since
    /// History and Council already mutually exclude each other), so their
    /// existing direct interactable-toggling logic stays untouched. See
    /// docs/superpowers/specs/2026-09-03-duel-modal-gate-design.md.
    /// </summary>
    public class DuelModalGate
    {
        public bool IsDuelInFlight { get; set; }
        public bool IsModalOpen { get; set; }
    }
}
```

- [ ] **Step 2: Update `Assets/Scripts/UI/DuelButtonController.cs`**

Change:

```csharp
        [SerializeField] private Slider armySlider;
        [SerializeField] private Slider tradeSlider;
        [SerializeField] private Slider religionSlider;
        [SerializeField] private Button challengeButton;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private BackendSyncCoordinator coordinator;
```

to:

```csharp
        [SerializeField] private Slider armySlider;
        [SerializeField] private Slider tradeSlider;
        [SerializeField] private Slider religionSlider;
        [SerializeField] private Button challengeButton;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private BackendSyncCoordinator coordinator;
        [SerializeField] private DuelModalGate gate;
```

Change:

```csharp
        public void Initialize(
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button challengeButton,
            TextMeshProUGUI resultText,
            BackendSyncCoordinator coordinator)
        {
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.challengeButton = challengeButton;
            this.resultText = resultText;
            this.coordinator = coordinator;

            Bind();
        }
```

to:

```csharp
        public void Initialize(
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button challengeButton,
            TextMeshProUGUI resultText,
            BackendSyncCoordinator coordinator,
            DuelModalGate gate)
        {
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.challengeButton = challengeButton;
            this.resultText = resultText;
            this.coordinator = coordinator;
            this.gate = gate;

            Bind();
        }
```

Change:

```csharp
        private void OnChallenge()
        {
            var allocation = new ResourceAllocation(
                Mathf.RoundToInt(armySlider.value),
                Mathf.RoundToInt(tradeSlider.value),
                Mathf.RoundToInt(religionSlider.value));

            resultText.text = "Resolving...";
            challengeButton.interactable = false;

            coordinator.RequestDuel(allocation, HandleResult, HandleError);
        }

        private void HandleResult(DuelResult result)
        {
            challengeButton.interactable = true;

            string templateTag = result.overridden ? "duel_lose" : "duel_win";
            resultText.text = DialogueTemplateEngine.Resolve(templateTag, new Dictionary<string, string>());
        }

        private void HandleError(string error)
        {
            challengeButton.interactable = true;
            resultText.text = $"Challenge failed: {error}";
        }
```

to:

```csharp
        private void OnChallenge()
        {
            var allocation = new ResourceAllocation(
                Mathf.RoundToInt(armySlider.value),
                Mathf.RoundToInt(tradeSlider.value),
                Mathf.RoundToInt(religionSlider.value));

            resultText.text = "Resolving...";
            challengeButton.interactable = false;
            gate.IsDuelInFlight = true;

            coordinator.RequestDuel(allocation, HandleResult, HandleError);
        }

        private void HandleResult(DuelResult result)
        {
            gate.IsDuelInFlight = false;
            if (!gate.IsModalOpen)
            {
                challengeButton.interactable = true;
            }

            string templateTag = result.overridden ? "duel_lose" : "duel_win";
            resultText.text = DialogueTemplateEngine.Resolve(templateTag, new Dictionary<string, string>());
        }

        private void HandleError(string error)
        {
            gate.IsDuelInFlight = false;
            if (!gate.IsModalOpen)
            {
                challengeButton.interactable = true;
            }
            resultText.text = $"Challenge failed: {error}";
        }
```

- [ ] **Step 3: Update `Assets/Tests/PlayMode/DuelButtonControllerTests.cs`**

Add a field next to the existing ones:

```csharp
        private TextMeshProUGUI resultText;
```

to:

```csharp
        private TextMeshProUGUI resultText;
        private DuelModalGate gate;
```

Change the `SetUp` method's controller construction:

```csharp
            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<DuelButtonController>();
            controller.Initialize(armySlider, tradeSlider, religionSlider, challengeButton, resultText, coordinator);
```

to:

```csharp
            gate = new DuelModalGate();

            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<DuelButtonController>();
            controller.Initialize(armySlider, tradeSlider, religionSlider, challengeButton, resultText, coordinator, gate);
```

Add this test at the end of the class, after `Challenge_WithNoSessionYet_ShowsErrorAndReEnablesButton`:

```csharp
        [Test]
        public void Challenge_WithModalOpen_LeavesButtonDisabledAfterError()
        {
            gate.IsModalOpen = true;

            challengeButton.onClick.Invoke();

            Assert.IsTrue(resultText.text.Contains("Challenge failed"));
            Assert.IsFalse(challengeButton.interactable,
                "a modal being open must keep Challenge disabled even after the duel request itself resolves");
        }
```

- [ ] **Step 4: Update `Assets/Editor/CoreLoopSceneBuilder.cs`'s duel section**

Change:

```csharp
            var duelButtonObject = new GameObject("ChallengeButton", typeof(Image), typeof(Button));
```

to:

```csharp
            var duelModalGate = new DuelModalGate();

            var duelButtonObject = new GameObject("ChallengeButton", typeof(Image), typeof(Button));
```

Change:

```csharp
            duelController.Initialize(armySlider, tradeSlider, religionSlider, duelButton, duelResultText, backendCoordinator);
```

to:

```csharp
            duelController.Initialize(armySlider, tradeSlider, religionSlider, duelButton, duelResultText, backendCoordinator, duelModalGate);
```

- [ ] **Step 5: Confirm no interactive Unity Editor GUI window is open**

Run (PowerShell): `Get-Process -Name Unity -ErrorAction SilentlyContinue`
Expected: no output. If one is found, ask the user to close it.

- [ ] **Step 6: Run the new/updated tests**

Run (no `-quit`):
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter DuelButtonControllerTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-dmg-duel-playmode.xml"
```
Expected: XML shows 2/2 passed, 0 failed.

- [ ] **Step 7: Run the full EditMode + PlayMode suite**

Expected: zero failures. `CoreLoopSceneBuilder.cs` compiles even though `historyController.Initialize(...)`/`councilController.Initialize(...)` don't yet reference `duelModalGate` — those two controllers' signatures haven't changed yet in this task.

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/UI/DuelModalGate.cs Assets/Scripts/UI/DuelButtonController.cs Assets/Tests/PlayMode/DuelButtonControllerTests.cs Assets/Editor/CoreLoopSceneBuilder.cs
git commit -m "feat: add DuelModalGate and wire it into DuelButtonController"
```

---

### Task 2: `HistoryPanelController`

**Files:**
- Modify: `Assets/Scripts/UI/HistoryPanelController.cs`
- Modify: `Assets/Tests/PlayMode/HistoryPanelControllerTests.cs`
- Modify: `Assets/Tests/PlayMode/HistoryPanelControllerRealDataTests.cs`
- Modify: `Assets/Editor/CoreLoopSceneBuilder.cs` (history section only)

**Interfaces:**
- Consumes: `DuelModalGate` (Task 1).
- Produces: `HistoryPanelController.Initialize(...)` gains a trailing `DuelModalGate gate` parameter — for Task 4's interleaving test to consume.

- [ ] **Step 1: Update `Assets/Scripts/UI/HistoryPanelController.cs`**

Change:

```csharp
        [SerializeField] private Button submitButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button councilButton;
```

to:

```csharp
        [SerializeField] private Button submitButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button councilButton;
        [SerializeField] private DuelModalGate gate;
```

Change:

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
            Button councilButton)
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

            Bind();
        }
```

to:

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
            DuelModalGate gate)
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
            this.gate = gate;

            Bind();
        }
```

Change:

```csharp
        private void OnViewHistory()
        {
            SetCoreLoopControlsInteractable(false);
            panelRoot.SetActive(true);
```

to:

```csharp
        private void OnViewHistory()
        {
            gate.IsModalOpen = true;
            SetCoreLoopControlsInteractable(false);
            panelRoot.SetActive(true);
```

Change:

```csharp
        private void OnClose()
        {
            panelRoot.SetActive(false);
            SetCoreLoopControlsInteractable(true);
        }

        private void SetCoreLoopControlsInteractable(bool interactable)
        {
            viewHistoryButton.interactable = interactable;
            armySlider.interactable = interactable;
            tradeSlider.interactable = interactable;
            religionSlider.interactable = interactable;
            submitButton.interactable = interactable;
            challengeButton.interactable = interactable;
            councilButton.interactable = interactable;
        }
```

to:

```csharp
        private void OnClose()
        {
            panelRoot.SetActive(false);
            gate.IsModalOpen = false;
            SetCoreLoopControlsInteractable(true);
        }

        private void SetCoreLoopControlsInteractable(bool interactable)
        {
            viewHistoryButton.interactable = interactable;
            armySlider.interactable = interactable;
            tradeSlider.interactable = interactable;
            religionSlider.interactable = interactable;
            submitButton.interactable = interactable;
            councilButton.interactable = interactable;

            // challengeButton has two independent disablers (this modal, and
            // Duel's own in-flight state) -- opening always disables it
            // unconditionally (a modal should always cover Challenge), but
            // enabling skips it while gate.IsDuelInFlight is still true;
            // DuelButtonController's own completion handler re-enables it
            // once the real duel resolves. See
            // docs/superpowers/specs/2026-09-03-duel-modal-gate-design.md.
            if (interactable && gate.IsDuelInFlight)
            {
                return;
            }
            challengeButton.interactable = interactable;
        }
```

- [ ] **Step 2: Update `Assets/Tests/PlayMode/HistoryPanelControllerTests.cs`**

Add a field next to the existing ones:

```csharp
        private TextMeshProUGUI[] rowTexts;
```

to:

```csharp
        private TextMeshProUGUI[] rowTexts;
        private DuelModalGate gate;
```

Change the `SetUp` method's controller construction:

```csharp
            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<HistoryPanelController>();
            controller.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, coordinator,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, councilButton);
```

to:

```csharp
            gate = new DuelModalGate();

            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<HistoryPanelController>();
            controller.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, coordinator,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, councilButton, gate);
```

Add this test at the end of the class, after `Close_ReEnablesControlsAndHidesPanel`:

```csharp
        [Test]
        public void Close_WithDuelInFlight_LeavesChallengeButtonDisabled()
        {
            gate.IsDuelInFlight = true;

            viewHistoryButton.onClick.Invoke();
            closeButton.onClick.Invoke();

            Assert.IsTrue(viewHistoryButton.interactable);
            Assert.IsTrue(armySlider.interactable);
            Assert.IsTrue(tradeSlider.interactable);
            Assert.IsTrue(religionSlider.interactable);
            Assert.IsTrue(submitButton.interactable);
            Assert.IsTrue(councilButton.interactable);
            Assert.IsFalse(challengeButton.interactable,
                "closing History while a duel is still in flight must NOT re-enable Challenge -- this is the exact bug DuelModalGate fixes");
        }
```

- [ ] **Step 3: Update `Assets/Tests/PlayMode/HistoryPanelControllerRealDataTests.cs`**

Add a field next to the existing ones:

```csharp
        private TextMeshProUGUI[] rowTexts;
```

to:

```csharp
        private TextMeshProUGUI[] rowTexts;
        private DuelModalGate gate;
```

Change the controller construction in `UnitySetUp`:

```csharp
            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<HistoryPanelController>();
            controller.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, coordinator,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, councilButton);
```

to:

```csharp
            gate = new DuelModalGate();

            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<HistoryPanelController>();
            controller.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, coordinator,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, councilButton, gate);
```

- [ ] **Step 4: Update `Assets/Editor/CoreLoopSceneBuilder.cs`'s history section**

Change:

```csharp
            historyController.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, backendCoordinator,
                armySlider, tradeSlider, religionSlider, button, duelButton, councilButton);
```

to:

```csharp
            historyController.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, backendCoordinator,
                armySlider, tradeSlider, religionSlider, button, duelButton, councilButton, duelModalGate);
```

- [ ] **Step 5: Confirm no interactive Unity Editor GUI window is open**

Run (PowerShell): `Get-Process -Name Unity -ErrorAction SilentlyContinue`
Expected: no output.

- [ ] **Step 6: Run the new/updated tests**

Run (no `-quit`):
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter HistoryPanelControllerTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-dmg-history-playmode.xml"
```
Expected: XML shows 4/4 passed, 0 failed.

- [ ] **Step 7: Run the full EditMode + PlayMode suite**

Expected: zero failures, count grows by this task's 1 new test.

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/UI/HistoryPanelController.cs Assets/Tests/PlayMode/HistoryPanelControllerTests.cs Assets/Tests/PlayMode/HistoryPanelControllerRealDataTests.cs Assets/Editor/CoreLoopSceneBuilder.cs
git commit -m "feat: wire DuelModalGate into HistoryPanelController"
```

---

### Task 3: `CouncilPanelController`

**Files:**
- Modify: `Assets/Scripts/UI/CouncilPanelController.cs`
- Modify: `Assets/Tests/PlayMode/CouncilPanelControllerTests.cs`
- Modify: `Assets/Tests/PlayMode/CouncilPanelControllerRealDataTests.cs`
- Modify: `Assets/Editor/CoreLoopSceneBuilder.cs` (council section only)

**Interfaces:**
- Consumes: `DuelModalGate` (Task 1).
- Produces: `CouncilPanelController.Initialize(...)` gains a trailing `DuelModalGate gate` parameter — for Task 4's interleaving test to consume if needed (Task 4 uses History as the representative modal, but Council's own fix must be equally real and tested here).

- [ ] **Step 1: Update `Assets/Scripts/UI/CouncilPanelController.cs`**

Change:

```csharp
        [SerializeField] private Button submitButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button viewHistoryButton;
```

to:

```csharp
        [SerializeField] private Button submitButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button viewHistoryButton;
        [SerializeField] private DuelModalGate gate;
```

Change:

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
            Button viewHistoryButton)
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

            Bind();
        }
```

to:

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
            DuelModalGate gate)
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
            this.gate = gate;

            Bind();
        }
```

Change:

```csharp
        private void OnCouncilButtonClicked()
        {
            SetCoreLoopControlsInteractable(false);
            panelRoot.SetActive(true);
            notInCouncilView.SetActive(false);
            inCouncilView.SetActive(false);
            statusMessageText.text = "Loading...";
```

to:

```csharp
        private void OnCouncilButtonClicked()
        {
            gate.IsModalOpen = true;
            SetCoreLoopControlsInteractable(false);
            panelRoot.SetActive(true);
            notInCouncilView.SetActive(false);
            inCouncilView.SetActive(false);
            statusMessageText.text = "Loading...";
```

Change:

```csharp
        private void OnClose()
        {
            panelRoot.SetActive(false);
            SetCoreLoopControlsInteractable(true);
        }

        private void SetCoreLoopControlsInteractable(bool interactable)
        {
            councilButton.interactable = interactable;
            viewHistoryButton.interactable = interactable;
            armySlider.interactable = interactable;
            tradeSlider.interactable = interactable;
            religionSlider.interactable = interactable;
            submitButton.interactable = interactable;
            challengeButton.interactable = interactable;
        }
```

to:

```csharp
        private void OnClose()
        {
            panelRoot.SetActive(false);
            gate.IsModalOpen = false;
            SetCoreLoopControlsInteractable(true);
        }

        private void SetCoreLoopControlsInteractable(bool interactable)
        {
            councilButton.interactable = interactable;
            viewHistoryButton.interactable = interactable;
            armySlider.interactable = interactable;
            tradeSlider.interactable = interactable;
            religionSlider.interactable = interactable;
            submitButton.interactable = interactable;

            // challengeButton has two independent disablers (this modal, and
            // Duel's own in-flight state) -- opening always disables it
            // unconditionally (a modal should always cover Challenge), but
            // enabling skips it while gate.IsDuelInFlight is still true;
            // DuelButtonController's own completion handler re-enables it
            // once the real duel resolves. See
            // docs/superpowers/specs/2026-09-03-duel-modal-gate-design.md.
            if (interactable && gate.IsDuelInFlight)
            {
                return;
            }
            challengeButton.interactable = interactable;
        }
```

- [ ] **Step 2: Update `Assets/Tests/PlayMode/CouncilPanelControllerTests.cs`**

Add a field next to the existing ones:

```csharp
        private TextMeshProUGUI statusMessageText;
```

to:

```csharp
        private TextMeshProUGUI statusMessageText;
        private DuelModalGate gate;
```

Change the `SetUp` method's controller construction:

```csharp
            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<CouncilPanelController>();
            controller.Initialize(councilButton, panelRootObject, closeButton, notInCouncilViewObject, inCouncilViewObject,
                nameInputField, createButton, joinCodeInputField, joinButton, statusMessageText,
                nameLabel, joinCodeLabel, memberCountLabel, progressLabel, rewardStatusLabel,
                coordinator, manager, screenController,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton);
```

to:

```csharp
            gate = new DuelModalGate();

            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<CouncilPanelController>();
            controller.Initialize(councilButton, panelRootObject, closeButton, notInCouncilViewObject, inCouncilViewObject,
                nameInputField, createButton, joinCodeInputField, joinButton, statusMessageText,
                nameLabel, joinCodeLabel, memberCountLabel, progressLabel, rewardStatusLabel,
                coordinator, manager, screenController,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, gate);
```

Add this test at the end of the class, after `Close_ReEnablesControlsAndHidesPanel`:

```csharp
        [Test]
        public void Close_WithDuelInFlight_LeavesChallengeButtonDisabled()
        {
            gate.IsDuelInFlight = true;

            councilButton.onClick.Invoke();
            closeButton.onClick.Invoke();

            Assert.IsTrue(councilButton.interactable);
            Assert.IsTrue(viewHistoryButton.interactable);
            Assert.IsTrue(armySlider.interactable);
            Assert.IsTrue(tradeSlider.interactable);
            Assert.IsTrue(religionSlider.interactable);
            Assert.IsTrue(submitButton.interactable);
            Assert.IsFalse(challengeButton.interactable,
                "closing Council while a duel is still in flight must NOT re-enable Challenge -- this is the exact bug DuelModalGate fixes");
        }
```

- [ ] **Step 3: Update `Assets/Tests/PlayMode/CouncilPanelControllerRealDataTests.cs`**

Add a field next to the existing ones:

```csharp
        private TextMeshProUGUI rewardStatusLabel;
```

to:

```csharp
        private TextMeshProUGUI rewardStatusLabel;
        private DuelModalGate gate;
```

Change the controller construction in `UnitySetUp`:

```csharp
            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<CouncilPanelController>();
            controller.Initialize(councilButton, panelRootObject, closeButton, notInCouncilViewObject, inCouncilViewObject,
                nameInputField, createButton, joinCodeInputField, joinButton, statusMessageText,
                nameLabel, joinCodeLabel, memberCountLabel, progressLabel, rewardStatusLabel,
                coordinator, manager, screenController,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton);
```

to:

```csharp
            gate = new DuelModalGate();

            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<CouncilPanelController>();
            controller.Initialize(councilButton, panelRootObject, closeButton, notInCouncilViewObject, inCouncilViewObject,
                nameInputField, createButton, joinCodeInputField, joinButton, statusMessageText,
                nameLabel, joinCodeLabel, memberCountLabel, progressLabel, rewardStatusLabel,
                coordinator, manager, screenController,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, gate);
```

- [ ] **Step 4: Update `Assets/Editor/CoreLoopSceneBuilder.cs`'s council section**

Change:

```csharp
            councilController.Initialize(councilButton, councilPanelRootObject, councilCloseButton, notInCouncilViewObject, inCouncilViewObject,
                nameInputField, createButton, joinCodeInputField, joinButton, councilStatusMessageText,
                councilNameLabel, councilJoinCodeLabel, councilMemberCountLabel, councilProgressLabel, councilRewardStatusLabel,
                backendCoordinator, manager, controller,
                armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton);
```

to:

```csharp
            councilController.Initialize(councilButton, councilPanelRootObject, councilCloseButton, notInCouncilViewObject, inCouncilViewObject,
                nameInputField, createButton, joinCodeInputField, joinButton, councilStatusMessageText,
                councilNameLabel, councilJoinCodeLabel, councilMemberCountLabel, councilProgressLabel, councilRewardStatusLabel,
                backendCoordinator, manager, controller,
                armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton, duelModalGate);
```

- [ ] **Step 5: Confirm no interactive Unity Editor GUI window is open**

Run (PowerShell): `Get-Process -Name Unity -ErrorAction SilentlyContinue`
Expected: no output.

- [ ] **Step 6: Run the new/updated tests**

Run (no `-quit`):
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter CouncilPanelControllerTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-dmg-council-playmode.xml"
```
Expected: XML shows 3/3 passed, 0 failed.

- [ ] **Step 7: Run the full EditMode + PlayMode suite**

Expected: zero failures, count grows by this task's 1 new test.

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/UI/CouncilPanelController.cs Assets/Tests/PlayMode/CouncilPanelControllerTests.cs Assets/Tests/PlayMode/CouncilPanelControllerRealDataTests.cs Assets/Editor/CoreLoopSceneBuilder.cs
git commit -m "feat: wire DuelModalGate into CouncilPanelController"
```

---

### Task 4: Cross-controller interleaving test, full regression, manual verification

**Files:**
- Create: `Assets/Tests/PlayMode/DuelModalGateInterleavingTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1-3.
- Produces: no further consumers — this is the final proof this milestone needs.

**Why only one new test file, and why it only exercises Duel+History (not Duel+Council):** History and Council apply the identical shape of fix (already proven independently in Tasks 2 and 3's own isolated tests). The interleaving that actually needs a real cross-controller proof is Case A from the spec's Data Flow section — a duel resolving while a modal is open, then the modal closing afterward — and either modal is equally representative of that composition; History is chosen arbitrarily as the one wired here. The reverse ordering (a modal open, then a duel starting) is structurally unreachable in the real UI: `challengeButton` is already disabled the moment any modal opens, so `OnChallenge` can never fire while a modal is up — not worth a contrived test for a path that cannot occur.

- [ ] **Step 1: Write `Assets/Tests/PlayMode/DuelModalGateInterleavingTests.cs`**

```csharp
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    /// <summary>
    /// Proves the interleaving DuelModalGate exists to fix -- neither
    /// DuelButtonController's nor HistoryPanelController's own isolated
    /// tests exercise both real controllers sharing one real gate instance
    /// at once. See docs/superpowers/specs/2026-09-03-duel-modal-gate-design.md.
    /// </summary>
    public class DuelModalGateInterleavingTests
    {
        private GameObject coordinatorObject;
        private GameObject duelControllerObject;
        private GameObject historyControllerObject;
        private GameObject canvasObject;
        private GameObject panelRootObject;
        private Slider armySlider;
        private Slider tradeSlider;
        private Slider religionSlider;
        private Button submitButton;
        private Button challengeButton;
        private Button councilButton;
        private Button viewHistoryButton;
        private Button closeButton;
        private TextMeshProUGUI resultText;
        private TextMeshProUGUI[] rowTexts;
        private DuelModalGate gate;

        [SetUp]
        public void SetUp()
        {
            // Built inactive so Start() never runs on the coordinator --
            // currentSession stays null, giving both RequestDuel's and
            // RequestHistory's synchronous no-session error paths with zero
            // network dependency. This test only cares about local
            // interactable-flag bookkeeping, not real duel/history results.
            coordinatorObject = new GameObject("Coordinator");
            coordinatorObject.SetActive(false);
            var coordinator = coordinatorObject.AddComponent<BackendSyncCoordinator>();

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

            var councilButtonObject = new GameObject("CouncilButton", typeof(Image), typeof(Button));
            councilButtonObject.transform.SetParent(canvasObject.transform, false);
            councilButton = councilButtonObject.GetComponent<Button>();

            var viewHistoryButtonObject = new GameObject("ViewHistoryButton", typeof(Image), typeof(Button));
            viewHistoryButtonObject.transform.SetParent(canvasObject.transform, false);
            viewHistoryButton = viewHistoryButtonObject.GetComponent<Button>();

            var resultObject = new GameObject("ResultText", typeof(TextMeshProUGUI));
            resultObject.transform.SetParent(canvasObject.transform, false);
            resultText = resultObject.GetComponent<TextMeshProUGUI>();

            panelRootObject = new GameObject("PanelRoot");
            panelRootObject.transform.SetParent(canvasObject.transform, false);

            var closeButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            closeButtonObject.transform.SetParent(panelRootObject.transform, false);
            closeButton = closeButtonObject.GetComponent<Button>();

            rowTexts = new TextMeshProUGUI[10];
            for (int i = 0; i < rowTexts.Length; i++)
            {
                var rowObject = new GameObject($"Row{i}", typeof(TextMeshProUGUI));
                rowObject.transform.SetParent(panelRootObject.transform, false);
                rowTexts[i] = rowObject.GetComponent<TextMeshProUGUI>();
            }

            gate = new DuelModalGate();

            duelControllerObject = new GameObject("DuelController");
            var duelController = duelControllerObject.AddComponent<DuelButtonController>();
            duelController.Initialize(armySlider, tradeSlider, religionSlider, challengeButton, resultText, coordinator, gate);

            historyControllerObject = new GameObject("HistoryController");
            var historyController = historyControllerObject.AddComponent<HistoryPanelController>();
            historyController.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, coordinator,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, councilButton, gate);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(duelControllerObject);
            Object.DestroyImmediate(historyControllerObject);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(coordinatorObject);
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

        [Test]
        public void DuelInFlightThenModalOpensAndCloses_ChallengeOnlyReenablesWhenDuelActuallyResolves()
        {
            var duelController = duelControllerObject.GetComponent<DuelButtonController>();

            // Real OnChallenge() disables Challenge and sets gate.IsDuelInFlight.
            challengeButton.onClick.Invoke();
            Assert.IsFalse(challengeButton.interactable);
            Assert.IsTrue(gate.IsDuelInFlight);

            // Real HistoryPanelController opens while the duel is still in flight.
            viewHistoryButton.onClick.Invoke();
            Assert.IsTrue(gate.IsModalOpen);
            Assert.IsFalse(challengeButton.interactable, "modal open must keep Challenge disabled");

            // Real HandleError fires (simulating the duel resolving) while
            // the modal is still open -- Challenge must stay disabled since
            // the modal owns it right now. HandleError is private; invoke it
            // via reflection, the same established technique used elsewhere
            // in this project's PlayMode tests for internal state.
            MethodInfo handleError = typeof(DuelButtonController).GetMethod("HandleError", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(handleError, "HandleError method not found -- DuelButtonController internals changed");
            handleError.Invoke(duelController, new object[] { "simulated failure" });

            Assert.IsFalse(gate.IsDuelInFlight);
            Assert.IsFalse(challengeButton.interactable,
                "the duel resolving while a modal is open must NOT re-enable Challenge underneath it");

            // Real modal closes -- now, and only now, Challenge re-enables.
            closeButton.onClick.Invoke();
            Assert.IsTrue(challengeButton.interactable,
                "closing the modal after the duel has already resolved must re-enable Challenge -- this is the exact bug DuelModalGate fixes");
        }
    }
}
```

- [ ] **Step 2: Confirm no interactive Unity Editor GUI window is open**

Run (PowerShell): `Get-Process -Name Unity -ErrorAction SilentlyContinue`
Expected: no output.

- [ ] **Step 3: Run the new test**

Run (no `-quit`):
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testFilter DuelModalGateInterleavingTests -testResults "C:\Users\rajes\understudy-kingdom\test-results-dmg-interleaving-playmode.xml"
```
Expected: XML shows 1/1 passed, 0 failed.

- [ ] **Step 4: Rebuild and verify the scene**

Run (uses `-quit`, correct here — this is `-executeMethod`, not `-runTests`):
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Build
```
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Verify
```
Expected: both exit 0. `Verify()` needs no code changes this milestone (`DuelModalGate` isn't a `MonoBehaviour`, nothing new to `FindFirstObjectByType`) — this step just confirms the rebuilt scene still opens and finds all 4 existing controllers cleanly.

- [ ] **Step 5: Run the full EditMode + PlayMode suite (no `-quit`, per Global Constraints)**

Run both:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-dmg-final-editmode.xml"
```
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-dmg-final-playmode.xml"
```
Expected: both XML files show zero failures.

- [ ] **Step 6: Commit**

```bash
git add Assets/Tests/PlayMode/DuelModalGateInterleavingTests.cs Assets/Scenes/CoreLoop.unity
git commit -m "test: add cross-controller interleaving proof for DuelModalGate"
```

- [ ] **Step 7: Manual Play Mode verification (human)**

This step cannot be scripted — the automated tests above prove the local
state-composition logic is correct, but only a real Play Mode session
exercises the actual real-network duel timing this bug lives in. Ask the
user to:
1. Confirm no other Unity Editor GUI window is open, then open
   `Assets/Scenes/CoreLoop.unity` and press Play.
2. Click "Challenge a Rival Kingdom" — while it's resolving ("Resolving..."
   showing), quickly click "View History" (or "Council").
3. Confirm the modal opens normally over the greyed-out screen.
4. Wait for the duel to actually resolve in the background (Console/narration
   won't be visible behind the modal, that's expected), then close the modal.
5. Confirm Challenge is now clickable again, and the duel result narration
   is visible.
6. Optionally, reverse the order: open History, close it quickly, then
   immediately click Challenge before it resolves, then open History again
   while the duel is still resolving, then close it — confirm Challenge only
   becomes clickable once the real duel result has actually arrived, not the
   moment the modal closes.
7. Stop Play Mode, confirm no Console errors.

If any step reveals a real bug, fix it directly, re-verify the full suite,
and ask the user to retest before proceeding to `finishing-a-development-branch`.
