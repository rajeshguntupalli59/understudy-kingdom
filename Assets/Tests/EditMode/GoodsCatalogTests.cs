using NUnit.Framework;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Tests
{
    public class GoodsCatalogTests
    {
        [Test]
        public void Count_IsCropsPlusProducts()
        {
            Assert.AreEqual(6, GoodsCatalog.Count);
        }

        [Test]
        public void IndexOf_CropIds_ResolveToZeroThroughTwo()
        {
            Assert.AreEqual(0, GoodsCatalog.IndexOf("wheat"));
            Assert.AreEqual(1, GoodsCatalog.IndexOf("carrot"));
            Assert.AreEqual(2, GoodsCatalog.IndexOf("pumpkin"));
        }

        [Test]
        public void IndexOf_ProductIds_ResolveToThreeThroughFive()
        {
            Assert.AreEqual(3, GoodsCatalog.IndexOf("bread"));
            Assert.AreEqual(4, GoodsCatalog.IndexOf("carrot_soup"));
            Assert.AreEqual(5, GoodsCatalog.IndexOf("pumpkin_pie"));
        }

        [Test]
        public void IndexOf_UnknownId_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, GoodsCatalog.IndexOf("does-not-exist"));
        }

        [Test]
        public void DisplayNameAndSellValue_ResolveForCropId()
        {
            Assert.AreEqual("Wheat", GoodsCatalog.DisplayName("wheat"));
            Assert.AreEqual(12, GoodsCatalog.SellValue("wheat"));
        }

        [Test]
        public void DisplayNameAndSellValue_ResolveForProductId()
        {
            Assert.AreEqual("Bread", GoodsCatalog.DisplayName("bread"));
            Assert.AreEqual(35, GoodsCatalog.SellValue("bread"));
        }
    }
}
