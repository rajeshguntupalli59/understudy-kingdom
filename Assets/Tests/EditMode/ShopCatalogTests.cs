using NUnit.Framework;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Tests
{
    public class ShopCatalogTests
    {
        [Test]
        public void All_HasExactlyThreeShops()
        {
            Assert.AreEqual(3, ShopCatalog.All.Length);
        }

        [Test]
        public void All_EveryShopHasPositiveValues()
        {
            foreach (ShopDefinition shop in ShopCatalog.All)
            {
                Assert.Greater(shop.UnlockCost, 0, $"{shop.Id} UnlockCost");
                Assert.Greater(shop.StartCost, 0, $"{shop.Id} StartCost");
                Assert.Greater(shop.ProductionSeconds, 0, $"{shop.Id} ProductionSeconds");
                Assert.IsFalse(string.IsNullOrEmpty(shop.InputGoodsId), $"{shop.Id} InputGoodsId");
                Assert.IsFalse(string.IsNullOrEmpty(shop.OutputGoodsId), $"{shop.Id} OutputGoodsId");
            }
        }

        [Test]
        public void All_ExactValues_MatchDesignSpec()
        {
            ShopDefinition bakery = ShopCatalog.Find("bakery").Value;
            Assert.AreEqual(150, bakery.UnlockCost);
            Assert.AreEqual("wheat", bakery.InputGoodsId);
            Assert.AreEqual("bread", bakery.OutputGoodsId);
            Assert.AreEqual(3, bakery.StartCost);
            Assert.AreEqual(60, bakery.ProductionSeconds);

            ShopDefinition kitchen = ShopCatalog.Find("kitchen").Value;
            Assert.AreEqual(300, kitchen.UnlockCost);
            Assert.AreEqual("carrot", kitchen.InputGoodsId);
            Assert.AreEqual("carrot_soup", kitchen.OutputGoodsId);
            Assert.AreEqual(8, kitchen.StartCost);
            Assert.AreEqual(180, kitchen.ProductionSeconds);

            ShopDefinition pieHouse = ShopCatalog.Find("pie_house").Value;
            Assert.AreEqual(600, pieHouse.UnlockCost);
            Assert.AreEqual("pumpkin", pieHouse.InputGoodsId);
            Assert.AreEqual("pumpkin_pie", pieHouse.OutputGoodsId);
            Assert.AreEqual(20, pieHouse.StartCost);
            Assert.AreEqual(420, pieHouse.ProductionSeconds);
        }

        [Test]
        public void Find_UnknownId_ReturnsNull()
        {
            Assert.IsNull(ShopCatalog.Find("does-not-exist"));
        }

        [Test]
        public void IndexOf_KnownIds_ReturnsCatalogOrder()
        {
            Assert.AreEqual(0, ShopCatalog.IndexOf("bakery"));
            Assert.AreEqual(1, ShopCatalog.IndexOf("kitchen"));
            Assert.AreEqual(2, ShopCatalog.IndexOf("pie_house"));
        }

        [Test]
        public void IndexOf_UnknownId_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, ShopCatalog.IndexOf("does-not-exist"));
        }
    }
}
