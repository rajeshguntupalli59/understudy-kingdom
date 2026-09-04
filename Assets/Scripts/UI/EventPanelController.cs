using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Fifth modal panel alongside Duel/History/Council/Tutorial. Server
    /// computes the active event and this player's live decisionsCompleted
    /// count only; this controller is the ONLY place that ever applies the
    /// event reward, client-side, exactly once, gated by
    /// RulerState.ClaimedEventWeekId -- same pattern as
    /// CouncilPanelController's reward handling. NOT DuelModalGate-aware
    /// this pass -- see
    /// docs/superpowers/specs/2026-09-03-live-ops-events-design.md's
    /// "Known Gap Flagged, Not Fixed Here" section.
    /// </summary>
    public class EventPanelController : MonoBehaviour
    {
        private const string RewardAlreadyClaimedMessage = "Claimed";

        [SerializeField] private Button eventsButton;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI narrationLabel;
        [SerializeField] private TextMeshProUGUI progressLabel;
        [SerializeField] private TextMeshProUGUI statusMessageText;
        [SerializeField] private Button claimButton;
        [SerializeField] private BackendSyncCoordinator coordinator;
        [SerializeField] private DecisionCycleManager manager;
        [SerializeField] private CoreLoopScreenController screenController;
        [SerializeField] private Slider armySlider;
        [SerializeField] private Slider tradeSlider;
        [SerializeField] private Slider religionSlider;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button viewHistoryButton;
        [SerializeField] private Button councilButton;
        [SerializeField] private Button customizeButton;

        private EventResponse latestResponse;

        private void Start()
        {
            Bind();
        }

        /// <summary>
        /// Mirrors CouncilPanelController/HistoryPanelController's
        /// Initialize pattern -- called by Start() in the real scene, and
        /// callable directly by tests to bypass Unity lifecycle timing.
        /// </summary>
        public void Initialize(
            Button eventsButton,
            GameObject panelRoot,
            Button closeButton,
            TextMeshProUGUI nameLabel,
            TextMeshProUGUI narrationLabel,
            TextMeshProUGUI progressLabel,
            TextMeshProUGUI statusMessageText,
            Button claimButton,
            BackendSyncCoordinator coordinator,
            DecisionCycleManager manager,
            CoreLoopScreenController screenController,
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button submitButton,
            Button challengeButton,
            Button viewHistoryButton,
            Button councilButton,
            Button customizeButton)
        {
            this.eventsButton = eventsButton;
            this.panelRoot = panelRoot;
            this.closeButton = closeButton;
            this.nameLabel = nameLabel;
            this.narrationLabel = narrationLabel;
            this.progressLabel = progressLabel;
            this.statusMessageText = statusMessageText;
            this.claimButton = claimButton;
            this.coordinator = coordinator;
            this.manager = manager;
            this.screenController = screenController;
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.submitButton = submitButton;
            this.challengeButton = challengeButton;
            this.viewHistoryButton = viewHistoryButton;
            this.councilButton = councilButton;
            this.customizeButton = customizeButton;

            Bind();
        }

        private void Bind()
        {
            eventsButton.onClick.RemoveAllListeners();
            eventsButton.onClick.AddListener(OnEventsButtonClicked);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnClose);
            claimButton.onClick.RemoveAllListeners();
            claimButton.onClick.AddListener(OnClaim);

            panelRoot.SetActive(false);
        }

        private void OnEventsButtonClicked()
        {
            SetCoreLoopControlsInteractable(false);
            panelRoot.SetActive(true);
            claimButton.interactable = false;
            statusMessageText.text = "Loading...";
            nameLabel.text = string.Empty;
            narrationLabel.text = string.Empty;
            progressLabel.text = string.Empty;
            latestResponse = null;

            coordinator.RequestActiveEvent(HandleResult, HandleError);
        }

        private void HandleResult(EventResponse response)
        {
            latestResponse = response;
            nameLabel.text = response.name;
            narrationLabel.text = response.narration;
            progressLabel.text = $"{response.decisionsCompleted} / {response.objectiveDecisionCount} decisions";

            bool alreadyClaimed = manager.Ruler.State.ClaimedEventWeekId == response.eventId;
            bool objectiveMet = response.decisionsCompleted >= response.objectiveDecisionCount;

            claimButton.interactable = objectiveMet && !alreadyClaimed;
            statusMessageText.text = alreadyClaimed ? RewardAlreadyClaimedMessage : string.Empty;
        }

        private void HandleError(string error)
        {
            latestResponse = null;
            statusMessageText.text = error;
            claimButton.interactable = false;
        }

        private void OnClaim()
        {
            if (latestResponse == null || manager.Ruler.State.ClaimedEventWeekId == latestResponse.eventId)
            {
                return;
            }

            manager.Ruler.State.ApplyDelta(latestResponse.rewardMood, latestResponse.rewardLoyalty);
            manager.Ruler.State.ClaimedEventWeekId = latestResponse.eventId;
            SaveService.Save(manager.Ruler.State);
            screenController.RefreshStatusLabels();

            claimButton.interactable = false;
            statusMessageText.text = $"This week's efforts have heartened your ruler! (+{latestResponse.rewardMood} mood, +{latestResponse.rewardLoyalty} loyalty)";
        }

        private void OnClose()
        {
            panelRoot.SetActive(false);
            SetCoreLoopControlsInteractable(true);
        }

        private void SetCoreLoopControlsInteractable(bool interactable)
        {
            eventsButton.interactable = interactable;
            viewHistoryButton.interactable = interactable;
            councilButton.interactable = interactable;
            armySlider.interactable = interactable;
            tradeSlider.interactable = interactable;
            religionSlider.interactable = interactable;
            submitButton.interactable = interactable;
            challengeButton.interactable = interactable;
            customizeButton.interactable = interactable;
        }
    }
}
