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
    /// End-to-end smoke test: real Supabase anonymous sign-in + real locally-running
    /// server/ (same external dependency as BackendApiClientTests). The coordinator's
    /// design is deliberately fire-and-forget with no observable sync-status (see the
    /// design spec's Scope Decisions), so this test can only confirm the wiring runs
    /// without an unhandled error -- Unity's Test Framework fails a PlayMode test
    /// automatically on any uncaught Debug.LogError, which is the signal this relies on.
    /// </summary>
    public class BackendSyncCoordinatorTests
    {
        private GameObject rulerObject;
        private GameObject managerObject;
        private GameObject coordinatorObject;
        private DecisionCycleManager manager;

        [SetUp]
        public void SetUp()
        {
            rulerObject = new GameObject("Ruler");
            var ruler = rulerObject.AddComponent<RulerNpcController>();

            managerObject = new GameObject("Manager");
            manager = managerObject.AddComponent<DecisionCycleManager>();
            manager.Ruler = ruler;

            coordinatorObject = new GameObject("Coordinator");
            var coordinator = coordinatorObject.AddComponent<BackendSyncCoordinator>();
            coordinator.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            coordinator.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";
            coordinator.BackendBaseUrl = "http://localhost:3000";
            coordinator.DecisionCycleManager = manager;
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
        public IEnumerator SubmitRecommendation_AfterCoordinatorStarts_SyncsWithoutUnhandledErrors()
        {
            // Coordinator.Start() (session bootstrap + EnsureKingdom) runs on the next
            // frame after AddComponent; wait for it before submitting a decision.
            yield return new WaitForSeconds(2f);

            var allocation = new ResourceAllocation(40, 30, 30);
            manager.SubmitRecommendation(allocation, roll: 0.99);

            // PostDecision is fire-and-forget; give the real network round-trip time
            // to complete before the test (and its teardown) ends.
            yield return new WaitForSeconds(2f);
        }
    }
}
