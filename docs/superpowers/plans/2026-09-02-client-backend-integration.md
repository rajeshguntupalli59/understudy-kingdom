# Unity Client ↔ Backend Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the Unity client to sign in anonymously with Supabase and sync each decision to the already-built `server/` backend in the background, without ever blocking or altering local gameplay.

**Architecture:** A new `Assets/Scripts/Backend/` folder holds all networking code (`SupabaseAuthClient`, `BackendApiClient`, session storage, DTOs) plus one orchestrating `BackendSyncCoordinator` MonoBehaviour. `DecisionCycleManager` gains a single new event (`OnDecisionRecorded`) that the coordinator subscribes to; it has zero other awareness of the backend. All I/O is `UnityWebRequest` coroutines with callback-based completion.

**Tech Stack:** Unity 6000.3.23f1, C#, Unity Test Framework (EditMode + PlayMode), `UnityEngine.Networking.UnityWebRequest`. No new packages.

## Global Constraints

- Anonymous-only Supabase sign-in — no OAuth/Google/Apple in this pass.
- `UnityWebRequest` (built-in) only — no new Unity package.
- Session persistence (local refresh token) is required, not optional, so identity survives app restarts.
- Real project values (already in `server/.env`, safe to embed client-side — the anon key is the publishable key):
  - `SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co"`
  - `SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw"`
  - `BackendBaseUrl = "http://localhost:3000"` (no deployment in this pass)
- Sync failures of any kind (network error, `401`, `404`, `503`, timeout) are logged via `Debug.LogWarning` and dropped — no retry, no queue, gameplay never blocked.
- A `409` response from `POST /api/v1/decisions` means "already synced" and must be treated as success, not a failure.
- No `GET /api/v1/decisions`, no cursor pagination, no history-viewing UI, no sync-status UI in this pass.
- Unity Editor executable for batch-mode commands: `C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe`.
- Full spec: `docs/superpowers/specs/2026-09-02-client-backend-integration-design.md`.

---

### Task 1: `DecisionRecord` + `DecisionCycleManager.OnDecisionRecorded` event

**Files:**
- Create: `Assets/Scripts/Core/DecisionRecord.cs`
- Modify: `Assets/Scripts/Core/DecisionCycleManager.cs`
- Test: `Assets/Tests/EditMode/DecisionCycleManagerTests.cs`

**Interfaces:**
- Produces: `UnderstudyKingdom.Core.DecisionRecord` (readonly struct: `CycleNumber:int`, `Recommendation:ResourceAllocation`, `Overridden:bool`, `Mood:int`, `Loyalty:int`) and `DecisionCycleManager.OnDecisionRecorded : event Action<DecisionRecord>`, fired at the end of `SubmitRecommendation`. Every later task that touches sync logic consumes this event and this struct.

- [ ] **Step 1: Write the failing test**

