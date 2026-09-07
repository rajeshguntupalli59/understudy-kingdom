using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using UnderstudyKingdom.Backend;

namespace UnderstudyKingdom.Tests
{
    /// <summary>
    /// Shared teardown helper for every PlayMode test that signs in for real
    /// and creates a real kingdom against the real configured DATABASE_URL
    /// (BackendSyncCoordinator/BackendApiClient real-network tests). None of
    /// these previously cleaned up their server-side rows -- only local Unity
    /// objects and the session file -- so the real DB grew by a handful of
    /// kingdoms/decisions on every single full PlayMode run. See
    /// docs/PROJECT_PLAN.md's Known follow-up items.
    ///
    /// Creates its own throwaway BackendApiClient rather than requiring each
    /// caller to already have one wired up -- works uniformly regardless of
    /// whether the caller's own coordinator/client is still alive at
    /// teardown time. Best-effort: DELETE /api/v1/kingdoms/me is gated
    /// behind ALLOW_TEST_DB_TRUNCATE server-side, so a misconfigured
    /// environment (flag off) fails this cleanup harmlessly (logged, not
    /// thrown) rather than failing the test itself.
    /// </summary>
    public static class TestKingdomCleanup
    {
        public static IEnumerator DeleteTestKingdom(string backendBaseUrl, string callerName)
        {
            SessionData session = SessionStore.Load();
            yield return DeleteTestKingdom(backendBaseUrl, session?.AccessToken, callerName);
        }

        /// <summary>
        /// For test fixtures that never persist to SessionStore (e.g. ones
        /// using SupabaseAuthClient directly for a second/third real user
        /// alongside the main one, like a duel's challenger+defender) --
        /// pass the raw access token straight through instead.
        /// </summary>
        public static IEnumerator DeleteTestKingdom(string backendBaseUrl, string accessToken, string callerName)
        {
            if (string.IsNullOrEmpty(accessToken))
            {
                yield break;
            }

            var tempObject = new GameObject("TestKingdomCleanupClient");
            var apiClient = tempObject.AddComponent<BackendApiClient>();
            apiClient.BackendBaseUrl = backendBaseUrl;

            bool done = false;
            apiClient.DeleteMyKingdom(accessToken,
                onSuccess: () => done = true,
                onError: err =>
                {
                    Debug.LogWarning($"{callerName}: test-kingdom cleanup failed (non-fatal): {err}");
                    done = true;
                });
            yield return new WaitUntil(() => done);

            Object.DestroyImmediate(tempObject);
        }
    }
}
