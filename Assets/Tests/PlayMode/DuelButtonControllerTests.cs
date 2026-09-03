using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    public class DuelButtonControllerTests
    {
        private GameObject coordinatorObject;
        private GameObject controllerObject;
        private GameObject canvasObject;
        private Slider armySlider;
        private Slider tradeSlider;
        private Slider religionSlider;
        private Button challengeButton;
        private TextMeshProUGUI resultText;
        private DuelModalGate gate;

        [SetUp]
        public void SetUp()
        {
            // Built inactive so Start() never runs on the coordinator --
            // currentSession stays null, giving RequestDuel's synchronous
            // no-session error path with zero network dependency. The real
            // win/lose network paths are covered by BackendSyncCoordinatorDuelTests
            // (Task 5) and the manual verification step (Task 7).
            coordinatorObject = new GameObject("Coordinator");
            coordinatorObject.SetActive(false);
            var coordinator = coordinatorObject.AddComponent<BackendSyncCoordinator>();

            canvasObject = new GameObject("Canvas", typeof(Canvas));

            armySlider = CreateSlider("ArmySlider", 40);
            tradeSlider = CreateSlider("TradeSlider", 30);
            religionSlider = CreateSlider("ReligionSlider", 30);

            var buttonObject = new GameObject("ChallengeButton", typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvasObject.transform, false);
            challengeButton = buttonObject.GetComponent<Button>();

            var resultObject = new GameObject("ResultText", typeof(TextMeshProUGUI));
            resultObject.transform.SetParent(canvasObject.transform, false);
            resultText = resultObject.GetComponent<TextMeshProUGUI>();

            gate = new DuelModalGate();

            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<DuelButtonController>();
            controller.Initialize(armySlider, tradeSlider, religionSlider, challengeButton, resultText, coordinator, gate);
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
        public void Challenge_WithNoSessionYet_ShowsErrorAndReEnablesButton()
        {
            challengeButton.onClick.Invoke();

            Assert.IsTrue(resultText.text.Contains("Challenge failed"));
            Assert.IsTrue(challengeButton.interactable);
        }

        [Test]
        public void Challenge_WithModalOpen_LeavesButtonDisabledAfterError()
        {
            gate.IsModalOpen = true;

            challengeButton.onClick.Invoke();

            Assert.IsTrue(resultText.text.Contains("Challenge failed"));
            Assert.IsFalse(challengeButton.interactable,
                "a modal being open must keep Challenge disabled even after the duel request itself resolves");
        }
    }
}
