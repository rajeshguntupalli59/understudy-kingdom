// Assets/Tests/PlayMode/EstatePanelControllerTests.cs
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    public class EstatePanelControllerTests
    {
        private GameObject canvasObject;
        private GameObject panelRootObject;
        private GameObject controllerObject;
        private EstatePanelController controller;
        private Button estateButton;
        private Button closeButton;
        private TextMeshProUGUI coinsLabel;
        private EstatePanelController.PlotView[] plotViews;
        private GameObject seedPickerRoot;
        private Button[] seedButtons;
        private TextMeshProUGUI[] seedCostLabels;
        private Slider armySlider;
        private Slider tradeSlider;
        private Slider religionSlider;
        private Button submitButton;
        private Button challengeButton;
        private Button viewHistoryButton;
        private Button councilButton;
        private Button eventsButton;
        private Button customizeButton;
        private GameObject gateObject;
        private DuelModalGate gate;

        [SetUp]
        public void SetUp()
        {
            if (File.Exists(SaveService.EstateSavePath))
            {
                File.Delete(SaveService.EstateSavePath);
            }

            canvasObject = new GameObject("Canvas", typeof(Canvas));

            estateButton = CreateButton("EstateButton");
            panelRootObject = new GameObject("EstatePanel");
            panelRootObject.transform.SetParent(canvasObject.transform, false);
            closeButton = CreateButton("CloseButton");
            coinsLabel = CreateLabel("CoinsLabel");

            plotViews = new EstatePanelController.PlotView[EstateState.PlotCount];
            for (int i = 0; i < plotViews.Length; i++)
            {
                var stageObject = new GameObject($"Plot{i}Stage", typeof(Image));
                stageObject.transform.SetParent(canvasObject.transform, false);
                var lockedOverlay = new GameObject($"Plot{i}Locked");
                lockedOverlay.transform.SetParent(canvasObject.transform, false);

                plotViews[i] = new EstatePanelController.PlotView
                {
                    stageImage = stageObject.GetComponent<Image>(),
                    tapButton = CreateButton($"Plot{i}Button"),
                    lockedOverlay = lockedOverlay,
                    lockCostLabel = CreateLabel($"Plot{i}LockCost")
                };
            }

            seedPickerRoot = new GameObject("SeedPicker");
            seedPickerRoot.transform.SetParent(canvasObject.transform, false);
            seedButtons = new Button[CropCatalog.All.Length];
            seedCostLabels = new TextMeshProUGUI[CropCatalog.All.Length];
            for (int i = 0; i < seedButtons.Length; i++)
            {
                seedButtons[i] = CreateButton($"SeedButton{i}");
                seedCostLabels[i] = CreateLabel($"SeedCostLabel{i}");
            }

            armySlider = CreateSlider("ArmySlider");
            tradeSlider = CreateSlider("TradeSlider");
            religionSlider = CreateSlider("ReligionSlider");
            submitButton = CreateButton("SubmitButton");
            challengeButton = CreateButton("ChallengeButton");
            viewHistoryButton = CreateButton("ViewHistoryButton");
            councilButton = CreateButton("CouncilButton");
            eventsButton = CreateButton("EventsButton");
            customizeButton = CreateButton("CustomizeButton");

            gateObject = new GameObject("DuelModalGate");
            gate = gateObject.AddComponent<DuelModalGate>();

            controllerObject = new GameObject("Controller");
            controller = controllerObject.AddComponent<EstatePanelController>();
            controller.Initialize(estateButton, panelRootObject, closeButton, coinsLabel,
                plotViews, seedPickerRoot, seedButtons, seedCostLabels, new Sprite[9],
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton,
                viewHistoryButton, councilButton, eventsButton, customizeButton, gate);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(gateObject);

            if (File.Exists(SaveService.EstateSavePath))
            {
                File.Delete(SaveService.EstateSavePath);
            }
        }

        private Slider CreateSlider(string name)
        {
            var sliderObject = new GameObject(name, typeof(Slider));
            sliderObject.transform.SetParent(canvasObject.transform, false);
            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 100f;
            return slider;
        }

        private Button CreateButton(string name)
        {
            var buttonObject = new GameObject(name, typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvasObject.transform, false);
            return buttonObject.GetComponent<Button>();
        }

        private TextMeshProUGUI CreateLabel(string name)
        {
            var labelObject = new GameObject(name, typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(canvasObject.transform, false);
            return labelObject.GetComponent<TextMeshProUGUI>();
        }

        [Test]
        public void EstateButton_OnFirstOpen_ShowsPanelWithStartingCoinsAndPlots()
        {
            estateButton.onClick.Invoke();

            Assert.IsTrue(panelRootObject.activeSelf);
            Assert.AreEqual("Coins: 200", coinsLabel.text);
        }

        [Test]
        public void EstateButton_OnOpen_LockedPlotsShowLockedOverlayWithCost()
        {
            estateButton.onClick.Invoke();

            Assert.IsTrue(plotViews[4].lockedOverlay.activeSelf);
            Assert.AreEqual("Unlock: 100", plotViews[4].lockCostLabel.text);
            Assert.IsFalse(plotViews[0].lockedOverlay.activeSelf);
        }

        [Test]
        public void EstateButton_OnOpen_DisablesSharedControls()
        {
            estateButton.onClick.Invoke();

            Assert.IsFalse(armySlider.interactable);
            Assert.IsFalse(submitButton.interactable);
            Assert.IsFalse(challengeButton.interactable);
            Assert.IsFalse(viewHistoryButton.interactable);
            Assert.IsFalse(councilButton.interactable);
            Assert.IsFalse(eventsButton.interactable);
            Assert.IsFalse(customizeButton.interactable);
        }

        [Test]
        public void Close_ReEnablesSharedControlsAndHidesPanel()
        {
            estateButton.onClick.Invoke();
            closeButton.onClick.Invoke();

            Assert.IsFalse(panelRootObject.activeSelf);
            Assert.IsTrue(armySlider.interactable);
            Assert.IsTrue(submitButton.interactable);
        }

        [Test]
        public void TapEmptyUnlockedPlot_ShowsSeedPickerWithAffordableButtonsEnabled()
        {
            estateButton.onClick.Invoke();

            plotViews[0].tapButton.onClick.Invoke();

            Assert.IsTrue(seedPickerRoot.activeSelf);
            Assert.IsTrue(seedButtons[0].interactable); // wheat, cost 5, affordable at 200 coins
            Assert.AreEqual("Wheat: 5", seedCostLabels[0].text);
        }

        [Test]
        public void PickSeed_DeductsCoinsAndPlantsOnPendingPlot()
        {
            estateButton.onClick.Invoke();
            plotViews[0].tapButton.onClick.Invoke();

            seedButtons[0].onClick.Invoke(); // wheat, cost 5

            Assert.AreEqual("Coins: 195", coinsLabel.text);
            Assert.IsFalse(seedPickerRoot.activeSelf);
        }

        [Test]
        public void TapPlantedUnwateredPlot_StartsGrowthByRecordingWaterTime()
        {
            estateButton.onClick.Invoke();
            plotViews[0].tapButton.onClick.Invoke();
            seedButtons[0].onClick.Invoke(); // plant wheat

            plotViews[0].tapButton.onClick.Invoke(); // water it

            // Reopen fresh to confirm the watered timestamp persisted into
            // the controller's in-memory state (checked indirectly via
            // save/reload below, which is the observable contract).
            closeButton.onClick.Invoke();
            var saved = SaveService.LoadEstate();
            Assert.AreNotEqual(0, saved.Plots[0].WateredAtUnixSeconds);
            Assert.AreEqual("wheat", saved.Plots[0].CropId);
        }

        [Test]
        public void TapMaturePlot_AwardsSellValueCoinsAndClearsPlot()
        {
            // Arrange a pre-existing save with plot 0 already mature (wheat,
            // 30s grow duration, watered far enough in the past).
            var seeded = new EstateState { Coins = 200 };
            seeded.Plots[0].CropId = "wheat";
            seeded.Plots[0].PlantedAtUnixSeconds = 1;
            seeded.Plots[0].WateredAtUnixSeconds = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3600;
            SaveService.SaveEstate(seeded);

            estateButton.onClick.Invoke(); // loads the seeded state
            plotViews[0].tapButton.onClick.Invoke(); // harvest (mature)

            Assert.AreEqual("Coins: 212", coinsLabel.text); // 200 + 12 (wheat SellValue)
        }

        [Test]
        public void TapMaturePlot_ClearsCropSoNextOpenShowsEmptyPlot()
        {
            var seeded = new EstateState { Coins = 200 };
            seeded.Plots[0].CropId = "wheat";
            seeded.Plots[0].PlantedAtUnixSeconds = 1;
            seeded.Plots[0].WateredAtUnixSeconds = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3600;
            SaveService.SaveEstate(seeded);

            estateButton.onClick.Invoke();
            plotViews[0].tapButton.onClick.Invoke();
            closeButton.onClick.Invoke();

            var saved = SaveService.LoadEstate();
            Assert.IsTrue(string.IsNullOrEmpty(saved.Plots[0].CropId));
        }

        [Test]
        public void TapGrowingNotMaturePlot_DoesNothing()
        {
            var seeded = new EstateState { Coins = 200 };
            seeded.Plots[0].CropId = "wheat";
            seeded.Plots[0].PlantedAtUnixSeconds = 1;
            seeded.Plots[0].WateredAtUnixSeconds = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // stage 0/1, not mature
            SaveService.SaveEstate(seeded);

            estateButton.onClick.Invoke();
            plotViews[0].tapButton.onClick.Invoke(); // should be a no-op harvest attempt

            Assert.AreEqual("Coins: 200", coinsLabel.text);
        }

        [Test]
        public void TapLockedPlot_WithEnoughCoins_UnlocksAndDeductsCost()
        {
            estateButton.onClick.Invoke(); // 200 coins, plot 4 costs 100

            plotViews[4].tapButton.onClick.Invoke();

            Assert.AreEqual("Coins: 100", coinsLabel.text);
            Assert.IsFalse(plotViews[4].lockedOverlay.activeSelf);
        }

        [Test]
        public void TapLockedPlot_WithoutEnoughCoins_DoesNothing()
        {
            var seeded = new EstateState { Coins = 50 }; // plot 4 costs 100
            SaveService.SaveEstate(seeded);

            estateButton.onClick.Invoke();
            plotViews[4].tapButton.onClick.Invoke();

            Assert.AreEqual("Coins: 50", coinsLabel.text);
            Assert.IsTrue(plotViews[4].lockedOverlay.activeSelf);
        }

        [Test]
        public void Close_PersistsStateForNextOpen()
        {
            estateButton.onClick.Invoke();
            plotViews[4].tapButton.onClick.Invoke(); // unlock plot 4, 200 -> 100
            closeButton.onClick.Invoke();

            var saved = SaveService.LoadEstate();
            Assert.AreEqual(100, saved.Coins);
            Assert.IsTrue(saved.Plots[4].Unlocked);
        }
    }
}
