using System.IO;
using NUnit.Framework;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.Tests
{
    public class SaveServiceTests
    {
        [TearDown]
        public void Cleanup()
        {
            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }
        }

        [Test]
        public void Load_NoSaveFile_ReturnsDefaultState()
        {
            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }

            var state = SaveService.Load();

            Assert.AreEqual(50, state.Mood);
            Assert.AreEqual(50, state.Loyalty);
        }

        [Test]
        public void SaveThenLoad_RoundTripsState()
        {
            var original = new RulerState { Mood = 70, Loyalty = 30, Agenda = RulerState.AgendaType.Isolationist };

            SaveService.Save(original);
            var loaded = SaveService.Load();

            Assert.AreEqual(70, loaded.Mood);
            Assert.AreEqual(30, loaded.Loyalty);
            Assert.AreEqual(RulerState.AgendaType.Isolationist, loaded.Agenda);
        }

        [Test]
        public void SaveThenLoad_RoundTripsCouncilRewardApplied()
        {
            var original = new RulerState { Mood = 60, Loyalty = 60, Agenda = RulerState.AgendaType.Mercantile, CouncilRewardApplied = true };

            SaveService.Save(original);
            var loaded = SaveService.Load();

            Assert.IsTrue(loaded.CouncilRewardApplied);
        }

        [Test]
        public void Load_NoSaveFile_CouncilRewardAppliedDefaultsFalse()
        {
            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }

            var state = SaveService.Load();

            Assert.IsFalse(state.CouncilRewardApplied);
        }

        [Test]
        public void Load_CorruptFile_ReturnsDefaultState()
        {
            File.WriteAllText(SaveService.SavePath, "not valid json {{{");

            var state = SaveService.Load();

            Assert.AreEqual(50, state.Mood);
            Assert.AreEqual(50, state.Loyalty);
        }

        [Test]
        public void Load_OutOfRangeValues_ClampsMoodAndLoyalty()
        {
            var corrupted = new RulerSaveData { Mood = 500, Loyalty = -50, Agenda = 0 };
            System.IO.File.WriteAllText(SaveService.SavePath, UnityEngine.JsonUtility.ToJson(corrupted));

            var state = SaveService.Load();

            Assert.AreEqual(100, state.Mood);
            Assert.AreEqual(0, state.Loyalty);
        }

        [Test]
        public void Load_OutOfRangeAgenda_FallsBackToExpansionist()
        {
            var corrupted = new RulerSaveData { Mood = 50, Loyalty = 50, Agenda = 99 };
            System.IO.File.WriteAllText(SaveService.SavePath, UnityEngine.JsonUtility.ToJson(corrupted));

            var state = SaveService.Load();

            Assert.AreEqual(RulerState.AgendaType.Expansionist, state.Agenda);
        }
    }
}
