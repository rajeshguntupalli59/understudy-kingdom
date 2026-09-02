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
    }
}
