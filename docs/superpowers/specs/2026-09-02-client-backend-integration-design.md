# Design: Unity Client ↔ Backend Integration (Milestone #4)

**Date:** 2026-09-02 | **Status:** Approved, pending implementation plan

## Purpose

Milestone #3 built `server/` (Fastify + Supabase Auth via JWKS + Postgres,
`kingdoms`/`decisions` endpoints, 35 tests passing) but explicitly deferred
wiring the Unity client to it — the client still runs fully offline via
local `SaveService`/`OverrideEvaluator` exactly as it did after milestone
#1, with no sign-in and no HTTP calls anywhere in `Assets/`. This milestone
closes that gap: the client signs in (anonymously), ensures a kingdom
exists server-side, and syncs each decision to the backend in the
background. This is a prerequisite for Social (needs a real logged-in user
to join a council) and Async PvP (needs decisions actually recorded
server-side to judge against) — neither was buildable before this existed,
even though the original milestone decomposition didn't name this as its
own numbered milestone.

This milestone also resolves the two items milestone #3's final review
flagged to carry forward: the client's retry/UX contract for a `503`
(auth-service-unavailable) response (see Error Handling below), and
cursor-pagination handling for `GET /decisions` — the latter is not
applicable in this pass, since no history-viewing UI exists yet to call
that endpoint (deferred, along with `GET /decisions` itself, to whenever
FR-06 builds that screen).

## Scope Decisions

- **Anonymous sign-in only, not Google/Apple OAuth.** Supabase Auth
  supports both; anonymous is sufficient to make the client an
  authenticated backend consumer, which is all this milestone needs. Real
  identity providers are a separate, later concern (tied to whenever
  cross-device account recovery or social features that require knowing
  who a real person is become requirements).
- **Sync plumbing only — no new decision-history-viewing screen.** The
  client starts writing to the server; nothing new reads from it yet.
  Viewing synced history is FR-06, a separate future milestone.
- **Local state remains authoritative; server sync is best-effort and
  never blocks gameplay.** `SaveService`/`OverrideEvaluator` are unchanged.
  A sync failure of any kind (network error, `503`, `401`) is logged and
  dropped — never retried, never queued, never surfaced to the player.
  Given there's no history-viewing UI or PvP consumer of server-side
  decision completeness yet, a dropped sync has zero current consequences;
  a retry queue would be built against a requirement that doesn't exist.
  Revisit if/when FR-06 or PvP make server-side completeness load-bearing.
- **Session persistence (a locally-stored Supabase refresh token) is
  required, not optional.** Without it, every app launch would create a
  new anonymous identity and a new server-side kingdom, defeating the
  point of syncing. This is a structural necessity for the milestone's
  goal, not scope creep.
- **`UnityWebRequest` (built-in), no additional package.** Sufficient for
  the request volume here (sign-in once per session, one POST per
  decision); avoids taking a new dependency for a solved problem.
- **No deployment of `server/` in this pass.** The client's backend base
  URL points at `http://localhost:3000`. End-to-end verification requires
  the dev server running locally alongside the Editor. Matches the
  project's current solo/greenfield stage; revisit when a real deployment
  target exists.

## Approach

**Transport:** `UnityWebRequest`, coroutine-based (`MonoBehaviour` +
`StartCoroutine`), matching the fact that nothing in this codebase has
used async/await or a networking package before — the simplest,
dependency-free option.

**Supabase Auth (raw REST, verified empirically against the real project
before writing this spec — not assumed from SDK docs):**
- Anonymous sign-in: `POST {SUPABASE_URL}/auth/v1/signup`, header
  `apikey: {SUPABASE_ANON_KEY}`, body `{}`. Returns `access_token`,
  `refresh_token`, `expires_in`, `expires_at` (unix seconds), `user.id`,
  `user.is_anonymous: true`.
- Refresh: `POST {SUPABASE_URL}/auth/v1/token?grant_type=refresh_token`,
  same `apikey` header, body `{"refresh_token": "..."}`. Returns the same
  shape. **Supabase rotates the refresh token on every use** — the
  response's `refresh_token` differs from the one sent and must overwrite
  what's stored, or the next refresh fails.
- `SUPABASE_ANON_KEY` is the publishable/anon key — safe to embed
  client-side; that is its intended purpose.

**Backend calls:** `POST /api/v1/kingdoms` (idempotent, called once per
launch after a valid session exists) and `POST /api/v1/decisions` (fired
after each `SubmitRecommendation`), both with
`Authorization: Bearer {access_token}`.

