using System;

namespace UnderstudyKingdom.Npc
{
    /// <summary>
    /// Plain-C# ruler state (mood/loyalty/agenda) with zero UnityEngine dependency,
    /// so it's directly unit-testable. See
    /// docs/superpowers/specs/2026-09-01-core-decision-cycle-design.md.
    /// </summary>
    [Serializable]
    public class RulerState
    {
        public enum AgendaType
        {
            Expansionist,
            Isolationist,
            Mercantile,
            Pious
        }

        public int Mood = 50;
        public int Loyalty = 50;
        public AgendaType Agenda = AgendaType.Expansionist;

        // True once the one-time council-milestone mood/loyalty reward has
        // been applied to THIS player's ruler -- prevents re-applying it on
        // every subsequent council-panel open. See
        // docs/superpowers/specs/2026-09-03-council-social-design.md.
        public bool CouncilRewardApplied = false;

        // True once the player has dismissed (via Skip or completing all
        // steps) the first-launch onboarding tutorial -- prevents it from
        // showing again on every subsequent launch. See
        // docs/superpowers/specs/2026-09-03-onboarding-tutorial-design.md.
        public bool TutorialCompleted = false;

        public void ApplyDelta(int moodDelta, int loyaltyDelta)
        {
            Mood = Clamp(Mood + moodDelta);
            Loyalty = Clamp(Loyalty + loyaltyDelta);
        }

        private static int Clamp(int value)
        {
            if (value < 0) return 0;
            if (value > 100) return 100;
            return value;
        }
    }
}
