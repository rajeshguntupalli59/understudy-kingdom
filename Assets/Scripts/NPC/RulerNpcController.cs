using UnityEngine;

namespace UnderstudyKingdom.Npc
{
    /// <summary>
    /// Drives the ruler NPC's behavior via a lightweight utility-AI / behavior-tree
    /// over mood, loyalty, and agenda state. Deliberately NOT a heavy on-device model
    /// -- see docs/NPC_PERFORMANCE_NOTES.md for the memory-budget rationale.
    /// See docs/PROJECT_PLAN.md FR-04.
    /// </summary>
    public class RulerNpcController : MonoBehaviour
    {
        public enum Agenda
        {
            Expansionist,
            Isolationist,
            Mercantile,
            Pious
        }

        [Range(0, 100)]
        public int mood = 50;

        [Range(0, 100)]
        public int loyalty = 50;

        public Agenda agenda;

        /// <summary>
        /// TODO(FR-04): evaluate the behavior tree against current mood/loyalty/agenda
        /// and the incoming recommendation, returning whether the ruler accepts it.
        /// Keep this a small, local utility-AI evaluation -- no network round trip and
        /// no ML inference, per NPC_PERFORMANCE_NOTES.md.
        /// </summary>
        public bool EvaluateRecommendation(object recommendation)
        {
            throw new System.NotImplementedException("FR-04: behavior tree evaluation not yet implemented");
        }

        /// <summary>
        /// TODO: apply a bounded mood/loyalty delta after a decision resolves.
        /// Must never be influenced by purchase history (PROJECT_PLAN.md BL-03).
        /// </summary>
        public void ApplyOutcome(int moodDelta, int loyaltyDelta)
        {
            throw new System.NotImplementedException("mood/loyalty update not yet implemented");
        }
    }
}
