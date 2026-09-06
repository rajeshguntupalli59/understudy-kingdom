using System.Reflection;
using NUnit.Framework;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    public class RulerPortraitMappingTests
    {
        private static int InvokeGetMoodTier(int mood)
        {
            MethodInfo method = typeof(CoreLoopScreenController).GetMethod(
                "GetMoodTier", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "GetMoodTier method not found -- CoreLoopScreenController internals changed");
            return (int)method.Invoke(null, new object[] { mood });
        }

        private static int InvokeGetLoyaltyTier(int loyalty)
        {
            MethodInfo method = typeof(CoreLoopScreenController).GetMethod(
                "GetLoyaltyTier", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "GetLoyaltyTier method not found -- CoreLoopScreenController internals changed");
            return (int)method.Invoke(null, new object[] { loyalty });
        }

        [Test]
        public void GetMoodTier_BoundaryValues_MapToCorrectTier()
        {
            Assert.AreEqual(0, InvokeGetMoodTier(0));
            Assert.AreEqual(0, InvokeGetMoodTier(20));
            Assert.AreEqual(1, InvokeGetMoodTier(21));
            Assert.AreEqual(1, InvokeGetMoodTier(40));
            Assert.AreEqual(2, InvokeGetMoodTier(41));
            Assert.AreEqual(2, InvokeGetMoodTier(60));
            Assert.AreEqual(3, InvokeGetMoodTier(61));
            Assert.AreEqual(3, InvokeGetMoodTier(80));
            Assert.AreEqual(4, InvokeGetMoodTier(81));
            Assert.AreEqual(4, InvokeGetMoodTier(100));
        }

        [Test]
        public void GetLoyaltyTier_BoundaryValues_MapToCorrectTier()
        {
            Assert.AreEqual(0, InvokeGetLoyaltyTier(0));
            Assert.AreEqual(0, InvokeGetLoyaltyTier(33));
            Assert.AreEqual(1, InvokeGetLoyaltyTier(34));
            Assert.AreEqual(1, InvokeGetLoyaltyTier(66));
            Assert.AreEqual(2, InvokeGetLoyaltyTier(67));
            Assert.AreEqual(2, InvokeGetLoyaltyTier(100));
        }

        [Test]
        public void CombinedIndex_MoodTierTimesThreePlusLoyaltyTier_MatchesMoodMajorOrder()
        {
            // index 0 = Furious/Low, index 7 = Neutral/Medium, index 14 = Delighted/High
            Assert.AreEqual(0, InvokeGetMoodTier(0) * 3 + InvokeGetLoyaltyTier(0));
            Assert.AreEqual(7, InvokeGetMoodTier(50) * 3 + InvokeGetLoyaltyTier(50));
            Assert.AreEqual(14, InvokeGetMoodTier(100) * 3 + InvokeGetLoyaltyTier(100));
        }
    }
}
