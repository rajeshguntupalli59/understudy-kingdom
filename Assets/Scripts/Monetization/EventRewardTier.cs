using System;

namespace UnderstudyKingdom.Monetization
{
    /// <summary>
    /// Reward structure for a live-ops event. Enforces, at the type level, that a
    /// free-to-play-completable tier always exists -- premium spend may only unlock
    /// the cosmetic/time-acceleration tier, never gate core progress.
    /// See docs/PROJECT_PLAN.md FR-11 and business rule BL-01.
    /// </summary>
    [Serializable]
    public class EventRewardTier
    {
        /// <summary>Reward reachable by any player using only earned currency. Required.</summary>
        public RewardPayload f2pTier;

        /// <summary>Cosmetic or time-skip reward only. Must never be required to
        /// complete the event's functional objectives (FR-11, BL-01).</summary>
        public RewardPayload premiumTier;

        public EventRewardTier(RewardPayload f2pTier, RewardPayload premiumTier)
        {
            // TODO(BL-01): validate at construction time that f2pTier is non-null,
            // so a live-ops event can never ship without an F2P-completable path.
            this.f2pTier = f2pTier ?? throw new ArgumentNullException(nameof(f2pTier),
                "BL-01: every event must define a non-null F2P reward tier");
            this.premiumTier = premiumTier;
        }
    }

    /// <summary>
    /// TODO: placeholder reward payload shape -- flesh out once the currency/
    /// cosmetic item schema is defined.
    /// </summary>
    [Serializable]
    public class RewardPayload
    {
    }
}
