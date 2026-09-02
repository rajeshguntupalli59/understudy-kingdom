namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// The one recommendation type implemented in this pass (FR-01, scoped to
    /// resource allocation only per docs/superpowers/specs/2026-09-01-core-decision-cycle-design.md).
    /// </summary>
    public readonly struct ResourceAllocation
    {
        public readonly int Army;
        public readonly int Trade;
        public readonly int Religion;

        public ResourceAllocation(int army, int trade, int religion)
        {
            Army = army;
            Trade = trade;
            Religion = religion;
        }

        public bool IsValid()
        {
            return Army >= 0 && Trade >= 0 && Religion >= 0 && (Army + Trade + Religion) == 100;
        }
    }
}
