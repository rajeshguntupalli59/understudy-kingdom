using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnderstudyKingdom.Backend;

namespace UnderstudyKingdom.Tests
{
    /// <summary>
    /// Hits the REAL local server/ and the REAL Supabase project -- no mocks.
    /// Submits real decisions via the existing PostDecision, then fetches history
    /// and asserts the real returned entries match what was submitted.
    /// </summary>
    public class BackendApiClientHistoryTests
    {
        private GameObject authObject;
        private GameObject apiClientObject;
        private string accessToken;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            authObject = new GameObject("Auth");
            var auth = authObject.AddComponent<SupabaseAuthClient>();
            auth.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            auth.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";

            apiClientObject = new GameObject("BackendApiClient");
            var apiClient = apiClientObject.AddComponent<BackendApiClient>();
            apiClient.BackendBaseUrl = "http://localhost:3000";

            SessionData session = null;
            auth.SignInAnonymously(s => session = s, err => Assert.Fail($"Sign-in failed: {err}"));
            yield return new WaitUntil(() => session != null);
            accessToken = session.AccessToken;

            bool kingdomReady = false;
            apiClient.EnsureKingdom(accessToken, () => kingdomReady = true, err => Assert.Fail($"EnsureKingdom failed: {err}"));
            yield return new WaitUntil(() => kingdomReady);

            var dto1 = new DecisionSyncRequest
            {
                cycle_number = 1,
                player_recommendation = new PlayerRecommendationDto { army = 70, trade = 15, religion = 15 },
                ruler_outcome = new RulerOutcomeDto { mood = 40, loyalty = 45 },
                overridden = true
            };
            bool decision1Posted = false;
            apiClient.PostDecision(accessToken, dto1, _ => decision1Posted = true, err => Assert.Fail($"PostDecision(1) failed: {err}"));
            yield return new WaitUntil(() => decision1Posted);

            var dto2 = new DecisionSyncRequest
            {
                cycle_number = 2,
                player_recommendation = new PlayerRecommendationDto { army = 40, trade = 30, religion = 30 },
                ruler_outcome = new RulerOutcomeDto { mood = 55, loyalty = 60 },
                overridden = false
            };
            bool decision2Posted = false;
            apiClient.PostDecision(accessToken, dto2, _ => decision2Posted = true, err => Assert.Fail($"PostDecision(2) failed: {err}"));
            yield return new WaitUntil(() => decision2Posted);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(authObject);
            Object.DestroyImmediate(apiClientObject);
        }

        [UnityTest]
        public IEnumerator GetDecisionHistory_WithValidToken_ReturnsRealSubmittedDecisions()
        {
            var apiClient = apiClientObject.GetComponent<BackendApiClient>();

            DecisionHistoryEntry[] result = null;
            string error = null;
            apiClient.GetDecisionHistory(accessToken, 10, r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Length);

            // Newest-first per the server's ORDER BY createdAt DESC.
            Assert.AreEqual(2, result[0].cycleNumber);
            Assert.AreEqual(40, result[0].playerRecommendation.army);
            Assert.AreEqual(55, result[0].rulerOutcome.mood);
            Assert.IsFalse(result[0].overridden);

            Assert.AreEqual(1, result[1].cycleNumber);
            Assert.IsTrue(result[1].overridden);
        }
    }
}
