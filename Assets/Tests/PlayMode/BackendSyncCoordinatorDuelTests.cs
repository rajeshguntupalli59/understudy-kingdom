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
    /// <summary>
    /// Hits the REAL local server/ and the REAL Supabase project. A separate
    /// defender opponent is created directly (not through a second
    /// BackendSyncCoordinator) so the endpoint always has someone to duel.
    /// </summary>
    public class BackendSyncCoordinatorDuelTests
    {
        private GameObject rulerObject;
        private GameObject managerObject;
        private GameObject coordinatorObject;
        private GameObject defenderAuthObject;
        private GameObject defenderApiObject;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            defenderAuthObject = new GameObject("DefenderAuth");
            var defenderAuth = defenderAuthObject.AddComponent<SupabaseAuthClient>();
            defenderAuth.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            defenderAuth.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";

            defenderApiObject = new GameObject("DefenderApi");
            var defenderApi = defenderApiObject.AddComponent<BackendApiClient>();
            defenderApi.BackendBaseUrl = "http://localhost:3000";

            SessionData defenderSession = null;
            defenderAuth.SignInAnonymously(s => defenderSession = s, err => Assert.Fail($"Defender sign-in failed: {err}"));
            yield return new WaitUntil(() => defenderSession != null);

            bool defenderReady = false;
            defenderApi.EnsureKingdom(defenderSession.AccessToken, () => defenderReady = true, err => Assert.Fail($"Defender EnsureKingdom failed: {err}"));
            yield return new WaitUntil(() => defenderReady);

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

            // Wait for the coordinator's own session bootstrap (Start()) to complete.
            yield return new WaitForSeconds(2f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(coordinatorObject);
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(rulerObject);
            Object.DestroyImmediate(defenderApiObject);
            Object.DestroyImmediate(defenderAuthObject);

            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }
            SessionStore.Clear();
        }

        [UnityTest]
        public IEnumerator RequestDuel_WithReadySession_ReturnsWellFormedResult()
        {
            var coordinator = coordinatorObject.GetComponent<BackendSyncCoordinator>();
            var allocation = new ResourceAllocation(40, 30, 30);

            DuelResult result = null;
            string error = null;
            coordinator.RequestDuel(allocation, r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.defenderRulerSnapshot);
        }
    }
}
