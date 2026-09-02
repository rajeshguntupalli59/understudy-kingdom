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
