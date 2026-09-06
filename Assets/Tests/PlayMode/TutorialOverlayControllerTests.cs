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
        private Button eventsButton;
        private Button customizeButton;
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
            eventsButton = CreateButton("EventsButton");
            customizeButton = CreateButton("CustomizeButton");

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
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, councilButton, eventsButton, customizeButton);
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
            Assert.IsFalse(eventsButton.interactable);
            Assert.IsFalse(customizeButton.interactable);
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
            eventsButton.interactable = false;
            customizeButton.interactable = false;

            Initialize();

            Assert.IsFalse(panelRootObject.activeSelf);
            Assert.IsTrue(armySlider.interactable);
            Assert.IsTrue(tradeSlider.interactable);
            Assert.IsTrue(religionSlider.interactable);
            Assert.IsTrue(submitButton.interactable);
            Assert.IsTrue(challengeButton.interactable);
            Assert.IsTrue(viewHistoryButton.interactable);
            Assert.IsTrue(councilButton.interactable);
            Assert.IsTrue(eventsButton.interactable);
            Assert.IsTrue(customizeButton.interactable);
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
            Assert.IsTrue(eventsButton.interactable);
            Assert.IsTrue(customizeButton.interactable);

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
