using NUnit.Framework;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Tests
{
    public class CropCatalogTests
    {
        [Test]
        public void All_HasExactlyThreeCrops()
        {
            Assert.AreEqual(3, CropCatalog.All.Length);
        }

        [Test]
        public void All_EveryCropHasPositiveCostDurationAndValue()
        {
            foreach (CropDefinition crop in CropCatalog.All)
            {
                Assert.Greater(crop.SeedCost, 0, $"{crop.Id} SeedCost");
                Assert.Greater(crop.GrowDurationSeconds, 0, $"{crop.Id} GrowDurationSeconds");
                Assert.Greater(crop.SellValue, 0, $"{crop.Id} SellValue");
                Assert.IsFalse(string.IsNullOrEmpty(crop.DisplayName), $"{crop.Id} DisplayName");
            }
        }

        [Test]
        public void All_ExactValues_MatchDesignSpec()
        {
            CropDefinition wheat = CropCatalog.Find("wheat").Value;
            Assert.AreEqual(5, wheat.SeedCost);
            Assert.AreEqual(30, wheat.GrowDurationSeconds);
            Assert.AreEqual(12, wheat.SellValue);

            CropDefinition carrot = CropCatalog.Find("carrot").Value;
            Assert.AreEqual(15, carrot.SeedCost);
            Assert.AreEqual(120, carrot.GrowDurationSeconds);
            Assert.AreEqual(40, carrot.SellValue);

            CropDefinition pumpkin = CropCatalog.Find("pumpkin").Value;
            Assert.AreEqual(40, pumpkin.SeedCost);
            Assert.AreEqual(300, pumpkin.GrowDurationSeconds);
            Assert.AreEqual(110, pumpkin.SellValue);
        }

        [Test]
        public void Find_UnknownId_ReturnsNull()
        {
            Assert.IsNull(CropCatalog.Find("does-not-exist"));
        }

        [Test]
        public void IndexOf_KnownIds_ReturnsCatalogOrder()
        {
            Assert.AreEqual(0, CropCatalog.IndexOf("wheat"));
            Assert.AreEqual(1, CropCatalog.IndexOf("carrot"));
            Assert.AreEqual(2, CropCatalog.IndexOf("pumpkin"));
        }

        [Test]
        public void IndexOf_UnknownId_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, CropCatalog.IndexOf("does-not-exist"));
        }
    }
}
