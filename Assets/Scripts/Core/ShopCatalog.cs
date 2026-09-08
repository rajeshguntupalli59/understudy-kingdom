namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// Static production-building content. InputGoodsId/OutputGoodsId are
    /// plain strings resolved at runtime via GoodsCatalog -- no compile-time
    /// dependency on CropCatalog/ProductCatalog, same reasoning
    /// LandPlot.CropId already uses. See
    /// docs/superpowers/specs/2026-09-08-estate-phase3-shops-production-design.md.
    /// </summary>
    public readonly struct ShopDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly int UnlockCost;
        public readonly string InputGoodsId;
        public readonly string OutputGoodsId;
        public readonly int StartCost;
        public readonly int ProductionSeconds;

        public ShopDefinition(string id, string displayName, int unlockCost, string inputGoodsId, string outputGoodsId, int startCost, int productionSeconds)
        {
            Id = id;
            DisplayName = displayName;
            UnlockCost = unlockCost;
            InputGoodsId = inputGoodsId;
            OutputGoodsId = outputGoodsId;
            StartCost = startCost;
            ProductionSeconds = productionSeconds;
        }
    }

    public static class ShopCatalog
    {
        public static readonly ShopDefinition[] All =
        {
            new ShopDefinition("bakery", "Bakery", unlockCost: 150, inputGoodsId: "wheat", outputGoodsId: "bread", startCost: 3, productionSeconds: 60),
            new ShopDefinition("kitchen", "Kitchen", unlockCost: 300, inputGoodsId: "carrot", outputGoodsId: "carrot_soup", startCost: 8, productionSeconds: 180),
            new ShopDefinition("pie_house", "Pie House", unlockCost: 600, inputGoodsId: "pumpkin", outputGoodsId: "pumpkin_pie", startCost: 20, productionSeconds: 420),
        };

        public static ShopDefinition? Find(string id)
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
