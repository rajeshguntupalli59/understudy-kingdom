using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnderstudyKingdom.Backend;

namespace UnderstudyKingdom.Tests
{
    /// <summary>
    /// Hits the REAL local server/ and REAL Supabase project, mirroring
    /// BackendApiClientCouncilTests's structure.
    /// </summary>
    public class BackendApiClientEventsTests
    {
        private GameObject apiClientObject;
        private BackendApiClient apiClient;
        private string jwt;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            apiClientObject = new GameObject("ApiClient");
            apiClient = apiClientObject.AddComponent<BackendApiClient>();
            apiClient.BackendBaseUrl = "http://localhost:3000";

            var authObject = new GameObject("Auth");
            var auth = authObject.AddComponent<SupabaseAuthClient>();
            auth.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            auth.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";

            SessionData session = null;
            auth.SignInAnonymously(s => session = s, err => Assert.Fail($"Sign-in failed: {err}"));
            yield return new WaitUntil(() => session != null);
            jwt = session.AccessToken;

            Object.DestroyImmediate(authObject);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(apiClientObject);
        }

        [UnityTest]
        public IEnumerator GetActiveEvent_WithNoKingdomYet_ReturnsNoKingdomError()
        {
            EventResponse result = null;
            string error = null;
            apiClient.GetActiveEvent(jwt, r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(result);
            Assert.AreEqual("No kingdom found for this user", error);
        }

        [UnityTest]
        public IEnumerator GetActiveEvent_AfterKingdomCreated_ReturnsWellFormedResult()
        {
            bool kingdomReady = false;
            apiClient.EnsureKingdom(jwt, () => kingdomReady = true, err => Assert.Fail($"EnsureKingdom failed: {err}"));
            yield return new WaitUntil(() => kingdomReady);

            EventResponse result = null;
            string error = null;
            apiClient.GetActiveEvent(jwt, r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.IsFalse(string.IsNullOrEmpty(result.eventId));
            Assert.IsFalse(string.IsNullOrEmpty(result.name));
            Assert.Greater(result.objectiveDecisionCount, 0);
            Assert.Greater(result.rewardMood, 0);
            Assert.Greater(result.rewardLoyalty, 0);
        }
    }
}
