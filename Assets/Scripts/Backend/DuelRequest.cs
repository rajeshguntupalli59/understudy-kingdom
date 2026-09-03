using System;

namespace UnderstudyKingdom.Backend
{
    // Reuses PlayerRecommendationDto from DecisionSyncRequest.cs -- same
    // shape, same wire field names, no need for a duplicate type.
    [Serializable]
    public class DuelRequest
    {
        public PlayerRecommendationDto recommendation;
    }

    [Serializable]
    public class RulerSnapshotDto
    {
        public int mood;
        public int loyalty;
        public string agenda;
    }

    [Serializable]
    public class DuelResult
    {
        public bool overridden;
        public RulerSnapshotDto defenderRulerSnapshot;
    }
}
