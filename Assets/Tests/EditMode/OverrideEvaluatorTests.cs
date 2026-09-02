using NUnit.Framework;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.Tests
{
    public class OverrideEvaluatorTests
    {
        [Test]
        public void LowLoyalty_AlwaysOverrides()
        {
            var state = new RulerState { Mood = 50, Loyalty = 10, Agenda = RulerState.AgendaType.Expansionist };
            var allocation = new ResourceAllocation(50, 30, 20);

            var result = OverrideEvaluator.Evaluate(state, allocation, roll: 0.90);

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
        public void HighLoyalty_MisalignedAgenda_MidRoll_Overrides()
        {
            var state = new RulerState { Mood = 50, Loyalty = 80, Agenda = RulerState.AgendaType.Pious };
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

            var result = OverrideEvaluator.Evaluate(state, allocation, roll: 0.90);

            Assert.AreEqual(-10, result.MoodDelta);
            Assert.AreEqual(-5, result.LoyaltyDelta);
        }
    }
}
