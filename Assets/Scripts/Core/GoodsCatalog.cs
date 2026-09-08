namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// Unifies CropCatalog (raw goods) and ProductCatalog (processed goods)
    /// into one flat inventory index space -- crops first (0..CropCatalog.All.Length-1),
    /// products after. Lets EstateState.Inventory be a single fixed-length
    /// int[] instead of a Dictionary (JsonUtility can't serialize those). See
    /// docs/superpowers/specs/2026-09-08-estate-phase3-shops-production-design.md.
    /// </summary>
    public static class GoodsCatalog
    {
        public static readonly int Count = CropCatalog.All.Length + ProductCatalog.All.Length;

        public static int IndexOf(string goodsId)
        {
            int cropIndex = CropCatalog.IndexOf(goodsId);
            if (cropIndex >= 0)
            {
                return cropIndex;
            }
            int productIndex = ProductCatalog.IndexOf(goodsId);
            return productIndex >= 0 ? CropCatalog.All.Length + productIndex : -1;
        }

        public static string DisplayName(string goodsId)
        {
            CropDefinition? crop = CropCatalog.Find(goodsId);
            if (crop != null)
            {
                return crop.Value.DisplayName;
            }
            ProductDefinition? product = ProductCatalog.Find(goodsId);
            return product?.DisplayName;
        }

        public static int SellValue(string goodsId)
        {
            CropDefinition? crop = CropCatalog.Find(goodsId);
            if (crop != null)
            {
                return crop.Value.SellValue;
            }
            ProductDefinition? product = ProductCatalog.Find(goodsId);
            return product?.SellValue ?? 0;
        }
    }
}
