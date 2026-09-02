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

        /// <summary>
        /// Proves persisted sessions survive an app restart: a second coordinator,
        /// standing in for a second app launch, must reuse the same identity that the
        /// first coordinator's bootstrap persisted to disk -- not sign in as a new
        /// anonymous user. Also exercises EnsureKingdom's 200-already-exists path,
        /// since the second coordinator's kingdom already exists from the first.
        /// </summary>
        [UnityTest]
        public IEnumerator SecondCoordinator_WithPersistedSession_ReusesSameIdentity()
        {
            yield return new WaitForSeconds(2f);

            SessionData firstSession = SessionStore.Load();
            Assert.IsNotNull(firstSession, "First coordinator should have persisted a session after bootstrap.");
            string firstUserId = firstSession.UserId;
            Assert.IsFalse(string.IsNullOrEmpty(firstUserId));

            // Simulate the app closing: tear down the first coordinator's GameObject,
            // but deliberately do NOT clear the session file -- that's the whole point.
            Object.DestroyImmediate(coordinatorObject);
            coordinatorObject = null; // avoid a double-destroy in the class TearDown below

            var secondRulerObject = new GameObject("Ruler2");
            var secondRuler = secondRulerObject.AddComponent<RulerNpcController>();

            var secondManagerObject = new GameObject("Manager2");
            var secondManager = secondManagerObject.AddComponent<DecisionCycleManager>();
            secondManager.Ruler = secondRuler;

            var secondCoordinatorObject = new GameObject("Coordinator2");
            var secondCoordinator = secondCoordinatorObject.AddComponent<BackendSyncCoordinator>();
            secondCoordinator.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            secondCoordinator.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";
            secondCoordinator.BackendBaseUrl = "http://localhost:3000";
            secondCoordinator.DecisionCycleManager = secondManager;

            try
            {
                yield return new WaitForSeconds(2f);

                SessionData secondSession = SessionStore.Load();
                Assert.IsNotNull(secondSession, "Second coordinator should have a session after bootstrap.");
                Assert.AreEqual(firstUserId, secondSession.UserId,
                    "Second coordinator should have reused the persisted session's identity, not signed in fresh.");
            }
            finally
            {
                Object.DestroyImmediate(secondCoordinatorObject);
                Object.DestroyImmediate(secondManagerObject);
                Object.DestroyImmediate(secondRulerObject);
                SessionStore.Clear();
            }
        }
    }
}
