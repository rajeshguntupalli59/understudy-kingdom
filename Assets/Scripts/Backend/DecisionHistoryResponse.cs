using System;

namespace UnderstudyKingdom.Backend
{
    // Reuses PlayerRecommendationDto/RulerOutcomeDto from DecisionSyncRequest.cs --
    // the server stores those jsonb blobs verbatim as originally sent by
    // DecisionSyncRequestFactory, so the nested shape is identical here.
    [Serializable]
    public class DecisionHistoryEntry
    {
        public int cycleNumber;
        public PlayerRecommendationDto playerRecommendation;
        public RulerOutcomeDto rulerOutcome;
        public bool overridden;
    }

    [Serializable]
    public class DecisionHistoryResponse
    {
        public DecisionHistoryEntry[] decisions;
    }
}
