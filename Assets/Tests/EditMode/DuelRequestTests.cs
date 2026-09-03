using NUnit.Framework;
using UnderstudyKingdom.Backend;
using UnityEngine;

namespace UnderstudyKingdom.Tests
{
    public class DuelRequestTests
    {
        [Test]
        public void DuelRequest_SerializesToExpectedJsonShape()
        {
            var request = new DuelRequest
            {
                recommendation = new PlayerRecommendationDto { army = 40, trade = 30, religion = 30 }
            };

            string json = JsonUtility.ToJson(request);

            Assert.IsTrue(json.Contains("\"army\":40"));
            Assert.IsTrue(json.Contains("\"trade\":30"));
            Assert.IsTrue(json.Contains("\"religion\":30"));
        }

        [Test]
        public void DuelResult_DeserializesFromServerResponseShape()
        {
            string json = "{\"overridden\":false,\"defenderRulerSnapshot\":{\"mood\":50,\"loyalty\":50,\"agenda\":\"Expansionist\"}}";

            DuelResult result = JsonUtility.FromJson<DuelResult>(json);

            Assert.IsFalse(result.overridden);
            Assert.AreEqual(50, result.defenderRulerSnapshot.mood);
            Assert.AreEqual(50, result.defenderRulerSnapshot.loyalty);
            Assert.AreEqual("Expansionist", result.defenderRulerSnapshot.agenda);
        }
    }
}
