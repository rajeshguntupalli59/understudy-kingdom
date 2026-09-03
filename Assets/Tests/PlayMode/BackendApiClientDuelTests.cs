using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnderstudyKingdom.Backend;

namespace UnderstudyKingdom.Tests
{
    /// <summary>
    /// Hits the REAL local server/ (npm run dev, port 3000) and the REAL Supabase
    /// project -- no mocks, matching this project's established testing culture.
    /// Two real anonymous users are created (challenger + defender) so the
    /// endpoint always has a real opponent to resolve against.
    /// </summary>
    public class BackendApiClientDuelTests
    {
        private GameObject challengerAuthObject;
        private GameObject defenderAuthObject;
        private GameObject apiClientObject;
        private string challengerToken;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            challengerAuthObject = new GameObject("ChallengerAuth");
            var challengerAuth = challengerAuthObject.AddComponent<SupabaseAuthClient>();
            challengerAuth.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            challengerAuth.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";

            defenderAuthObject = new GameObject("DefenderAuth");
            var defenderAuth = defenderAuthObject.AddComponent<SupabaseAuthClient>();
            defenderAuth.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            defenderAuth.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";

            apiClientObject = new GameObject("BackendApiClient");
            var apiClient = apiClientObject.AddComponent<BackendApiClient>();
            apiClient.BackendBaseUrl = "http://localhost:3000";

            SessionData challengerSession = null;
            challengerAuth.SignInAnonymously(s => challengerSession = s, err => Assert.Fail($"Challenger sign-in failed: {err}"));
            yield return new WaitUntil(() => challengerSession != null);
            challengerToken = challengerSession.AccessToken;

            SessionData defenderSession = null;
            defenderAuth.SignInAnonymously(s => defenderSession = s, err => Assert.Fail($"Defender sign-in failed: {err}"));
            yield return new WaitUntil(() => defenderSession != null);

            bool challengerKingdomReady = false;
            apiClient.EnsureKingdom(challengerToken, () => challengerKingdomReady = true, err => Assert.Fail($"Challenger EnsureKingdom failed: {err}"));
            yield return new WaitUntil(() => challengerKingdomReady);

            bool defenderKingdomReady = false;
            apiClient.EnsureKingdom(defenderSession.AccessToken, () => defenderKingdomReady = true, err => Assert.Fail($"Defender EnsureKingdom failed: {err}"));
            yield return new WaitUntil(() => defenderKingdomReady);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(challengerAuthObject);
            Object.DestroyImmediate(defenderAuthObject);
            Object.DestroyImmediate(apiClientObject);
        }

        [UnityTest]
        public IEnumerator PostDuel_WithValidToken_ReturnsWellFormedResult()
        {
            var apiClient = apiClientObject.GetComponent<BackendApiClient>();
            var dto = new DuelRequest
            {
                recommendation = new PlayerRecommendationDto { army = 40, trade = 30, religion = 30 }
            };

            DuelResult result = null;
            string error = null;
            apiClient.PostDuel(challengerToken, dto, r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.defenderRulerSnapshot);
            Assert.IsFalse(string.IsNullOrEmpty(result.defenderRulerSnapshot.agenda));
        }

        /// <summary>
        /// I-1 regression test: a real 404 from the real server carries
        /// {"error":"No kingdom found for this user"} in its body -- PostDuel must
        /// surface that exact message to onError, not a generic
        /// "failed: ProtocolError (404)" string. Uses a fresh anonymous user that
        /// never calls EnsureKingdom, so the server's kingdom lookup genuinely misses.
        /// </summary>
        [UnityTest]
        public IEnumerator PostDuel_ChallengerWithNoKingdom_SurfacesRealServerErrorMessage()
        {
            var freshAuthObject = new GameObject("FreshAuth");
            var freshAuth = freshAuthObject.AddComponent<SupabaseAuthClient>();
            freshAuth.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            freshAuth.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";

            try
            {
                SessionData freshSession = null;
                freshAuth.SignInAnonymously(s => freshSession = s, err => Assert.Fail($"Fresh sign-in failed: {err}"));
                yield return new WaitUntil(() => freshSession != null);

                var apiClient = apiClientObject.GetComponent<BackendApiClient>();
                var dto = new DuelRequest
                {
                    recommendation = new PlayerRecommendationDto { army = 40, trade = 30, religion = 30 }
                };

                DuelResult result = null;
                string error = null;
                apiClient.PostDuel(freshSession.AccessToken, dto, r => result = r, err => error = err);
                yield return new WaitUntil(() => result != null || error != null);

                Assert.IsNull(result, "Expected the request to fail (no kingdom exists for this fresh user).");
                Assert.IsNotNull(error);
                Assert.IsTrue(error.Contains("No kingdom found for this user"),
                    $"Expected the real server error message, got: {error}");
            }
            finally
            {
                Object.DestroyImmediate(freshAuthObject);
            }
        }
    }
}
