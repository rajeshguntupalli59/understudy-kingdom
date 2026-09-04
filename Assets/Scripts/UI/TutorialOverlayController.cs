using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Fourth overlay in this scene alongside Duel/History/Council, but
    /// unlike those it is not player-triggered -- it shows itself once on
    /// Start() based on persisted state, and nothing else needs a reference
    /// to it or a way to disable it in return, since by construction nothing
    /// else is interactable while it is up. See
    /// docs/superpowers/specs/2026-09-03-onboarding-tutorial-design.md.
    /// </summary>
    public class TutorialOverlayController : MonoBehaviour
    {
        private static readonly (string Title, string Body)[] Steps =
        {
            ("Your Resources",
                "These three sliders control your recommendation: Army, Trade, and Religion. " +
                "They always add up to 100 -- adjust one and the others rebalance automatically."),
            ("Submit Your Recommendation",
                "Once you're happy with your allocation, tap Submit Recommendation. Your ruler " +
                "will accept or override it based on their mood, loyalty, and agenda."),
            ("Reading Your Ruler",
                "Mood, Loyalty, and Agenda (top of screen) describe your ruler's state -- they " +
                "shift based on how well your recommendations match what your ruler actually wants."),
            ("Beyond the Basics",
                "Once you're comfortable with the core loop, you can Challenge rival kingdoms, " +
                "view your History, or join a Council with other players.")
        };

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TextMeshProUGUI stepIndicatorLabel;
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI bodyLabel;
        [SerializeField] private Button nextButton;
        [SerializeField] private TextMeshProUGUI nextButtonLabel;
        [SerializeField] private Button skipButton;
        [SerializeField] private DecisionCycleManager manager;
        [SerializeField] private Slider armySlider;
        [SerializeField] private Slider tradeSlider;
        [SerializeField] private Slider religionSlider;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button viewHistoryButton;
        [SerializeField] private Button councilButton;
        [SerializeField] private Button eventsButton;

        private int currentStep;

        private void Start()
        {
            Bind();
        }

        /// <summary>
        /// Mirrors HistoryPanelController/CouncilPanelController's Initialize
        /// pattern -- called by Start() in the real scene, and callable
        /// directly by tests to bypass Unity lifecycle timing.
        /// </summary>
        public void Initialize(
            GameObject panelRoot,
            TextMeshProUGUI stepIndicatorLabel,
            TextMeshProUGUI titleLabel,
            TextMeshProUGUI bodyLabel,
            Button nextButton,
            TextMeshProUGUI nextButtonLabel,
            Button skipButton,
            DecisionCycleManager manager,
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button submitButton,
            Button challengeButton,
            Button viewHistoryButton,
            Button councilButton,
            Button eventsButton)
        {
            this.panelRoot = panelRoot;
            this.stepIndicatorLabel = stepIndicatorLabel;
            this.titleLabel = titleLabel;
            this.bodyLabel = bodyLabel;
            this.nextButton = nextButton;
            this.nextButtonLabel = nextButtonLabel;
            this.skipButton = skipButton;
            this.manager = manager;
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.submitButton = submitButton;
            this.challengeButton = challengeButton;
            this.viewHistoryButton = viewHistoryButton;
            this.councilButton = councilButton;
            this.eventsButton = eventsButton;

            Bind();
        }

        private void Bind()
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(OnNext);
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(OnSkip);

            if (manager.Ruler.State.TutorialCompleted)
            {
                panelRoot.SetActive(false);
                SetCoreLoopControlsInteractable(true);
                return;
            }

            currentStep = 0;
            SetCoreLoopControlsInteractable(false);
            panelRoot.SetActive(true);
            RenderCurrentStep();
        }

        private void OnNext()
        {
            if (currentStep >= Steps.Length - 1)
            {
                Complete();
                return;
            }

            currentStep++;
            RenderCurrentStep();
        }

        private void OnSkip()
        {
            Complete();
        }

        private void Complete()
        {
            manager.Ruler.State.TutorialCompleted = true;
            SaveService.Save(manager.Ruler.State);
            panelRoot.SetActive(false);
            SetCoreLoopControlsInteractable(true);
        }

        private void RenderCurrentStep()
        {
            var step = Steps[currentStep];
            titleLabel.text = step.Title;
            bodyLabel.text = step.Body;
            stepIndicatorLabel.text = $"Step {currentStep + 1} of {Steps.Length}";
            nextButtonLabel.text = currentStep == Steps.Length - 1 ? "Done" : "Next";
        }

        private void SetCoreLoopControlsInteractable(bool interactable)
        {
            armySlider.interactable = interactable;
            tradeSlider.interactable = interactable;
            religionSlider.interactable = interactable;
            submitButton.interactable = interactable;
            challengeButton.interactable = interactable;
            viewHistoryButton.interactable = interactable;
            councilButton.interactable = interactable;
            eventsButton.interactable = interactable;
        }
    }
}
