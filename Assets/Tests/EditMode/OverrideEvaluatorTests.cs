using NUnit.Framework;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.Tests
{
    public class OverrideEvaluatorTests
    {
        [Test]
        public void VeryLowLoyalty_NeutralMood_Aligned_RollBelowProbability_Overrides()
        {
            var state = new RulerState { Mood = 50, Loyalty = 10, Agenda = RulerState.AgendaType.Expansionist };
            var allocation = new ResourceAllocation(50, 30, 20); // Army 50 >= 40 -> aligned

            var result = OverrideEvaluator.Evaluate(state, allocation, roll: 0.30);

            Assert.IsTrue(result.Overridden);
        }

        [Test]
        public void HighLoyalty_AlignedAgenda_LowRoll_DoesNotOverride()
        {
            var state = new RulerState { Mood = 50, Loyalty = 80, Agenda = RulerState.AgendaType.Mercantile };
            var allocation = new ResourceAllocation(20, 60, 20);

            var result = OverrideEvaluator.Evaluate(state, allocation, roll: 0.50);

            Assert.IsFalse(result.Overridden);
        }

        [Test]
        public void NeutralLoyalty_MisalignedAgenda_MidRoll_Overrides()
        {
            var state = new RulerState { Mood = 50, Loyalty = 50, Agenda = RulerState.AgendaType.Pious };
            var allocation = new ResourceAllocation(50, 30, 20); // Religion 20 < 40 threshold -> misaligned

            var result = OverrideEvaluator.Evaluate(state, allocation, roll: 0.25);

            Assert.IsTrue(result.Overridden);
        }

        [Test]
        public void NotOverridden_AppliesPositiveDeltas()
        {
            var state = new RulerState { Mood = 50, Loyalty = 80, Agenda = RulerState.AgendaType.Mercantile };
            var allocation = new ResourceAllocation(20, 60, 20);

            var result = OverrideEvaluator.Evaluate(state, allocation, roll: 0.99);

            Assert.AreEqual(5, result.MoodDelta);
            Assert.AreEqual(3, result.LoyaltyDelta);
        }

        [Test]
        public void Overridden_AppliesNegativeDeltas()
        {
            var state = new RulerState { Mood = 50, Loyalty = 10, Agenda = RulerState.AgendaType.Expansionist };
            var allocation = new ResourceAllocation(50, 30, 20);

            var result = OverrideEvaluator.Evaluate(state, allocation, roll: 0.30);

            Assert.AreEqual(-10, result.MoodDelta);
            Assert.AreEqual(-5, result.LoyaltyDelta);
        }

        [Test]
        public void HighLoyalty_NeutralMood_Aligned_ProbabilityIsLow()
        {
            var state = new RulerState { Mood = 50, Loyalty = 80, Agenda = RulerState.AgendaType.Mercantile };
            var allocation = new ResourceAllocation(20, 60, 20); // aligned

            double probability = OverrideEvaluator.OverrideProbability(state, allocation);

            Assert.LessOrEqual(probability, 0.10);
        }

        [Test]
        public void LowLoyalty_NeutralMood_ProbabilityIsHigh()
        {
            var state = new RulerState { Mood = 50, Loyalty = 10, Agenda = RulerState.AgendaType.Expansionist };
            var allocation = new ResourceAllocation(50, 30, 20); // aligned

            double probability = OverrideEvaluator.OverrideProbability(state, allocation);

            Assert.GreaterOrEqual(probability, 0.50);
        }

        [Test]
        public void LowLoyalty_LoweringMoodFurther_IncreasesProbabilityFurther()
        {
            var allocation = new ResourceAllocation(50, 30, 20); // aligned for Expansionist
            var neutralMoodState = new RulerState { Mood = 50, Loyalty = 10, Agenda = RulerState.AgendaType.Expansionist };
            var lowMoodState = new RulerState { Mood = 10, Loyalty = 10, Agenda = RulerState.AgendaType.Expansionist };

            double neutralMoodProbability = OverrideEvaluator.OverrideProbability(neutralMoodState, allocation);
            double lowMoodProbability = OverrideEvaluator.OverrideProbability(lowMoodState, allocation);

            Assert.Greater(lowMoodProbability, neutralMoodProbability);
        }

        [Test]
        public void MisalignedAgenda_AddsBumpOnTopOfLoyaltyAndMood()
        {
            var state = new RulerState { Mood = 50, Loyalty = 50, Agenda = RulerState.AgendaType.Pious };
            var alignedAllocation = new ResourceAllocation(20, 20, 60); // Religion 60 >= 40 -> aligned
            var misalignedAllocation = new ResourceAllocation(50, 30, 20); // Religion 20 < 40 -> misaligned

            double alignedProbability = OverrideEvaluator.OverrideProbability(state, alignedAllocation);
            double misalignedProbability = OverrideEvaluator.OverrideProbability(state, misalignedAllocation);

            Assert.Greater(misalignedProbability, alignedProbability);
        }

        [Test]
        public void ExtremeWorstCase_ClampsToMaxProbability()
        {
            var state = new RulerState { Mood = 0, Loyalty = 0, Agenda = RulerState.AgendaType.Pious };
            var allocation = new ResourceAllocation(50, 30, 20); // Religion 20 < 40 -> misaligned

            double probability = OverrideEvaluator.OverrideProbability(state, allocation);

            Assert.AreEqual(0.95, probability, 0.0001);
        }

        [Test]
        public void ExtremeBestCase_ClampsToMinProbability()
        {
            var state = new RulerState { Mood = 100, Loyalty = 100, Agenda = RulerState.AgendaType.Mercantile };
            var allocation = new ResourceAllocation(20, 60, 20); // aligned

            double probability = OverrideEvaluator.OverrideProbability(state, allocation);

            Assert.AreEqual(0.02, probability, 0.0001);
        }
    }
}
