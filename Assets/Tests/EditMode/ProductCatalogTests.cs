using NUnit.Framework;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Tests
{
    public class ProductCatalogTests
    {
        [Test]
        public void All_HasExactlyThreeProducts()
        {
            Assert.AreEqual(3, ProductCatalog.All.Length);
        }

        [Test]
        public void All_EveryProductHasPositiveSellValue()
        {
            foreach (ProductDefinition product in ProductCatalog.All)
            {
                Assert.Greater(product.SellValue, 0, $"{product.Id} SellValue");
                Assert.IsFalse(string.IsNullOrEmpty(product.DisplayName), $"{product.Id} DisplayName");
            }
        }

        [Test]
        public void All_ExactValues_MatchDesignSpec()
        {
            Assert.AreEqual(35, ProductCatalog.Find("bread").Value.SellValue);
            Assert.AreEqual(100, ProductCatalog.Find("carrot_soup").Value.SellValue);
            Assert.AreEqual(275, ProductCatalog.Find("pumpkin_pie").Value.SellValue);
        }

        [Test]
        public void Find_UnknownId_ReturnsNull()
        {
            Assert.IsNull(ProductCatalog.Find("does-not-exist"));
        }

        [Test]
        public void IndexOf_KnownIds_ReturnsCatalogOrder()
        {
            Assert.AreEqual(0, ProductCatalog.IndexOf("bread"));
            Assert.AreEqual(1, ProductCatalog.IndexOf("carrot_soup"));
            Assert.AreEqual(2, ProductCatalog.IndexOf("pumpkin_pie"));
        }

        [Test]
        public void IndexOf_UnknownId_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, ProductCatalog.IndexOf("does-not-exist"));
        }
    }
}
