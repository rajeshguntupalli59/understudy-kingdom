using NUnit.Framework;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Tests
{
    public class EstateStateTests
    {
        [Test]
        public void NewEstateState_HasStartingCoinsAndFourUnlockedPlots()
        {
            var state = new EstateState();

            Assert.AreEqual(200, state.Coins);
            Assert.AreEqual(8, state.Plots.Length);
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(state.Plots[i].Unlocked, $"plot {i} should start unlocked");
            }
            for (int i = 4; i < 8; i++)
            {
                Assert.IsFalse(state.Plots[i].Unlocked, $"plot {i} should start locked");
            }
        }

        [Test]
        public void GrowthStage_NeverWatered_StaysStageZeroRegardlessOfElapsedTime()
        {
            var plot = new LandPlot { CropId = "wheat", PlantedAtUnixSeconds = 1000, WateredAtUnixSeconds = 0 };
            var wheat = new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12);

            int stage = EstateState.GrowthStage(plot, wheat, nowUnixSeconds: 1000 + 999999);

            Assert.AreEqual(0, stage);
        }

        [Test]
        public void GrowthStage_JustWatered_IsStageZero()
        {
            var plot = new LandPlot { CropId = "wheat", WateredAtUnixSeconds = 1000 };
            var wheat = new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12);

            int stage = EstateState.GrowthStage(plot, wheat, nowUnixSeconds: 1000);

            Assert.AreEqual(0, stage);
        }

        [Test]
        public void GrowthStage_AtHalfDuration_IsStageOne()
        {
            var plot = new LandPlot { CropId = "wheat", WateredAtUnixSeconds = 1000 };
            var wheat = new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12);

            int stage = EstateState.GrowthStage(plot, wheat, nowUnixSeconds: 1015); // 15s = half of 30s

            Assert.AreEqual(1, stage);
        }

        [Test]
        public void GrowthStage_AtFullDuration_IsStageTwo()
        {
            var plot = new LandPlot { CropId = "wheat", WateredAtUnixSeconds = 1000 };
            var wheat = new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12);

            int stage = EstateState.GrowthStage(plot, wheat, nowUnixSeconds: 1030);

            Assert.AreEqual(2, stage);
        }

        [Test]
        public void GrowthStage_PastFullDuration_ClampsAtStageTwo()
        {
            var plot = new LandPlot { CropId = "wheat", WateredAtUnixSeconds = 1000 };
            var wheat = new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12);

            int stage = EstateState.GrowthStage(plot, wheat, nowUnixSeconds: 1000 + 999999);

            Assert.AreEqual(2, stage);
        }

        [Test]
        public void GrowthStage_OddGrowDuration_DoesNotTransitionEarly()
        {
            var plot = new LandPlot { CropId = "test", WateredAtUnixSeconds = 1000 };
            var crop = new CropDefinition("test", "Test", seedCost: 1, growDurationSeconds: 5, sellValue: 1);

            Assert.AreEqual(0, EstateState.GrowthStage(plot, crop, nowUnixSeconds: 1002)); // elapsed=2, true half=2.5, still stage 0
            Assert.AreEqual(1, EstateState.GrowthStage(plot, crop, nowUnixSeconds: 1003)); // elapsed=3, past half, stage 1
            Assert.AreEqual(1, EstateState.GrowthStage(plot, crop, nowUnixSeconds: 1004)); // elapsed=4, still stage 1 (not yet full 5)
            Assert.AreEqual(2, EstateState.GrowthStage(plot, crop, nowUnixSeconds: 1005)); // elapsed=5, full duration, stage 2
        }

        [Test]
        public void GrowthProgress01_NeverWatered_IsZero()
        {
            var plot = new LandPlot { CropId = "wheat", WateredAtUnixSeconds = 0 };
            var wheat = new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12);

            float progress = EstateState.GrowthProgress01(plot, wheat, nowUnixSeconds: 999999);

            Assert.AreEqual(0f, progress);
        }

        [Test]
        public void GrowthProgress01_JustWatered_IsZero()
        {
            var plot = new LandPlot { CropId = "wheat", WateredAtUnixSeconds = 1000 };
            var wheat = new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12);

            float progress = EstateState.GrowthProgress01(plot, wheat, nowUnixSeconds: 1000);

            Assert.AreEqual(0f, progress);
        }

        [Test]
        public void GrowthProgress01_AtHalfDuration_IsAboutHalf()
        {
            var plot = new LandPlot { CropId = "wheat", WateredAtUnixSeconds = 1000 };
            var wheat = new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12);

            float progress = EstateState.GrowthProgress01(plot, wheat, nowUnixSeconds: 1015); // 15s of 30s

            Assert.AreEqual(0.5f, progress, 0.001f);
        }

        [Test]
        public void GrowthProgress01_AtFullDuration_IsOne()
        {
            var plot = new LandPlot { CropId = "wheat", WateredAtUnixSeconds = 1000 };
            var wheat = new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12);

            float progress = EstateState.GrowthProgress01(plot, wheat, nowUnixSeconds: 1030);

            Assert.AreEqual(1f, progress, 0.001f);
        }

        [Test]
        public void GrowthProgress01_PastFullDuration_StaysClampedAtOne()
        {
            var plot = new LandPlot { CropId = "wheat", WateredAtUnixSeconds = 1000 };
            var wheat = new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12);

            float progress = EstateState.GrowthProgress01(plot, wheat, nowUnixSeconds: 1000 + 999999);

            Assert.AreEqual(1f, progress, 0.001f);
        }

        [Test]
        public void UnlockCost_FirstLockedPlot_Is100()
        {
            Assert.AreEqual(100, EstateState.UnlockCost(4));
        }

        [Test]
        public void UnlockCost_LastLockedPlot_Is410()
        {
            Assert.AreEqual(410, EstateState.UnlockCost(7));
        }

        [Test]
        public void UnlockCost_MiddleLockedPlots_Match160And260()
        {
            Assert.AreEqual(160, EstateState.UnlockCost(5));
            Assert.AreEqual(260, EstateState.UnlockCost(6));
        }

        [Test]
        public void NewEstateState_HasEmptyInventoryOfCorrectLength()
        {
            var state = new EstateState();

            Assert.AreEqual(GoodsCatalog.Count, state.Inventory.Length);
            foreach (int count in state.Inventory)
            {
                Assert.AreEqual(0, count);
            }
        }

        [Test]
        public void NewEstateState_HasAllShopsLockedAndIdle()
        {
            var state = new EstateState();

            Assert.AreEqual(ShopCatalog.All.Length, state.Shops.Length);
            foreach (ShopState shop in state.Shops)
            {
                Assert.IsFalse(shop.Unlocked);
                Assert.AreEqual(0, shop.ProductionStartedAtUnixSeconds);
            }
        }

        [Test]
        public void ShopProductionStage_Idle_IsStageZeroRegardlessOfElapsedTime()
        {
            var shop = new ShopState { Unlocked = true, ProductionStartedAtUnixSeconds = 0 };
            ShopDefinition bakery = ShopCatalog.Find("bakery").Value;

            int stage = EstateState.ShopProductionStage(shop, bakery, nowUnixSeconds: 999999);

            Assert.AreEqual(0, stage);
        }

        [Test]
        public void ShopProductionStage_JustStarted_IsStageOne()
        {
            var shop = new ShopState { Unlocked = true, ProductionStartedAtUnixSeconds = 1000 };
            ShopDefinition bakery = ShopCatalog.Find("bakery").Value; // 60s production

            int stage = EstateState.ShopProductionStage(shop, bakery, nowUnixSeconds: 1001);

            Assert.AreEqual(1, stage);
        }

        [Test]
        public void ShopProductionStage_AtFullDuration_IsStageTwo()
        {
            var shop = new ShopState { Unlocked = true, ProductionStartedAtUnixSeconds = 1000 };
            ShopDefinition bakery = ShopCatalog.Find("bakery").Value;

            int stage = EstateState.ShopProductionStage(shop, bakery, nowUnixSeconds: 1060);

            Assert.AreEqual(2, stage);
        }

        [Test]
        public void ShopProductionStage_PastFullDuration_StaysStageTwo()
        {
            var shop = new ShopState { Unlocked = true, ProductionStartedAtUnixSeconds = 1000 };
            ShopDefinition bakery = ShopCatalog.Find("bakery").Value;

            int stage = EstateState.ShopProductionStage(shop, bakery, nowUnixSeconds: 999999);

            Assert.AreEqual(2, stage);
        }
    }
}
