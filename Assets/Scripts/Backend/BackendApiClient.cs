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

        /// <summary>
        /// The project's first GET-based call (everything else so far is POST).
        /// Reuses TryExtractServerErrorMessage (added for PostDuel in milestone #5)
        /// so a real server error message reaches the player instead of a generic
        /// status code.
        /// </summary>
        public void GetDecisionHistory(string accessToken, int limit, Action<DecisionHistoryEntry[]> onSuccess, Action<string> onError)
        {
            StartCoroutine(SendGetDecisionHistory(accessToken, limit, onSuccess, onError));
        }

        private IEnumerator SendGetDecisionHistory(string accessToken, int limit, Action<DecisionHistoryEntry[]> onSuccess, Action<string> onError)
        {
            string url = $"{BackendBaseUrl}/api/v1/decisions?limit={limit}";
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string message = TryExtractServerErrorMessage(request.downloadHandler.text)
                    ?? $"Decision history request to {url} failed: {request.result} ({request.responseCode})";
                onError?.Invoke(message);
                yield break;
            }

            DecisionHistoryResponse response;
            try
            {
                response = JsonUtility.FromJson<DecisionHistoryResponse>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Decision history response parse failed: {ex.Message}");
                yield break;
            }

            if (response == null || response.decisions == null)
            {
                onError?.Invoke("Decision history response missing expected fields");
                yield break;
            }

            onSuccess?.Invoke(response.decisions);
        }

        /// <summary>
        /// Mirrors PostDuel's response-parsing shape (SendDuelRequest) -- the
        /// response body carries real council data, not just a status code.
        /// </summary>
        public void CreateCouncil(string accessToken, string name, Action<CouncilResponse> onSuccess, Action<string> onError)
        {
            string body = JsonUtility.ToJson(new CreateCouncilRequest { name = name });
            StartCoroutine(SendCouncilRequest("POST", $"{BackendBaseUrl}/api/v1/councils", body, accessToken, onSuccess, onError));
        }

        public void JoinCouncil(string accessToken, string joinCode, Action<CouncilResponse> onSuccess, Action<string> onError)
        {
            string body = JsonUtility.ToJson(new JoinCouncilRequest { joinCode = joinCode });
            StartCoroutine(SendCouncilRequest("POST", $"{BackendBaseUrl}/api/v1/councils/join", body, accessToken, onSuccess, onError));
        }

        private IEnumerator SendCouncilRequest(string method, string url, string jsonBody, string accessToken,
            Action<CouncilResponse> onSuccess, Action<string> onError)
        {
            using var request = new UnityWebRequest(url, method);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string message = TryExtractServerErrorMessage(request.downloadHandler.text)
                    ?? $"Council request to {url} failed: {request.result} ({request.responseCode})";
                onError?.Invoke(message);
                yield break;
            }

            CouncilResponse response;
            try
            {
                response = JsonUtility.FromJson<CouncilResponse>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Council response parse failed: {ex.Message}");
                yield break;
            }

            if (response == null || response.id == null)
            {
                onError?.Invoke("Council response missing expected fields");
                yield break;
            }

            onSuccess?.Invoke(response);
        }

        /// <summary>
        /// The second GET-based call in this project (see GetDecisionHistory).
        /// A real "Not in a council" 404 is surfaced via onError like any
        /// other non-2xx response -- the UI layer (CouncilPanelController)
        /// decides whether that specific message means "show the empty
        /// state" rather than "show an error," mirroring
        /// HistoryPanelController's own 404-vs-real-error split.
        /// </summary>
        public void GetCouncilStatus(string accessToken, Action<CouncilResponse> onSuccess, Action<string> onError)
        {
            StartCoroutine(SendGetCouncilStatus(accessToken, onSuccess, onError));
        }

        private IEnumerator SendGetCouncilStatus(string accessToken, Action<CouncilResponse> onSuccess, Action<string> onError)
        {
            string url = $"{BackendBaseUrl}/api/v1/councils/me";
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string message = TryExtractServerErrorMessage(request.downloadHandler.text)
                    ?? $"Council status request to {url} failed: {request.result} ({request.responseCode})";
                onError?.Invoke(message);
                yield break;
            }

            CouncilResponse response;
            try
            {
                response = JsonUtility.FromJson<CouncilResponse>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Council status response parse failed: {ex.Message}");
                yield break;
            }

            if (response == null || response.id == null)
            {
                onError?.Invoke("Council status response missing expected fields");
                yield break;
            }

            onSuccess?.Invoke(response);
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