**Decoupling from existing gameplay code:** `DecisionCycleManager` gains
one new event, `OnDecisionRecorded`, fired at the end of
`SubmitRecommendation`. It carries a new neutral data type, `DecisionRecord`
(defined in `Core`, not `Backend`), so `DecisionCycleManager` has no
dependency on networking code at all. If nothing subscribes to the event,
behavior is unchanged from today. All new networking code lives in a new
`Assets/Scripts/Backend/` folder.

## Components

### `Assets/Scripts/Core/DecisionRecord.cs` (new)
```csharp
public readonly struct DecisionRecord
{
    public readonly int CycleNumber;
    public readonly ResourceAllocation Recommendation;
    public readonly bool Overridden;
    public readonly int Mood;
    public readonly int Loyalty;

    public DecisionRecord(int cycleNumber, ResourceAllocation recommendation,
        bool overridden, int mood, int loyalty)
    {
        CycleNumber = cycleNumber;
        Recommendation = recommendation;
        Overridden = overridden;
        Mood = mood;
        Loyalty = loyalty;
    }
}
```

### `Assets/Scripts/Core/DecisionCycleManager.cs` (modified)
Add `public event Action<DecisionRecord> OnDecisionRecorded;`. At the end of
`SubmitRecommendation`, after the existing `SaveService.Save(Ruler.State)`
call, invoke:
```csharp
OnDecisionRecorded?.Invoke(new DecisionRecord(
    currentCycleNumber, recommendation, result.Overridden,
    Ruler.State.Mood, Ruler.State.Loyalty));
```
This is the only change to existing gameplay code in this milestone.

### `Assets/Scripts/Backend/SessionData.cs` (new)
```csharp
[Serializable]
public class SessionData
{
    public string AccessToken;
    public string RefreshToken;
    public long ExpiresAtUnixSeconds;
    public string UserId;

    public bool IsExpired(long nowUnixSeconds, long skewSeconds = 60)
    {
        return nowUnixSeconds >= ExpiresAtUnixSeconds - skewSeconds;
    }
}
```
`IsExpired` is pure and EditMode-testable; the 60s skew avoids racing a
token that expires mid-request.

### `Assets/Scripts/Backend/SessionStore.cs` (new)
Static class, mirrors `SaveService.cs`'s existing pattern exactly: file
`backend_session.json` under `Application.persistentDataPath`,
`JsonUtility`, same defensive handling (missing or corrupt file → treated
as no session, never throws). `Save(SessionData)`, `Load()` (returns
`null` if none/corrupt), `Clear()`.

### `Assets/Scripts/Backend/SupabaseAuthClient.cs` (new)
`MonoBehaviour`. Two coroutine methods, each taking a completion callback
(`Action<SessionData>` on success, `Action<string>` on failure — no
exceptions across the coroutine boundary):
- `SignInAnonymously(Action<SessionData> onSuccess, Action<string> onError)`
- `RefreshSession(string refreshToken, Action<SessionData> onSuccess, Action<string> onError)`

Both parse the Supabase Auth REST response shape confirmed above into a
`SessionData`. Serialized inspector fields: `SupabaseUrl`,
`SupabaseAnonKey`.

### `Assets/Scripts/Backend/BackendApiClient.cs` (new)
`MonoBehaviour`. Serialized inspector field: `BackendBaseUrl`.
- `EnsureKingdom(string accessToken, Action onSuccess, Action<string> onError)`
  — `POST /api/v1/kingdoms`, empty body, `Authorization: Bearer` header.
  Any `2xx` is success.
- `PostDecision(string accessToken, DecisionSyncRequest dto, Action onSuccess, Action<string> onError)`
  — `POST /api/v1/decisions`, JSON body. `2xx` or `409` (already recorded —
  treated as already-synced, not a failure) both call `onSuccess`.

### `Assets/Scripts/Backend/DecisionSyncRequest.cs` (new)
Wire DTO matching the backend's JSON Schema field names exactly
(`server/src/routes/decisions.ts`), so `JsonUtility.ToJson` needs no
attribute-based renaming:
```csharp
[Serializable]
public class DecisionSyncRequest
{
    public int cycle_number;
    public PlayerRecommendationDto player_recommendation;
    public RulerOutcomeDto ruler_outcome;
    public bool overridden;
}
[Serializable] public class PlayerRecommendationDto { public int army; public int trade; public int religion; }
[Serializable] public class RulerOutcomeDto { public int mood; public int loyalty; }

public static class DecisionSyncRequestFactory
{
    public static DecisionSyncRequest From(DecisionRecord record)
    {
        return new DecisionSyncRequest
        {
            cycle_number = record.CycleNumber,
            player_recommendation = new PlayerRecommendationDto
            {
                army = record.Recommendation.Army,
                trade = record.Recommendation.Trade,
                religion = record.Recommendation.Religion
            },
            ruler_outcome = new RulerOutcomeDto { mood = record.Mood, loyalty = record.Loyalty },
            overridden = record.Overridden
        };
    }
}
```
`DecisionSyncRequestFactory.From` is pure and EditMode-testable in
isolation from any networking.

