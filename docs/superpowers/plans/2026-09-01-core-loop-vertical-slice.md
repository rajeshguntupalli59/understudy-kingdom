# Core Loop Vertical Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first playable scene for Understudy Kingdom — a Canvas UI wired to the existing, already-tested `DecisionCycleManager` core loop, runnable in Unity Editor Play Mode.

**Architecture:** A pure-C# `SliderRebalancer` (no UnityEngine dependency, EditMode-testable) does the three-slider rebalance math. A `CoreLoopScreenController` MonoBehaviour wires Canvas widgets to it and to `DecisionCycleManager.SubmitRecommendation` — no new decision logic. An Editor-only script builds and saves `Assets/Scenes/CoreLoop.unity` programmatically (hand-authoring Unity scene YAML is error-prone; building it via script is reproducible and inspectable).

**Tech Stack:** Unity 6000.3.23f1 (already installed/licensed at `C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe`), C#, NUnit via Unity Test Framework (EditMode + PlayMode), uGUI (`UnityEngine.UI`) + TextMeshPro (`TMPro`), both already present via the `com.unity.ugui` package (see `Packages/manifest.json`).

## Global Constraints

- No changes to `DecisionCycleManager`, `OverrideEvaluator`, `DialogueTemplateEngine`, `RulerState`, or `SaveService` — this milestone is UI wiring only, per `docs/superpowers/specs/2026-09-01-core-loop-vertical-slice-design.md`.
- Target is Unity Editor Play Mode only — no Android/iOS build in this plan.
- One recommendation type only: `ResourceAllocation` (Army/Trade/Religion, sum to 100).
- The existing 22 EditMode tests must continue to pass unchanged after every task.
- Every git commit message ends with:
  ```
  Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_017JNi1ThZutdGUt6toaGBmq
  ```

---

## Task 1: `SliderRebalancer` — pure rebalance math + EditMode tests

**Files:**
- Create: `Assets/Scripts/UI/SliderRebalancer.cs`
- Create: `Assets/Scripts/UI/SliderRebalancer.cs.meta` (Unity auto-generates on next Editor open; if running headless between tasks, this is fine to leave to Unity — do not hand-author `.meta` files)
- Test: `Assets/Tests/EditMode/SliderRebalancerTests.cs`

