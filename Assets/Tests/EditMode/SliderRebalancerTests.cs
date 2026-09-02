using NUnit.Framework;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    public class SliderRebalancerTests
    {
        [Test]
        public void ChangingOneValue_OthersAbsorbRemainderProportionally()
        {
            var (a, b, c) = SliderRebalancer.Rebalance(40, 30, 30, changedIndex: 0, newValue: 70);

            Assert.AreEqual(70, a);
            Assert.AreEqual(15, b);
            Assert.AreEqual(15, c);
            Assert.AreEqual(100, a + b + c);
        }

        [Test]
        public void ChangedValueSetToMaximum_OthersGoToZero()
        {
            var (a, b, c) = SliderRebalancer.Rebalance(40, 30, 30, changedIndex: 1, newValue: 100);

            Assert.AreEqual(0, a);
            Assert.AreEqual(100, b);
            Assert.AreEqual(0, c);
        }

        [Test]
        public void ChangedValueSetToZero_OthersAbsorbFullRemainder()
        {
            var (a, b, c) = SliderRebalancer.Rebalance(20, 50, 30, changedIndex: 2, newValue: 0);

            Assert.AreEqual(29, a);
            Assert.AreEqual(71, b);
            Assert.AreEqual(0, c);
            Assert.AreEqual(100, a + b + c);
        }

        [Test]
        public void BothOtherValuesAreZero_RemainderSplitsEvenly()
        {
            var (a, b, c) = SliderRebalancer.Rebalance(100, 0, 0, changedIndex: 0, newValue: 40);

            Assert.AreEqual(40, a);
            Assert.AreEqual(30, b);
            Assert.AreEqual(30, c);
            Assert.AreEqual(100, a + b + c);
        }

        [Test]
        public void BothOtherValuesAreZero_OddRemainderSplitsWithExtraOnSecondOther()
        {
            var (a, b, c) = SliderRebalancer.Rebalance(100, 0, 0, changedIndex: 0, newValue: 1);

            Assert.AreEqual(1, a);
            Assert.AreEqual(49, b);
            Assert.AreEqual(50, c);
            Assert.AreEqual(100, a + b + c);
        }

        [Test]
        public void NewValueAboveMaximum_IsClampedTo100()
        {
            var (a, b, c) = SliderRebalancer.Rebalance(40, 30, 30, changedIndex: 0, newValue: 150);

            Assert.AreEqual(100, a);
            Assert.AreEqual(0, b);
            Assert.AreEqual(0, c);
        }

        [Test]
        public void NewValueBelowMinimum_IsClampedTo0()
        {
            var (a, b, c) = SliderRebalancer.Rebalance(40, 30, 30, changedIndex: 0, newValue: -20);

            Assert.AreEqual(0, a);
            Assert.AreEqual(50, b);
            Assert.AreEqual(50, c);
        }
    }
}
