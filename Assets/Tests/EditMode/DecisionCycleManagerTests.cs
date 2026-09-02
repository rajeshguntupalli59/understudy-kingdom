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

            string narration = manager.SubmitRecommendation(allocation, roll: 0.99); // high loyalty + neutral mood -> low probability (clamped), no override

            Assert.IsTrue(narration.Contains("wise allocation"));
            Assert.AreEqual(55, ruler.State.Mood);
            Assert.AreEqual(83, ruler.State.Loyalty);

            var reloaded = SaveService.Load();
            Assert.AreEqual(55, reloaded.Mood);
        }

        [Test]
        public void SubmitRecommendation_Overridden_ReturnsOverrideNarration()
        {
            ruler.State.Loyalty = 10; // low loyalty alone -> probability 0.58, comfortably above roll 0.50

            var allocation = new ResourceAllocation(20, 60, 20);
            string narration = manager.SubmitRecommendation(allocation, roll: 0.50);

            Assert.IsTrue(narration.Contains("other plans"));
            Assert.AreEqual(40, ruler.State.Mood);
        }

        [Test]
        public void Awake_WithRulerAlreadyAssigned_LoadsPersistedState()
        {
            // Pre-populate a save file with known non-default values.
            var savedState = new RulerState { Mood = 66, Loyalty = 44, Agenda = RulerState.AgendaType.Isolationist };
            SaveService.Save(savedState);

            // Build inactive so AddComponent does NOT run Awake() yet.
            var freshRulerObject = new GameObject("FreshRuler");
            freshRulerObject.SetActive(false);
            var freshRuler = freshRulerObject.AddComponent<RulerNpcController>();

            var freshManagerObject = new GameObject("FreshManager");
            freshManagerObject.SetActive(false);
            var freshManager = freshManagerObject.AddComponent<DecisionCycleManager>();
            freshManager.Ruler = freshRuler;

            freshManagerObject.SetActive(true);

            // EditMode tests run outside Play Mode, where Unity does not invoke Awake()
            // for plain MonoBehaviours -- exercise the load logic directly instead.
            freshManager.LoadPersistedStateIfPresent();

            try
            {
                Assert.AreEqual(66, freshRuler.State.Mood);
                Assert.AreEqual(44, freshRuler.State.Loyalty);
                Assert.AreEqual(RulerState.AgendaType.Isolationist, freshRuler.State.Agenda);
            }
            finally
            {
                Object.DestroyImmediate(freshManagerObject);
                Object.DestroyImmediate(freshRulerObject);
            }
        }

        [Test]
        public void Awake_NoSaveFile_PreservesAuthoredState()
        {
            // No SaveService.Save() call -- simulates first launch, no save file exists.
            if (System.IO.File.Exists(SaveService.SavePath))
            {
                System.IO.File.Delete(SaveService.SavePath);
            }

            var authoredRulerObject = new GameObject("AuthoredRuler");
            authoredRulerObject.SetActive(false);
            var authoredRuler = authoredRulerObject.AddComponent<RulerNpcController>();
            authoredRuler.State = new RulerState { Mood = 70, Loyalty = 60, Agenda = RulerState.AgendaType.Pious };

            var authoredManagerObject = new GameObject("AuthoredManager");
            authoredManagerObject.SetActive(false);
            var authoredManager = authoredManagerObject.AddComponent<DecisionCycleManager>();
            authoredManager.Ruler = authoredRuler;

            authoredManagerObject.SetActive(true); // triggers Awake()

            try
            {
                Assert.AreEqual(70, authoredRuler.State.Mood);
                Assert.AreEqual(60, authoredRuler.State.Loyalty);
                Assert.AreEqual(RulerState.AgendaType.Pious, authoredRuler.State.Agenda);
            }
            finally
            {
                Object.DestroyImmediate(authoredManagerObject);
                Object.DestroyImmediate(authoredRulerObject);
            }
        }

        [Test]
        public void SubmitRecommendation_FiresOnDecisionRecorded_WithMatchingData()
        {
            DecisionRecord? captured = null;
            manager.OnDecisionRecorded += record => captured = record;

            var allocation = new ResourceAllocation(20, 60, 20); // aligned with Mercantile
            manager.SubmitRecommendation(allocation, roll: 0.99); // low probability (clamped), no override

            Assert.IsTrue(captured.HasValue);
            Assert.AreEqual(1, captured.Value.CycleNumber);
            Assert.AreEqual(20, captured.Value.Recommendation.Army);
            Assert.AreEqual(60, captured.Value.Recommendation.Trade);
            Assert.AreEqual(20, captured.Value.Recommendation.Religion);
            Assert.IsFalse(captured.Value.Overridden);
            Assert.AreEqual(55, captured.Value.Mood);
            Assert.AreEqual(83, captured.Value.Loyalty);
        }
    }
}
