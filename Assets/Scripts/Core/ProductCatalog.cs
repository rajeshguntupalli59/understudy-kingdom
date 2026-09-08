namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// Static processed-goods content, mirrors CropCatalog's exact shape.
    /// See docs/superpowers/specs/2026-09-08-estate-phase3-shops-production-design.md.
    /// </summary>
    public readonly struct ProductDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly int SellValue;

        public ProductDefinition(string id, string displayName, int sellValue)
        {
            Id = id;
            DisplayName = displayName;
            SellValue = sellValue;
        }
    }

    public static class ProductCatalog
    {
        public static readonly ProductDefinition[] All =
        {
            new ProductDefinition("bread", "Bread", sellValue: 35),
            new ProductDefinition("carrot_soup", "Carrot Soup", sellValue: 100),
            new ProductDefinition("pumpkin_pie", "Pumpkin Pie", sellValue: 275),
        };

        public static ProductDefinition? Find(string id)
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