**Interfaces:**
- Produces: `UnderstudyKingdom.UI.SliderRebalancer.Rebalance(int a, int b, int c, int changedIndex, int newValue) -> (int a, int b, int c)` — a pure static method. `changedIndex` is 0 (a), 1 (b), or 2 (c). Always returns three non-negative ints summing to exactly 100 (given `a+b+c==100` on input and `0<=newValue<=100`... `newValue` is clamped internally regardless).
- Consumes: nothing (no dependencies).

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/SliderRebalancerTests.cs`:

```csharp
using NUnit.Framework;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    public class SliderRebalancerTests
    {
        [Test]
        public void ChangingOneValue_OthersAbsorbRemainderProportionally()
        {
            var (a, b, c) = SliderRebalancer.Rebalance(40, 30, 30, changedIndex: 0, newValue: 70);

            Assert.AreEqual(70, a);
            Assert.AreEqual(15, b);
            Assert.AreEqual(15, c);
            Assert.AreEqual(100, a + b + c);
        }

        [Test]
        public void ChangedValueSetToMaximum_OthersGoToZero()
        {
            var (a, b, c) = SliderRebalancer.Rebalance(40, 30, 30, changedIndex: 1, newValue: 100);

            Assert.AreEqual(0, a);
            Assert.AreEqual(100, b);
            Assert.AreEqual(0, c);
        }

        [Test]
        public void ChangedValueSetToZero_OthersAbsorbFullRemainder()
        {
            var (a, b, c) = SliderRebalancer.Rebalance(20, 50, 30, changedIndex: 2, newValue: 0);

            Assert.AreEqual(20, a);
            Assert.AreEqual(80, b);
            Assert.AreEqual(0, c);
            Assert.AreEqual(100, a + b + c);
        }

        [Test]
        public void BothOtherValuesAreZero_RemainderSplitsEvenly()
        {
            var (a, b, c) = SliderRebalancer.Rebalance(100, 0, 0, changedIndex: 0, newValue: 40);

            Assert.AreEqual(40, a);
            Assert.AreEqual(30, b);
            Assert.AreEqual(30, c);
            Assert.AreEqual(100, a + b + c);
        }

        [Test]
        public void BothOtherValuesAreZero_OddRemainderSplitsWithExtraOnSecondOther()
        {
            var (a, b, c) = SliderRebalancer.Rebalance(100, 0, 0, changedIndex: 0, newValue: 1);

            Assert.AreEqual(1, a);
            Assert.AreEqual(49, b);
            Assert.AreEqual(50, c);
            Assert.AreEqual(100, a + b + c);
        }

        [Test]
        public void NewValueAboveMaximum_IsClampedTo100()
        {
            var (a, b, c) = SliderRebalancer.Rebalance(40, 30, 30, changedIndex: 0, newValue: 150);

            Assert.AreEqual(100, a);
            Assert.AreEqual(0, b);
            Assert.AreEqual(0, c);
        }

        [Test]
        public void NewValueBelowMinimum_IsClampedTo0()
        {
            var (a, b, c) = SliderRebalancer.Rebalance(40, 30, 30, changedIndex: 0, newValue: -20);

            Assert.AreEqual(0, a);
            Assert.AreEqual(50, b);
            Assert.AreEqual(50, c);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:
```bash
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" \
  -runTests -batchmode \
  -projectPath "C:\Users\rajes\understudy-kingdom" \
  -testResults "C:\Users\rajes\understudy-kingdom\TestResults\task1.xml" \
  -testPlatform EditMode \
  -testFilter "UnderstudyKingdom.Tests.SliderRebalancerTests" \
  -logFile "C:\Users\rajes\understudy-kingdom\TestResults\task1.log" \
  -nographics
```
Expected: exit code 2 (test run failed) — the log will show a compile error (`SliderRebalancer` does not exist in namespace `UnderstudyKingdom.UI`), since `TestResults/task1.xml` may not even be produced. This compile-error failure is the expected "red" state for this step; confirm the log shows `error CS0234` (or similar "type or namespace not found") referencing `SliderRebalancer`.

- [ ] **Step 3: Write the minimal implementation**

Create `Assets/Scripts/UI/SliderRebalancer.cs`:

```csharp
namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Pure rebalance math for the three-slider (Army/Trade/Religion) allocation
    /// input, kept free of UnityEngine dependencies so it's directly unit-testable.
    /// See docs/superpowers/specs/2026-09-01-core-loop-vertical-slice-design.md.
    /// </summary>
    public static class SliderRebalancer
    {
        /// <summary>
        /// Given three values that summed to 100, and one of them (at
        /// changedIndex) being set to newValue, returns all three values
        /// re-adjusted so they sum to exactly 100 again. The two values NOT at
        /// changedIndex absorb the remainder in proportion to their current
        /// relative weight; if both are zero, the remainder is split evenly
        /// (with any odd remainder unit going to the second of the two).
        /// newValue is clamped to [0, 100] before rebalancing.
        /// </summary>
        public static (int, int, int) Rebalance(int a, int b, int c, int changedIndex, int newValue)
        {
            int[] values = { a, b, c };
            newValue = Clamp(newValue, 0, 100);
            int remainder = 100 - newValue;

            int otherIndex1 = (changedIndex + 1) % 3;
            int otherIndex2 = (changedIndex + 2) % 3;

            int other1 = values[otherIndex1];
            int other2 = values[otherIndex2];
            int otherSum = other1 + other2;

            int newOther1;
            int newOther2;

            if (otherSum <= 0)
            {
                newOther1 = remainder / 2;
                newOther2 = remainder - newOther1;
            }
            else
            {
                newOther1 = (int)System.Math.Round(remainder * (other1 / (double)otherSum));
                newOther2 = remainder - newOther1;
            }

            values[changedIndex] = newValue;
            values[otherIndex1] = newOther1;
            values[otherIndex2] = newOther2;

            return (values[0], values[1], values[2]);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run the same command as Step 2.
Expected: exit code 0. Read `TestResults/task1.xml` and confirm `<test-run ... result="Passed" total="7" passed="7" failed="0" .../>`.

- [ ] **Step 5: Commit**

```bash
cd "C:\Users\rajes\understudy-kingdom"
git add Assets/Scripts/UI/SliderRebalancer.cs Assets/Tests/EditMode/SliderRebalancerTests.cs
git commit -m "feat: add SliderRebalancer pure rebalance math for the allocation UI

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_017JNi1ThZutdGUt6toaGBmq"
```

(If `Assets/Scripts/UI/SliderRebalancer.cs.meta` or `Assets/Tests/EditMode/SliderRebalancerTests.cs.meta` exist after Unity has touched the project, `git add` them too — check `git status --short` first.)

---

## Task 2: `CoreLoopScreenController` + PlayMode test assembly + tests

**Files:**
- Modify: `Assets/Scripts/UnderstudyKingdom.Runtime.asmdef`
- Create: `Assets/Scripts/UI/CoreLoopScreenController.cs`
- Create: `Assets/Tests/PlayMode/UnderstudyKingdom.PlayModeTests.asmdef`
- Test: `Assets/Tests/PlayMode/CoreLoopScreenControllerTests.cs`

**Interfaces:**
- Consumes: `SliderRebalancer.Rebalance(int,int,int,int,int) -> (int,int,int)` (Task 1). `UnderstudyKingdom.Core.DecisionCycleManager` — public fields `RulerNpcController Ruler`, method `string SubmitRecommendation(ResourceAllocation, double roll)` (existing). `UnderstudyKingdom.Core.ResourceAllocation(int army, int trade, int religion)` (existing). `UnderstudyKingdom.Npc.RulerNpcController` — public field `RulerState State` (existing, `RulerState` has public `int Mood`, `int Loyalty`, `AgendaType Agenda`, existing).
- Produces: `UnderstudyKingdom.UI.CoreLoopScreenController` — a `MonoBehaviour` with `public void Initialize(DecisionCycleManager manager, UnityEngine.UI.Slider armySlider, UnityEngine.UI.Slider tradeSlider, UnityEngine.UI.Slider religionSlider, TMPro.TextMeshProUGUI moodLabel, TMPro.TextMeshProUGUI loyaltyLabel, TMPro.TextMeshProUGUI agendaLabel, TMPro.TextMeshProUGUI narrationText, UnityEngine.UI.Button submitButton)`. Task 3 (the scene builder) calls this exact method with this exact signature.

- [ ] **Step 1: Add UI package references to the Runtime asmdef**

Read `Assets/Scripts/UnderstudyKingdom.Runtime.asmdef` first to confirm current content matches below (it should, from the existing project), then replace its `references` array.

Current content:
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

New content:
```json
{
    "name": "UnderstudyKingdom.Runtime",
    "rootNamespace": "UnderstudyKingdom",
    "references": [
        "UnityEngine.UI",
        "Unity.TextMeshPro"
    ],
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

- [ ] **Step 2: Write the failing PlayMode tests**

Create `Assets/Tests/PlayMode/UnderstudyKingdom.PlayModeTests.asmdef`:

```json
{
    "name": "UnderstudyKingdom.PlayModeTests",
    "rootNamespace": "UnderstudyKingdom.Tests",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "UnderstudyKingdom.Runtime",
        "UnityEngine.UI",
        "Unity.TextMeshPro"
    ],
    "includePlatforms": [],
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

Create `Assets/Tests/PlayMode/CoreLoopScreenControllerTests.cs`:

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
    public class CoreLoopScreenControllerTests
    {
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

        [SetUp]
        public void SetUp()
        {
            rulerObject = new GameObject("Ruler");
            var ruler = rulerObject.AddComponent<RulerNpcController>();

            managerObject = new GameObject("Manager");
            manager = managerObject.AddComponent<DecisionCycleManager>();
            manager.Ruler = ruler;

            canvasObject = new GameObject("Canvas", typeof(Canvas));

            armySlider = CreateSlider("ArmySlider", 40);
            tradeSlider = CreateSlider("TradeSlider", 30);
            religionSlider = CreateSlider("ReligionSlider", 30);

            moodLabel = CreateLabel("MoodLabel");
            loyaltyLabel = CreateLabel("LoyaltyLabel");
            agendaLabel = CreateLabel("AgendaLabel");
            narrationText = CreateLabel("NarrationText");

            var buttonObject = new GameObject("SubmitButton", typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvasObject.transform, false);
            submitButton = buttonObject.GetComponent<Button>();

            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<CoreLoopScreenController>();
            controller.Initialize(manager, armySlider, tradeSlider, religionSlider,
                moodLabel, loyaltyLabel, agendaLabel, narrationText, submitButton);
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

        private TextMeshProUGUI CreateLabel(string name)
        {
            var labelObject = new GameObject(name, typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(canvasObject.transform, false);
            return labelObject.GetComponent<TextMeshProUGUI>();
        }

        [Test]
        public void ChangingOneSlider_RebalancesOtherTwoToKeepSumAt100()
        {
            armySlider.value = 70;

            int total = Mathf.RoundToInt(armySlider.value)
                + Mathf.RoundToInt(tradeSlider.value)
                + Mathf.RoundToInt(religionSlider.value);

            Assert.AreEqual(100, total);
            Assert.AreEqual(70, Mathf.RoundToInt(armySlider.value));
            Assert.AreEqual(15, Mathf.RoundToInt(tradeSlider.value));
            Assert.AreEqual(15, Mathf.RoundToInt(religionSlider.value));
        }

        [Test]
        public void Submit_UpdatesNarrationAndStatusLabels()
        {
            submitButton.onClick.Invoke();

            Assert.IsFalse(string.IsNullOrEmpty(narrationText.text));
            Assert.AreEqual($"Mood: {manager.Ruler.State.Mood}", moodLabel.text);
            Assert.AreEqual($"Loyalty: {manager.Ruler.State.Loyalty}", loyaltyLabel.text);
            Assert.AreEqual($"Agenda: {manager.Ruler.State.Agenda}", agendaLabel.text);
        }

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

- [ ] **Step 3: Run the tests to verify they fail**

```bash
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" \
  -runTests -batchmode \
  -projectPath "C:\Users\rajes\understudy-kingdom" \
  -testResults "C:\Users\rajes\understudy-kingdom\TestResults\task2.xml" \
  -testPlatform PlayMode \
  -testFilter "UnderstudyKingdom.Tests.CoreLoopScreenControllerTests" \
  -logFile "C:\Users\rajes\understudy-kingdom\TestResults\task2.log" \
  -nographics
```
Expected: exit code 2, log shows a compile error referencing `CoreLoopScreenController` (type not found) — expected red state.

- [ ] **Step 4: Write the minimal implementation**

Create `Assets/Scripts/UI/CoreLoopScreenController.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Wires the core-loop Canvas widgets to the existing, already-tested
    /// DecisionCycleManager. Contains no decision logic of its own -- every
    /// value it displays or submits comes from DecisionCycleManager /
    /// SliderRebalancer. See
    /// docs/superpowers/specs/2026-09-01-core-loop-vertical-slice-design.md.
    /// </summary>
    public class CoreLoopScreenController : MonoBehaviour
    {
        [SerializeField] private DecisionCycleManager manager;
        [SerializeField] private Slider armySlider;
        [SerializeField] private Slider tradeSlider;
        [SerializeField] private Slider religionSlider;
        [SerializeField] private TextMeshProUGUI moodLabel;
        [SerializeField] private TextMeshProUGUI loyaltyLabel;
        [SerializeField] private TextMeshProUGUI agendaLabel;
        [SerializeField] private TextMeshProUGUI narrationText;
        [SerializeField] private Button submitButton;

        private bool rebalancing;

        private void Start()
        {
            Bind();
        }

        /// <summary>
        /// Assigns all dependencies and binds listeners. Called by Start() in
        /// the real scene (fields pre-wired via the Inspector / scene builder),
        /// and callable directly by tests to bypass the Unity lifecycle timing
        /// entirely -- mirrors DecisionCycleManager.LoadPersistedStateIfPresent.
        /// </summary>
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

        private void Bind()
        {
            armySlider.onValueChanged.RemoveAllListeners();
            tradeSlider.onValueChanged.RemoveAllListeners();
            religionSlider.onValueChanged.RemoveAllListeners();
            submitButton.onClick.RemoveAllListeners();

            armySlider.onValueChanged.AddListener(v => OnSliderChanged(0, v));
            tradeSlider.onValueChanged.AddListener(v => OnSliderChanged(1, v));
            religionSlider.onValueChanged.AddListener(v => OnSliderChanged(2, v));
            submitButton.onClick.AddListener(OnSubmit);

            RefreshStatusLabels();
        }

        private void OnSliderChanged(int changedIndex, float newValueFloat)
        {
            if (rebalancing)
            {
                return;
            }

            Slider[] sliders = { armySlider, tradeSlider, religionSlider };
            int a = Mathf.RoundToInt(sliders[0].value);
            int t = Mathf.RoundToInt(sliders[1].value);
            int r = Mathf.RoundToInt(sliders[2].value);
            int newValue = Mathf.RoundToInt(newValueFloat);

            var (na, nt, nr) = SliderRebalancer.Rebalance(a, t, r, changedIndex, newValue);

            rebalancing = true;
            sliders[0].value = na;
            sliders[1].value = nt;
            sliders[2].value = nr;
            rebalancing = false;
        }

        private void OnSubmit()
        {
            var allocation = new ResourceAllocation(
                Mathf.RoundToInt(armySlider.value),
                Mathf.RoundToInt(tradeSlider.value),
                Mathf.RoundToInt(religionSlider.value));

            string narration = manager.SubmitRecommendation(allocation, Random.value);

            narrationText.text = narration;
            RefreshStatusLabels();
        }

        private void RefreshStatusLabels()
        {
            moodLabel.text = $"Mood: {manager.Ruler.State.Mood}";
            loyaltyLabel.text = $"Loyalty: {manager.Ruler.State.Loyalty}";
            agendaLabel.text = $"Agenda: {manager.Ruler.State.Agenda}";
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run the same command as Step 3.
Expected: exit code 0. Read `TestResults/task2.xml` and confirm `total="3" passed="3" failed="0"`.

- [ ] **Step 6: Run the full EditMode suite to confirm no regression**

```bash
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" \
  -runTests -batchmode \
  -projectPath "C:\Users\rajes\understudy-kingdom" \
  -testResults "C:\Users\rajes\understudy-kingdom\TestResults\task2-editmode.xml" \
  -testPlatform EditMode \
  -logFile "C:\Users\rajes\understudy-kingdom\TestResults\task2-editmode.log" \
  -nographics
```
Expected: exit code 0, `TestResults/task2-editmode.xml` shows the project's assembly (`UnderstudyKingdom.EditModeTests.dll`) at `total="23" passed="23" failed="0"` (22 existing + the 7 new `SliderRebalancerTests` from Task 1 = 29 — recompute the exact expected count from the assembly-level `<test-suite type="Assembly" name="UnderstudyKingdom.EditModeTests.dll" ...>` line in the XML rather than assuming a fixed number; the key assertion is `failed="0"` for that assembly).

- [ ] **Step 7: Commit**

```bash
cd "C:\Users\rajes\understudy-kingdom"
git add Assets/Scripts/UnderstudyKingdom.Runtime.asmdef Assets/Scripts/UI/CoreLoopScreenController.cs Assets/Tests/PlayMode/UnderstudyKingdom.PlayModeTests.asmdef Assets/Tests/PlayMode/CoreLoopScreenControllerTests.cs
git commit -m "feat: add CoreLoopScreenController wiring the core loop UI to DecisionCycleManager

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_017JNi1ThZutdGUt6toaGBmq"
```

(Check `git status --short` for any newly-generated `.meta` files under `Assets/Tests/PlayMode/` and `Assets/Scripts/UI/` and add those too.)

---

## Task 3: Scene builder + generate `CoreLoop.unity`

**Files:**
- Create: `Assets/Editor/CoreLoopSceneBuilder.cs`
- Create (generated by running the builder, not hand-authored): `Assets/Scenes/CoreLoop.unity`

**Interfaces:**
- Consumes: `CoreLoopScreenController.Initialize(...)` (Task 2, exact signature above). `DecisionCycleManager.Ruler` (public field, existing). `RulerNpcController` (existing, no-arg-constructible via `AddComponent`).
- Produces: the saved scene `Assets/Scenes/CoreLoop.unity`, containing a `Ruler`, `Manager`, `Canvas` (with 3 sliders, 4 labels, 1 button), `EventSystem`, and a `CoreLoopScreenController`-holding GameObject, all wired via `Initialize`.

- [ ] **Step 1: Write the scene builder script**

Create `Assets/Editor/CoreLoopSceneBuilder.cs`:

```csharp
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/CoreLoop.unity programmatically. Unity scene YAML
    /// is error-prone to hand-author (GUIDs, component fileIDs); building it
    /// via script is reproducible, inspectable, and re-runnable. See
    /// docs/superpowers/specs/2026-09-01-core-loop-vertical-slice-design.md.
    /// </summary>
    public static class CoreLoopSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/CoreLoop.unity";

        [MenuItem("Understudy Kingdom/Build Core Loop Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var rulerObject = new GameObject("Ruler");
            var ruler = rulerObject.AddComponent<RulerNpcController>();

            var managerObject = new GameObject("Manager");
            var manager = managerObject.AddComponent<DecisionCycleManager>();
            manager.Ruler = ruler;

            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            Slider armySlider = CreateSlider(canvasObject.transform, "ArmySlider", 40f, 40f);
            Slider tradeSlider = CreateSlider(canvasObject.transform, "TradeSlider", 90f, 30f);
            Slider religionSlider = CreateSlider(canvasObject.transform, "ReligionSlider", 140f, 30f);

            TextMeshProUGUI moodLabel = CreateLabel(canvasObject.transform, "MoodLabel", 200f, "Mood: 50");
            TextMeshProUGUI loyaltyLabel = CreateLabel(canvasObject.transform, "LoyaltyLabel", 240f, "Loyalty: 50");
            TextMeshProUGUI agendaLabel = CreateLabel(canvasObject.transform, "AgendaLabel", 280f, "Agenda: Expansionist");
            TextMeshProUGUI narrationText = CreateLabel(canvasObject.transform, "NarrationText", 340f, string.Empty);

            var buttonObject = new GameObject("SubmitButton", typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvasObject.transform, false);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchoredPosition = new Vector2(0f, -420f);
            buttonRect.sizeDelta = new Vector2(220f, 44f);
            var button = buttonObject.GetComponent<Button>();
            TextMeshProUGUI buttonLabel = CreateLabel(buttonObject.transform, "Text", 0f, "Submit Recommendation");
            var buttonLabelRect = buttonLabel.GetComponent<RectTransform>();
            buttonLabelRect.anchorMin = Vector2.zero;
            buttonLabelRect.anchorMax = Vector2.one;
            buttonLabelRect.sizeDelta = Vector2.zero;
            buttonLabelRect.anchoredPosition = Vector2.zero;

            var controllerObject = new GameObject("CoreLoopScreenController");
            var controller = controllerObject.AddComponent<CoreLoopScreenController>();
            controller.Initialize(manager, armySlider, tradeSlider, religionSlider,
                moodLabel, loyaltyLabel, agendaLabel, narrationText, button);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"CoreLoopSceneBuilder: saved scene to {ScenePath}");
        }

        private static Slider CreateSlider(Transform parent, string name, float yOffset, float initialValue)
        {
            var sliderObject = new GameObject(name, typeof(Slider));
            sliderObject.transform.SetParent(parent, false);
            var rect = sliderObject.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0f, -yOffset);
            rect.sizeDelta = new Vector2(320f, 20f);

            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;

            var backgroundObject = new GameObject("Background", typeof(Image));
            backgroundObject.transform.SetParent(sliderObject.transform, false);
            var bgRect = backgroundObject.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            backgroundObject.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaObject.transform.SetParent(sliderObject.transform, false);
            var fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = Vector2.zero;

            var fillObject = new GameObject("Fill", typeof(Image));
            fillObject.transform.SetParent(fillAreaObject.transform, false);
            var fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            fillObject.GetComponent<Image>().color = new Color(0.4f, 0.7f, 0.9f, 1f);

            slider.fillRect = fillRect;
            slider.targetGraphic = fillObject.GetComponent<Image>();
            slider.SetValueWithoutNotify(initialValue);

            return slider;
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
    }
}
```

- [ ] **Step 2: Run the builder headlessly to generate the scene**

```bash
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" \
  -batchmode \
  -projectPath "C:\Users\rajes\understudy-kingdom" \
  -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Build \
  -logFile "C:\Users\rajes\understudy-kingdom\TestResults\scenebuild.log" \
  -quit -nographics
```
Expected: exit code 0. Confirm `Assets/Scenes/CoreLoop.unity` now exists (`ls Assets/Scenes/`), and the log contains `CoreLoopSceneBuilder: saved scene to Assets/Scenes/CoreLoop.unity`.

- [ ] **Step 3: Add the scene to Build Settings**

```bash
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" \
  -batchmode \
  -projectPath "C:\Users\rajes\understudy-kingdom" \
  -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.AddToBuildSettings \
  -logFile "C:\Users\rajes\understudy-kingdom\TestResults\scenebuild2.log" \
  -quit -nographics
```

Before running this, add the following method to `Assets/Editor/CoreLoopSceneBuilder.cs` (inside the `CoreLoopSceneBuilder` class, alongside `Build`):

```csharp
        [MenuItem("Understudy Kingdom/Add Core Loop Scene To Build Settings")]
        public static void AddToBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool alreadyPresent = scenes.Exists(s => s.path == ScenePath);
            if (!alreadyPresent)
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log($"CoreLoopSceneBuilder: added {ScenePath} to Build Settings");
            }
            else
            {
                Debug.Log($"CoreLoopSceneBuilder: {ScenePath} already in Build Settings");
            }
        }
```
Expected: exit code 0, log shows either the "added" or "already present" message. Confirm with:
```bash
grep -A2 "m_Scenes" "C:\Users\rajes\understudy-kingdom\ProjectSettings\EditorBuildSettings.asset"
```
Expected: no longer `m_Scenes: []` — now contains an entry with `path: Assets/Scenes/CoreLoop.unity`.

- [ ] **Step 4: Run full EditMode + PlayMode suites once more to confirm nothing broke**

```bash
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" \
  -runTests -batchmode \
  -projectPath "C:\Users\rajes\understudy-kingdom" \
  -testResults "C:\Users\rajes\understudy-kingdom\TestResults\task3-editmode.xml" \
  -testPlatform EditMode \
  -logFile "C:\Users\rajes\understudy-kingdom\TestResults\task3-editmode.log" \
  -nographics
```
```bash
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" \
  -runTests -batchmode \
  -projectPath "C:\Users\rajes\understudy-kingdom" \
  -testResults "C:\Users\rajes\understudy-kingdom\TestResults\task3-playmode.xml" \
  -testPlatform PlayMode \
  -logFile "C:\Users\rajes\understudy-kingdom\TestResults\task3-playmode.log" \
  -nographics
```
Expected: both exit code 0, both XMLs show `failed="0"` for the `UnderstudyKingdom.*Tests.dll` assembly.

- [ ] **Step 5: Commit**

```bash
cd "C:\Users\rajes\understudy-kingdom"
git add Assets/Editor/CoreLoopSceneBuilder.cs Assets/Scenes/CoreLoop.unity ProjectSettings/EditorBuildSettings.asset
git commit -m "feat: add CoreLoop scene builder and generate the playable scene

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_017JNi1ThZutdGUt6toaGBmq"
```

(Check `git status --short` for generated `.meta` files under `Assets/Editor/` and `Assets/Scenes/` and add those too.)

---

## Task 4: Manual Play Mode verification

**Files:** none (verification only — no code changes in this task).

- [ ] **Step 1: Scene-integrity sanity check, driven headlessly via an Editor script**

This is a batch-mode sanity check, not a persistence test: it opens the built scene and confirms a `CoreLoopScreenController` is present, catching the case where the scene fails to load or the controller wiring is missing. It does **not** drive `EditorApplication.isPlaying` and does **not** verify persistence across a Play Mode stop/restart — that check is still manual, and is exactly what the human checkpoint in Step 2 below is for. Add this method to `Assets/Editor/CoreLoopSceneBuilder.cs`:

```csharp
        // Reachable only via -executeMethod, not the Editor menu: on failure in
        // batch mode it calls EditorApplication.Exit(1), which would kill the
        // Editor and discard unsaved work if it were exposed as a MenuItem.
        public static void Verify()
        {
            EditorSceneManager.OpenScene(ScenePath);

            var controller = Object.FindFirstObjectByType<CoreLoopScreenController>();
            if (controller == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no CoreLoopScreenController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            Debug.Log("CoreLoopSceneBuilder.Verify: scene opened and controller found successfully.");
        }
```
Run:
```bash
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" \
  -batchmode \
  -projectPath "C:\Users\rajes\understudy-kingdom" \
  -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Verify \
  -logFile "C:\Users\rajes\understudy-kingdom\TestResults\verify.log" \
  -quit -nographics
```
Expected: exit code 0, log contains "scene opened and controller found successfully."

- [ ] **Step 2: Human checkpoint**

Report to the user: the scene is built, all EditMode + PlayMode tests pass, and the scene loads cleanly with the controller present. Ask the user to open `Assets/Scenes/CoreLoop.unity` in the Unity Editor themselves, press Play, drag the sliders, click Submit a few times, and confirm the Mood/Loyalty/Agenda labels and narration text update and persist as expected — this is the one check that genuinely needs human eyes on the running game, not something to fake past.

- [ ] **Step 3: Commit the verification helper**

```bash
cd "C:\Users\rajes\understudy-kingdom"
git add Assets/Editor/CoreLoopSceneBuilder.cs
git commit -m "chore: add headless scene-open verification helper

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_017JNi1ThZutdGUt6toaGBmq"
```
