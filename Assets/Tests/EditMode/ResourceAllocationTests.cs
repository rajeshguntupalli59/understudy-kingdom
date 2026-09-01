using NUnit.Framework;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Tests
{
    public class ResourceAllocationTests
    {
        [Test]
        public void SummingTo100_IsValid()
        {
            var allocation = new ResourceAllocation(40, 30, 30);

            Assert.IsTrue(allocation.IsValid());
        }

        [Test]
        public void NotSummingTo100_IsNotValid()
        {
            var allocation = new ResourceAllocation(40, 30, 20);

            Assert.IsFalse(allocation.IsValid());
        }

        [Test]
        public void NegativeValue_IsNotValid()
        {
            var allocation = new ResourceAllocation(-10, 60, 50);

            Assert.IsFalse(allocation.IsValid());
        }
    }
}