Add this test method inside the existing `DecisionCycleManagerTests` class in `Assets/Tests/EditMode/DecisionCycleManagerTests.cs` (it reuses the class's existing `[SetUp]` fixture: `ruler.State = Mood 50, Loyalty 80, Agenda Mercantile`):

```csharp
        [Test]
        public void SubmitRecommendation_FiresOnDecisionRecorded_WithMatchingData()
        {
            DecisionRecord? captured = null;
            manager.OnDecisionRecorded += record => captured = record;

            var allocation = new ResourceAllocation(20, 60, 20); // aligned with Mercantile
            manager.SubmitRecommendation(allocation, roll: 0.99); // low probability (clamped), no override

            Assert.IsTrue(captured.HasValue);
            Assert.AreEqual(1, captured.Value.CycleNumber);
            Assert.AreEqual(20, captured.Value.Recommendation.Army);
            Assert.AreEqual(60, captured.Value.Recommendation.Trade);
            Assert.AreEqual(20, captured.Value.Recommendation.Religion);
            Assert.IsFalse(captured.Value.Overridden);
            Assert.AreEqual(55, captured.Value.Mood);
            Assert.AreEqual(83, captured.Value.Loyalty);
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform EditMode -testFilter "DecisionCycleManagerTests" -testResults "C:\Users\rajes\understudy-kingdom\test-results-task1-fail.xml"
```
Expected: FAIL — `DecisionRecord` does not exist / `OnDecisionRecorded` does not exist (compile error).

- [ ] **Step 3: Create `DecisionRecord`**

`Assets/Scripts/Core/DecisionRecord.cs`:
```csharp
namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// Neutral data produced by DecisionCycleManager.OnDecisionRecorded. Lives in
    /// Core (not Backend) so DecisionCycleManager has zero dependency on networking
    /// code -- see docs/superpowers/specs/2026-09-02-client-backend-integration-design.md.
    /// </summary>
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
}
```

- [ ] **Step 4: Add the event to `DecisionCycleManager`**

In `Assets/Scripts/Core/DecisionCycleManager.cs`, add `using System;` to the usings at the top (alongside the existing `using System.Collections.Generic;`), add a public event field, and invoke it at the end of `SubmitRecommendation`, after the existing `SaveService.Save(Ruler.State);` line and before the `templateTag` line:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.Core
{
    public class DecisionCycleManager : MonoBehaviour
    {
        public RulerNpcController Ruler;

        public event Action<DecisionRecord> OnDecisionRecorded;

        private int currentCycleNumber;

        private void Awake()
        {
            LoadPersistedStateIfPresent();
        }

        public void LoadPersistedStateIfPresent()
        {
            if (Ruler != null && SaveService.HasSave())
            {
                Ruler.State = SaveService.Load();
            }
        }

        public string SubmitRecommendation(ResourceAllocation recommendation, double roll)
        {
            currentCycleNumber++;

            OverrideResult result = OverrideEvaluator.Evaluate(Ruler.State, recommendation, roll);
            Ruler.State.ApplyDelta(result.MoodDelta, result.LoyaltyDelta);
            SaveService.Save(Ruler.State);

            OnDecisionRecorded?.Invoke(new DecisionRecord(
                currentCycleNumber, recommendation, result.Overridden, Ruler.State.Mood, Ruler.State.Loyalty));

            string templateTag = result.Overridden ? "ruler_override" : "ruler_accept";
            var variables = new Dictionary<string, string>
            {
                { "mood", Ruler.State.Mood.ToString() },
                { "loyalty", Ruler.State.Loyalty.ToString() }
            };

            return DialogueTemplateEngine.Resolve(templateTag, variables);
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform EditMode -testFilter "DecisionCycleManagerTests" -testResults "C:\Users\rajes\understudy-kingdom\test-results-task1-pass.xml"
```
Expected: PASS, all `DecisionCycleManagerTests` tests green (the new one plus the 4 pre-existing ones).

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Core/DecisionRecord.cs Assets/Scripts/Core/DecisionRecord.cs.meta Assets/Scripts/Core/DecisionCycleManager.cs Assets/Tests/EditMode/DecisionCycleManagerTests.cs
git commit -m "feat: add DecisionRecord and DecisionCycleManager.OnDecisionRecorded event"
```
(The `.meta` file is generated by the Unity Editor the first time it imports the new script — if it doesn't exist yet after Step 3, open the project in the Editor once, or run any `-batchmode ... -quit` invocation against the project, which triggers asset import.)

---

### Task 2: `SessionData` + `SessionStore`

**Files:**
- Create: `Assets/Scripts/Backend/SessionData.cs`
- Create: `Assets/Scripts/Backend/SessionStore.cs`
- Test: `Assets/Tests/EditMode/SessionDataTests.cs`
- Test: `Assets/Tests/EditMode/SessionStoreTests.cs`

**Interfaces:**
- Consumes: nothing new (this task is standalone).
- Produces: `UnderstudyKingdom.Backend.SessionData` (`[Serializable]` class: `AccessToken:string`, `RefreshToken:string`, `ExpiresAtUnixSeconds:long`, `UserId:string`, `IsExpired(long nowUnixSeconds, long skewSeconds = 60):bool`) and `UnderstudyKingdom.Backend.SessionStore` (static: `Save(SessionData)`, `Load():SessionData` [null if none/corrupt], `Clear()`, `SessionPath:string`). Every later task in `Backend/` consumes both.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/SessionDataTests.cs`:
```csharp
using NUnit.Framework;
using UnderstudyKingdom.Backend;

namespace UnderstudyKingdom.Tests
{
    public class SessionDataTests
    {
        [Test]
        public void IsExpired_WellBeforeExpiry_ReturnsFalse()
        {
            var session = new SessionData { ExpiresAtUnixSeconds = 1000 };
            Assert.IsFalse(session.IsExpired(nowUnixSeconds: 500, skewSeconds: 60));
        }

        [Test]
        public void IsExpired_PastExpiry_ReturnsTrue()
        {
            var session = new SessionData { ExpiresAtUnixSeconds = 1000 };
            Assert.IsTrue(session.IsExpired(nowUnixSeconds: 1500, skewSeconds: 60));
        }

        [Test]
        public void IsExpired_ExactlyAtSkewBoundary_ReturnsTrue()
        {
            var session = new SessionData { ExpiresAtUnixSeconds = 1000 };
            Assert.IsTrue(session.IsExpired(nowUnixSeconds: 940, skewSeconds: 60));
        }

        [Test]
        public void IsExpired_JustOutsideSkewWindow_ReturnsFalse()
        {
            var session = new SessionData { ExpiresAtUnixSeconds = 1000 };
            Assert.IsFalse(session.IsExpired(nowUnixSeconds: 939, skewSeconds: 60));
        }
    }
}
```

`Assets/Tests/EditMode/SessionStoreTests.cs`:
```csharp
using System.IO;
using NUnit.Framework;
using UnderstudyKingdom.Backend;

namespace UnderstudyKingdom.Tests
{
    public class SessionStoreTests
    {
        [TearDown]
        public void Cleanup()
        {
            SessionStore.Clear();
        }

        [Test]
        public void Load_NoFile_ReturnsNull()
        {
            SessionStore.Clear();
            Assert.IsNull(SessionStore.Load());
        }

        [Test]
        public void SaveThenLoad_RoundTripsSession()
        {
            var original = new SessionData
            {
                AccessToken = "access-123",
                RefreshToken = "refresh-456",
                ExpiresAtUnixSeconds = 1234567890,
                UserId = "user-789"
            };

            SessionStore.Save(original);
            var loaded = SessionStore.Load();

            Assert.IsNotNull(loaded);
            Assert.AreEqual("access-123", loaded.AccessToken);
            Assert.AreEqual("refresh-456", loaded.RefreshToken);
            Assert.AreEqual(1234567890, loaded.ExpiresAtUnixSeconds);
            Assert.AreEqual("user-789", loaded.UserId);
        }

        [Test]
        public void Load_CorruptFile_ReturnsNull()
        {
            File.WriteAllText(SessionStore.SessionPath, "not valid json {{{");
            Assert.IsNull(SessionStore.Load());
        }

        [Test]
        public void Clear_RemovesFile()
        {
            SessionStore.Save(new SessionData { AccessToken = "x" });
            SessionStore.Clear();
            Assert.IsFalse(File.Exists(SessionStore.SessionPath));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform EditMode -testFilter "SessionDataTests|SessionStoreTests" -testResults "C:\Users\rajes\understudy-kingdom\test-results-task2-fail.xml"
```
Expected: FAIL — `UnderstudyKingdom.Backend` namespace / `SessionData` / `SessionStore` do not exist.

- [ ] **Step 3: Implement `SessionData`**

`Assets/Scripts/Backend/SessionData.cs`:
```csharp
using System;

namespace UnderstudyKingdom.Backend
{
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
}
```

- [ ] **Step 4: Implement `SessionStore`**

`Assets/Scripts/Backend/SessionStore.cs`:
```csharp
using System;
using System.IO;
using UnityEngine;

namespace UnderstudyKingdom.Backend
{
    /// <summary>
    /// Local JSON persistence for SessionData, mirroring
    /// UnderstudyKingdom.Core.SaveService's exact pattern (Application.persistentDataPath,
    /// JsonUtility, defensive corrupt-file handling -- never throws). See
    /// docs/superpowers/specs/2026-09-02-client-backend-integration-design.md.
    /// </summary>
    public static class SessionStore
    {
        private const string FileName = "backend_session.json";

        public static string SessionPath => Path.Combine(Application.persistentDataPath, FileName);

        public static void Save(SessionData session)
        {
            File.WriteAllText(SessionPath, JsonUtility.ToJson(session));
        }

        public static SessionData Load()
        {
            if (!File.Exists(SessionPath))
            {
                return null;
            }

            try
            {
                string raw = File.ReadAllText(SessionPath);
                string trimmed = raw.TrimStart();

                if (trimmed.Length == 0 || trimmed[0] != '{')
                {
                    return null;
                }

                return JsonUtility.FromJson<SessionData>(raw);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Clear()
        {
            if (File.Exists(SessionPath))
            {
                File.Delete(SessionPath);
            }
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform EditMode -testFilter "SessionDataTests|SessionStoreTests" -testResults "C:\Users\rajes\understudy-kingdom\test-results-task2-pass.xml"
```
Expected: PASS, all 8 tests green.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Backend/SessionData.cs Assets/Scripts/Backend/SessionData.cs.meta Assets/Scripts/Backend/SessionStore.cs Assets/Scripts/Backend/SessionStore.cs.meta Assets/Tests/EditMode/SessionDataTests.cs Assets/Tests/EditMode/SessionStoreTests.cs
git commit -m "feat: add SessionData and SessionStore for local session persistence"
```

---

### Task 3: `DecisionSyncRequest` DTOs + factory

**Files:**
- Create: `Assets/Scripts/Backend/DecisionSyncRequest.cs`
- Test: `Assets/Tests/EditMode/DecisionSyncRequestFactoryTests.cs`

**Interfaces:**
- Consumes: `UnderstudyKingdom.Core.DecisionRecord` (Task 1).
- Produces: `UnderstudyKingdom.Backend.DecisionSyncRequest`, `PlayerRecommendationDto`, `RulerOutcomeDto` (all `[Serializable]`, snake_case fields matching `server/src/routes/decisions.ts`'s JSON schema exactly), and `DecisionSyncRequestFactory.From(DecisionRecord):DecisionSyncRequest`. Task 5 (`BackendApiClient.PostDecision`) and Task 6 (`BackendSyncCoordinator`) consume `DecisionSyncRequestFactory.From`.

- [ ] **Step 1: Write the failing test**

`Assets/Tests/EditMode/DecisionSyncRequestFactoryTests.cs`:
```csharp
using NUnit.Framework;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.Core;
using UnityEngine;

namespace UnderstudyKingdom.Tests
{
    public class DecisionSyncRequestFactoryTests
    {
        [Test]
        public void From_MapsAllFieldsFromDecisionRecord()
        {
            var record = new DecisionRecord(
                cycleNumber: 3,
                recommendation: new ResourceAllocation(20, 50, 30),
                overridden: true,
                mood: 65,
                loyalty: 40);

            DecisionSyncRequest dto = DecisionSyncRequestFactory.From(record);

            Assert.AreEqual(3, dto.cycle_number);
            Assert.AreEqual(20, dto.player_recommendation.army);
            Assert.AreEqual(50, dto.player_recommendation.trade);
            Assert.AreEqual(30, dto.player_recommendation.religion);
            Assert.AreEqual(65, dto.ruler_outcome.mood);
            Assert.AreEqual(40, dto.ruler_outcome.loyalty);
            Assert.IsTrue(dto.overridden);
        }

        [Test]
        public void From_SerializesToExpectedJsonShape()
        {
            var record = new DecisionRecord(1, new ResourceAllocation(40, 30, 30), false, 55, 83);
            DecisionSyncRequest dto = DecisionSyncRequestFactory.From(record);

            string json = JsonUtility.ToJson(dto);

            Assert.IsTrue(json.Contains("\"cycle_number\":1"));
            Assert.IsTrue(json.Contains("\"army\":40"));
            Assert.IsTrue(json.Contains("\"overridden\":false"));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform EditMode -testFilter "DecisionSyncRequestFactoryTests" -testResults "C:\Users\rajes\understudy-kingdom\test-results-task3-fail.xml"
```
Expected: FAIL — `DecisionSyncRequest`/`DecisionSyncRequestFactory` do not exist.

- [ ] **Step 3: Implement the DTOs and factory**

`Assets/Scripts/Backend/DecisionSyncRequest.cs`:
```csharp
using System;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Backend
{
    // snake_case field names are intentional: they must match
    // server/src/routes/decisions.ts's JSON schema exactly since JsonUtility has
    // no attribute-based field renaming.
    [Serializable]
    public class PlayerRecommendationDto
    {
        public int army;
        public int trade;
        public int religion;
    }

    [Serializable]
    public class RulerOutcomeDto
    {
        public int mood;
        public int loyalty;
    }

    [Serializable]
    public class DecisionSyncRequest
    {
        public int cycle_number;
        public PlayerRecommendationDto player_recommendation;
        public RulerOutcomeDto ruler_outcome;
        public bool overridden;
    }

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
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform EditMode -testFilter "DecisionSyncRequestFactoryTests" -testResults "C:\Users\rajes\understudy-kingdom\test-results-task3-pass.xml"
```
Expected: PASS, both tests green.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Backend/DecisionSyncRequest.cs Assets/Scripts/Backend/DecisionSyncRequest.cs.meta Assets/Tests/EditMode/DecisionSyncRequestFactoryTests.cs
git commit -m "feat: add DecisionSyncRequest wire DTOs and factory"
```

---

### Task 4: `SupabaseAuthClient`

**Files:**
- Create: `Assets/Scripts/Backend/SupabaseAuthClient.cs`
- Test: `Assets/Tests/PlayMode/SupabaseAuthClientTests.cs`

**Interfaces:**
- Consumes: `UnderstudyKingdom.Backend.SessionData` (Task 2).
- Produces: `UnderstudyKingdom.Backend.SupabaseAuthClient` (`MonoBehaviour`, public fields `SupabaseUrl:string`, `SupabaseAnonKey:string`; methods `SignInAnonymously(Action<SessionData> onSuccess, Action<string> onError)` and `RefreshSession(string refreshToken, Action<SessionData> onSuccess, Action<string> onError)`). Tasks 6 and 7 (coordinator, scene wiring) consume this type and both methods.

**External dependency:** this task's test requires real internet access — it hits the actual Supabase project at `https://kszwkvxtnzbbndclpbbe.supabase.co`, the same one `server/.env` already points at. No mock.

- [ ] **Step 1: Write the failing test**

`Assets/Tests/PlayMode/SupabaseAuthClientTests.cs`:
```csharp
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
```

- [ ] **Step 2: Run test to verify it fails**

Run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform PlayMode -testFilter "SupabaseAuthClientTests" -testResults "C:\Users\rajes\understudy-kingdom\test-results-task4-fail.xml"
```
Expected: FAIL — `SupabaseAuthClient` does not exist.

- [ ] **Step 3: Implement `SupabaseAuthClient`**

`Assets/Scripts/Backend/SupabaseAuthClient.cs`:
```csharp
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
```

- [ ] **Step 4: Run test to verify it passes**

Run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform PlayMode -testFilter "SupabaseAuthClientTests" -testResults "C:\Users\rajes\understudy-kingdom\test-results-task4-pass.xml"
```
Expected: PASS, both tests green (requires internet access from the machine running this).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Backend/SupabaseAuthClient.cs Assets/Scripts/Backend/SupabaseAuthClient.cs.meta Assets/Tests/PlayMode/SupabaseAuthClientTests.cs
git commit -m "feat: add SupabaseAuthClient for anonymous sign-in and refresh"
```

---

### Task 5: `BackendApiClient`

**Files:**
- Create: `Assets/Scripts/Backend/BackendApiClient.cs`
- Test: `Assets/Tests/PlayMode/BackendApiClientTests.cs`

**Interfaces:**
- Consumes: `UnderstudyKingdom.Backend.DecisionSyncRequest` (Task 3), `UnderstudyKingdom.Backend.SupabaseAuthClient` (Task 4, used only in this task's own test setup to obtain a real token).
- Produces: `UnderstudyKingdom.Backend.BackendApiClient` (`MonoBehaviour`, public field `BackendBaseUrl:string`; methods `EnsureKingdom(string accessToken, Action onSuccess, Action<string> onError)` and `PostDecision(string accessToken, DecisionSyncRequest dto, Action onSuccess, Action<string> onError)`). Task 6 consumes both methods.

**External dependency:** this task's test requires `server/` running locally. Before running the test step below, start it in a separate terminal: `cd server && npm run dev` (must show it listening on port 3000).

- [ ] **Step 1: Write the failing test**

`Assets/Tests/PlayMode/BackendApiClientTests.cs`:
```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Tests
{
    /// <summary>
    /// Hits a REAL locally-running server/ instance (npm run dev in server/, listening
    /// on http://localhost:3000) authenticated with a REAL Supabase-issued anonymous
    /// token -- no mocks, matching this project's established testing culture. Requires
    /// server/ to be running locally before this test suite executes.
    /// </summary>
    public class BackendApiClientTests
    {
        private GameObject authClientObject;
        private GameObject apiClientObject;
        private SupabaseAuthClient authClient;
        private BackendApiClient apiClient;
        private string accessToken;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            authClientObject = new GameObject("SupabaseAuthClient");
            authClient = authClientObject.AddComponent<SupabaseAuthClient>();
            authClient.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            authClient.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";

            apiClientObject = new GameObject("BackendApiClient");
            apiClient = apiClientObject.AddComponent<BackendApiClient>();
            apiClient.BackendBaseUrl = "http://localhost:3000";

            SessionData session = null;
            authClient.SignInAnonymously(s => session = s, err => Assert.Fail($"Sign-in failed: {err}"));
            yield return new WaitUntil(() => session != null);
            accessToken = session.AccessToken;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(authClientObject);
            Object.DestroyImmediate(apiClientObject);
        }

        [UnityTest]
        public IEnumerator EnsureKingdom_WithValidToken_Succeeds()
        {
            bool succeeded = false;
            string error = null;

            apiClient.EnsureKingdom(accessToken, () => succeeded = true, err => error = err);
            yield return new WaitUntil(() => succeeded || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.IsTrue(succeeded);
        }

        [UnityTest]
        public IEnumerator PostDecision_AfterEnsureKingdom_Succeeds()
        {
            bool kingdomReady = false;
            apiClient.EnsureKingdom(accessToken, () => kingdomReady = true, err => Assert.Fail($"EnsureKingdom failed: {err}"));
            yield return new WaitUntil(() => kingdomReady);

            // Each test run signs in as a brand-new anonymous user (new kingdom), so
            // this cycle_number never actually collides with a prior run -- but
            // PostDecision treats 409 as success regardless (see Step 3 below), so
            // this assertion holds either way.
            var record = new DecisionRecord(
                cycleNumber: 1,
                recommendation: new ResourceAllocation(40, 30, 30),
                overridden: false,
                mood: 55,
                loyalty: 60);
            var dto = DecisionSyncRequestFactory.From(record);

            bool succeeded = false;
            string error = null;
            apiClient.PostDecision(accessToken, dto, () => succeeded = true, err => error = err);
            yield return new WaitUntil(() => succeeded || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.IsTrue(succeeded);
        }

        [UnityTest]
        public IEnumerator PostDecision_DuplicateCycleNumber_StillReportsSuccess()
        {
            bool kingdomReady = false;
            apiClient.EnsureKingdom(accessToken, () => kingdomReady = true, err => Assert.Fail($"EnsureKingdom failed: {err}"));
            yield return new WaitUntil(() => kingdomReady);

            var record = new DecisionRecord(2, new ResourceAllocation(40, 30, 30), false, 55, 60);
            var dto = DecisionSyncRequestFactory.From(record);

            bool firstSucceeded = false;
            apiClient.PostDecision(accessToken, dto, () => firstSucceeded = true, err => Assert.Fail($"First post failed: {err}"));
            yield return new WaitUntil(() => firstSucceeded);

            bool secondSucceeded = false;
            string secondError = null;
            apiClient.PostDecision(accessToken, dto, () => secondSucceeded = true, err => secondError = err);
            yield return new WaitUntil(() => secondSucceeded || secondError != null);

            Assert.IsNull(secondError, $"Expected 409 to be treated as success, got error: {secondError}");
            Assert.IsTrue(secondSucceeded);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

With `server/` running locally (`cd server && npm run dev`), run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform PlayMode -testFilter "BackendApiClientTests" -testResults "C:\Users\rajes\understudy-kingdom\test-results-task5-fail.xml"
```
Expected: FAIL — `BackendApiClient` does not exist.

- [ ] **Step 3: Implement `BackendApiClient`**

`Assets/Scripts/Backend/BackendApiClient.cs`:
```csharp
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
                acceptedStatusCodes: null, onSuccess, onError));
        }

        public void PostDecision(string accessToken, DecisionSyncRequest dto, Action onSuccess, Action<string> onError)
        {
            string body = JsonUtility.ToJson(dto);
            StartCoroutine(Post($"{BackendBaseUrl}/api/v1/decisions", body, accessToken,
                acceptedStatusCodes: new long[] { 409 }, onSuccess, onError));
        }

        private IEnumerator Post(string url, string jsonBody, string accessToken,
            long[] acceptedStatusCodes, Action onSuccess, Action<string> onError)
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
                onSuccess?.Invoke();
                yield break;
            }

            onError?.Invoke($"Backend request to {url} failed: {request.result} ({request.responseCode})");
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

With `server/` still running locally, run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform PlayMode -testFilter "BackendApiClientTests" -testResults "C:\Users\rajes\understudy-kingdom\test-results-task5-pass.xml"
```
Expected: PASS, all 3 tests green.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Backend/BackendApiClient.cs Assets/Scripts/Backend/BackendApiClient.cs.meta Assets/Tests/PlayMode/BackendApiClientTests.cs
git commit -m "feat: add BackendApiClient for kingdom ensure and decision sync"
```

---

### Task 6: `BackendSyncCoordinator`

**Files:**
- Create: `Assets/Scripts/Backend/BackendSyncCoordinator.cs`
- Test: `Assets/Tests/PlayMode/BackendSyncCoordinatorTests.cs`

**Interfaces:**
- Consumes: `DecisionCycleManager.OnDecisionRecorded` + `DecisionRecord` (Task 1), `SessionStore`/`SessionData` (Task 2), `DecisionSyncRequestFactory` (Task 3), `SupabaseAuthClient` (Task 4), `BackendApiClient` (Task 5).
- Produces: `UnderstudyKingdom.Backend.BackendSyncCoordinator` (`MonoBehaviour`, public fields `SupabaseUrl:string`, `SupabaseAnonKey:string`, `BackendBaseUrl:string`, `DecisionCycleManager:DecisionCycleManager`). Task 7 (scene wiring) instantiates and configures this component.

**External dependency:** same as Task 5 — requires `server/` running locally (`cd server && npm run dev`).

- [ ] **Step 1: Write the failing test**

`Assets/Tests/PlayMode/BackendSyncCoordinatorTests.cs`:
```csharp
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.Tests
{
    /// <summary>
    /// End-to-end smoke test: real Supabase anonymous sign-in + real locally-running
    /// server/ (same external dependency as BackendApiClientTests). The coordinator's
    /// design is deliberately fire-and-forget with no observable sync-status (see the
    /// design spec's Scope Decisions), so this test can only confirm the wiring runs
    /// without an unhandled error -- Unity's Test Framework fails a PlayMode test
    /// automatically on any uncaught Debug.LogError, which is the signal this relies on.
    /// </summary>
    public class BackendSyncCoordinatorTests
    {
        private GameObject rulerObject;
        private GameObject managerObject;
        private GameObject coordinatorObject;
        private DecisionCycleManager manager;

        [SetUp]
        public void SetUp()
        {
            rulerObject = new GameObject("Ruler");
            var ruler = rulerObject.AddComponent<RulerNpcController>();

            managerObject = new GameObject("Manager");
            manager = managerObject.AddComponent<DecisionCycleManager>();
            manager.Ruler = ruler;

            coordinatorObject = new GameObject("Coordinator");
            var coordinator = coordinatorObject.AddComponent<BackendSyncCoordinator>();
            coordinator.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            coordinator.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";
            coordinator.BackendBaseUrl = "http://localhost:3000";
            coordinator.DecisionCycleManager = manager;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(coordinatorObject);
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(rulerObject);

            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }
            SessionStore.Clear();
        }

        [UnityTest]
        public IEnumerator SubmitRecommendation_AfterCoordinatorStarts_SyncsWithoutUnhandledErrors()
        {
            // Coordinator.Start() (session bootstrap + EnsureKingdom) runs on the next
            // frame after AddComponent; wait for it before submitting a decision.
            yield return new WaitForSeconds(2f);

            var allocation = new ResourceAllocation(40, 30, 30);
            manager.SubmitRecommendation(allocation, roll: 0.99);

            // PostDecision is fire-and-forget; give the real network round-trip time
            // to complete before the test (and its teardown) ends.
            yield return new WaitForSeconds(2f);
        }
    }
}
```

Note: `[UnityTest]` methods returning `IEnumerator` need no `[Test]`/`[UnitySetUp]` attribute beyond what's shown — `UnityEngine.TestTools` (already referenced via the `UnityEngine.TestRunner`/`UnityEditor.TestRunner` entries in `Assets/Tests/PlayMode/UnderstudyKingdom.PlayModeTests.asmdef`) supplies `[UnityTest]`; add `using UnityEngine.TestTools;` to the test file's usings.

- [ ] **Step 2: Run test to verify it fails**

With `server/` running locally, run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform PlayMode -testFilter "BackendSyncCoordinatorTests" -testResults "C:\Users\rajes\understudy-kingdom\test-results-task6-fail.xml"
```
Expected: FAIL — `BackendSyncCoordinator` does not exist.

- [ ] **Step 3: Implement `BackendSyncCoordinator`**

`Assets/Scripts/Backend/BackendSyncCoordinator.cs`:
```csharp
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
                authClient.RefreshSession(stored.RefreshToken, HandleSessionObtained, HandleSessionError);
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
                onSuccess: () => { },
                onError: err => Debug.LogWarning($"BackendSyncCoordinator: EnsureKingdom failed: {err}"));

            if (DecisionCycleManager != null)
            {
                DecisionCycleManager.OnDecisionRecorded += HandleDecisionRecorded;
            }
        }

        private void HandleDecisionRecorded(DecisionRecord record)
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

        private void SyncDecision(DecisionRecord record)
        {
            DecisionSyncRequest dto = DecisionSyncRequestFactory.From(record);
            apiClient.PostDecision(currentSession.AccessToken, dto,
                onSuccess: () => { },
                onError: err => Debug.LogWarning($"BackendSyncCoordinator: decision sync failed for cycle {record.CycleNumber}: {err}"));
        }

        private void OnDestroy()
        {
            if (DecisionCycleManager != null)
            {
                DecisionCycleManager.OnDecisionRecorded -= HandleDecisionRecorded;
            }
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

With `server/` still running locally, run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform PlayMode -testFilter "BackendSyncCoordinatorTests" -testResults "C:\Users\rajes\understudy-kingdom\test-results-task6-pass.xml"
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Backend/BackendSyncCoordinator.cs Assets/Scripts/Backend/BackendSyncCoordinator.cs.meta Assets/Tests/PlayMode/BackendSyncCoordinatorTests.cs
git commit -m "feat: add BackendSyncCoordinator orchestrating session bootstrap and decision sync"
```

---

### Task 7: Scene wiring + manual end-to-end verification

**Files:**
- Modify: `Assets/Editor/CoreLoopSceneBuilder.cs`
- Modify (regenerated): `Assets/Scenes/CoreLoop.unity`

**Interfaces:**
- Consumes: `BackendSyncCoordinator` (Task 6).
- Produces: nothing further consumed by later tasks — this is the final task.

- [ ] **Step 1: Add `BackendSyncCoordinator` wiring to `CoreLoopSceneBuilder.Build()`**

In `Assets/Editor/CoreLoopSceneBuilder.cs`, add `using UnderstudyKingdom.Backend;` to the usings block at the top (alongside the existing `using UnderstudyKingdom.Core;` etc.), and insert this block into `Build()` immediately after the existing `manager.Ruler = ruler;` line (before the `canvasObject` creation):

```csharp
            var backendCoordinatorObject = new GameObject("BackendSyncCoordinator");
            var backendCoordinator = backendCoordinatorObject.AddComponent<BackendSyncCoordinator>();
            backendCoordinator.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            backendCoordinator.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";
            backendCoordinator.BackendBaseUrl = "http://localhost:3000";
            backendCoordinator.DecisionCycleManager = manager;
```

- [ ] **Step 2: Regenerate `CoreLoop.unity`**

Run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Build -projectPath "C:\Users\rajes\understudy-kingdom" -quit
```
Expected: console output ends with `CoreLoopSceneBuilder: saved scene to Assets/Scenes/CoreLoop.unity`, no errors.

- [ ] **Step 3: Verify the scene via the existing sanity check**

Run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Verify -projectPath "C:\Users\rajes\understudy-kingdom" -quit
```
Expected: `CoreLoopSceneBuilder.Verify: scene opened and controller found successfully.`, exit code 0.

- [ ] **Step 4: Run the full EditMode + PlayMode suites**

With `server/` running locally, run both:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform EditMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-final-editmode.xml"
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform PlayMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-final-playmode.xml"
```
Expected: PASS, zero failures in both result files.

- [ ] **Step 5: Manual verification — real Play Mode run against the real local backend**

With `server/` running locally (`cd server && npm run dev`, confirm it logs listening on port 3000):
1. Open the project in the Unity Editor (not batch mode): launch `C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe` with `-projectPath "C:\Users\rajes\understudy-kingdom"` (no `-batchmode`).
2. Open `Assets/Scenes/CoreLoop.unity`, press Play.
3. In the Console window, confirm there is no `BackendSyncCoordinator: session bootstrap failed` warning (a successful sign-in produces no log by design — absence of the warning is the signal).
4. Move a slider and click "Submit Recommendation" at least once.
5. In a separate terminal, confirm the decision landed server-side:
   ```bash
   curl -s http://localhost:3000/health
   ```
   should return `{"status":"ok"}` (or equivalent) confirming the server is reachable — for a definitive per-decision check, tail the `server/` process's own request log (Fastify logs each request by default) and confirm a `POST /api/v1/decisions` with a `201` or `409` status appears immediately after clicking Submit.
6. Stop Play Mode.

Record the result of this manual check in the final report before moving to the whole-branch review — this is the project's established "definition of done" bar (see milestone #3), not just green automated tests.

- [ ] **Step 6: Commit**

```bash
git add Assets/Editor/CoreLoopSceneBuilder.cs Assets/Scenes/CoreLoop.unity
git commit -m "feat: wire BackendSyncCoordinator into the CoreLoop scene"
```
