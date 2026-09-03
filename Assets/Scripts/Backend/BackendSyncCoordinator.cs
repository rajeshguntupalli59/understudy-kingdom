using System;
using System.Collections.Generic;
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

        // This coordinator now has three independent, player- or event-triggered
        // entry points (HandleDecisionRecorded, RequestDuel, RequestHistory) that
        // all share currentSession. Each used to run its own "is it expired? then
        // refresh" check inline -- if two fired in the same window, both would send
        // Supabase the SAME refresh token. Supabase rotates refresh tokens on use,
        // so the loser of that race gets a rejected/superseded token, and whichever
        // response landed last silently overwrote SessionStore -- risking a
        // permanently-orphaned identity on a later BootstrapSession (a failed
        // bootstrap refresh clears the session and signs in fresh, i.e. a NEW
        // anonymous user, abandoning the old kingdom and its history). Flagged in
        // milestone #6's final review (I-4). EnsureFreshSession below is the single
        // place that may ever call RefreshSession -- every caller queues onto it
        // instead of racing their own call.
        private bool refreshInFlight;
        private readonly List<(Action onReady, Action<string> onError)> pendingRefreshCallbacks = new List<(Action, Action<string>)>();

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

        /// <summary>
        /// Single choke point for "make sure currentSession is usable right now."
        /// If no refresh is needed, onReady fires immediately (synchronously). If a
        /// refresh is needed and none is in flight, this starts the one and only
        /// RefreshSession call and queues onReady/onError to fire when it resolves.
        /// If a refresh is ALREADY in flight (a concurrent caller started it), this
        /// just queues onto that same call rather than firing a second one with the
        /// same (about-to-be-rotated) refresh token. By the time onReady fires,
        /// currentSession is guaranteed fresh -- callers should read
        /// currentSession.AccessToken only after onReady, never before.
        /// </summary>
        private void EnsureFreshSession(Action onReady, Action<string> onError)
        {
            if (currentSession == null)
            {
                onError?.Invoke("No session available yet -- try again in a moment.");
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (!currentSession.IsExpired(now))
            {
                onReady?.Invoke();
                return;
            }

            if (string.IsNullOrEmpty(currentSession.RefreshToken))
            {
                onError?.Invoke("Session expired and cannot be refreshed -- please restart the app.");
                return;
            }

            pendingRefreshCallbacks.Add((onReady, onError));

            if (refreshInFlight)
            {
                // A concurrent caller already started the refresh this one needs --
                // just wait for it instead of sending a second RefreshSession call
                // with the same (about-to-be-rotated) refresh token.
                return;
            }

            refreshInFlight = true;
            authClient.RefreshSession(currentSession.RefreshToken,
                onSuccess: refreshed =>
                {
                    currentSession = refreshed;
                    SessionStore.Save(refreshed);
                    refreshInFlight = false;
                    DrainPendingRefreshCallbacks(ready => ready?.Invoke());
                },
                onError: err =>
                {
                    refreshInFlight = false;
                    DrainPendingRefreshCallbacks(_ => { }, err);
                });
        }

        private void DrainPendingRefreshCallbacks(Action<Action> invokeReady, string errorMessage = null)
        {
            var callbacks = new List<(Action onReady, Action<string> onError)>(pendingRefreshCallbacks);
            pendingRefreshCallbacks.Clear();

            foreach (var (onReady, onError) in callbacks)
            {
                if (errorMessage != null)
                {
                    onError?.Invoke($"Session refresh failed: {errorMessage}");
                }
                else
                {
                    invokeReady(onReady);
                }
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
                EnsureFreshSession(
                    onReady: () => SyncDecision(record),
                    onError: err => Debug.LogWarning($"BackendSyncCoordinator: {err} -- dropping decision sync for cycle {record.CycleNumber}."));
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
            EnsureFreshSession(
                onReady: () => EnsureKingdomThenSendDuel(recommendation, onSuccess, onError),
                onError: onError);
        }

        private void EnsureKingdomThenSendDuel(ResourceAllocation recommendation, Action<DuelResult> onSuccess, Action<string> onError)
        {
            // The kingdom-ensure round-trip from OnSessionReady may still be in
            // flight (or may have failed at startup) -- rather than fail a duel
            // tapped very early in the app's lifetime, re-attempt EnsureKingdom
            // here and chain into the duel send on its success. See I-2. By the
            // time we get here, currentSession.AccessToken is guaranteed fresh
            // (EnsureFreshSession already ran).
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
        /// Mirrors RequestDuel's structure: refresh-if-needed runs first (now via
        /// the shared EnsureFreshSession, so it can never race RequestDuel's or
        /// HandleDecisionRecorded's refresh), then the shared kingdomReady gate,
        /// then the send.
        /// </summary>
        public void RequestHistory(int limit, Action<DecisionHistoryEntry[]> onSuccess, Action<string> onError)
        {
            EnsureFreshSession(
                onReady: () => EnsureKingdomThenSendHistory(limit, onSuccess, onError),
                onError: onError);
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
