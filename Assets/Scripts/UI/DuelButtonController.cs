using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Owns the "Challenge a Rival Kingdom" button. Unlike decision sync (fire-
    /// and-forget, silent on failure), a duel is an explicit player-initiated
    /// request -- failures are shown to the player. See
    /// docs/superpowers/specs/2026-09-02-async-pvp-design.md.
    /// </summary>
    public class DuelButtonController : MonoBehaviour
    {
        [SerializeField] private Slider armySlider;
        [SerializeField] private Slider tradeSlider;
        [SerializeField] private Slider religionSlider;
        [SerializeField] private Button challengeButton;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private BackendSyncCoordinator coordinator;

        private void Start()
        {
            Bind();
        }

        /// <summary>
        /// Mirrors CoreLoopScreenController.Initialize -- called by Start() in the
        /// real scene (fields pre-wired via the scene builder), and callable
        /// directly by tests to bypass Unity lifecycle timing.
        /// </summary>
        public void Initialize(
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button challengeButton,
            TextMeshProUGUI resultText,
            BackendSyncCoordinator coordinator)
        {
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.challengeButton = challengeButton;
            this.resultText = resultText;
            this.coordinator = coordinator;

            Bind();
        }

        private void Bind()
        {
            challengeButton.onClick.RemoveAllListeners();
            challengeButton.onClick.AddListener(OnChallenge);
        }

        private void OnChallenge()
        {
            var allocation = new ResourceAllocation(
                Mathf.RoundToInt(armySlider.value),
                Mathf.RoundToInt(tradeSlider.value),
                Mathf.RoundToInt(religionSlider.value));

            resultText.text = "Resolving...";
            challengeButton.interactable = false;

            coordinator.RequestDuel(allocation, HandleResult, HandleError);
        }

        private void HandleResult(DuelResult result)
        {
            challengeButton.interactable = true;

            string templateTag = result.overridden ? "duel_lose" : "duel_win";
            resultText.text = DialogueTemplateEngine.Resolve(templateTag, new Dictionary<string, string>());
        }

        private void HandleError(string error)
        {
            challengeButton.interactable = true;
            resultText.text = $"Challenge failed: {error}";
        }
    }
}
