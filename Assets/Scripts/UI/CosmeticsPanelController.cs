using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Sixth modal panel alongside Duel/History/Council/Tutorial/Events.
    /// Makes zero network calls -- every theme's unlock condition is a
    /// RulerState flag milestones #7 (CouncilRewardApplied) and #10
    /// (ClaimedEventWeekId) already persist. Recolors the History/Council/
    /// Events panel backgrounds only; individual action buttons keep their
    /// existing distinct colors. DuelModalGate-aware since milestone #9
    /// merged -- see
    /// docs/superpowers/specs/2026-09-03-duel-modal-gate-design.md.
    /// </summary>
    public class CosmeticsPanelController : MonoBehaviour
    {
        private struct ThemeDefinition
        {
            public string Id;
            public string DisplayName;
            public Color PanelColor;
            public string LockedDescription;
        }

        // Panel colors are distinct from each other, from Default's
        // existing (0.1, 0.1, 0.15) navy, and from every button color
        // already used in CoreLoopSceneBuilder.cs.
        private static readonly ThemeDefinition[] Themes =
        {
            new ThemeDefinition
            {
                Id = "Default",
                DisplayName = "Default",
                PanelColor = new Color(0.1f, 0.1f, 0.15f, 0.95f),
                LockedDescription = null
            },
            new ThemeDefinition
            {
                Id = "Council",
                DisplayName = "Council Chamber",
                PanelColor = new Color(0.22f, 0.08f, 0.16f, 0.95f),
                LockedDescription = "Unlocks once your council reaches its milestone"
            },
            new ThemeDefinition
            {
                Id = "Event",
                DisplayName = "Harvest Hall",
                PanelColor = new Color(0.16f, 0.13f, 0.04f, 0.95f),
                LockedDescription = "Unlocks once you claim a live-ops event reward"
            }
        };

        [SerializeField] private Button customizeButton;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI[] statusLabels;
        [SerializeField] private Button[] applyButtons;
        [SerializeField] private Image eventPanelImage;
        [SerializeField] private Image councilPanelImage;
        [SerializeField] private Image historyPanelImage;
        [SerializeField] private DecisionCycleManager manager;
        [SerializeField] private Slider armySlider;
        [SerializeField] private Slider tradeSlider;
        [SerializeField] private Slider religionSlider;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button viewHistoryButton;
        [SerializeField] private Button councilButton;
        [SerializeField] private Button eventsButton;
        [SerializeField] private DuelModalGate gate;
        [SerializeField] private Image sceneBackgroundImage;
        [SerializeField] private Sprite[] backgroundSprites;
        [SerializeField] private Sprite[] historyPanelSprites;
        [SerializeField] private Sprite[] councilPanelSprites;
        [SerializeField] private Sprite[] eventPanelSprites;
        [SerializeField] private Image historyArtImage;
        [SerializeField] private Image councilArtImage;
        [SerializeField] private Image eventArtImage;
        [SerializeField] private Image customizeIcon;
        [SerializeField] private Button estateButton;

        private void Start()
        {
            Bind();
        }

        /// <summary>
        /// Mirrors EventPanelController/CouncilPanelController's Initialize
        /// pattern -- called by Start() in the real scene, and callable
        /// directly by tests to bypass Unity lifecycle timing.
        /// </summary>
        public void Initialize(
            Button customizeButton,
            GameObject panelRoot,
            Button closeButton,
            TextMeshProUGUI[] statusLabels,
            Button[] applyButtons,
            Image eventPanelImage,
            Image councilPanelImage,
            Image historyPanelImage,
            DecisionCycleManager manager,
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button submitButton,
            Button challengeButton,
            Button viewHistoryButton,
            Button councilButton,
            Button eventsButton,
            DuelModalGate gate,
            Image sceneBackgroundImage,
            Sprite[] backgroundSprites,
            Sprite[] historyPanelSprites,
            Sprite[] councilPanelSprites,
            Sprite[] eventPanelSprites,
            Image historyArtImage,
            Image councilArtImage,
            Image eventArtImage,
            Image customizeIcon,
            Button estateButton)
        {
            this.customizeButton = customizeButton;
            this.panelRoot = panelRoot;
            this.closeButton = closeButton;
            this.statusLabels = statusLabels;
            this.applyButtons = applyButtons;
            this.eventPanelImage = eventPanelImage;
            this.councilPanelImage = councilPanelImage;
            this.historyPanelImage = historyPanelImage;
            this.manager = manager;
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.submitButton = submitButton;
            this.challengeButton = challengeButton;
            this.viewHistoryButton = viewHistoryButton;
            this.councilButton = councilButton;
            this.eventsButton = eventsButton;
            this.gate = gate;
            this.sceneBackgroundImage = sceneBackgroundImage;
            this.backgroundSprites = backgroundSprites;
            this.historyPanelSprites = historyPanelSprites;
            this.councilPanelSprites = councilPanelSprites;
            this.eventPanelSprites = eventPanelSprites;
            this.historyArtImage = historyArtImage;
            this.councilArtImage = councilArtImage;
            this.eventArtImage = eventArtImage;
            this.customizeIcon = customizeIcon;
            this.estateButton = estateButton;

            Bind();
        }

        private void Bind()
        {
            customizeButton.onClick.RemoveAllListeners();
            customizeButton.onClick.AddListener(OnCustomizeButtonClicked);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnClose);

            for (int i = 0; i < Themes.Length; i++)
            {
                int themeIndex = i; // capture by value, not the loop variable
                applyButtons[i].onClick.RemoveAllListeners();
                applyButtons[i].onClick.AddListener(() => OnApplyTheme(themeIndex));
            }

            panelRoot.SetActive(false);

            // Re-applies the saved theme on every scene load (relaunch),
            // not just when the panel is explicitly opened.
            ApplyTheme(manager.Ruler.State.SelectedTheme);
        }

        private void OnCustomizeButtonClicked()
        {
            gate.IsModalOpen = true;
            SetCoreLoopControlsInteractable(false);
            panelRoot.SetActive(true);
            RenderThemeRows();
        }

        private void RenderThemeRows()
        {
            for (int i = 0; i < Themes.Length; i++)
            {
                ThemeDefinition theme = Themes[i];
                bool unlocked = IsUnlocked(theme.Id);
                bool isSelected = manager.Ruler.State.SelectedTheme == theme.Id;

                statusLabels[i].text = unlocked
                    ? (isSelected ? $"{theme.DisplayName} (Selected)" : theme.DisplayName)
                    : $"{theme.DisplayName} -- {theme.LockedDescription}";
                applyButtons[i].interactable = unlocked && !isSelected;
            }
        }

        private void OnApplyTheme(int themeIndex)
        {
            ThemeDefinition theme = Themes[themeIndex];
            if (!IsUnlocked(theme.Id))
            {
                return;
            }

            manager.Ruler.State.SelectedTheme = theme.Id;
            SaveService.Save(manager.Ruler.State);
            ApplyTheme(theme.Id);
            RenderThemeRows();
        }

        private void OnClose()
        {
            panelRoot.SetActive(false);
            gate.IsModalOpen = false;
            SetCoreLoopControlsInteractable(true);
        }

        private bool IsUnlocked(string themeId)
        {
            switch (themeId)
            {
                case "Default":
                    return true;
                case "Council":
                    return manager.Ruler.State.CouncilRewardApplied;
                case "Event":
                    return !string.IsNullOrEmpty(manager.Ruler.State.ClaimedEventWeekId);
                default:
                    return false;
            }
        }

        private void ApplyTheme(string themeId)
        {
            Color color = GetThemeColor(themeId);
            eventPanelImage.color = color;
            councilPanelImage.color = color;
            historyPanelImage.color = color;
            eventArtImage.sprite = GetBackgroundSprite(themeId, eventPanelSprites);
            councilArtImage.sprite = GetBackgroundSprite(themeId, councilPanelSprites);
            historyArtImage.sprite = GetBackgroundSprite(themeId, historyPanelSprites);
            if (sceneBackgroundImage != null)
            {
                sceneBackgroundImage.sprite = GetBackgroundSprite(themeId, backgroundSprites);
            }
        }

        // Unrecognized ids (e.g. a future save-file edge case) resolve to
        // Default's color rather than throwing or leaving panels uncolored.
        private static Color GetThemeColor(string themeId)
        {
            foreach (ThemeDefinition theme in Themes)
            {
                if (theme.Id == themeId)
                {
                    return theme.PanelColor;
                }
            }

            return Themes[0].PanelColor;
        }

        // Same fallback shape as GetThemeColor -- a missing/null sprite for
        // an unrecognized or not-yet-loaded theme resolves to Default's
        // background (index 0) rather than leaving the background blank.
        private static Sprite GetBackgroundSprite(string themeId, Sprite[] sprites)
        {
            if (sprites == null || sprites.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < Themes.Length && i < sprites.Length; i++)
            {
                if (Themes[i].Id == themeId)
                {
                    return sprites[i] != null ? sprites[i] : sprites[0];
                }
            }

            return sprites[0];
        }

        private void SetCoreLoopControlsInteractable(bool interactable)
        {
            customizeButton.interactable = interactable;
            viewHistoryButton.interactable = interactable;
            councilButton.interactable = interactable;
            eventsButton.interactable = interactable;
            armySlider.interactable = interactable;
            tradeSlider.interactable = interactable;
            religionSlider.interactable = interactable;
            submitButton.interactable = interactable;
            if (estateButton != null)
                estateButton.interactable = interactable;

            // challengeButton has two independent disablers (this modal, and
            // Duel's own in-flight state) -- see
            // docs/superpowers/specs/2026-09-03-duel-modal-gate-design.md.
            if (interactable && gate.IsDuelInFlight)
            {
                return;
            }
            challengeButton.interactable = interactable;
        }
    }
}
