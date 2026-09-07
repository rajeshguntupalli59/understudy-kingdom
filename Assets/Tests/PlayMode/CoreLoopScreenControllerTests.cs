using System.Reflection;
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
        private Image rulerPortraitImage;
        private Sprite[] rulerPortraits;
        private Image submitIcon;
        private CoreLoopScreenController controller;

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

            var rulerPortraitObject = new GameObject("RulerPortraitImage", typeof(Image));
            rulerPortraitObject.transform.SetParent(canvasObject.transform, false);
            rulerPortraitImage = rulerPortraitObject.GetComponent<Image>();

            rulerPortraits = new Sprite[15];
            for (int i = 0; i < rulerPortraits.Length; i++)
            {
                rulerPortraits[i] = CreateDummySprite();
            }

            var submitIconObject = new GameObject("SubmitIcon", typeof(Image));
            submitIconObject.transform.SetParent(canvasObject.transform, false);
            submitIcon = submitIconObject.GetComponent<Image>();

            controllerObject = new GameObject("Controller");
            controller = controllerObject.AddComponent<CoreLoopScreenController>();
            controller.Initialize(manager, armySlider, tradeSlider, religionSlider,
                moodLabel, loyaltyLabel, agendaLabel, narrationText, submitButton,
                rulerPortraitImage, rulerPortraits, submitIcon);
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

        private static Sprite CreateDummySprite()
        {
            var texture = new Texture2D(1, 1);
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
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

        [Test]
        public void RefreshStatusLabels_SelectsPortraitMatchingMoodAndLoyaltyTier()
        {
            manager.Ruler.State.Mood = 90;
            manager.Ruler.State.Loyalty = 10;

            controller.RefreshStatusLabels();

            // Delighted (tier 4) x Low (tier 0) = index 4*3+0 = 12
            Assert.AreSame(rulerPortraits[12], rulerPortraitImage.sprite);
        }

        [Test]
        public void RefreshStatusLabels_WithMissingSelectedPortrait_FallsBackToNeutralMediumPortrait()
        {
            manager.Ruler.State.Mood = 90;
            manager.Ruler.State.Loyalty = 10;
            rulerPortraits[12] = null;

            controller.RefreshStatusLabels();

            Assert.AreSame(rulerPortraits[7], rulerPortraitImage.sprite);
        }

        [Test]
        public void Initialize_WithNullPortraitsArray_DoesNotThrowAndLeavesPortraitSpriteNull()
        {
            // Regression guard for the recurring "new [SerializeField] deserializes
            // as null on an old committed scene, indexed without a null-array
            // guard" pattern -- see docs/PROJECT_PLAN.md's "Recurring pattern
            // across milestones #12 and #13" note. rulerPortraits[portraitIndex]
            // previously indexed directly with no null-array check at all, so a
            // null rulerPortraits array (e.g. a stale scene predating this field)
            // would NRE inside Bind() -> RefreshStatusLabels(), not just leave a
            // blank portrait.
            var freshControllerObject = new GameObject("FreshController");
            var freshController = freshControllerObject.AddComponent<CoreLoopScreenController>();

            Assert.DoesNotThrow(() =>
            {
                freshController.Initialize(manager, armySlider, tradeSlider, religionSlider,
                    moodLabel, loyaltyLabel, agendaLabel, narrationText, submitButton,
                    rulerPortraitImage, null, submitIcon);
            });

            Assert.IsNull(rulerPortraitImage.sprite);

            Object.DestroyImmediate(freshControllerObject);
        }

        [Test]
        public void Initialize_StoresSubmitIconReference()
        {
            FieldInfo iconField = typeof(CoreLoopScreenController).GetField("submitIcon", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.AreSame(submitIcon, iconField.GetValue(controller));
        }
    }
}
