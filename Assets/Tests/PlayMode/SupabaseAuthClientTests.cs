using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnderstudyKingdom.Backend;

namespace UnderstudyKingdom.Tests
{
    /// <summary>
    /// Hits the REAL Supabase project over the network -- no mock, matching this
    /// project's established testing culture (see server/'s own integration tests).
    /// Requires real internet access.
    /// </summary>
    public class SupabaseAuthClientTests
    {
        private GameObject clientObject;
        private SupabaseAuthClient client;

        [SetUp]
        public void SetUp()
        {
            clientObject = new GameObject("SupabaseAuthClient");
            client = clientObject.AddComponent<SupabaseAuthClient>();
            client.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            client.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(clientObject);
        }

        [UnityTest]
        public IEnumerator SignInAnonymously_ReturnsValidSession()
        {
            SessionData result = null;
            string error = null;

            client.SignInAnonymously(session => result = session, err => error = err);

            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.IsNotNull(result);
            Assert.IsFalse(string.IsNullOrEmpty(result.AccessToken));
            Assert.IsFalse(string.IsNullOrEmpty(result.RefreshToken));
            Assert.IsFalse(string.IsNullOrEmpty(result.UserId));
            Assert.Greater(result.ExpiresAtUnixSeconds, 0);
        }

        [UnityTest]
        public IEnumerator RefreshSession_WithValidRefreshToken_ReturnsRotatedSession()
        {
            SessionData signInResult = null;
            client.SignInAnonymously(session => signInResult = session, err => Assert.Fail($"Sign-in failed: {err}"));
            yield return new WaitUntil(() => signInResult != null);

            SessionData refreshResult = null;
            string error = null;
            client.RefreshSession(signInResult.RefreshToken, session => refreshResult = session, err => error = err);
            yield return new WaitUntil(() => refreshResult != null || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.IsNotNull(refreshResult);
            Assert.AreEqual(signInResult.UserId, refreshResult.UserId);
            Assert.AreNotEqual(signInResult.RefreshToken, refreshResult.RefreshToken,
                "Supabase rotates the refresh token on every use -- storing the old one would break the next refresh.");
        }
    }
}
