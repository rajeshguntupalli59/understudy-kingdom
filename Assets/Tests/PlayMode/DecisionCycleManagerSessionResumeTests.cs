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
    /// Real end-to-end regression test for C-1 (milestone #10's final whole-branch
    /// review): DecisionCycleManager.currentCycleNumber is pure in-memory state
    /// that resets to 0 on every relaunch, but the player's Supabase session (and
    /// therefore their kingdom/decisions) persists server-side -- without the
    /// SeedCycleNumberIfHigher fix, a returning player's first post-relaunch
    /// submission collides with a cycle_number already recorded server-side and is
    /// silently dropped (server/src/routes/decisions.ts's onConflictDoNothing).
    ///
    /// This simulates that exact restart boundary: session 1 posts real decisions
    /// under a real persisted Supabase session, its BackendSyncCoordinator is
    /// destroyed WITHOUT clearing the session (exactly like an app relaunch, which
    /// reuses the same persisted session file), and a session-2 coordinator+manager
    /// pair is constructed pointed at that same persisted session. If the fix
    /// works, session 2's cycle counter is seeded above the highest server-known
    /// cycle and its first submission does not collide.
    /// </summary>
    public class DecisionCycleManagerSessionResumeTests
    {
        private const string SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
        private const string SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";
        private const string BackendBaseUrl = "http://localhost:3000";
        private const int LastPostedCycle = 3;

        private GameObject coordinator1Object;
        private GameObject directApiClientObject;
        private GameObject ruler2Object;
        private GameObject manager2Object;
        private GameObject coordinator2Object;
        private DecisionCycleManager manager2;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            // --- Session 1: bootstrap a real session and post real decisions. ---
            coordinator1Object = new GameObject("Coordinator1");
            var coordinator1 = coordinator1Object.AddComponent<BackendSyncCoordinator>();
            coordinator1.SupabaseUrl = SupabaseUrl;
            coordinator1.SupabaseAnonKey = SupabaseAnonKey;
            coordinator1.BackendBaseUrl = BackendBaseUrl;

            yield return new WaitForSeconds(2f);

            SessionData session1 = SessionStore.Load();
            Assert.IsNotNull(session1, "Coordinator1 did not persist a session during bootstrap");

            directApiClientObject = new GameObject("DirectApiClient");
            var directApiClient = directApiClientObject.AddComponent<BackendApiClient>();
            directApiClient.BackendBaseUrl = BackendBaseUrl;

            for (int cycle = 1; cycle <= LastPostedCycle; cycle++)
            {
                var dto = new DecisionSyncRequest
                {
                    cycle_number = cycle,
                    player_recommendation = new PlayerRecommendationDto { army = 40, trade = 30, religion = 30 },
                    ruler_outcome = new RulerOutcomeDto { mood = 55, loyalty = 60 },
                    overridden = false
                };
                bool posted = false;
                directApiClient.PostDecision(session1.AccessToken, dto, _ => posted = true, err => Assert.Fail($"PostDecision failed: {err}"));
                yield return new WaitUntil(() => posted);
            }

            // --- Simulate an app relaunch: destroy the first coordinator WITHOUT
            // clearing the persisted session, exactly like closing and reopening
            // the app would (SessionStore.Save wrote the file to disk; it survives). ---
            Object.DestroyImmediate(coordinator1Object);
            coordinator1Object = null;

            // --- Session 2: a fresh coordinator+manager pair pointed at the SAME
            // persisted session. BootstrapSession will find the stored, unexpired
            // session and reuse it (no fresh sign-in), then OnSessionReady's
            // EnsureKingdom->GetDecisionHistory chain seeds the cycle counter. ---
            ruler2Object = new GameObject("Ruler2");
            var ruler2 = ruler2Object.AddComponent<RulerNpcController>();

            manager2Object = new GameObject("Manager2");
            manager2 = manager2Object.AddComponent<DecisionCycleManager>();
            manager2.Ruler = ruler2;

            coordinator2Object = new GameObject("Coordinator2");
            var coordinator2 = coordinator2Object.AddComponent<BackendSyncCoordinator>();
            coordinator2.SupabaseUrl = SupabaseUrl;
            coordinator2.SupabaseAnonKey = SupabaseAnonKey;
            coordinator2.BackendBaseUrl = BackendBaseUrl;
            coordinator2.DecisionCycleManager = manager2;

            // Reusing a persisted session skips sign-in, but OnSessionReady's
            // EnsureKingdom->GetDecisionHistory seed chain is still two sequential
            // real network round trips -- give it more headroom than session 1's
            // sign-in-only bootstrap.
            yield return new WaitForSeconds(3f);
        }

        [TearDown]
        public void TearDown()
        {
            if (coordinator1Object != null) Object.DestroyImmediate(coordinator1Object);
            Object.DestroyImmediate(coordinator2Object);
            Object.DestroyImmediate(manager2Object);
            Object.DestroyImmediate(ruler2Object);
            Object.DestroyImmediate(directApiClientObject);

            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }
            SessionStore.Clear();
        }

        [UnityTest]
        public IEnumerator SecondSession_AfterResume_SeedsCycleAboveServerKnownCycles()
        {
            DecisionRecord? captured = null;
            manager2.OnDecisionRecorded += record => captured = record;

            var allocation = new ResourceAllocation(20, 60, 20);
            manager2.SubmitRecommendation(allocation, roll: 0.99);

            Assert.IsTrue(captured.HasValue);

            // Proves no collision: without the fix, session 2's currentCycleNumber
            // starts at 0 again and this submission would be cycle_number 1, which
            // the server already has recorded for this kingdom (dropped via
            // onConflictDoNothing). With the fix, the seed fetch raises the local
            // counter to LastPostedCycle (3) before this submission, so it lands as
            // cycle 4 -- strictly above every cycle posted in session 1.
            Assert.Greater(captured.Value.CycleNumber, LastPostedCycle,
                "Cycle number after resume must be above every cycle already recorded server-side in session 1");
            Assert.AreEqual(LastPostedCycle + 1, captured.Value.CycleNumber);

            yield break;
        }
    }
}
