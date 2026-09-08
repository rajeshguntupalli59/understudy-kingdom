using System.IO;
using NUnit.Framework;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Tests
{
    public class SaveServiceEstateTests
    {
        [TearDown]
        public void Cleanup()
        {
            if (File.Exists(SaveService.EstateSavePath))
            {
                File.Delete(SaveService.EstateSavePath);
            }
        }

        [Test]
        public void LoadEstate_NoSaveFile_ReturnsDefaultState()
        {
            if (File.Exists(SaveService.EstateSavePath))
            {
                File.Delete(SaveService.EstateSavePath);
            }

            var state = SaveService.LoadEstate();

            Assert.AreEqual(200, state.Coins);
            Assert.AreEqual(8, state.Plots.Length);
            Assert.IsTrue(state.Plots[0].Unlocked);
            Assert.IsFalse(state.Plots[4].Unlocked);
        }

        [Test]
        public void SaveEstateThenLoadEstate_RoundTripsCoinsAndPlots()
        {
            var original = new EstateState { Coins = 355 };
            original.Plots[0].CropId = "wheat";
            original.Plots[0].PlantedAtUnixSeconds = 1000;
            original.Plots[0].WateredAtUnixSeconds = 1005;
            original.Plots[4].Unlocked = true;

            SaveService.SaveEstate(original);
            var loaded = SaveService.LoadEstate();

            Assert.AreEqual(355, loaded.Coins);
            Assert.AreEqual("wheat", loaded.Plots[0].CropId);
            Assert.AreEqual(1000, loaded.Plots[0].PlantedAtUnixSeconds);
            Assert.AreEqual(1005, loaded.Plots[0].WateredAtUnixSeconds);
            Assert.IsTrue(loaded.Plots[4].Unlocked);
        }

        [Test]
        public void LoadEstate_CorruptFile_ReturnsDefaultState()
        {
            File.WriteAllText(SaveService.EstateSavePath, "not valid json {{{");

            var state = SaveService.LoadEstate();

            Assert.AreEqual(200, state.Coins);
        }

        [Test]
        public void LoadEstate_WrongPlotCount_ReturnsDefaultState()
        {
            // Simulates a hand-corrupted or foreign-format file -- a Plots
            // array of the wrong length must not be trusted as-is (every
            // caller indexes Plots[0..7] directly).
            var corrupted = new EstateSaveData { Coins = 50, Plots = new LandPlot[3] };
            System.IO.File.WriteAllText(SaveService.EstateSavePath, UnityEngine.JsonUtility.ToJson(corrupted));

            var state = SaveService.LoadEstate();

            Assert.AreEqual(200, state.Coins);
            Assert.AreEqual(8, state.Plots.Length);
        }

        [Test]
        public void SaveService_RulerSaveFile_UnaffectedByEstateSave()
        {
            // The two save files are fully independent -- saving Estate
            // state must never touch ruler_save.json.
            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }

            SaveService.SaveEstate(new EstateState { Coins = 999 });

            Assert.IsFalse(File.Exists(SaveService.SavePath));

            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }
        }

        [Test]
        public void SaveEstateThenLoadEstate_RoundTripsInventoryAndShops()
        {
            var original = new EstateState { Coins = 100 };
            original.Inventory[0] = 3;
            original.Shops[0].Unlocked = true;
            original.Shops[0].ProductionStartedAtUnixSeconds = 555;

            SaveService.SaveEstate(original);
            var loaded = SaveService.LoadEstate();

            Assert.AreEqual(3, loaded.Inventory[0]);
            Assert.IsTrue(loaded.Shops[0].Unlocked);
            Assert.AreEqual(555, loaded.Shops[0].ProductionStartedAtUnixSeconds);
        }

        [Test]
        public void LoadEstate_SaveFileMissingInventoryAndShops_KeepsCoinsAndPlotsDefaultsRest()
        {
            System.IO.File.WriteAllText(SaveService.EstateSavePath,
                "{\"Version\":1,\"Coins\":250,\"Plots\":[" +
                "{\"Unlocked\":true,\"CropId\":null,\"PlantedAtUnixSeconds\":0,\"WateredAtUnixSeconds\":0}," +
                "{\"Unlocked\":true,\"CropId\":null,\"PlantedAtUnixSeconds\":0,\"WateredAtUnixSeconds\":0}," +
                "{\"Unlocked\":true,\"CropId\":null,\"PlantedAtUnixSeconds\":0,\"WateredAtUnixSeconds\":0}," +
                "{\"Unlocked\":true,\"CropId\":null,\"PlantedAtUnixSeconds\":0,\"WateredAtUnixSeconds\":0}," +
                "{\"Unlocked\":false,\"CropId\":null,\"PlantedAtUnixSeconds\":0,\"WateredAtUnixSeconds\":0}," +
                "{\"Unlocked\":false,\"CropId\":null,\"PlantedAtUnixSeconds\":0,\"WateredAtUnixSeconds\":0}," +
                "{\"Unlocked\":false,\"CropId\":null,\"PlantedAtUnixSeconds\":0,\"WateredAtUnixSeconds\":0}," +
                "{\"Unlocked\":false,\"CropId\":null,\"PlantedAtUnixSeconds\":0,\"WateredAtUnixSeconds\":0}]}");

            var loaded = SaveService.LoadEstate();

            Assert.AreEqual(250, loaded.Coins);
            Assert.IsTrue(loaded.Plots[0].Unlocked);
            Assert.IsFalse(loaded.Plots[4].Unlocked);
            Assert.AreEqual(GoodsCatalog.Count, loaded.Inventory.Length);
            Assert.AreEqual(0, loaded.Inventory[0]);
            Assert.AreEqual(ShopCatalog.All.Length, loaded.Shops.Length);
            Assert.IsFalse(loaded.Shops[0].Unlocked);
        }

        [Test]
        public void LoadEstate_WrongInventoryLength_DefaultsOnlyInventory()
        {
            var corrupted = new EstateSaveData
            {
                Coins = 50,
                Plots = new EstateState().Plots,
                Inventory = new int[2],
                Shops = new EstateState().Shops
            };
            System.IO.File.WriteAllText(SaveService.EstateSavePath, UnityEngine.JsonUtility.ToJson(corrupted));

            var loaded = SaveService.LoadEstate();

            Assert.AreEqual(50, loaded.Coins);
            Assert.AreEqual(GoodsCatalog.Count, loaded.Inventory.Length);
        }
    }
}
