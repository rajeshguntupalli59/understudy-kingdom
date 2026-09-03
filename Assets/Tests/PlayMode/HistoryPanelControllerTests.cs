using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    public class HistoryPanelControllerTests
    {
        private GameObject coordinatorObject;
        private GameObject controllerObject;
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
        private TextMeshProUGUI[] rowTexts;

        [SetUp]
        public void SetUp()
        {
            // Built inactive so Start() never runs on the coordinator --
            // currentSession stays null, giving RequestHistory's synchronous
            // no-session error path with zero network dependency. The real
            // network paths are covered by BackendSyncCoordinatorHistoryTests
            // and HistoryPanelControllerRealDataTests.
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

            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<HistoryPanelController>();
            controller.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, coordinator,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, councilButton);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(controllerObject);
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
        public void ViewHistory_WithNoSessionYet_DisablesControlsAndShowsMessage()
        {
            viewHistoryButton.onClick.Invoke();

            Assert.IsFalse(viewHistoryButton.interactable);
            Assert.IsFalse(armySlider.interactable);
            Assert.IsFalse(tradeSlider.interactable);
            Assert.IsFalse(religionSlider.interactable);
            Assert.IsFalse(submitButton.interactable);
            Assert.IsFalse(challengeButton.interactable);
            Assert.IsFalse(councilButton.interactable);
            Assert.IsTrue(panelRootObject.activeSelf);
            Assert.AreEqual("No session available yet -- try again in a moment.", rowTexts[0].text);
        }

        [Test]
        public void Close_ReEnablesControlsAndHidesPanel()
        {
            viewHistoryButton.onClick.Invoke();
            closeButton.onClick.Invoke();

            Assert.IsTrue(viewHistoryButton.interactable);
            Assert.IsTrue(armySlider.interactable);
            Assert.IsTrue(tradeSlider.interactable);
            Assert.IsTrue(religionSlider.interactable);
            Assert.IsTrue(submitButton.interactable);
            Assert.IsTrue(challengeButton.interactable);
            Assert.IsTrue(councilButton.interactable);
            Assert.IsFalse(panelRootObject.activeSelf);
        }

        [Test]
        public void HandleResult_WithEmptyDecisionsArray_ShowsEmptyStateMessage()
        {
            // Realistic first-launch case: a fresh kingdom with zero decisions yet.
            // HandleResult is private, so invoke it via reflection -- same technique
            // already established for internal state in
            // BackendSyncCoordinatorHistoryTests.cs/BackendSyncCoordinatorDuelTests.cs.
            var controller = controllerObject.GetComponent<HistoryPanelController>();
            MethodInfo handleResult = typeof(HistoryPanelController).GetMethod(
                "HandleResult", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(handleResult, "HandleResult method not found -- HistoryPanelController internals changed");

            handleResult.Invoke(controller, new object[] { new DecisionHistoryEntry[0] });

            Assert.AreEqual("No decisions yet -- submit your first recommendation!", rowTexts[0].text);
            Assert.IsTrue(rowTexts[0].gameObject.activeSelf);
            for (int i = 1; i < rowTexts.Length; i++)
            {
                Assert.IsFalse(rowTexts[i].gameObject.activeSelf, $"rowTexts[{i}] should be hidden for the empty-state render");
            }
        }
    }
}
