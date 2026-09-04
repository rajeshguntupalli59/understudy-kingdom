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
