using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Backend;

namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Modal history panel: while open, the "View History" button and the core
    /// loop's sliders/Submit/Challenge are non-interactive. Single fixed page
    /// (up to rowTexts.Length rows), no scrolling, no "Load More" -- see
    /// docs/superpowers/specs/2026-09-02-decision-history-design.md.
    /// </summary>
    public class HistoryPanelController : MonoBehaviour
    {
        private const string NoKingdomErrorMessage = "No kingdom found for this user";
        private const string EmptyHistoryMessage = "No decisions yet -- submit your first recommendation!";
        private const string LoadingMessage = "Loading...";

        [SerializeField] private Button viewHistoryButton;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI[] rowTexts;
        [SerializeField] private BackendSyncCoordinator coordinator;
        [SerializeField] private Slider armySlider;
        [SerializeField] private Slider tradeSlider;
        [SerializeField] private Slider religionSlider;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button councilButton;

        private void Start()
        {
            Bind();
        }

        /// <summary>
        /// Mirrors CoreLoopScreenController/DuelButtonController's Initialize pattern
        /// -- called by Start() in the real scene, and callable directly by tests to
        /// bypass Unity lifecycle timing.
        /// </summary>
        public void Initialize(
            Button viewHistoryButton,
            GameObject panelRoot,
            Button closeButton,
            TextMeshProUGUI[] rowTexts,
            BackendSyncCoordinator coordinator,
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button submitButton,
            Button challengeButton,
            Button councilButton)
        {
            this.viewHistoryButton = viewHistoryButton;
            this.panelRoot = panelRoot;
            this.closeButton = closeButton;
            this.rowTexts = rowTexts;
            this.coordinator = coordinator;
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.submitButton = submitButton;
            this.challengeButton = challengeButton;
            this.councilButton = councilButton;

            Bind();
        }

        private void Bind()
        {
            viewHistoryButton.onClick.RemoveAllListeners();
            viewHistoryButton.onClick.AddListener(OnViewHistory);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnClose);

            panelRoot.SetActive(false);
        }

        private void OnViewHistory()
        {
            SetCoreLoopControlsInteractable(false);
            panelRoot.SetActive(true);

            // Instant feedback for the round-trip: avoids a blank panel on first
            // open and stale rows from a previous fetch on reopen.
            rowTexts[0].gameObject.SetActive(true);
            rowTexts[0].text = LoadingMessage;
            for (int i = 1; i < rowTexts.Length; i++)
            {
                rowTexts[i].gameObject.SetActive(false);
            }

            coordinator.RequestHistory(rowTexts.Length, HandleResult, HandleError);
        }

        private void HandleResult(DecisionHistoryEntry[] entries)
        {
            if (entries.Length == 0)
            {
                rowTexts[0].gameObject.SetActive(true);
                rowTexts[0].text = EmptyHistoryMessage;
                for (int i = 1; i < rowTexts.Length; i++)
                {
                    rowTexts[i].gameObject.SetActive(false);
                }
                return;
            }

            for (int i = 0; i < rowTexts.Length; i++)
            {
                if (i < entries.Length)
                {
                    rowTexts[i].gameObject.SetActive(true);
                    rowTexts[i].text = HistoryRowFormatter.Format(entries[i]);
                }
                else
                {
                    rowTexts[i].gameObject.SetActive(false);
                }
            }
        }

        // NoKingdomErrorMessage must stay byte-identical to the 404 body
        // server/src/routes/decisions.ts returns when the caller has no kingdom
        // yet -- if that server-side message is ever reworded, this comparison
        // silently stops matching and the player sees the raw error text instead
        // of the friendly empty-state message.
        private void HandleError(string error)
        {
            // A fresh player who's never had a kingdom created yet gets the same
            // friendly empty-state message as a kingdom with zero decisions -- the
            // player doesn't need to know which case it is, both mean "nothing to
            // show yet." Any other error is shown verbatim.
            rowTexts[0].gameObject.SetActive(true);
            rowTexts[0].text = error == NoKingdomErrorMessage ? EmptyHistoryMessage : error;

            for (int i = 1; i < rowTexts.Length; i++)
            {
                rowTexts[i].gameObject.SetActive(false);
            }
        }

        private void OnClose()
        {
            panelRoot.SetActive(false);
            SetCoreLoopControlsInteractable(true);
        }

        private void SetCoreLoopControlsInteractable(bool interactable)
        {
            viewHistoryButton.interactable = interactable;
            armySlider.interactable = interactable;
            tradeSlider.interactable = interactable;
            religionSlider.interactable = interactable;
            submitButton.interactable = interactable;
            challengeButton.interactable = interactable;
            councilButton.interactable = interactable;
        }
    }
}
