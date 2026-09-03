using UnderstudyKingdom.Backend;

namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Formats one decision history entry into a single display line. Kept
    /// separate from DialogueTemplateEngine -- that engine's templates are
    /// single-purpose flavor narration for the moment a decision resolves, not a
    /// data-dense summary row. See
    /// docs/superpowers/specs/2026-09-02-decision-history-design.md.
    /// </summary>
    public static class HistoryRowFormatter
    {
        public static string Format(DecisionHistoryEntry entry)
        {
            string outcome = entry.overridden ? "Overridden" : "Accepted";
            return $"Cycle {entry.cycleNumber}: Army {entry.playerRecommendation.army} / " +
                   $"Trade {entry.playerRecommendation.trade} / Religion {entry.playerRecommendation.religion} " +
                   $"-> {outcome} (Mood {entry.rulerOutcome.mood}, Loyalty {entry.rulerOutcome.loyalty})";
        }
    }
}
