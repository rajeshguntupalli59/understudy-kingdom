using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace UnderstudyKingdom.Backend
{
    /// <summary>
    /// Raw REST calls to Supabase Auth (no SDK). Request/response shapes verified
    /// empirically against the real project before this was written -- see
    /// docs/superpowers/specs/2026-09-02-client-backend-integration-design.md.
    /// </summary>
    public class SupabaseAuthClient : MonoBehaviour
    {
        public string SupabaseUrl;
        public string SupabaseAnonKey;

        [Serializable]
        private class SupabaseAuthResponse
        {
            public string access_token;
            public string refresh_token;
            public long expires_at;
            public SupabaseUser user;
        }

        [Serializable]
        private class SupabaseUser
        {
            public string id;
        }

        [Serializable]
        private class RefreshRequestBody
        {
            public string refresh_token;
        }

        public void SignInAnonymously(Action<SessionData> onSuccess, Action<string> onError)
        {
            StartCoroutine(PostJson($"{SupabaseUrl}/auth/v1/signup", "{}", onSuccess, onError));
        }

        public void RefreshSession(string refreshToken, Action<SessionData> onSuccess, Action<string> onError)
        {
            string body = JsonUtility.ToJson(new RefreshRequestBody { refresh_token = refreshToken });
            StartCoroutine(PostJson($"{SupabaseUrl}/auth/v1/token?grant_type=refresh_token", body, onSuccess, onError));
        }

        private IEnumerator PostJson(string url, string jsonBody, Action<SessionData> onSuccess, Action<string> onError)
        {
            using var request = new UnityWebRequest(url, "POST");
            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", SupabaseAnonKey);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Supabase auth request failed: {request.result} ({request.responseCode})");
                yield break;
            }

            SupabaseAuthResponse parsed;
            try
            {
                parsed = JsonUtility.FromJson<SupabaseAuthResponse>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Supabase auth response parse failed: {ex.Message}");
                yield break;
            }

            if (parsed == null || string.IsNullOrEmpty(parsed.access_token))
            {
                onError?.Invoke("Supabase auth response missing access_token");
                yield break;
            }

            onSuccess?.Invoke(new SessionData
            {
                AccessToken = parsed.access_token,
                RefreshToken = parsed.refresh_token,
                ExpiresAtUnixSeconds = parsed.expires_at,
                UserId = parsed.user?.id
            });
        }
    }
}
