using UnityEngine;

namespace UnderstudyKingdom.Npc
{
    /// <summary>
    /// Drives the ruler NPC's behavior via a lightweight utility-AI / behavior-tree
    /// over mood, loyalty, and agenda state, held in RulerState. Deliberately NOT a
    /// heavy on-device model -- see docs/NPC_PERFORMANCE_NOTES.md.
    /// See docs/PROJECT_PLAN.md FR-04.
    /// </summary>
    public class RulerNpcController : MonoBehaviour
    {
        public RulerState State = new RulerState();
    }
}
