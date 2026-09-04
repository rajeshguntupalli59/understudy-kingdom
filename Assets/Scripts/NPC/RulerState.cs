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

        // Id (format "W<isoWeekYear>-<isoWeek>") of the live-ops event whose
        // reward has already been applied to THIS player's ruler -- compared
        // against the CURRENT active event's id so the reward is granted
        // once per real calendar week, even though the rotating event
        // list's content repeats every EVENTS.length weeks. Empty string
        // (never null -- sidesteps JsonUtility's string-null serialization
        // quirks) means "nothing claimed yet." See
        // docs/superpowers/specs/2026-09-03-live-ops-events-design.md.
        public string ClaimedEventWeekId = string.Empty;

        // Id of the currently-selected cosmetic panel-background theme
        // ("Default", "Council", or "Event"). Empty/unrecognized values
        // resolve to Default's color at application time -- see
        // CosmeticsPanelController.GetThemeColor. Never null (same
        // rationale as ClaimedEventWeekId). See
        // docs/superpowers/specs/2026-09-04-cosmetics-customization-design.md.
        public string SelectedTheme = "Default";

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
