using NUnit.Framework;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.Core;
using UnityEngine;

namespace UnderstudyKingdom.Tests
{
    public class DecisionSyncRequestFactoryTests
    {
        [Test]
        public void From_MapsAllFieldsFromDecisionRecord()
        {
            var record = new DecisionRecord(
                cycleNumber: 3,
                recommendation: new ResourceAllocation(20, 50, 30),
                overridden: true,
                mood: 65,
                loyalty: 40);

            DecisionSyncRequest dto = DecisionSyncRequestFactory.From(record);

            Assert.AreEqual(3, dto.cycle_number);
            Assert.AreEqual(20, dto.player_recommendation.army);
            Assert.AreEqual(50, dto.player_recommendation.trade);
            Assert.AreEqual(30, dto.player_recommendation.religion);
            Assert.AreEqual(65, dto.ruler_outcome.mood);
            Assert.AreEqual(40, dto.ruler_outcome.loyalty);
            Assert.IsTrue(dto.overridden);
        }

        [Test]
        public void From_SerializesToExpectedJsonShape()
        {
            var record = new DecisionRecord(1, new ResourceAllocation(40, 30, 30), false, 55, 83);
            DecisionSyncRequest dto = DecisionSyncRequestFactory.From(record);

            string json = JsonUtility.ToJson(dto);

            Assert.IsTrue(json.Contains("\"cycle_number\":1"));
            Assert.IsTrue(json.Contains("\"army\":40"));
            Assert.IsTrue(json.Contains("\"overridden\":false"));
        }
    }
}
