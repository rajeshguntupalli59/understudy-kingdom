using UnityEngine;

namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Tracks the two independent things that can want the shared
    /// "Challenge a Rival Kingdom" button disabled at once: a duel actually
    /// in flight, and a modal panel (History or Council) currently open.
    /// Nothing else needs this -- the other 6 shared controls only ever have
    /// one thing wanting them disabled (whichever modal is open, since
    /// History and Council already mutually exclude each other), so their
    /// existing direct interactable-toggling logic stays untouched.
    ///
    /// A MonoBehaviour, not a plain C# class -- [SerializeField] on a plain
    /// class Unity doesn't know how to serialize silently drops the
    /// reference (fails as null at runtime, only visible once the real
    /// built scene is loaded and clicked, not in any test that calls
    /// Initialize() directly). Adding [System.Serializable] instead would
    /// remove the null but serialize BY VALUE, giving each controller its
    /// own private copy and silently reintroducing the exact bug this
    /// milestone exists to fix. A MonoBehaviour is a real UnityEngine.Object
    /// Unity can serialize a single shared reference to, matching every
    /// other Initialize()-injected dependency in this project. See
    /// docs/superpowers/specs/2026-09-03-duel-modal-gate-design.md.
    /// </summary>
    public class DuelModalGate : MonoBehaviour
    {
        public bool IsDuelInFlight { get; set; }
        public bool IsModalOpen { get; set; }
    }
}
