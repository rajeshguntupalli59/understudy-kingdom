using System;

namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// Plain-C# land-plot state, zero UnityEngine dependency beyond Math,
    /// directly unit-testable -- same shape/role as RulerState. See
    /// docs/superpowers/specs/2026-09-07-estate-phase1-land-crops-design.md.
    /// </summary>
    [Serializable]
    public class LandPlot
    {
        public bool Unlocked;
        public string CropId;
        public long PlantedAtUnixSeconds;
        public long WateredAtUnixSeconds;
    }

    [Serializable]
    public class EstateState
    {
        public const int PlotCount = 8;
        public const int StartingUnlockedPlots = 4;

        public int Coins = 200;
        public LandPlot[] Plots;

        public EstateState()
        {
            Plots = new LandPlot[PlotCount];
            for (int i = 0; i < Plots.Length; i++)
            {
                Plots[i] = new LandPlot { Unlocked = i < StartingUnlockedPlots };
            }
        }

        /// <summary>
        /// 0 (sprout) while never watered -- growth never starts until the
        /// player waters the plot, no timer runs and nothing decays while
        /// waiting. Once watered, 1 (growing) at half GrowDurationSeconds
        /// elapsed, 2 (mature) at full GrowDurationSeconds elapsed, clamped
        /// at 2 past that (no further stages).
        /// </summary>
        public static int GrowthStage(LandPlot plot, CropDefinition crop, long nowUnixSeconds)
        {
            if (plot.WateredAtUnixSeconds == 0)
            {
                return 0;
            }

            long elapsed = nowUnixSeconds - plot.WateredAtUnixSeconds;
            long halfDuration = crop.GrowDurationSeconds / 2;
            if (halfDuration <= 0)
            {
                halfDuration = 1;
            }

            long stage = elapsed / halfDuration;
            if (stage > 2) return 2;
            if (stage < 0) return 0;
            return (int)stage;
        }

        /// <summary>
        /// Geometric cost curve for locked plots (index 4-7), rounded to
        /// the nearest 10 and always shown before purchase -- see the
        /// design doc's "opaque expansion cost" differentiation note.
        /// </summary>
        public static int UnlockCost(int plotIndex)
        {
            int lockedIndex = plotIndex - StartingUnlockedPlots;
            double raw = 100.0 * Math.Pow(1.6, lockedIndex);
            return (int)(Math.Round(raw / 10.0) * 10.0);
        }
    }
}