### `Assets/Scripts/Backend/BackendSyncCoordinator.cs` (new)
`MonoBehaviour`, added to the `CoreLoop` scene. Inspector fields:
`SupabaseUrl`, `SupabaseAnonKey`, `BackendBaseUrl`, a reference to
`DecisionCycleManager`, and references to the two client components above
(or it creates/owns them — implementation plan's call).

`Start()`:
1. `SessionStore.Load()`. If `null` or expired, sign in anonymously (or
   refresh, if a refresh token exists but the access token is expired);
   on success, `SessionStore.Save()` the result.
2. `BackendApiClient.EnsureKingdom(session.AccessToken, ...)`.
3. Subscribe to `decisionCycleManager.OnDecisionRecorded`.

On `OnDecisionRecorded(record)`: if the session is expired, refresh it
first (save the rotated result); build the DTO via
`DecisionSyncRequestFactory.From(record)`; call `PostDecision`; on any
failure, `Debug.LogWarning` and drop — no retry, no queue, no UI impact
(see Scope Decisions).

## Data Flow

```
App launch
  -> BackendSyncCoordinator.Start()
  -> SessionStore.Load() -> valid session? use it
                          -> expired? SupabaseAuthClient.RefreshSession()
                          -> none? SupabaseAuthClient.SignInAnonymously()
  -> SessionStore.Save(session)
  -> BackendApiClient.EnsureKingdom(session.AccessToken)   // idempotent, every launch
  -> subscribe to DecisionCycleManager.OnDecisionRecorded

Each SubmitRecommendation (existing local flow, unchanged)
  -> SaveService.Save(Ruler.State)                          // unchanged, authoritative
  -> OnDecisionRecorded fires
       -> BackendSyncCoordinator: ensure session fresh, build DTO
       -> BackendApiClient.PostDecision(...)                 // fire-and-forget
       -> failure of any kind -> logged, dropped, gameplay unaffected
```

## Error Handling

- **Any sign-in/refresh/sync network failure** (unreachable host, timeout,
  `503` from our backend's auth-verification path, `401`, `409` handled as
  above) — logged via `Debug.LogWarning`, never surfaced to the player,
  never retried within this pass. This is the explicit resolution of
  milestone #3's flagged "client retry/UX contract for `503`" question:
  the contract is "no retry."
- **Corrupt or missing `backend_session.json`** — treated as no session;
  triggers a fresh anonymous sign-in, exactly like `SaveService`'s existing
  corrupt-save handling.
- **`EnsureKingdom` failure at launch** — logged; the coordinator does not
  block scene startup or gameplay, and `EnsureKingdom` is not retried
  automatically. If it never succeeded, every subsequent `PostDecision`
  for that session will get back the backend's `404 No kingdom found for
  this user` (confirmed in `server/src/routes/decisions.ts`) and be
  dropped the same way as any other sync failure — no special-cased
  recovery logic beyond "each sync attempt is independent and
  best-effort."

## Testing

**EditMode (pure logic, no network, no Play Mode):**
- `SessionData.IsExpired` — not expired, expired, exactly at the skew
  boundary.
- `SessionStore` save/load round-trip and corrupt-file handling — same
  pattern as the existing `SaveService` tests.
- `DecisionCycleManager.OnDecisionRecorded` fires exactly once per
  `SubmitRecommendation`, with `DecisionRecord` fields matching the
  resulting `Ruler.State` and the override outcome.
- `DecisionSyncRequestFactory.From` — field-for-field mapping from a
  `DecisionRecord` to the wire DTO.

**PlayMode (real network, external dependency — flagged explicitly, same
as milestone #3's real-Supabase-credentials dependency):** requires
`server/` running locally (`npm run dev` in `server/`) during the test
run. Exercises real anonymous sign-in against the real Supabase project
and real `EnsureKingdom`/`PostDecision` calls against the real local
backend — no mocks on this path, matching this project's established
testing culture.

**Manual verification (definition of done):** run the `CoreLoop` scene in
Play Mode with `server/` running locally; confirm via console logs that
sign-in, kingdom creation, and at least one decision sync round-trip
genuinely succeed against the real running backend — not just green tests
in isolation.

## Explicitly Out of Scope for This Pass

- Google/Apple OAuth or any non-anonymous identity provider.
- Any decision-history-viewing UI, and therefore `GET /api/v1/decisions`
  and its cursor pagination — deferred to FR-06.
- Retry queues, offline-sync-on-reconnect, or any UI indicating sync
  status to the player.
- Deploying `server/` anywhere — the client points at `localhost:3000` for
  this pass.
