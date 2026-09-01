using NUnit.Framework;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;
using UnityEngine;

namespace UnderstudyKingdom.Tests
{
    public class DecisionCycleManagerTests
    {
        private GameObject rulerObject;
        private GameObject managerObject;

        [SetUp]
        public void SetUp()
        {
            rulerObject = new GameObject("Ruler");
            var ruler = rulerObject.AddComponent<RulerNpcController>();
            ruler.State = new RulerState { Mood = 50, Loyalty = 80, Agenda = RulerState.AgendaType.Mercantile };

            managerObject = new GameObject("Manager");
            var manager = managerObject.AddComponent<DecisionCycleManager>();
            manager.Ruler = ruler;

            this.manager = manager;
            this.ruler = ruler;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(rulerObject);

            if (System.IO.File.Exists(SaveService.SavePath))
            {
                System.IO.File.Delete(SaveService.SavePath);
            }
        }

        private DecisionCycleManager manager;
        private RulerNpcController ruler;

        [Test]
        public void SubmitRecommendation_Accepted_ReturnsAcceptNarrationAndSaves()
        {
            var allocation = new ResourceAllocation(20, 60, 20); // aligned with Mercantile

            string narration = manager.SubmitRecommendation(allocation, roll: 0.99); // baseline 0.10, no override

            Assert.IsTrue(narration.Contains("wise allocation"));
            Assert.AreEqual(55, ruler.State.Mood);
            Assert.AreEqual(83, ruler.State.Loyalty);

            var reloaded = SaveService.Load();
            Assert.AreEqual(55, reloaded.Mood);
        }

        [Test]
        public void SubmitRecommendation_Overridden_ReturnsOverrideNarration()
        {
            ruler.State.Loyalty = 10; // forces near-certain override

            var allocation = new ResourceAllocation(20, 60, 20);
            string narration = manager.SubmitRecommendation(allocation, roll: 0.50);

            Assert.IsTrue(narration.Contains("other plans"));
            Assert.AreEqual(40, ruler.State.Mood);
        }
    }
}
