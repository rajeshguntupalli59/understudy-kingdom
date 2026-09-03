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
    public class BackendSyncCoordinatorCouncilTests
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
        public IEnumerator RequestCreateCouncil_WithReadySession_ReturnsWellFormedResult()
        {
            var coordinator = coordinatorObject.GetComponent<BackendSyncCoordinator>();

            CouncilResponse result = null;
            string error = null;
            coordinator.RequestCreateCouncil("Grinders", r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.AreEqual("Grinders", result.name);
            Assert.AreEqual(1, result.memberCount);
        }

        [UnityTest]
        public IEnumerator RequestCouncilStatus_WithNoCouncilYet_ReturnsNotInACouncilError()
        {
            var coordinator = coordinatorObject.GetComponent<BackendSyncCoordinator>();

            CouncilResponse result = null;
            string error = null;
            coordinator.RequestCouncilStatus(r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(result);
            Assert.AreEqual("Not in a council", error);
        }

        [UnityTest]
        public IEnumerator RequestJoinCouncil_WithUnknownCode_ReturnsRealServerError()
        {
            var coordinator = coordinatorObject.GetComponent<BackendSyncCoordinator>();

            CouncilResponse result = null;
            string error = null;
            coordinator.RequestJoinCouncil("ZZZZZZ", r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(result);
            Assert.AreEqual("No council found for that code", error);
        }
    }
}
