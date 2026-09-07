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
    }
}
