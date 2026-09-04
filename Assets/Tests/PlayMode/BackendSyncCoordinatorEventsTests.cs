using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.Tests
{
    public class BackendSyncCoordinatorEventsTests
    {
        private GameObject rulerObject;
        private GameObject managerObject;
        private GameObject coordinatorObject;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            rulerObject = new GameObject("Ruler");
            var ruler = rulerObject.AddComponent<RulerNpcController>();

            managerObject = new GameObject("Manager");
            var manager = managerObject.AddComponent<DecisionCycleManager>();
            manager.Ruler = ruler;

            coordinatorObject = new GameObject("Coordinator");
            var coordinator = coordinatorObject.AddComponent<BackendSyncCoordinator>();
            coordinator.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            coordinator.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";
            coordinator.BackendBaseUrl = "http://localhost:3000";
            coordinator.DecisionCycleManager = manager;

            yield return new WaitForSeconds(2f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(coordinatorObject);
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(rulerObject);

            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }
            SessionStore.Clear();
        }

        [UnityTest]
        public IEnumerator RequestActiveEvent_WithReadySession_ReturnsWellFormedResult()
        {
            var coordinator = coordinatorObject.GetComponent<BackendSyncCoordinator>();

            EventResponse result = null;
            string error = null;
            coordinator.RequestActiveEvent(r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.IsFalse(string.IsNullOrEmpty(result.eventId));
            Assert.Greater(result.objectiveDecisionCount, 0);
        }
    }
}
