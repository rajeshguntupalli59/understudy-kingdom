using System;
using UnityEngine;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Backend
{
    /// <summary>
    /// Orchestrates session bootstrap and best-effort background decision sync.
    /// Local SaveService/OverrideEvaluator remain authoritative; every sync call here
    /// is fire-and-forget -- failure is logged and dropped, never surfaced to the
    /// player, never retried. See
    /// docs/superpowers/specs/2026-09-02-client-backend-integration-design.md.
    /// </summary>
    public class BackendSyncCoordinator : MonoBehaviour
    {
        public string SupabaseUrl;
        public string SupabaseAnonKey;
        public string BackendBaseUrl;
        public DecisionCycleManager DecisionCycleManager;

        private SupabaseAuthClient authClient;
        private BackendApiClient apiClient;
        private SessionData currentSession;
        private bool kingdomReady;

        private void Start()
        {
            authClient = gameObject.AddComponent<SupabaseAuthClient>();
            authClient.SupabaseUrl = SupabaseUrl;
            authClient.SupabaseAnonKey = SupabaseAnonKey;

            apiClient = gameObject.AddComponent<BackendApiClient>();
            apiClient.BackendBaseUrl = BackendBaseUrl;

            BootstrapSession();
        }

        private void BootstrapSession()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SessionData stored = SessionStore.Load();

            if (stored != null && !stored.IsExpired(now))
            {
                currentSession = stored;
                OnSessionReady();
                return;
            }

            if (stored != null && !string.IsNullOrEmpty(stored.RefreshToken))
            {
                authClient.RefreshSession(stored.RefreshToken, HandleSessionObtained, error =>
                {
                    Debug.LogWarning($"BackendSyncCoordinator: bootstrap refresh failed, clearing stale session and signing in fresh: {error}");
                    SessionStore.Clear();
                    authClient.SignInAnonymously(HandleSessionObtained, HandleSessionError);
                });
                return;
            }

            authClient.SignInAnonymously(HandleSessionObtained, HandleSessionError);
        }

        private void HandleSessionObtained(SessionData session)
        {
            currentSession = session;
            SessionStore.Save(session);
            OnSessionReady();
        }

        private void HandleSessionError(string error)
        {
            Debug.LogWarning($"BackendSyncCoordinator: session bootstrap failed, sync disabled for this launch: {error}");
        }

        private void OnSessionReady()
        {
            apiClient.EnsureKingdom(currentSession.AccessToken,
                onSuccess: () => { kingdomReady = true; },
                onError: err => Debug.LogWarning($"BackendSyncCoordinator: EnsureKingdom failed: {err}"));

            if (DecisionCycleManager != null)
            {
                DecisionCycleManager.OnDecisionRecorded += HandleDecisionRecorded;
            }
        }

        // Defense-in-depth: OnDecisionRecorded is invoked synchronously from
        // DecisionCycleManager.SubmitRecommendation, so any uncaught exception here
        // would propagate into gameplay code and violate this milestone's core
        // "never blocks gameplay" contract.
        private void HandleDecisionRecorded(DecisionRecord record)
        {
            try
            {
                if (currentSession == null)
                {
                    Debug.LogWarning("BackendSyncCoordinator: no session available, dropping decision sync.");
                    return;
                }

                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (currentSession.IsExpired(now))
                {
                    if (string.IsNullOrEmpty(currentSession.RefreshToken))
                    {
                        Debug.LogWarning("BackendSyncCoordinator: session expired with no refresh token, dropping decision sync.");
                        return;
                    }

                    authClient.RefreshSession(currentSession.RefreshToken,
                        onSuccess: refreshed =>
                        {
                            currentSession = refreshed;
                            SessionStore.Save(refreshed);
                            SyncDecision(record);
                        },
                        onError: err => Debug.LogWarning($"BackendSyncCoordinator: session refresh failed, dropping decision sync: {err}"));
                    return;
                }

                SyncDecision(record);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"BackendSyncCoordinator: unexpected exception during decision sync for cycle {record.CycleNumber}, dropping: {ex.Message}");
            }
        }

        private void SyncDecision(DecisionRecord record)
        {
            DecisionSyncRequest dto = DecisionSyncRequestFactory.From(record);
            apiClient.PostDecision(currentSession.AccessToken, dto,
                onSuccess: wasAlreadyRecorded =>
                {
                    if (wasAlreadyRecorded)
                    {
                        Debug.LogWarning($"BackendSyncCoordinator: cycle {record.CycleNumber} was already recorded server-side (409) — if this wasn't an intentional re-sync, the local cycle counter may have reset independently of the persisted session, and this decision was NOT newly recorded.");
                    }
                },
                onError: err => Debug.LogWarning($"BackendSyncCoordinator: decision sync failed for cycle {record.CycleNumber}: {err}"));
        }

        private void OnDestroy()
        {
            if (DecisionCycleManager != null)
            {
                DecisionCycleManager.OnDecisionRecorded -= HandleDecisionRecorded;
            }
        }

        /// <summary>
        /// Unlike HandleDecisionRecorded (fire-and-forget, every failure logged
        /// and dropped), a duel is an explicit player-initiated request -- failures
        /// are reported to the caller via onError, not swallowed. See
        /// docs/superpowers/specs/2026-09-02-async-pvp-design.md's Error Handling
        /// section.
        /// </summary>
        public void RequestDuel(ResourceAllocation recommendation, Action<DuelResult> onSuccess, Action<string> onError)
        {
            if (currentSession == null)
            {
                onError?.Invoke("No session available yet -- try again in a moment.");
                return;
            }

            // The session must be refreshed (if needed) BEFORE we branch on
            // kingdomReady -- EnsureKingdom below sends currentSession.AccessToken,
            // and if that token is stale the request would 401 against the real
            // server. See final-review I-1: this used to check kingdomReady first,
            // which skipped the refresh entirely on the not-ready retry path.
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (currentSession.IsExpired(now))
            {
                if (string.IsNullOrEmpty(currentSession.RefreshToken))
                {
                    onError?.Invoke("Session expired and cannot be refreshed -- please restart the app.");
                    return;
                }

                authClient.RefreshSession(currentSession.RefreshToken,
                    onSuccess: refreshed =>
                    {
                        currentSession = refreshed;
                        SessionStore.Save(refreshed);
                        EnsureKingdomThenSendDuel(recommendation, onSuccess, onError);
                    },
                    onError: err => onError?.Invoke($"Session refresh failed: {err}"));
                return;
            }

            EnsureKingdomThenSendDuel(recommendation, onSuccess, onError);
        }

        private void EnsureKingdomThenSendDuel(ResourceAllocation recommendation, Action<DuelResult> onSuccess, Action<string> onError)
        {
            // The kingdom-ensure round-trip from OnSessionReady may still be in
            // flight (or may have failed at startup) -- rather than fail a duel
            // tapped very early in the app's lifetime, re-attempt EnsureKingdom
            // here and chain into the duel send on its success. See I-2. By the
            // time we get here, currentSession.AccessToken is guaranteed fresh.
            if (!kingdomReady)
            {
                apiClient.EnsureKingdom(currentSession.AccessToken,
                    onSuccess: () =>
                    {
                        kingdomReady = true;
                        SendDuelRequest(recommendation, onSuccess, onError);
                    },
                    onError: err => onError?.Invoke($"Your kingdom isn't ready yet: {err}"));
                return;
            }

            SendDuelRequest(recommendation, onSuccess, onError);
        }

        private void SendDuelRequest(ResourceAllocation recommendation, Action<DuelResult> onSuccess, Action<string> onError)
        {
            var dto = new DuelRequest
            {
                recommendation = new PlayerRecommendationDto
                {
                    army = recommendation.Army,
                    trade = recommendation.Trade,
                    religion = recommendation.Religion
                }
            };
            apiClient.PostDuel(currentSession.AccessToken, dto, onSuccess, onError);
        }

        /// <summary>
        /// Mirrors RequestDuel's structure exactly: refresh-if-needed runs
        /// unconditionally first, then the shared kingdomReady gate, then the send.
        /// This is written directly against RequestDuel's corrected (post-fix)
        /// shape, not a fresh reinvention of the same ordering decision -- see
        /// RequestDuel's own comment for why the ordering matters (final-review
        /// I-1/I-2 on the async-pvp milestone found and fixed the opposite ordering
        /// as a real bug).
        /// </summary>
        public void RequestHistory(int limit, Action<DecisionHistoryEntry[]> onSuccess, Action<string> onError)
        {
            if (currentSession == null)
            {
                onError?.Invoke("No session available yet -- try again in a moment.");
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (currentSession.IsExpired(now))
            {
                if (string.IsNullOrEmpty(currentSession.RefreshToken))
                {
                    onError?.Invoke("Session expired and cannot be refreshed -- please restart the app.");
                    return;
                }

                authClient.RefreshSession(currentSession.RefreshToken,
                    onSuccess: refreshed =>
                    {
                        currentSession = refreshed;
                        SessionStore.Save(refreshed);
                        EnsureKingdomThenSendHistory(limit, onSuccess, onError);
                    },
                    onError: err => onError?.Invoke($"Session refresh failed: {err}"));
                return;
            }

            EnsureKingdomThenSendHistory(limit, onSuccess, onError);
        }

        private void EnsureKingdomThenSendHistory(int limit, Action<DecisionHistoryEntry[]> onSuccess, Action<string> onError)
        {
            if (!kingdomReady)
            {
                apiClient.EnsureKingdom(currentSession.AccessToken,
                    onSuccess: () =>
                    {
                        kingdomReady = true;
                        apiClient.GetDecisionHistory(currentSession.AccessToken, limit, onSuccess, onError);
                    },
                    onError: err => onError?.Invoke($"Your kingdom isn't ready yet: {err}"));
                return;
            }

            apiClient.GetDecisionHistory(currentSession.AccessToken, limit, onSuccess, onError);
        }
    }
}
