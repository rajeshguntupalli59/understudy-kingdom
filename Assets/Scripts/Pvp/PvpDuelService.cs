using System;

namespace UnderstudyKingdom.Pvp
{
    /// <summary>
    /// Client for asynchronous advisor-vs-advisor PvP. Deliberately has no live
    /// matchmaking or bot-facing code -- duels are submitted and resolved server-side
    /// on the player's own schedule. See docs/PROJECT_PLAN.md FR-09 and
    /// docs/COMPETITOR_ANALYSIS.md (this design choice exists specifically to avoid
    /// the matchmaking/bot complaints seen in competitor games).
    /// </summary>
    public class PvpDuelService
    {
        /// <summary>
        /// TODO(FR-09): submit a prepared strategy for the given scenario to be
        /// scored asynchronously against another player's submission. The client
        /// never computes the winner (PROJECT_PLAN.md BL-04) -- this only posts to
        /// the backend and later polls/receives the result.
        /// </summary>
        public void SubmitStrategy(Guid scenarioId, object preparedStrategy)
        {
            throw new NotImplementedException("FR-09: async PvP submission not yet implemented");
        }
    }
}
