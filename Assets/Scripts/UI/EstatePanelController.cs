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

        [Serializable]
        public class InventoryRowView
        {
            public GameObject root;
            public TextMeshProUGUI label;
            public Button sellButton;
        }

        [Serializable]
        public class ShopRowView
        {
            public TextMeshProUGUI statusLabel;
            public Button actionButton;
            public TextMeshProUGUI actionButtonLabel;
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
        [SerializeField] private Button landTabButton;
        [SerializeField] private Button shopsTabButton;
        [SerializeField] private GameObject landTabRoot;
        [SerializeField] private GameObject shopsTabRoot;
        [SerializeField] private InventoryRowView[] inventoryRows;
        [SerializeField] private ShopRowView[] shopRows;

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
            DuelModalGate gate,
            Button landTabButton,
            Button shopsTabButton,
            GameObject landTabRoot,
            GameObject shopsTabRoot,
            InventoryRowView[] inventoryRows,
            ShopRowView[] shopRows)
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
            this.landTabButton = landTabButton;
            this.shopsTabButton = shopsTabButton;
            this.landTabRoot = landTabRoot;
            this.shopsTabRoot = shopsTabRoot;
            this.inventoryRows = inventoryRows;
            this.shopRows = shopRows;

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

            // null-safe until Task 7 wires these fields in CoreLoopSceneBuilder
            if (landTabButton != null)
            {
                landTabButton.onClick.RemoveAllListeners();
                landTabButton.onClick.AddListener(() => SetActiveTab(true));
            }
            if (shopsTabButton != null)
            {
                shopsTabButton.onClick.RemoveAllListeners();
                shopsTabButton.onClick.AddListener(() => SetActiveTab(false));
            }

            if (inventoryRows != null)
            {
                for (int i = 0; i < inventoryRows.Length; i++)
                {
                    int goodsIndex = i;
                    inventoryRows[i].sellButton.onClick.RemoveAllListeners();
                    inventoryRows[i].sellButton.onClick.AddListener(() => OnSellStack(goodsIndex));
                }
            }

            if (shopRows != null)
            {
                for (int i = 0; i < shopRows.Length; i++)
                {
                    int shopIndex = i;
                    shopRows[i].actionButton.onClick.RemoveAllListeners();
                    shopRows[i].actionButton.onClick.AddListener(() => OnShopActionTapped(shopIndex));
                }
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
            SetActiveTab(true);
            RefreshPlots();
            RefreshInventoryAndShops();
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
                SaveService.SaveEstate(state);
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
                SaveService.SaveEstate(state);
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

            state.Inventory[GoodsCatalog.IndexOf(plot.CropId)] += 1;
            plot.CropId = null;
            plot.PlantedAtUnixSeconds = 0;
            plot.WateredAtUnixSeconds = 0;
            StartCoroutine(HarvestFly(plotViews[plotIndex].stageImage));
            RefreshPlots();
            RefreshInventoryAndShops();
            SaveService.SaveEstate(state);
        }

        private void ShowSeedPicker()
        {
            RefreshSeedPickerAffordability();
            seedPickerRoot.SetActive(true);
        }

        // Extracted from ShowSeedPicker so RefreshPlots() can re-run the same
        // affordability check while the picker is already open (e.g. a
        // different plot's harvest/unlock changes state.Coins mid-pick) --
        // otherwise the interactable/cost display goes stale until the picker
        // is closed and reopened.
        private void RefreshSeedPickerAffordability()
        {
            for (int i = 0; i < CropCatalog.All.Length; i++)
            {
                CropDefinition crop = CropCatalog.All[i];
                seedCostLabels[i].text = $"{crop.DisplayName}: {crop.SeedCost}";
                seedButtons[i].interactable = state.Coins >= crop.SeedCost;
            }
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
            SaveService.SaveEstate(state);
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

            if (seedPickerRoot.activeSelf)
            {
                RefreshSeedPickerAffordability();
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

        private void SetActiveTab(bool land)
        {
            // null-safe until Task 7 wires these fields in CoreLoopSceneBuilder
            if (landTabRoot != null)
            {
                landTabRoot.SetActive(land);
            }
            if (shopsTabRoot != null)
            {
                shopsTabRoot.SetActive(!land);
            }
        }

        private void OnSellStack(int goodsIndex)
        {
            int count = state.Inventory[goodsIndex];
            if (count <= 0)
            {
                return;
            }

            string goodsId = GoodsIdForIndex(goodsIndex);
            state.Coins += count * GoodsCatalog.SellValue(goodsId);
            state.Inventory[goodsIndex] = 0;
            SaveService.SaveEstate(state);
            RefreshInventoryAndShops();
            coinsLabel.text = $"Coins: {state.Coins}";
        }

        private void OnShopActionTapped(int shopIndex)
        {
            ShopDefinition def = ShopCatalog.All[shopIndex];
            ShopState shop = state.Shops[shopIndex];

            if (!shop.Unlocked)
            {
                if (state.Coins < def.UnlockCost)
                {
                    return;
                }
                state.Coins -= def.UnlockCost;
                shop.Unlocked = true;
                SaveService.SaveEstate(state);
                RefreshInventoryAndShops();
                coinsLabel.text = $"Coins: {state.Coins}";
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int stage = EstateState.ShopProductionStage(shop, def, now);

            if (stage == 0)
            {
                int inputIndex = GoodsCatalog.IndexOf(def.InputGoodsId);
                if (state.Coins < def.StartCost || state.Inventory[inputIndex] < 1)
                {
                    return;
                }
                state.Coins -= def.StartCost;
                state.Inventory[inputIndex] -= 1;
                shop.ProductionStartedAtUnixSeconds = now;
                SaveService.SaveEstate(state);
                RefreshInventoryAndShops();
                coinsLabel.text = $"Coins: {state.Coins}";
                return;
            }

            if (stage == 2)
            {
                int outputIndex = GoodsCatalog.IndexOf(def.OutputGoodsId);
                state.Inventory[outputIndex] += 1;
                shop.ProductionStartedAtUnixSeconds = 0;
                SaveService.SaveEstate(state);
                RefreshInventoryAndShops();
                return;
            }

            // stage == 1 (in progress): no-op, matches crop-growth's own
            // "tapping a growing plot does nothing" contract.
        }

        private static string GoodsIdForIndex(int goodsIndex)
        {
            return goodsIndex < CropCatalog.All.Length
                ? CropCatalog.All[goodsIndex].Id
                : ProductCatalog.All[goodsIndex - CropCatalog.All.Length].Id;
        }

        private void RefreshInventoryAndShops()
        {
            // null-safe until Task 7 wires these fields in CoreLoopSceneBuilder
            if (inventoryRows != null)
            {
                for (int i = 0; i < inventoryRows.Length; i++)
                {
                    int count = state.Inventory[i];
                    inventoryRows[i].root.SetActive(count > 0);
                    if (count > 0)
                    {
                        string goodsId = GoodsIdForIndex(i);
                        inventoryRows[i].label.text = $"{GoodsCatalog.DisplayName(goodsId)} x{count}";
                    }
                }
            }

            if (shopRows != null)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                for (int i = 0; i < shopRows.Length; i++)
                {
                    ShopDefinition def = ShopCatalog.All[i];
                    ShopState shop = state.Shops[i];

                    if (!shop.Unlocked)
                    {
                        shopRows[i].statusLabel.text = $"{def.DisplayName} -- Unlock: {def.UnlockCost}";
                        shopRows[i].actionButtonLabel.text = "Unlock";
                        shopRows[i].actionButton.interactable = state.Coins >= def.UnlockCost;
                        continue;
                    }

                    int stage = EstateState.ShopProductionStage(shop, def, now);
                    if (stage == 0)
                    {
                        int inputIndex = GoodsCatalog.IndexOf(def.InputGoodsId);
                        shopRows[i].statusLabel.text = $"{def.DisplayName}: {def.InputGoodsId} x1 + {def.StartCost} coins";
                        shopRows[i].actionButtonLabel.text = "Start";
                        shopRows[i].actionButton.interactable = state.Coins >= def.StartCost && state.Inventory[inputIndex] >= 1;
                    }
                    else if (stage == 1)
                    {
                        shopRows[i].statusLabel.text = $"{def.DisplayName}: producing {def.OutputGoodsId}...";
                        shopRows[i].actionButtonLabel.text = "...";
                        shopRows[i].actionButton.interactable = false;
                    }
                    else
                    {
                        shopRows[i].statusLabel.text = $"{def.DisplayName}: {def.OutputGoodsId} ready!";
                        shopRows[i].actionButtonLabel.text = "Collect";
                        shopRows[i].actionButton.interactable = true;
                    }
                }
            }
        }
    }
}
