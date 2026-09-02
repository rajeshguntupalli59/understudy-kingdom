using System;

namespace UnderstudyKingdom.Social
{
    /// <summary>
    /// Client for the council (guild) backend endpoints.
    /// See docs/PROJECT_PLAN.md FR-07, FR-08.
    /// </summary>
    public class CouncilService
    {
        /// <summary>
        /// TODO(FR-07): join a council of advisors serving the same or a rival ruler.
        /// Backed by the councils / council_members tables in PROJECT_PLAN.md §6.
        /// </summary>
        public void JoinCouncil(Guid councilId)
        {
            throw new NotImplementedException("FR-07: join council not yet implemented");
        }

        /// <summary>
        /// TODO(FR-08): called when the backend reports a shared council milestone
        /// was reached, to grant the reward to this member.
        /// </summary>
        public void OnMilestoneReached(Guid councilId, object reward)
        {
            throw new NotImplementedException("FR-08: milestone reward not yet implemented");
        }
    }
}
