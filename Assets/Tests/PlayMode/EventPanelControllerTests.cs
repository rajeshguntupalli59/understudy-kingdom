using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    public class EventPanelControllerTests
    {
        private GameObject coordinatorObject;
        private GameObject managerObject;
        private GameObject rulerObject;
        private GameObject screenControllerObject;
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
        private Button closeButton;
        private Button claimButton;
        private TextMeshProUGUI nameLabel;
        private TextMeshProUGUI narrationLabel;
        private TextMeshProUGUI progressLabel;
        private TextMeshProUGUI statusMessageText;
        private EventPanelController controller;

        [SetUp]
        public void SetUp()
        {
            // Built inactive so Start() never runs on the coordinator --
            // currentSession stays null, giving RequestActiveEvent's
            // synchronous no-session error path with zero network
            // dependency. Real network paths are covered by
            // BackendSyncCoordinatorEventsTests and
            // EventPanelControllerRealDataTests.
            coordinatorObject = new GameObject("Coordinator");
            coordinatorObject.SetActive(false);
            var coordinator = coordinatorObject.AddComponent<BackendSyncCoordinator>();

            rulerObject = new GameObject("Ruler");
            ruler = rulerObject.AddComponent<RulerNpcController>();

            managerObject = new GameObject("Manager");
            var manager = managerObject.AddComponent<DecisionCycleManager>();
            manager.Ruler = ruler;

            canvasObject = new GameObject("Canvas", typeof(Canvas));

            armySlider = CreateSlider("ArmySlider", 40);
            tradeSlider = CreateSlider("TradeSlider", 30);
            religionSlider = CreateSlider("ReligionSlider", 30);

            var moodLabel = CreateLabel("MoodLabel");
            var loyaltyLabel = CreateLabel("LoyaltyLabel");
            var agendaLabel = CreateLabel("AgendaLabel");
            var narrationText = CreateLabel("NarrationText");

            var submitButtonObject = new GameObject("SubmitButton", typeof(Image), typeof(Button));
            submitButtonObject.transform.SetParent(canvasObject.transform, false);
            submitButton = submitButtonObject.GetComponent<Button>();

            screenControllerObject = new GameObject("ScreenController");
            var screenController = screenControllerObject.AddComponent<CoreLoopScreenController>();
            screenController.Initialize(manager, armySlider, tradeSlider, religionSlider,
                moodLabel, loyaltyLabel, agendaLabel, narrationText, submitButton);

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

            panelRootObject = new GameObject("PanelRoot");
            panelRootObject.transform.SetParent(canvasObject.transform, false);

            var closeButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            closeButtonObject.transform.SetParent(panelRootObject.transform, false);
            closeButton = closeButtonObject.GetComponent<Button>();

            nameLabel = CreateLabel("NameLabel", panelRootObject.transform);
            narrationLabel = CreateLabel("NarrationLabel", panelRootObject.transform);
            progressLabel = CreateLabel("ProgressLabel", panelRootObject.transform);
            statusMessageText = CreateLabel("StatusMessageText", panelRootObject.transform);

            var claimButtonObject = new GameObject("ClaimButton", typeof(Image), typeof(Button));
            claimButtonObject.transform.SetParent(panelRootObject.transform, false);
            claimButton = claimButtonObject.GetComponent<Button>();

            controllerObject = new GameObject("Controller");
            controller = controllerObject.AddComponent<EventPanelController>();
            controller.Initialize(eventsButton, panelRootObject, closeButton, nameLabel, narrationLabel,
                progressLabel, statusMessageText, claimButton, coordinator, manager, screenController,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, councilButton);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(screenControllerObject);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(coordinatorObject);
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

        private TextMeshProUGUI CreateLabel(string name, Transform parent = null)
        {
            var labelObject = new GameObject(name, typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent != null ? parent : canvasObject.transform, false);
            return labelObject.GetComponent<TextMeshProUGUI>();
        }

        private void InvokeHandleResult(EventResponse response)
        {
            MethodInfo handleResult = typeof(EventPanelController).GetMethod(
                "HandleResult", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(handleResult, "HandleResult method not found -- EventPanelController internals changed");
            handleResult.Invoke(controller, new object[] { response });
        }

        [Test]
        public void EventsButton_WithNoSessionYet_DisablesControlsAndShowsMessage()
        {
            eventsButton.onClick.Invoke();

            Assert.IsFalse(eventsButton.interactable);
            Assert.IsFalse(viewHistoryButton.interactable);
            Assert.IsFalse(councilButton.interactable);
            Assert.IsFalse(armySlider.interactable);
            Assert.IsFalse(tradeSlider.interactable);
            Assert.IsFalse(religionSlider.interactable);
            Assert.IsFalse(submitButton.interactable);
            Assert.IsFalse(challengeButton.interactable);
            Assert.IsTrue(panelRootObject.activeSelf);
            Assert.AreEqual("No session available yet -- try again in a moment.", statusMessageText.text);
        }

        [Test]
        public void Close_ReEnablesControlsAndHidesPanel()
        {
            eventsButton.onClick.Invoke();
            closeButton.onClick.Invoke();

            Assert.IsTrue(eventsButton.interactable);
            Assert.IsTrue(viewHistoryButton.interactable);
            Assert.IsTrue(councilButton.interactable);
            Assert.IsTrue(armySlider.interactable);
            Assert.IsTrue(tradeSlider.interactable);
            Assert.IsTrue(religionSlider.interactable);
            Assert.IsTrue(submitButton.interactable);
            Assert.IsTrue(challengeButton.interactable);
            Assert.IsFalse(panelRootObject.activeSelf);
        }

        [Test]
        public void HandleResult_BelowThreshold_ClaimButtonStaysDisabled()
        {
            InvokeHandleResult(new EventResponse
            {
                eventId = "W2026-37",
                name = "Harvest Tithe",
                narration = "...",
                objectiveDecisionCount = 3,
                decisionsCompleted = 2,
                rewardMood = 15,
                rewardLoyalty = 15
            });

            Assert.IsFalse(claimButton.interactable);
            Assert.AreEqual("2 / 3 decisions", progressLabel.text);
        }

        [Test]
        public void HandleResult_AtThreshold_ClaimButtonBecomesInteractable()
        {
            InvokeHandleResult(new EventResponse
            {
                eventId = "W2026-37",
                name = "Harvest Tithe",
                narration = "...",
                objectiveDecisionCount = 3,
                decisionsCompleted = 3,
                rewardMood = 15,
                rewardLoyalty = 15
            });

            Assert.IsTrue(claimButton.interactable);
        }

        [Test]
        public void Claim_AppliesRewardExactlyOnceAndPersists()
        {
            InvokeHandleResult(new EventResponse
            {
                eventId = "W2026-37",
                name = "Harvest Tithe",
                narration = "...",
                objectiveDecisionCount = 3,
                decisionsCompleted = 3,
                rewardMood = 15,
                rewardLoyalty = 15
            });

            int moodBefore = ruler.State.Mood;
            int loyaltyBefore = ruler.State.Loyalty;

            claimButton.onClick.Invoke();

            Assert.AreEqual(moodBefore + 15, ruler.State.Mood);
            Assert.AreEqual(loyaltyBefore + 15, ruler.State.Loyalty);
            Assert.AreEqual("W2026-37", ruler.State.ClaimedEventWeekId);
            Assert.IsFalse(claimButton.interactable);

            // Clicking again (button is now non-interactable, but exercise
            // the guard directly via onClick.Invoke() -- Unity's Button
            // still permits a direct onClick.Invoke() call regardless of
            // interactable state, so this proves OnClaim's own re-entrancy
            // guard, not just the UI-level disable).
            claimButton.onClick.Invoke();

            Assert.AreEqual(moodBefore + 15, ruler.State.Mood, "Reward must not be applied twice");
            Assert.AreEqual(loyaltyBefore + 15, ruler.State.Loyalty, "Reward must not be applied twice");

            RulerState persisted = SaveService.Load();
            Assert.AreEqual("W2026-37", persisted.ClaimedEventWeekId);
        }

        [Test]
        public void HandleResult_ForAlreadyClaimedEvent_ClaimButtonStaysDisabledAndShowsClaimedStatus()
        {
            ruler.State.ClaimedEventWeekId = "W2026-37";

            InvokeHandleResult(new EventResponse
            {
                eventId = "W2026-37",
                name = "Harvest Tithe",
                narration = "...",
                objectiveDecisionCount = 3,
                decisionsCompleted = 3,
                rewardMood = 15,
                rewardLoyalty = 15
            });

            Assert.IsFalse(claimButton.interactable);
            Assert.AreEqual("Claimed", statusMessageText.text);
        }
    }
}
