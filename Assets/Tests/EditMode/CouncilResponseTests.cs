using NUnit.Framework;
using UnderstudyKingdom.Backend;
using UnityEngine;

namespace UnderstudyKingdom.Tests
{
    public class CouncilResponseTests
    {
        [Test]
        public void CouncilResponse_DeserializesFromServerResponseShape()
        {
            string json = "{\"id\":\"c1\",\"name\":\"Grinders\",\"joinCode\":\"ABC123\"," +
                "\"memberCount\":3,\"totalDecisions\":7,\"milestoneThreshold\":10," +
                "\"milestoneReached\":false,\"rewardEligible\":false}";

            CouncilResponse response = JsonUtility.FromJson<CouncilResponse>(json);

            Assert.AreEqual("c1", response.id);
            Assert.AreEqual("Grinders", response.name);
            Assert.AreEqual("ABC123", response.joinCode);
            Assert.AreEqual(3, response.memberCount);
            Assert.AreEqual(7, response.totalDecisions);
            Assert.AreEqual(10, response.milestoneThreshold);
            Assert.IsFalse(response.milestoneReached);
            Assert.IsFalse(response.rewardEligible);
        }

        [Test]
        public void CouncilResponse_MilestoneReachedAndRewardEligibleTrue_Deserializes()
        {
            string json = "{\"id\":\"c1\",\"name\":\"Grinders\",\"joinCode\":\"ABC123\"," +
                "\"memberCount\":2,\"totalDecisions\":10,\"milestoneThreshold\":10," +
                "\"milestoneReached\":true,\"rewardEligible\":true}";

            CouncilResponse response = JsonUtility.FromJson<CouncilResponse>(json);

            Assert.IsTrue(response.milestoneReached);
            Assert.IsTrue(response.rewardEligible);
        }

        [Test]
        public void CreateCouncilRequest_SerializesToExpectedWireShape()
        {
            var request = new CreateCouncilRequest { name = "Grinders" };
            string json = JsonUtility.ToJson(request);
            Assert.AreEqual("{\"name\":\"Grinders\"}", json);
        }

        [Test]
        public void JoinCouncilRequest_SerializesToExpectedWireShape()
        {
            var request = new JoinCouncilRequest { joinCode = "ABC123" };
            string json = JsonUtility.ToJson(request);
            Assert.AreEqual("{\"joinCode\":\"ABC123\"}", json);
        }
    }
}
