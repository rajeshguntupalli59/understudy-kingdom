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
    /// Rule-based override decision table (design approach B in
    /// docs/superpowers/specs/2026-09-01-core-decision-cycle-design.md).
    /// Pure function -- no UnityEngine dependency, no side effects. The caller
    /// supplies `roll` (a [0,1) random value) so this is deterministic and
    /// testable; DecisionCycleManager passes UnityEngine.Random.value at the
    /// real call site.
    /// </summary>
    public static class OverrideEvaluator
    {
        private const int LoyaltyOverrideThreshold = 20;
        private const double LoyaltyOverrideProbability = 0.95;
        private const double MisalignedOverrideProbability = 0.30;
        private const double BaselineOverrideProbability = 0.10;

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

        public static double OverrideProbability(RulerState state, ResourceAllocation allocation)
        {
            if (state.Loyalty < LoyaltyOverrideThreshold)
            {
                return LoyaltyOverrideProbability;
            }

            return IsAligned(state.Agenda, allocation)
                ? BaselineOverrideProbability
                : MisalignedOverrideProbability;
        }

        public static OverrideResult Evaluate(RulerState state, ResourceAllocation allocation, double roll)
        {
            bool overridden = roll < OverrideProbability(state, allocation);

            return overridden
                ? new OverrideResult(true, OverriddenMoodDelta, OverriddenLoyaltyDelta)
                : new OverrideResult(false, AcceptedMoodDelta, AcceptedLoyaltyDelta);
        }
    }
}
