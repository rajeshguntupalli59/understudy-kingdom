using NUnit.Framework;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.Tests
{
    public class RulerStateTests
    {
        [Test]
        public void ApplyDelta_ClampsAtUpperBound()
        {
            var state = new RulerState { Mood = 95, Loyalty = 98 };

            state.ApplyDelta(moodDelta: 10, loyaltyDelta: 10);

            Assert.AreEqual(100, state.Mood);
            Assert.AreEqual(100, state.Loyalty);
        }

        [Test]
        public void ApplyDelta_ClampsAtLowerBound()
        {
            var state = new RulerState { Mood = 3, Loyalty = 2 };

            state.ApplyDelta(moodDelta: -10, loyaltyDelta: -10);

            Assert.AreEqual(0, state.Mood);
            Assert.AreEqual(0, state.Loyalty);
        }

        [Test]
        public void DefaultState_StartsAtFifty()
        {
            var state = new RulerState();

            Assert.AreEqual(50, state.Mood);
            Assert.AreEqual(50, state.Loyalty);
        }
    }
}
