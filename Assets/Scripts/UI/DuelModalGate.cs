namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Tracks the two independent things that can want the shared
    /// "Challenge a Rival Kingdom" button disabled at once: a duel actually
    /// in flight, and a modal panel (History or Council) currently open.
    /// Nothing else needs this -- the other 6 shared controls only ever have
    /// one thing wanting them disabled (whichever modal is open, since
    /// History and Council already mutually exclude each other), so their
    /// existing direct interactable-toggling logic stays untouched. See
    /// docs/superpowers/specs/2026-09-03-duel-modal-gate-design.md.
    /// </summary>
    public class DuelModalGate
    {
        public bool IsDuelInFlight { get; set; }
        public bool IsModalOpen { get; set; }
    }
}
