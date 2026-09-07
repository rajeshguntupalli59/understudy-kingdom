namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// Static crop content, not save data -- same "static catalog vs. save
    /// data" split the ruler dialogue templates already use. See
    /// docs/superpowers/specs/2026-09-07-estate-phase1-land-crops-design.md.
    /// </summary>
    public readonly struct CropDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly int SeedCost;
        public readonly int GrowDurationSeconds;
        public readonly int SellValue;

        public CropDefinition(string id, string displayName, int seedCost, int growDurationSeconds, int sellValue)
        {
            Id = id;
            DisplayName = displayName;
            SeedCost = seedCost;
            GrowDurationSeconds = growDurationSeconds;
            SellValue = sellValue;
        }
    }

    public static class CropCatalog
    {
        public static readonly CropDefinition[] All =
        {
            new CropDefinition("wheat", "Wheat", seedCost: 5, growDurationSeconds: 30, sellValue: 12),
            new CropDefinition("carrot", "Carrot", seedCost: 15, growDurationSeconds: 120, sellValue: 40),
            new CropDefinition("pumpkin", "Pumpkin", seedCost: 40, growDurationSeconds: 300, sellValue: 110),
        };

        public static CropDefinition? Find(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return All[i];
                }
            }
            return null;
        }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
