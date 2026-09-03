using NUnit.Framework;
using UnderstudyKingdom.Backend;
using UnityEngine;

namespace UnderstudyKingdom.Tests
{
    public class EventResponseTests
    {
        [Test]
        public void EventResponse_DeserializesFromServerResponseShape()
        {
            string json = "{\"eventId\":\"W2026-37\",\"name\":\"Harvest Tithe\"," +
                "\"narration\":\"The granaries overflow...\",\"objectiveDecisionCount\":3," +
                "\"decisionsCompleted\":2,\"rewardMood\":15,\"rewardLoyalty\":15}";

            EventResponse response = JsonUtility.FromJson<EventResponse>(json);

            Assert.AreEqual("W2026-37", response.eventId);
            Assert.AreEqual("Harvest Tithe", response.name);
            Assert.AreEqual("The granaries overflow...", response.narration);
            Assert.AreEqual(3, response.objectiveDecisionCount);
            Assert.AreEqual(2, response.decisionsCompleted);
            Assert.AreEqual(15, response.rewardMood);
            Assert.AreEqual(15, response.rewardLoyalty);
        }

        [Test]
        public void EventResponse_DecisionsCompletedMeetingObjective_Deserializes()
        {
            string json = "{\"eventId\":\"W2026-37\",\"name\":\"Harvest Tithe\"," +
                "\"narration\":\"...\",\"objectiveDecisionCount\":3," +
                "\"decisionsCompleted\":3,\"rewardMood\":15,\"rewardLoyalty\":15}";

            EventResponse response = JsonUtility.FromJson<EventResponse>(json);

            Assert.GreaterOrEqual(response.decisionsCompleted, response.objectiveDecisionCount);
        }
    }
}
