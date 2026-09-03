using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.Tests
{
    public class BackendSyncCoordinatorHistoryTests
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
        public IEnumerator RequestHistory_WithReadySession_ReturnsWellFormedResult()
        {
            var coordinator = coordinatorObject.GetComponent<BackendSyncCoordinator>();

            DecisionHistoryEntry[] result = null;
            string error = null;
            coordinator.RequestHistory(10, r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.IsNotNull(result);
            // A brand-new kingdom (just created by EnsureKingdom during bootstrap)
            // has no decisions yet -- an empty array is the correct, well-formed result.
            Assert.AreEqual(0, result.Length);
        }

        /// <summary>
        /// Same technique as BackendSyncCoordinatorDuelTests's
        /// RequestDuel_CalledBeforeKingdomReady_ExercisesRetryPathAndSettles (see that
        /// test's own doc comment for the full rationale) -- proves RequestHistory's
        /// separate implementation of the same refresh-then-kingdomReady ordering
        /// doesn't reintroduce the bug that was found and fixed in RequestDuel.
        /// </summary>
        [UnityTest]
        public IEnumerator RequestHistory_CalledBeforeKingdomReady_ExercisesRetryPathAndSettles()
        {
            var freshCoordinatorObject = new GameObject("FreshCoordinator");
            try
            {
                var coordinator = freshCoordinatorObject.AddComponent<BackendSyncCoordinator>();
                coordinator.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
                coordinator.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";
                coordinator.BackendBaseUrl = "http://localhost:3000";

                FieldInfo sessionField = typeof(BackendSyncCoordinator).GetField("currentSession", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo kingdomReadyField = typeof(BackendSyncCoordinator).GetField("kingdomReady", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(sessionField, "currentSession field not found -- BackendSyncCoordinator internals changed");
                Assert.IsNotNull(kingdomReadyField, "kingdomReady field not found -- BackendSyncCoordinator internals changed");

                float deadline = Time.realtimeSinceStartup + 10f;
                while (sessionField.GetValue(coordinator) == null && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                bool sessionWasReady = sessionField.GetValue(coordinator) != null;
                bool observedRetryWindow = sessionWasReady && !(bool)kingdomReadyField.GetValue(coordinator);

                DecisionHistoryEntry[] result = null;
                string error = null;
                coordinator.RequestHistory(10, r => result = r, err => error = err);

                yield return new WaitUntil(() => result != null || error != null);

                Assert.IsTrue(result != null || error != null, "RequestHistory never settled");

                if (observedRetryWindow)
                {
                    Assert.IsNull(error, $"Expected the kingdomReady retry path to succeed, got error: {error}");
                    Assert.IsNotNull(result);
                }
                else if (!sessionWasReady)
                {
                    Assert.AreEqual("No session available yet -- try again in a moment.", error);
                }
            }
            finally
            {
                Object.DestroyImmediate(freshCoordinatorObject);
            }
        }
    }
}
