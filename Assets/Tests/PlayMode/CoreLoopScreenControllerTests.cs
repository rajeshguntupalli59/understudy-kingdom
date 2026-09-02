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
