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
    public class CouncilPanelControllerTests
    {
        private GameObject coordinatorObject;
        private GameObject managerObject;
        private GameObject rulerObject;
        private GameObject screenControllerObject;
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
        private Button closeButton;
        private Button createButton;
        private Button joinButton;
        private GameObject notInCouncilViewObject;
        private GameObject inCouncilViewObject;
        private TMP_InputField nameInputField;
        private TMP_InputField joinCodeInputField;
        private TextMeshProUGUI statusMessageText;

        [SetUp]
        public void SetUp()
        {
            // Built inactive so Start() never runs on the coordinator --
            // currentSession stays null, giving RequestCouncilStatus's
            // synchronous no-session error path with zero network
            // dependency. Real network paths are covered by
            // BackendSyncCoordinatorCouncilTests and
            // CouncilPanelControllerRealDataTests.
            coordinatorObject = new GameObject("Coordinator");
            coordinatorObject.SetActive(false);
            var coordinator = coordinatorObject.AddComponent<BackendSyncCoordinator>();

            rulerObject = new GameObject("Ruler");
            var ruler = rulerObject.AddComponent<RulerNpcController>();

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

            panelRootObject = new GameObject("PanelRoot");
            panelRootObject.transform.SetParent(canvasObject.transform, false);

            var closeButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            closeButtonObject.transform.SetParent(panelRootObject.transform, false);
            closeButton = closeButtonObject.GetComponent<Button>();

            notInCouncilViewObject = new GameObject("NotInCouncilView");
            notInCouncilViewObject.transform.SetParent(panelRootObject.transform, false);

            var nameInputObject = new GameObject("NameInput", typeof(TMP_InputField));
            nameInputObject.transform.SetParent(notInCouncilViewObject.transform, false);
            nameInputField = nameInputObject.GetComponent<TMP_InputField>();

            var createButtonObject = new GameObject("CreateButton", typeof(Image), typeof(Button));
            createButtonObject.transform.SetParent(notInCouncilViewObject.transform, false);
            createButton = createButtonObject.GetComponent<Button>();

            var joinCodeInputObject = new GameObject("JoinCodeInput", typeof(TMP_InputField));
            joinCodeInputObject.transform.SetParent(notInCouncilViewObject.transform, false);
            joinCodeInputField = joinCodeInputObject.GetComponent<TMP_InputField>();

            var joinButtonObject = new GameObject("JoinButton", typeof(Image), typeof(Button));
            joinButtonObject.transform.SetParent(notInCouncilViewObject.transform, false);
            joinButton = joinButtonObject.GetComponent<Button>();

            inCouncilViewObject = new GameObject("InCouncilView");
            inCouncilViewObject.transform.SetParent(panelRootObject.transform, false);

            var nameLabel = CreateLabel("NameLabel", inCouncilViewObject.transform);
            var joinCodeLabel = CreateLabel("JoinCodeLabel", inCouncilViewObject.transform);
            var memberCountLabel = CreateLabel("MemberCountLabel", inCouncilViewObject.transform);
            var progressLabel = CreateLabel("ProgressLabel", inCouncilViewObject.transform);
            var rewardStatusLabel = CreateLabel("RewardStatusLabel", inCouncilViewObject.transform);
            statusMessageText = CreateLabel("StatusMessageText", panelRootObject.transform);

            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<CouncilPanelController>();
            controller.Initialize(councilButton, panelRootObject, closeButton, notInCouncilViewObject, inCouncilViewObject,
                nameInputField, createButton, joinCodeInputField, joinButton, statusMessageText,
                nameLabel, joinCodeLabel, memberCountLabel, progressLabel, rewardStatusLabel,
                coordinator, manager, screenController,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton);
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

        [Test]
        public void CouncilButton_WithNoSessionYet_DisablesControlsAndShowsMessage()
        {
            councilButton.onClick.Invoke();

            Assert.IsFalse(councilButton.interactable);
            Assert.IsFalse(viewHistoryButton.interactable);
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
            councilButton.onClick.Invoke();
            closeButton.onClick.Invoke();

            Assert.IsTrue(councilButton.interactable);
            Assert.IsTrue(viewHistoryButton.interactable);
            Assert.IsTrue(armySlider.interactable);
            Assert.IsTrue(tradeSlider.interactable);
            Assert.IsTrue(religionSlider.interactable);
            Assert.IsTrue(submitButton.interactable);
            Assert.IsTrue(challengeButton.interactable);
            Assert.IsFalse(panelRootObject.activeSelf);
        }
    }
}
