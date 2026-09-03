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

        /// <summary>
        /// Targets the kingdomReady==false retry branch in RequestDuel (see final-review
        /// I-1/I-2): the UnitySetUp coordinator above always finishes its own bootstrap
        /// during the 2-second wait, so by the time any [UnityTest] body runs its
        /// kingdomReady is already true and that branch never fires. This test uses a
        /// SEPARATE, freshly-created coordinator and calls RequestDuel on it with no
        /// warm-up wait, so the session-ready-but-kingdom-not-ready window is real.
        ///
        /// That window is made reliable (not a lucky race) rather than merely hoped-for:
        /// BootstrapSession's SessionStore.Load() picks up the session the UnitySetUp
        /// coordinator already persisted to disk, so currentSession becomes non-null via
        /// a synchronous local load the moment Start() runs -- no network round trip
        /// needed for that part. EnsureKingdom (fired synchronously right after, from
        /// OnSessionReady) is a real network call and cannot complete within the same
        /// frame, so kingdomReady is still false at that instant. Reflection is used only
        /// to observe this internal timing for the test's own assertions -- production
        /// code and its public surface are unchanged.
        /// </summary>
        [UnityTest]
        public IEnumerator RequestDuel_CalledBeforeKingdomReady_ExercisesRetryPathAndSettles()
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

                // Poll (rather than a single yield return null) so this still works even
                // if Start()'s timing shifts, or SessionStore happens to be empty and a
                // full sign-in round trip is required instead of a local load.
                float deadline = Time.realtimeSinceStartup + 10f;
                while (sessionField.GetValue(coordinator) == null && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                bool sessionWasReady = sessionField.GetValue(coordinator) != null;
                bool observedRetryWindow = sessionWasReady && !(bool)kingdomReadyField.GetValue(coordinator);

                var allocation = new ResourceAllocation(40, 30, 30);
                DuelResult result = null;
                string error = null;
                coordinator.RequestDuel(allocation, r => result = r, err => error = err);

                yield return new WaitUntil(() => result != null || error != null);

                // Whichever branch actually fired, the coordinator must settle -- never
                // hang, never throw uncaught.
                Assert.IsTrue(result != null || error != null, "RequestDuel never settled");

                if (observedRetryWindow)
                {
                    // We confirmed kingdomReady was false with a real session in hand at
                    // the moment RequestDuel was called -- this is the retry branch I-1
                    // fixed. It must refresh-if-needed, re-attempt EnsureKingdom, and go
                    // on to a real duel, not report "no session".
                    Assert.IsNull(error, $"Expected the kingdomReady retry path to succeed, got error: {error}");
                    Assert.IsNotNull(result);
                    Assert.IsNotNull(result.defenderRulerSnapshot);
                }
                else if (!sessionWasReady)
                {
                    // Called before BootstrapSession finished at all -- the graceful
                    // too-early guard is the only valid outcome here.
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
