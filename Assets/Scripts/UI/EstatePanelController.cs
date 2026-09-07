// Assets/Scripts/UI/EstatePanelController.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Seventh modal panel alongside Duel/History/Council/Tutorial/Events/
    /// Cosmetics. Fully client-authoritative and locally persisted (no
    /// network calls) -- see
    /// docs/superpowers/specs/2026-09-07-estate-phase1-land-crops-design.md.
    /// </summary>
    public class EstatePanelController : MonoBehaviour
    {
        [Serializable]
        public class PlotView
        {
            public Image stageImage;
            public Button tapButton;
            public GameObject lockedOverlay;
            public TextMeshProUGUI lockCostLabel;
        }

        [SerializeField] private Button estateButton;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI coinsLabel;
        [SerializeField] private PlotView[] plotViews;
        [SerializeField] private GameObject seedPickerRoot;
        [SerializeField] private Button[] seedButtons;
        [SerializeField] private TextMeshProUGUI[] seedCostLabels;
        // Flat, index = CropCatalog.IndexOf(cropId) * 3 + stage. Loaded and
        // assigned once at scene-build time (CoreLoopSceneBuilder), same
        // pattern as CosmeticsPanelController's backgroundSprites arrays --
        // never loaded at runtime.
        [SerializeField] private Sprite[] cropStageSprites;
        [SerializeField] private Slider armySlider;
        [SerializeField] private Slider tradeSlider;
        [SerializeField] private Slider religionSlider;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button viewHistoryButton;
        [SerializeField] private Button councilButton;
        [SerializeField] private Button eventsButton;
        [SerializeField] private Button customizeButton;
        [SerializeField] private DuelModalGate gate;

        private EstateState state;
        private int pendingPlantPlotIndex = -1;

        private void Start()
        {
            Bind();
        }

        /// <summary>
        /// Mirrors CouncilPanelController/CosmeticsPanelController's
        /// Initialize pattern -- called by Start() in the real scene, and
        /// callable directly by tests to bypass Unity lifecycle timing.
        /// </summary>
        public void Initialize(
            Button estateButton,
            GameObject panelRoot,
            Button closeButton,
            TextMeshProUGUI coinsLabel,
            PlotView[] plotViews,
            GameObject seedPickerRoot,
            Button[] seedButtons,
            TextMeshProUGUI[] seedCostLabels,
            Sprite[] cropStageSprites,
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button submitButton,
            Button challengeButton,
            Button viewHistoryButton,
            Button councilButton,
            Button eventsButton,
            Button customizeButton,
            DuelModalGate gate)
        {
            this.estateButton = estateButton;
            this.panelRoot = panelRoot;
            this.closeButton = closeButton;
            this.coinsLabel = coinsLabel;
            this.plotViews = plotViews;
            this.seedPickerRoot = seedPickerRoot;
            this.seedButtons = seedButtons;
            this.seedCostLabels = seedCostLabels;
            this.cropStageSprites = cropStageSprites;
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.submitButton = submitButton;
            this.challengeButton = challengeButton;
            this.viewHistoryButton = viewHistoryButton;
            this.councilButton = councilButton;
            this.eventsButton = eventsButton;
            this.customizeButton = customizeButton;
            this.gate = gate;

            Bind();
        }

        private void Bind()
        {
            estateButton.onClick.RemoveAllListeners();
            estateButton.onClick.AddListener(OnEstateButtonClicked);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnClose);

            for (int i = 0; i < plotViews.Length; i++)
            {
                int plotIndex = i; // capture by value, not the loop variable
                plotViews[i].tapButton.onClick.RemoveAllListeners();
                plotViews[i].tapButton.onClick.AddListener(() => OnPlotTapped(plotIndex));
            }

            for (int i = 0; i < seedButtons.Length; i++)
            {
                int cropIndex = i;
                seedButtons[i].onClick.RemoveAllListeners();
                seedButtons[i].onClick.AddListener(() => OnSeedPicked(cropIndex));
            }

            panelRoot.SetActive(false);
            seedPickerRoot.SetActive(false);
        }

        private void OnEstateButtonClicked()
        {
            gate.IsModalOpen = true;
            SetCoreLoopControlsInteractable(false);
            state = SaveService.LoadEstate();
            panelRoot.SetActive(true);
            RefreshPlots();
        }

        private void OnClose()
        {
            SaveService.SaveEstate(state);
            seedPickerRoot.SetActive(false);
            pendingPlantPlotIndex = -1;
            panelRoot.SetActive(false);
            gate.IsModalOpen = false;
            SetCoreLoopControlsInteractable(true);
        }

        private void OnPlotTapped(int plotIndex)
        {
            LandPlot plot = state.Plots[plotIndex];

            if (!plot.Unlocked)
            {
                int cost = EstateState.UnlockCost(plotIndex);
                if (state.Coins < cost)
                {
                    return;
                }
                state.Coins -= cost;
                plot.Unlocked = true;
                RefreshPlots();
                return;
            }

            if (string.IsNullOrEmpty(plot.CropId))
            {
                pendingPlantPlotIndex = plotIndex;
                ShowSeedPicker();
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (plot.WateredAtUnixSeconds == 0)
            {
                plot.WateredAtUnixSeconds = now;
                StartCoroutine(ColorFlash(plotViews[plotIndex].stageImage, new Color(0.4f, 0.7f, 1f), 0.2f));
                RefreshPlots();
                return;
            }

            CropDefinition? crop = CropCatalog.Find(plot.CropId);
            if (crop == null)
            {
                return;
            }

            int stage = EstateState.GrowthStage(plot, crop.Value, now);
            if (stage < 2)
            {
                return;
            }

            state.Coins += crop.Value.SellValue;
            plot.CropId = null;
            plot.PlantedAtUnixSeconds = 0;
            plot.WateredAtUnixSeconds = 0;
            StartCoroutine(HarvestFly(plotViews[plotIndex].stageImage));
            RefreshPlots();
        }

        private void ShowSeedPicker()
        {
            for (int i = 0; i < CropCatalog.All.Length; i++)
            {
                CropDefinition crop = CropCatalog.All[i];
                seedCostLabels[i].text = $"{crop.DisplayName}: {crop.SeedCost}";
                seedButtons[i].interactable = state.Coins >= crop.SeedCost;
            }
            seedPickerRoot.SetActive(true);
        }

        private void OnSeedPicked(int cropIndex)
        {
            if (pendingPlantPlotIndex < 0)
            {
                return;
            }

            CropDefinition crop = CropCatalog.All[cropIndex];
            if (state.Coins < crop.SeedCost)
            {
                return;
            }

            state.Coins -= crop.SeedCost;
            LandPlot plot = state.Plots[pendingPlantPlotIndex];
            plot.CropId = crop.Id;
            plot.PlantedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            plot.WateredAtUnixSeconds = 0;

            int plantedPlotIndex = pendingPlantPlotIndex;
            seedPickerRoot.SetActive(false);
            pendingPlantPlotIndex = -1;
            RefreshPlots();
            StartCoroutine(ScaleBounce(plotViews[plantedPlotIndex].stageImage.transform, 1.3f, 0.15f));
        }

        private void RefreshPlots()
        {
            coinsLabel.text = $"Coins: {state.Coins}";
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            for (int i = 0; i < EstateState.PlotCount; i++)
            {
                LandPlot plot = state.Plots[i];
                PlotView view = plotViews[i];

                if (!plot.Unlocked)
                {
                    view.lockedOverlay.SetActive(true);
                    view.stageImage.gameObject.SetActive(false);
                    view.lockCostLabel.text = $"Unlock: {EstateState.UnlockCost(i)}";
                    continue;
                }

                view.lockedOverlay.SetActive(false);
                view.stageImage.gameObject.SetActive(true);

                if (string.IsNullOrEmpty(plot.CropId))
                {
                    view.stageImage.sprite = null;
                    continue;
                }

                int cropIndex = CropCatalog.IndexOf(plot.CropId);
                CropDefinition? crop = CropCatalog.Find(plot.CropId);
                if (crop == null || cropIndex < 0)
                {
                    continue;
                }

                int stage = EstateState.GrowthStage(plot, crop.Value, now);
                view.stageImage.sprite = GetStageSprite(cropIndex, stage);
            }
        }

        // Missing/not-yet-assigned sprites resolve to null (Image just
        // shows nothing) rather than throwing -- matches every other
        // panel's null/length-guarded sprite lookup precedent
        // (CosmeticsPanelController.GetBackgroundSprite).
        private Sprite GetStageSprite(int cropIndex, int stage)
        {
            if (cropStageSprites == null)
            {
                return null;
            }
            int index = cropIndex * 3 + stage;
            return index >= 0 && index < cropStageSprites.Length ? cropStageSprites[index] : null;
        }

        private void SetCoreLoopControlsInteractable(bool interactable)
        {
            estateButton.interactable = interactable;
            armySlider.interactable = interactable;
            tradeSlider.interactable = interactable;
            religionSlider.interactable = interactable;
            submitButton.interactable = interactable;
            viewHistoryButton.interactable = interactable;
            councilButton.interactable = interactable;
            eventsButton.interactable = interactable;
            customizeButton.interactable = interactable;

            // challengeButton has two independent disablers (this modal, and
            // Duel's own in-flight state) -- see
            // docs/superpowers/specs/2026-09-03-duel-modal-gate-design.md.
            if (interactable && gate.IsDuelInFlight)
            {
                return;
            }
            challengeButton.interactable = interactable;
        }

        private static IEnumerator ScaleBounce(Transform target, float peakScale, float duration)
        {
            Vector3 original = target.localScale;
            float half = duration / 2f;
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                target.localScale = Vector3.Lerp(original, original * peakScale, t / half);
                yield return null;
            }
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                target.localScale = Vector3.Lerp(original * peakScale, original, t / half);
                yield return null;
            }
            target.localScale = original;
        }

        private static IEnumerator ColorFlash(Image target, Color flashColor, float duration)
        {
            Color original = target.color;
            float half = duration / 2f;
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                target.color = Color.Lerp(original, flashColor, t / half);
                yield return null;
            }
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                target.color = Color.Lerp(flashColor, original, t / half);
                yield return null;
            }
            target.color = original;
        }

        // Spawns a short-lived copy of the harvested plot's sprite that
        // arcs to the coin label then is destroyed -- both live under the
        // same Canvas so a straight world-position lerp is a valid arc
        // target, no coroutine-owning-object lifetime issue since this
        // MonoBehaviour outlives the tween.
        private IEnumerator HarvestFly(Image sourceImage)
        {
            Sprite sprite = sourceImage.sprite;
            if (sprite == null)
            {
                yield break;
            }

            var flyer = new GameObject("HarvestFlyer", typeof(Image));
            flyer.transform.SetParent(sourceImage.transform.parent, false);
            var flyerImage = flyer.GetComponent<Image>();
            flyerImage.sprite = sprite;
            flyerImage.raycastTarget = false;
            var flyerRect = flyer.GetComponent<RectTransform>();
            flyerRect.position = sourceImage.transform.position;
            flyerRect.sizeDelta = sourceImage.rectTransform.sizeDelta;

            Vector3 start = flyerRect.position;
            Vector3 end = coinsLabel.transform.position;
            const float duration = 0.4f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                flyerRect.position = Vector3.Lerp(start, end, t / duration);
                yield return null;
            }

            Destroy(flyer);
        }
    }
}
