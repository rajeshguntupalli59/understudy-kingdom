using System;

namespace UnderstudyKingdom.Backend
{
    // Response shape for GET /api/v1/events/active -- field names and
    // types must match server/src/routes/events.ts's JSON response
    // exactly. See
    // docs/superpowers/specs/2026-09-03-live-ops-events-design.md.
    [Serializable]
    public class EventResponse
    {
        public string eventId;
        public string name;
        public string narration;
        public int objectiveDecisionCount;
        public int decisionsCompleted;
        public int rewardMood;
        public int rewardLoyalty;
    }
}
