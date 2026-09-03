using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnderstudyKingdom.Backend;

namespace UnderstudyKingdom.Tests
{
    /// <summary>
    /// Hits the REAL local server/ and REAL Supabase project, mirroring
    /// BackendApiClientHistoryTests's structure.
    /// </summary>
    public class BackendApiClientCouncilTests
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
        public IEnumerator CreateCouncil_ReturnsWellFormedResult()
        {
            CouncilResponse result = null;
            string error = null;
            apiClient.CreateCouncil(jwt, "Grinders", r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.AreEqual("Grinders", result.name);
            Assert.AreEqual(1, result.memberCount);
        }

        [UnityTest]
        public IEnumerator CreateCouncil_CalledTwiceForSameUser_SecondCallReturnsAlreadyInCouncilError()
        {
            CouncilResponse first = null;
            apiClient.CreateCouncil(jwt, "First", r => first = r, err => Assert.Fail($"First create failed: {err}"));
            yield return new WaitUntil(() => first != null);

            CouncilResponse second = null;
            string error = null;
            apiClient.CreateCouncil(jwt, "Second", r => second = r, err => error = err);
            yield return new WaitUntil(() => second != null || error != null);

            Assert.IsNull(second);
            Assert.AreEqual("You are already in a council", error);
        }

        [UnityTest]
        public IEnumerator JoinCouncil_WithRealJoinCode_AddsSecondMember()
        {
            CouncilResponse created = null;
            apiClient.CreateCouncil(jwt, "Open Council", r => created = r, err => Assert.Fail($"Create failed: {err}"));
            yield return new WaitUntil(() => created != null);

            var joinerAuthObject = new GameObject("JoinerAuth");
            var joinerAuth = joinerAuthObject.AddComponent<SupabaseAuthClient>();
            joinerAuth.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            joinerAuth.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";
            SessionData joinerSession = null;
            joinerAuth.SignInAnonymously(s => joinerSession = s, err => Assert.Fail($"Joiner sign-in failed: {err}"));
            yield return new WaitUntil(() => joinerSession != null);

            CouncilResponse joined = null;
            string error = null;
            apiClient.JoinCouncil(joinerSession.AccessToken, created.joinCode, r => joined = r, err => error = err);
            yield return new WaitUntil(() => joined != null || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.AreEqual(2, joined.memberCount);

            Object.DestroyImmediate(joinerAuthObject);
        }

        [UnityTest]
        public IEnumerator GetCouncilStatus_WithNoCouncilYet_ReturnsNotInACouncilError()
        {
            CouncilResponse result = null;
            string error = null;
            apiClient.GetCouncilStatus(jwt, r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(result);
            Assert.AreEqual("Not in a council", error);
        }

        [UnityTest]
        public IEnumerator GetCouncilStatus_AfterCreating_ReturnsRealMembershipData()
        {
            CouncilResponse created = null;
            apiClient.CreateCouncil(jwt, "Grinders", r => created = r, err => Assert.Fail($"Create failed: {err}"));
            yield return new WaitUntil(() => created != null);

            CouncilResponse status = null;
            string error = null;
            apiClient.GetCouncilStatus(jwt, r => status = r, err => error = err);
            yield return new WaitUntil(() => status != null || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.AreEqual(created.id, status.id);
            Assert.AreEqual(created.joinCode, status.joinCode);
        }
    }
}
