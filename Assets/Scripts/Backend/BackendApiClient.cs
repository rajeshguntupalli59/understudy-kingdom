using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace UnderstudyKingdom.Backend
{
    /// <summary>
    /// Raw REST calls to this project's own backend (server/). See
    /// docs/superpowers/specs/2026-09-02-client-backend-integration-design.md.
    /// </summary>
    public class BackendApiClient : MonoBehaviour
    {
        public string BackendBaseUrl;

        [Serializable]
        private class ErrorResponseDto
        {
            public string error;
        }

        public void EnsureKingdom(string accessToken, Action onSuccess, Action<string> onError)
        {
            StartCoroutine(Post($"{BackendBaseUrl}/api/v1/kingdoms", "{}", accessToken,
                acceptedStatusCodes: null, wasAcceptedStatus => onSuccess?.Invoke(), onError));
        }

        /// <summary>
        /// wasAlreadyRecorded is true when the server responded 409 (this exact
        /// cycle_number was already recorded for this kingdom -- could mean this is a
        /// genuine re-sync, or it could mean the local cycle counter reset independently
        /// of the persisted session, e.g. after an app restart that reused a persisted
        /// session -- see BackendSyncCoordinator.SyncDecision), and false when the
        /// server genuinely created a new decision record (2xx). Both cases still count
        /// as "success" for the fire-and-forget sync contract; the bool exists purely so
        /// callers can log the ambiguous case instead of it passing silently.
        /// </summary>
        public void PostDecision(string accessToken, DecisionSyncRequest dto, Action<bool> onSuccess, Action<string> onError)
        {
            string body = JsonUtility.ToJson(dto);
            StartCoroutine(Post($"{BackendBaseUrl}/api/v1/decisions", body, accessToken,
                acceptedStatusCodes: new long[] { 409 }, wasAcceptedStatus => onSuccess?.Invoke(wasAcceptedStatus), onError));
        }

        private IEnumerator Post(string url, string jsonBody, string accessToken,
            long[] acceptedStatusCodes, Action<bool> onSuccess, Action<string> onError)
        {
            using var request = new UnityWebRequest(url, "POST");
            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

            yield return request.SendWebRequest();

            bool isSuccess = request.result == UnityWebRequest.Result.Success;
            bool isAcceptedStatus = acceptedStatusCodes != null
                && Array.IndexOf(acceptedStatusCodes, request.responseCode) >= 0;

            if (isSuccess || isAcceptedStatus)
            {
                onSuccess?.Invoke(isAcceptedStatus);
                yield break;
            }

            onError?.Invoke($"Backend request to {url} failed: {request.result} ({request.responseCode})");
        }

        /// <summary>
        /// Unlike EnsureKingdom/PostDecision (which only ever care about status
        /// codes via the shared Post coroutine), a duel's response body carries the
        /// actual result -- this needs its own coroutine that parses it, following
        /// the same three-way error discrimination (network failure / parse
        /// failure / missing field) SupabaseAuthClient.PostJson already uses.
        /// </summary>
        public void PostDuel(string accessToken, DuelRequest dto, Action<DuelResult> onSuccess, Action<string> onError)
        {
            string body = JsonUtility.ToJson(dto);
            StartCoroutine(SendDuelRequest(body, accessToken, onSuccess, onError));
        }

        private IEnumerator SendDuelRequest(string jsonBody, string accessToken, Action<DuelResult> onSuccess, Action<string> onError)
        {
            string url = $"{BackendBaseUrl}/api/v1/duels";
            using var request = new UnityWebRequest(url, "POST");
            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string message = TryExtractServerErrorMessage(request.downloadHandler.text)
                    ?? $"Duel request to {url} failed: {request.result} ({request.responseCode})";
                onError?.Invoke(message);
                yield break;
            }

            DuelResult result;
            try
            {
                result = JsonUtility.FromJson<DuelResult>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Duel response parse failed: {ex.Message}");
                yield break;
            }

            if (result == null || result.defenderRulerSnapshot == null)
            {
                onError?.Invoke("Duel response missing expected fields");
                yield break;
            }

            onSuccess?.Invoke(result);
        }

        private static string TryExtractServerErrorMessage(string responseBody)
        {
            if (string.IsNullOrEmpty(responseBody)) return null;
            try
            {
                var parsed = JsonUtility.FromJson<ErrorResponseDto>(responseBody);
                return string.IsNullOrEmpty(parsed?.error) ? null : parsed.error;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
