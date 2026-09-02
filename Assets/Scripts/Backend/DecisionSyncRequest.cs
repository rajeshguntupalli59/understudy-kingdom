using System;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Backend
{
    // snake_case field names are intentional: they must match
    // server/src/routes/decisions.ts's JSON schema exactly since JsonUtility has
    // no attribute-based field renaming.
    [Serializable]
    public class PlayerRecommendationDto
    {
        public int army;
        public int trade;
        public int religion;
    }

    [Serializable]
    public class RulerOutcomeDto
    {
        public int mood;
        public int loyalty;
    }

    [Serializable]
    public class DecisionSyncRequest
    {
        public int cycle_number;
        public PlayerRecommendationDto player_recommendation;
        public RulerOutcomeDto ruler_outcome;
        public bool overridden;
    }

    public static class DecisionSyncRequestFactory
    {
        public static DecisionSyncRequest From(DecisionRecord record)
        {
            return new DecisionSyncRequest
            {
                cycle_number = record.CycleNumber,
                player_recommendation = new PlayerRecommendationDto
                {
                    army = record.Recommendation.Army,
                    trade = record.Recommendation.Trade,
                    religion = record.Recommendation.Religion
                },
                ruler_outcome = new RulerOutcomeDto { mood = record.Mood, loyalty = record.Loyalty },
                overridden = record.Overridden
            };
        }
    }
}
