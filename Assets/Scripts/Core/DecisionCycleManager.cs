using System;
using UnityEngine;

namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// Drives the core prep -> ruler-decision loop for a single kingdom.
    /// See docs/PROJECT_PLAN.md FR-01, FR-02, FR-03.
    /// </summary>
    public class DecisionCycleManager : MonoBehaviour
    {
        [SerializeField]
        private int currentCycleNumber;

        /// <summary>
        /// Submits the player's recommendation for the current decision cycle.
        /// TODO(FR-01): accept a structured recommendation (resource allocation,
        /// army move, or diplomatic choice) and hand it to the ruler NPC for resolution.
        /// </summary>
        public void SubmitRecommendation(object recommendation)
        {
            throw new NotImplementedException("FR-01: recommendation submission not yet implemented");
        }

        /// <summary>
        /// Resolves the current cycle: asks the ruler NPC whether it accepts or
        /// overrides the player's recommendation, then narrates the outcome.
        /// TODO(FR-02): probability of override must be weighted by ruler mood/loyalty
        /// (see RulerNpcController), never by purchase history (PROJECT_PLAN.md BL-03).
        /// TODO(FR-03): persist the resulting mood/loyalty/trust delta.
        /// </summary>
        public void ResolveCycle()
        {
            throw new NotImplementedException("FR-02/FR-03: cycle resolution not yet implemented");
        }
    }
}
