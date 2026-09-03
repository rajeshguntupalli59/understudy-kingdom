using NUnit.Framework;
using UnderstudyKingdom.Backend;
using UnityEngine;

namespace UnderstudyKingdom.Tests
{
    public class DecisionHistoryResponseTests
    {
        [Test]
        public void DecisionHistoryResponse_DeserializesFromServerResponseShape()
        {
            string json = "{\"decisions\":[" +
                "{\"cycleNumber\":2,\"playerRecommendation\":{\"army\":40,\"trade\":30,\"religion\":30}," +
                "\"rulerOutcome\":{\"mood\":55,\"loyalty\":60},\"overridden\":false}," +
                "{\"cycleNumber\":1,\"playerRecommendation\":{\"army\":70,\"trade\":15,\"religion\":15}," +
                "\"rulerOutcome\":{\"mood\":40,\"loyalty\":45},\"overridden\":true}" +
                "],\"nextCursor\":null}";

            DecisionHistoryResponse response = JsonUtility.FromJson<DecisionHistoryResponse>(json);

            Assert.IsNotNull(response.decisions);
            Assert.AreEqual(2, response.decisions.Length);

            Assert.AreEqual(2, response.decisions[0].cycleNumber);
            Assert.AreEqual(40, response.decisions[0].playerRecommendation.army);
            Assert.AreEqual(55, response.decisions[0].rulerOutcome.mood);
            Assert.IsFalse(response.decisions[0].overridden);

            Assert.AreEqual(1, response.decisions[1].cycleNumber);
            Assert.IsTrue(response.decisions[1].overridden);
        }

        [Test]
        public void DecisionHistoryResponse_EmptyDecisionsArray_DeserializesToZeroLengthArray()
        {
            string json = "{\"decisions\":[],\"nextCursor\":null}";

            DecisionHistoryResponse response = JsonUtility.FromJson<DecisionHistoryResponse>(json);

            Assert.IsNotNull(response.decisions);
            Assert.AreEqual(0, response.decisions.Length);
        }
    }
}
