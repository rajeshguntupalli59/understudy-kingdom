namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// Neutral data produced by DecisionCycleManager.OnDecisionRecorded. Lives in
    /// Core (not Backend) so DecisionCycleManager has zero dependency on networking
    /// code -- see docs/superpowers/specs/2026-09-02-client-backend-integration-design.md.
    /// </summary>
    public readonly struct DecisionRecord
    {
        public readonly int CycleNumber;
        public readonly ResourceAllocation Recommendation;
        public readonly bool Overridden;
        public readonly int Mood;
        public readonly int Loyalty;

        public DecisionRecord(int cycleNumber, ResourceAllocation recommendation,
            bool overridden, int mood, int loyalty)
        {
            CycleNumber = cycleNumber;
            Recommendation = recommendation;
            Overridden = overridden;
            Mood = mood;
            Loyalty = loyalty;
        }
    }
}
