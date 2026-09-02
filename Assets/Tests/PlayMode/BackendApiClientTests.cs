using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Tests
{
    /// <summary>
    /// Hits a REAL locally-running server/ instance (npm run dev in server/, listening
    /// on http://localhost:3000) authenticated with a REAL Supabase-issued anonymous
    /// token -- no mocks, matching this project's established testing culture. Requires
    /// server/ to be running locally before this test suite executes.
    /// </summary>
    public class BackendApiClientTests
    {
        private GameObject authClientObject;
        private GameObject apiClientObject;
        private SupabaseAuthClient authClient;
        private BackendApiClient apiClient;
        private string accessToken;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            authClientObject = new GameObject("SupabaseAuthClient");
            authClient = authClientObject.AddComponent<SupabaseAuthClient>();
            authClient.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            authClient.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";

            apiClientObject = new GameObject("BackendApiClient");
            apiClient = apiClientObject.AddComponent<BackendApiClient>();
            apiClient.BackendBaseUrl = "http://localhost:3000";

            SessionData session = null;
            authClient.SignInAnonymously(s => session = s, err => Assert.Fail($"Sign-in failed: {err}"));
            yield return new WaitUntil(() => session != null);
            accessToken = session.AccessToken;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(authClientObject);
            Object.DestroyImmediate(apiClientObject);
        }

        [UnityTest]
        public IEnumerator EnsureKingdom_WithValidToken_Succeeds()
        {
            bool succeeded = false;
            string error = null;

            apiClient.EnsureKingdom(accessToken, () => succeeded = true, err => error = err);
            yield return new WaitUntil(() => succeeded || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.IsTrue(succeeded);
        }

        [UnityTest]
        public IEnumerator PostDecision_AfterEnsureKingdom_Succeeds()
        {
            bool kingdomReady = false;
            apiClient.EnsureKingdom(accessToken, () => kingdomReady = true, err => Assert.Fail($"EnsureKingdom failed: {err}"));
            yield return new WaitUntil(() => kingdomReady);

            // Each test run signs in as a brand-new anonymous user (new kingdom), so
            // this cycle_number never actually collides with a prior run -- but
            // PostDecision treats 409 as success regardless (see Step 3 below), so
            // this assertion holds either way.
            var record = new DecisionRecord(
                cycleNumber: 1,
                recommendation: new ResourceAllocation(40, 30, 30),
                overridden: false,
                mood: 55,
                loyalty: 60);
            var dto = DecisionSyncRequestFactory.From(record);

            bool succeeded = false;
            string error = null;
            apiClient.PostDecision(accessToken, dto, _ => succeeded = true, err => error = err);
            yield return new WaitUntil(() => succeeded || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.IsTrue(succeeded);
        }

        [UnityTest]
        public IEnumerator PostDecision_DuplicateCycleNumber_StillReportsSuccess()
        {
            bool kingdomReady = false;
            apiClient.EnsureKingdom(accessToken, () => kingdomReady = true, err => Assert.Fail($"EnsureKingdom failed: {err}"));
            yield return new WaitUntil(() => kingdomReady);

            var record = new DecisionRecord(2, new ResourceAllocation(40, 30, 30), false, 55, 60);
            var dto = DecisionSyncRequestFactory.From(record);

            bool firstSucceeded = false;
            bool firstWasAlreadyRecorded = true; // starts wrong so the assertion below actually proves it flipped
            apiClient.PostDecision(accessToken, dto, wasAlreadyRecorded =>
            {
                firstSucceeded = true;
                firstWasAlreadyRecorded = wasAlreadyRecorded;
            }, err => Assert.Fail($"First post failed: {err}"));
            yield return new WaitUntil(() => firstSucceeded);

            bool secondSucceeded = false;
            bool secondWasAlreadyRecorded = false;
            string secondError = null;
            apiClient.PostDecision(accessToken, dto, wasAlreadyRecorded =>
            {
                secondSucceeded = true;
                secondWasAlreadyRecorded = wasAlreadyRecorded;
            }, err => secondError = err);
            yield return new WaitUntil(() => secondSucceeded || secondError != null);

            Assert.IsNull(secondError, $"Expected 409 to be treated as success, got error: {secondError}");
            Assert.IsTrue(secondSucceeded);
            Assert.IsFalse(firstWasAlreadyRecorded, "First post should have created the decision (2xx), not collided.");
            Assert.IsTrue(secondWasAlreadyRecorded, "Second post should have collided with the first (409, already recorded).");
        }
    }
}
