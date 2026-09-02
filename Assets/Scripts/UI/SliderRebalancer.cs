namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Pure rebalance math for the three-slider (Army/Trade/Religion) allocation
    /// input, kept free of UnityEngine dependencies so it's directly unit-testable.
    /// See docs/superpowers/specs/2026-09-01-core-loop-vertical-slice-design.md.
    /// </summary>
    public static class SliderRebalancer
    {
        /// <summary>
        /// Given three values that summed to 100, and one of them (at
        /// changedIndex) being set to newValue, returns all three values
        /// re-adjusted so they sum to exactly 100 again. The two values NOT at
        /// changedIndex absorb the remainder in proportion to their current
        /// relative weight; if both are zero, the remainder is split evenly
        /// (with any odd remainder unit going to the second of the two).
        /// newValue is clamped to [0, 100] before rebalancing.
        /// </summary>
        public static (int, int, int) Rebalance(int a, int b, int c, int changedIndex, int newValue)
        {
            int[] values = { a, b, c };
            newValue = Clamp(newValue, 0, 100);
            int remainder = 100 - newValue;

            int otherIndex1 = (changedIndex + 1) % 3;
            int otherIndex2 = (changedIndex + 2) % 3;

            int other1 = values[otherIndex1];
            int other2 = values[otherIndex2];
            int otherSum = other1 + other2;

            int newOther1;
            int newOther2;

            if (otherSum <= 0)
            {
                newOther1 = remainder / 2;
                newOther2 = remainder - newOther1;
            }
            else
            {
                newOther1 = (int)System.Math.Round(remainder * (other1 / (double)otherSum));
                newOther2 = remainder - newOther1;
            }

            values[changedIndex] = newValue;
            values[otherIndex1] = newOther1;
            values[otherIndex2] = newOther2;

            return (values[0], values[1], values[2]);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
