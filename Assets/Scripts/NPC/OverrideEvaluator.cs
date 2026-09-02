using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Npc
{
    public readonly struct OverrideResult
    {
        public readonly bool Overridden;
        public readonly int MoodDelta;
        public readonly int LoyaltyDelta;

        public OverrideResult(bool overridden, int moodDelta, int loyaltyDelta)
        {
            Overridden = overridden;
            MoodDelta = moodDelta;
            LoyaltyDelta = loyaltyDelta;
        }
    }

    /// <summary>
    /// Weighted utility-AI override decision (design approach A in
    /// docs/superpowers/specs/2026-09-02-ruler-ai-depth-design.md,
    /// superseding the milestone #1 rule table). Pure function -- no
    /// UnityEngine dependency, no side effects. The caller supplies `roll`
    /// (a [0,1) random value) so this is deterministic and testable;
    /// DecisionCycleManager passes UnityEngine.Random.value at the real
    /// call site.
    /// </summary>
    public static class OverrideEvaluator
    {
        private const int Neutral = 50;
        private const double Baseline = 0.10;
        private const double LoyaltyWeight = 0.012;
        private const double MoodWeight = 0.005;
        private const double AgendaMisalignedBump = 0.25;
        private const double MinProbability = 0.02;
        private const double MaxProbability = 0.95;

        private const int AcceptedMoodDelta = 5;
        private const int AcceptedLoyaltyDelta = 3;
        private const int OverriddenMoodDelta = -10;
        private const int OverriddenLoyaltyDelta = -5;

        public static bool IsAligned(RulerState.AgendaType agenda, ResourceAllocation allocation)
        {
            switch (agenda)
            {
                case RulerState.AgendaType.Expansionist: return allocation.Army >= 40;
                case RulerState.AgendaType.Isolationist: return allocation.Army <= 20;
                case RulerState.AgendaType.Mercantile: return allocation.Trade >= 40;
                case RulerState.AgendaType.Pious: return allocation.Religion >= 40;
                default: return true;
            }
        }

        /// <summary>
        /// Weighted-sum utility score: Baseline plus a contribution each from
        /// loyalty, mood, and agenda-alignment (each measured relative to its
        /// neutral midpoint), clamped to [MinProbability, MaxProbability].
        /// LoyaltyWeight > MoodWeight so loyalty stays the dominant factor.
        /// </summary>
        public static double OverrideProbability(RulerState state, ResourceAllocation allocation)
        {
            double probability = Baseline
                + (Neutral - state.Loyalty) * LoyaltyWeight
                + (Neutral - state.Mood) * MoodWeight
                + (IsAligned(state.Agenda, allocation) ? 0.0 : AgendaMisalignedBump);

            return Clamp(probability, MinProbability, MaxProbability);
        }

        public static OverrideResult Evaluate(RulerState state, ResourceAllocation allocation, double roll)
        {
            bool overridden = roll < OverrideProbability(state, allocation);

            return overridden
                ? new OverrideResult(true, OverriddenMoodDelta, OverriddenLoyaltyDelta)
                : new OverrideResult(false, AcceptedMoodDelta, AcceptedLoyaltyDelta);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
