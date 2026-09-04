using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Third modal panel alongside Duel and History. Server tracks
    /// membership/counts/eligibility only; this controller is the ONLY place
    /// that ever applies the council reward, and it does so client-side,
    /// exactly once, the same way every other mood/loyalty change already
    /// works. See docs/superpowers/specs/2026-09-03-council-social-design.md.
    /// </summary>
    public class CouncilPanelController : MonoBehaviour
    {
        private const string NotInCouncilErrorMessage = "Not in a council";
        private const string RewardJustAppliedMessage =
            "Your council's shared effort has lifted your ruler's spirits! (+10 mood, +10 loyalty)";
        private const string RewardAlreadyClaimedMessage = "Reward claimed";
        private const int RewardMoodDelta = 10;
        private const int RewardLoyaltyDelta = 10;

        [SerializeField] private Button councilButton;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject notInCouncilView;
        [SerializeField] private GameObject inCouncilView;
        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private Button createButton;
        [SerializeField] private TMP_InputField joinCodeInputField;
        [SerializeField] private Button joinButton;
        [SerializeField] private TextMeshProUGUI statusMessageText;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI joinCodeLabel;
        [SerializeField] private TextMeshProUGUI memberCountLabel;
        [SerializeField] private TextMeshProUGUI progressLabel;
        [SerializeField] private TextMeshProUGUI rewardStatusLabel;
        [SerializeField] private BackendSyncCoordinator coordinator;
        [SerializeField] private DecisionCycleManager manager;
        [SerializeField] private CoreLoopScreenController screenController;
        [SerializeField] private Slider armySlider;
        [SerializeField] private Slider tradeSlider;
        [SerializeField] private Slider religionSlider;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button viewHistoryButton;
        [SerializeField] private Button eventsButton;
        [SerializeField] private Button customizeButton;

        private void Start()
        {
            Bind();
        }

        /// <summary>
        /// Mirrors HistoryPanelController/DuelButtonController's Initialize
        /// pattern -- called by Start() in the real scene, and callable
        /// directly by tests to bypass Unity lifecycle timing.
        /// </summary>
        public void Initialize(
            Button councilButton,
            GameObject panelRoot,
            Button closeButton,
            GameObject notInCouncilView,
            GameObject inCouncilView,
            TMP_InputField nameInputField,
            Button createButton,
            TMP_InputField joinCodeInputField,
            Button joinButton,
            TextMeshProUGUI statusMessageText,
            TextMeshProUGUI nameLabel,
            TextMeshProUGUI joinCodeLabel,
            TextMeshProUGUI memberCountLabel,
            TextMeshProUGUI progressLabel,
            TextMeshProUGUI rewardStatusLabel,
            BackendSyncCoordinator coordinator,
            DecisionCycleManager manager,
            CoreLoopScreenController screenController,
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button submitButton,
            Button challengeButton,
            Button viewHistoryButton,
            Button eventsButton,
            Button customizeButton)
        {
            this.councilButton = councilButton;
            this.panelRoot = panelRoot;
            this.closeButton = closeButton;
            this.notInCouncilView = notInCouncilView;
            this.inCouncilView = inCouncilView;
            this.nameInputField = nameInputField;
            this.createButton = createButton;
            this.joinCodeInputField = joinCodeInputField;
            this.joinButton = joinButton;
            this.statusMessageText = statusMessageText;
            this.nameLabel = nameLabel;
            this.joinCodeLabel = joinCodeLabel;
            this.memberCountLabel = memberCountLabel;
            this.progressLabel = progressLabel;
            this.rewardStatusLabel = rewardStatusLabel;
            this.coordinator = coordinator;
            this.manager = manager;
            this.screenController = screenController;
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.submitButton = submitButton;
            this.challengeButton = challengeButton;
            this.viewHistoryButton = viewHistoryButton;
            this.eventsButton = eventsButton;
            this.customizeButton = customizeButton;

            Bind();
        }

        private void Bind()
        {
            councilButton.onClick.RemoveAllListeners();
            councilButton.onClick.AddListener(OnCouncilButtonClicked);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnClose);
            createButton.onClick.RemoveAllListeners();
            createButton.onClick.AddListener(OnCreate);
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(OnJoin);

            panelRoot.SetActive(false);
        }

        private void OnCouncilButtonClicked()
        {
            SetCoreLoopControlsInteractable(false);
            panelRoot.SetActive(true);
            notInCouncilView.SetActive(false);
            inCouncilView.SetActive(false);
            statusMessageText.text = "Loading...";

            coordinator.RequestCouncilStatus(HandleStatusResult, HandleStatusError);
        }

        private void HandleStatusResult(CouncilResponse response)
        {
            ShowInCouncilView(response);

            if (response.rewardEligible && !manager.Ruler.State.CouncilRewardApplied)
            {
                manager.Ruler.State.ApplyDelta(RewardMoodDelta, RewardLoyaltyDelta);
                manager.Ruler.State.CouncilRewardApplied = true;
                SaveService.Save(manager.Ruler.State);
                screenController.RefreshStatusLabels();
                rewardStatusLabel.text = RewardJustAppliedMessage;
            }
            else if (manager.Ruler.State.CouncilRewardApplied)
            {
                rewardStatusLabel.text = RewardAlreadyClaimedMessage;
            }
            else
            {
                rewardStatusLabel.text = string.Empty;
            }
        }

        // NotInCouncilErrorMessage must stay byte-identical to the 404 body
        // server/src/routes/councils.ts returns for GET /api/v1/councils/me
        // when the caller has no council yet -- see HistoryPanelController's
        // identical NoKingdomErrorMessage comment for the same reasoning.
        private void HandleStatusError(string error)
        {
            ShowNotInCouncilView();
            statusMessageText.text = error == NotInCouncilErrorMessage ? string.Empty : error;
        }

        private void OnCreate()
        {
            // Disable both buttons for the duration of the request, not just
            // the one clicked -- matches DuelButtonController's established
            // disable-during-request pattern, preventing the exact button
            // re-entrancy race milestone #6's final review caught (I-2).
            createButton.interactable = false;
            joinButton.interactable = false;
            statusMessageText.text = "Creating...";
            coordinator.RequestCreateCouncil(nameInputField.text, HandleCreateOrJoinResult, HandleCreateOrJoinError);
        }

        private void OnJoin()
        {
            createButton.interactable = false;
            joinButton.interactable = false;
            statusMessageText.text = "Joining...";
            // Server generates join codes from an uppercase-only alphabet and
            // does an exact-match lookup -- normalize here so a lowercase
            // paste or stray whitespace (e.g. copied from a chat message)
            // doesn't produce a misleading "no council found" error.
            string normalizedJoinCode = joinCodeInputField.text.Trim().ToUpperInvariant();
            coordinator.RequestJoinCouncil(normalizedJoinCode, HandleCreateOrJoinResult, HandleCreateOrJoinError);
        }

        private void HandleCreateOrJoinResult(CouncilResponse response)
        {
            createButton.interactable = true;
            joinButton.interactable = true;
            ShowInCouncilView(response);
            // response.rewardEligible can legitimately be true here (e.g.
            // creating a council with 10+ pre-existing decisions crosses the
            // threshold immediately server-side) -- but this path
            // deliberately never applies the reward. It lands the next time
            // HandleStatusResult runs (reopening the panel), keeping exactly
            // one code path responsible for the client-side reward mutation.
            rewardStatusLabel.text = manager.Ruler.State.CouncilRewardApplied ? RewardAlreadyClaimedMessage : string.Empty;
        }

        private void HandleCreateOrJoinError(string error)
        {
            createButton.interactable = true;
            joinButton.interactable = true;
            statusMessageText.text = error;
        }

        private void ShowNotInCouncilView()
        {
            inCouncilView.SetActive(false);
            notInCouncilView.SetActive(true);
        }

        private void ShowInCouncilView(CouncilResponse response)
        {
            notInCouncilView.SetActive(false);
            inCouncilView.SetActive(true);
            statusMessageText.text = string.Empty;
            nameLabel.text = response.name;
            joinCodeLabel.text = $"Join Code: {response.joinCode}";
            memberCountLabel.text = $"{response.memberCount} members";
            progressLabel.text = $"{response.totalDecisions} / {response.milestoneThreshold} decisions";
        }

        private void OnClose()
        {
            panelRoot.SetActive(false);
            SetCoreLoopControlsInteractable(true);
        }

        private void SetCoreLoopControlsInteractable(bool interactable)
        {
            councilButton.interactable = interactable;
            viewHistoryButton.interactable = interactable;
            armySlider.interactable = interactable;
            tradeSlider.interactable = interactable;
            religionSlider.interactable = interactable;
            submitButton.interactable = interactable;
            challengeButton.interactable = interactable;
            eventsButton.interactable = interactable;
            customizeButton.interactable = interactable;
        }
    }
}
