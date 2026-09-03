# Relationship History Log Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A player can tap "View History" and see up to 10 of their own most recent decisions (cycle number, allocation, outcome, mood/loyalty), using `server/`'s existing `GET /api/v1/decisions` endpoint, which has been built and tested since milestone #3 but never called by the client.

**Architecture:** Entirely client-side. A new GET-based `BackendApiClient` method and a new `BackendSyncCoordinator.RequestHistory` method (deliberately copying `RequestDuel`'s corrected refresh-then-kingdomReady ordering, not reinventing it) feed a new modal `HistoryPanelController` showing a single fixed page of pre-created row labels — no scrolling, no dynamic instantiation, no "Load More."

**Tech Stack:** Unity 6000.3.23f1, C#, Unity Test Framework, `UnityEngine.Networking.UnityWebRequest`. No server-side changes, no new packages.

## Global Constraints

- This is a client-only milestone — no changes to `server/` anywhere in this plan.
- The panel is modal: while open, the 3 sliders + Submit + Challenge buttons are non-interactive.
- Single fixed page, up to 10 rows, pre-created labels — no `ScrollRect`, no dynamic row instantiation, no "Load More," no use of the response's `nextCursor`.
- `RequestHistory` must copy `RequestDuel`'s CURRENT (post-fix) structure exactly: session refresh-if-needed runs unconditionally first, then the `kingdomReady` gate/retry, then the send. This ordering was a real bug in milestone #5, found and fixed after two review rounds — do not reintroduce the original (buggy) ordering by writing this from scratch without reference to the fixed code.
- A `404` "No kingdom found for this user" and a `200` with an empty `decisions` array are both shown to the player as the same friendly message: "No decisions yet -- submit your first recommendation!"
- Any other error is shown to the player verbatim (reusing `BackendApiClient`'s existing `TryExtractServerErrorMessage` helper, already shared/private in that class — do not duplicate it).
- Never pass `-quit` alongside Unity's `-runTests` (confirmed repeatedly across this project that the combination makes the Editor exit before the test runner ever executes, silently producing no results with exit code 0).
- Unity Editor executable: `C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe`.
- `server/` must be running locally (`npm run dev`, port 3000) for any PlayMode test hitting the real backend.
- Full spec: `docs/superpowers/specs/2026-09-02-decision-history-design.md`.

---

### Task 1: `DecisionHistoryResponse` DTOs

**Files:**
- Create: `Assets/Scripts/Backend/DecisionHistoryResponse.cs`
- Test: `Assets/Tests/EditMode/DecisionHistoryResponseTests.cs`

**Interfaces:**
- Consumes: `PlayerRecommendationDto`/`RulerOutcomeDto` (already defined in `Assets/Scripts/Backend/DecisionSyncRequest.cs`, reused directly — the server stores those jsonb blobs verbatim as originally sent, so the nested shape is identical here).
- Produces: `DecisionHistoryEntry` (`{ int cycleNumber; PlayerRecommendationDto playerRecommendation; RulerOutcomeDto rulerOutcome; bool overridden; }`) and `DecisionHistoryResponse` (`{ DecisionHistoryEntry[] decisions; }`). Task 2 consumes both.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/DecisionHistoryResponseTests.cs`:
```csharp
using NUnit.Framework;
using UnderstudyKingdom.Backend;
using UnityEngine;

namespace UnderstudyKingdom.Tests
{
    public class DecisionHistoryResponseTests
    {
        [Test]
        public void DecisionHistoryResponse_DeserializesFromServerResponseShape()
        {
            string json = "{\"decisions\":[" +
                "{\"cycleNumber\":2,\"playerRecommendation\":{\"army\":40,\"trade\":30,\"religion\":30}," +
                "\"rulerOutcome\":{\"mood\":55,\"loyalty\":60},\"overridden\":false}," +
                "{\"cycleNumber\":1,\"playerRecommendation\":{\"army\":70,\"trade\":15,\"religion\":15}," +
                "\"rulerOutcome\":{\"mood\":40,\"loyalty\":45},\"overridden\":true}" +
                "],\"nextCursor\":null}";

            DecisionHistoryResponse response = JsonUtility.FromJson<DecisionHistoryResponse>(json);

            Assert.IsNotNull(response.decisions);
            Assert.AreEqual(2, response.decisions.Length);

            Assert.AreEqual(2, response.decisions[0].cycleNumber);
            Assert.AreEqual(40, response.decisions[0].playerRecommendation.army);
            Assert.AreEqual(55, response.decisions[0].rulerOutcome.mood);
            Assert.IsFalse(response.decisions[0].overridden);

            Assert.AreEqual(1, response.decisions[1].cycleNumber);
            Assert.IsTrue(response.decisions[1].overridden);
        }

        [Test]
        public void DecisionHistoryResponse_EmptyDecisionsArray_DeserializesToZeroLengthArray()
        {
            string json = "{\"decisions\":[],\"nextCursor\":null}";

            DecisionHistoryResponse response = JsonUtility.FromJson<DecisionHistoryResponse>(json);

            Assert.IsNotNull(response.decisions);
            Assert.AreEqual(0, response.decisions.Length);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform EditMode -testFilter "DecisionHistoryResponseTests" -testResults "C:\Users\rajes\understudy-kingdom\test-results-task1.xml"
```
Expected: FAIL — `DecisionHistoryResponse`/`DecisionHistoryEntry` do not exist (compile error).

- [ ] **Step 3: Create the DTOs**

`Assets/Scripts/Backend/DecisionHistoryResponse.cs`:
```csharp
using System;

namespace UnderstudyKingdom.Backend
{
    // Reuses PlayerRecommendationDto/RulerOutcomeDto from DecisionSyncRequest.cs --
    // the server stores those jsonb blobs verbatim as originally sent by
    // DecisionSyncRequestFactory, so the nested shape is identical here.
    [Serializable]
    public class DecisionHistoryEntry
    {
        public int cycleNumber;
        public PlayerRecommendationDto playerRecommendation;
        public RulerOutcomeDto rulerOutcome;
        public bool overridden;
    }

    [Serializable]
    public class DecisionHistoryResponse
    {
        public DecisionHistoryEntry[] decisions;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run the same command as Step 2 (new results file name, e.g. `test-results-task1-pass.xml`).
Expected: PASS, both tests green.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Backend/DecisionHistoryResponse.cs Assets/Scripts/Backend/DecisionHistoryResponse.cs.meta Assets/Tests/EditMode/DecisionHistoryResponseTests.cs
git commit -m "feat: add DecisionHistoryResponse DTOs"
```

---

### Task 2: `BackendApiClient.GetDecisionHistory`

**Files:**
- Modify: `Assets/Scripts/Backend/BackendApiClient.cs`
- Test: `Assets/Tests/PlayMode/BackendApiClientHistoryTests.cs`

**Interfaces:**
- Consumes: `DecisionHistoryResponse`/`DecisionHistoryEntry` (Task 1), the existing private `TryExtractServerErrorMessage(string)` helper already in `BackendApiClient.cs` (added in milestone #5 — reuse directly, do not duplicate).
- Produces: `BackendApiClient.GetDecisionHistory(string accessToken, int limit, Action<DecisionHistoryEntry[]> onSuccess, Action<string> onError)`. Task 3 consumes this.

**External dependency:** requires `server/` running locally and real internet access (hits the real Supabase project).

- [ ] **Step 1: Write the failing test**

`Assets/Tests/PlayMode/BackendApiClientHistoryTests.cs`:
```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnderstudyKingdom.Backend;

namespace UnderstudyKingdom.Tests
{
    /// <summary>
    /// Hits the REAL local server/ and the REAL Supabase project -- no mocks.
    /// Submits real decisions via the existing PostDecision, then fetches history
    /// and asserts the real returned entries match what was submitted.
    /// </summary>
    public class BackendApiClientHistoryTests
    {
        private GameObject authObject;
        private GameObject apiClientObject;
        private string accessToken;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            authObject = new GameObject("Auth");
            var auth = authObject.AddComponent<SupabaseAuthClient>();
            auth.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            auth.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";

            apiClientObject = new GameObject("BackendApiClient");
            var apiClient = apiClientObject.AddComponent<BackendApiClient>();
            apiClient.BackendBaseUrl = "http://localhost:3000";

            SessionData session = null;
            auth.SignInAnonymously(s => session = s, err => Assert.Fail($"Sign-in failed: {err}"));
            yield return new WaitUntil(() => session != null);
            accessToken = session.AccessToken;

            bool kingdomReady = false;
            apiClient.EnsureKingdom(accessToken, () => kingdomReady = true, err => Assert.Fail($"EnsureKingdom failed: {err}"));
            yield return new WaitUntil(() => kingdomReady);

            var dto1 = new DecisionSyncRequest
            {
                cycle_number = 1,
                player_recommendation = new PlayerRecommendationDto { army = 70, trade = 15, religion = 15 },
                ruler_outcome = new RulerOutcomeDto { mood = 40, loyalty = 45 },
                overridden = true
            };
            bool decision1Posted = false;
            apiClient.PostDecision(accessToken, dto1, _ => decision1Posted = true, err => Assert.Fail($"PostDecision(1) failed: {err}"));
            yield return new WaitUntil(() => decision1Posted);

            var dto2 = new DecisionSyncRequest
            {
                cycle_number = 2,
                player_recommendation = new PlayerRecommendationDto { army = 40, trade = 30, religion = 30 },
                ruler_outcome = new RulerOutcomeDto { mood = 55, loyalty = 60 },
                overridden = false
            };
            bool decision2Posted = false;
            apiClient.PostDecision(accessToken, dto2, _ => decision2Posted = true, err => Assert.Fail($"PostDecision(2) failed: {err}"));
            yield return new WaitUntil(() => decision2Posted);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(authObject);
            Object.DestroyImmediate(apiClientObject);
        }

        [UnityTest]
        public IEnumerator GetDecisionHistory_WithValidToken_ReturnsRealSubmittedDecisions()
        {
            var apiClient = apiClientObject.GetComponent<BackendApiClient>();

            DecisionHistoryEntry[] result = null;
            string error = null;
            apiClient.GetDecisionHistory(accessToken, 10, r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Length);

            // Newest-first per the server's ORDER BY createdAt DESC.
            Assert.AreEqual(2, result[0].cycleNumber);
            Assert.AreEqual(40, result[0].playerRecommendation.army);
            Assert.AreEqual(55, result[0].rulerOutcome.mood);
            Assert.IsFalse(result[0].overridden);

            Assert.AreEqual(1, result[1].cycleNumber);
            Assert.IsTrue(result[1].overridden);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

With `server/` running locally, run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform PlayMode -testFilter "BackendApiClientHistoryTests" -testResults "C:\Users\rajes\understudy-kingdom\test-results-task2-fail.xml"
```
Expected: FAIL — `GetDecisionHistory` does not exist (compile error).

- [ ] **Step 3: Add `GetDecisionHistory` to `BackendApiClient`**

In `Assets/Scripts/Backend/BackendApiClient.cs`, add this method and this private coroutine (alongside the existing `EnsureKingdom`/`PostDecision`/`PostDuel`/`Post`/`SendDuelRequest`/`TryExtractServerErrorMessage` — none of which change):

```csharp
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
```

- [ ] **Step 4: Run the test to verify it passes**

With `server/` still running locally, run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform PlayMode -testFilter "BackendApiClientHistoryTests" -testResults "C:\Users\rajes\understudy-kingdom\test-results-task2-pass.xml"
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Backend/BackendApiClient.cs Assets/Tests/PlayMode/BackendApiClientHistoryTests.cs
git commit -m "feat: add BackendApiClient.GetDecisionHistory"
```

---

### Task 3: `BackendSyncCoordinator.RequestHistory`

**Files:**
- Modify: `Assets/Scripts/Backend/BackendSyncCoordinator.cs`
- Test: `Assets/Tests/PlayMode/BackendSyncCoordinatorHistoryTests.cs`

**Interfaces:**
- Consumes: `BackendApiClient.GetDecisionHistory` (Task 2).
- Produces: `BackendSyncCoordinator.RequestHistory(int limit, Action<DecisionHistoryEntry[]> onSuccess, Action<string> onError)`. Task 5 consumes this.

**Design note carried into this task:** `RequestHistory` must be written directly against `RequestDuel`'s CURRENT structure (refresh-if-needed unconditionally first, then the shared `kingdomReady` gate/retry, then the send) — not a fresh reinvention of that same ordering decision. Milestone #5 shipped the opposite ordering once, found it was a real bug (a stale token could be used against `EnsureKingdom` when `kingdomReady` was false and the session was expired), and fixed it after two review rounds. Read `RequestDuel`'s and `EnsureKingdomThenSendDuel`'s current code in `BackendSyncCoordinator.cs` before writing this task's code and follow the same shape.

**External dependency:** requires `server/` running locally and real internet access.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/PlayMode/BackendSyncCoordinatorHistoryTests.cs`:
```csharp
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.Tests
{
    public class BackendSyncCoordinatorHistoryTests
    {
        private GameObject rulerObject;
        private GameObject managerObject;
        private GameObject coordinatorObject;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            rulerObject = new GameObject("Ruler");
            var ruler = rulerObject.AddComponent<RulerNpcController>();

            managerObject = new GameObject("Manager");
            var manager = managerObject.AddComponent<DecisionCycleManager>();
            manager.Ruler = ruler;

            coordinatorObject = new GameObject("Coordinator");
            var coordinator = coordinatorObject.AddComponent<BackendSyncCoordinator>();
            coordinator.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            coordinator.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";
            coordinator.BackendBaseUrl = "http://localhost:3000";
            coordinator.DecisionCycleManager = manager;

            yield return new WaitForSeconds(2f);
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
        public IEnumerator RequestHistory_WithReadySession_ReturnsWellFormedResult()
        {
            var coordinator = coordinatorObject.GetComponent<BackendSyncCoordinator>();

            DecisionHistoryEntry[] result = null;
            string error = null;
            coordinator.RequestHistory(10, r => result = r, err => error = err);
            yield return new WaitUntil(() => result != null || error != null);

            Assert.IsNull(error, $"Expected success, got error: {error}");
            Assert.IsNotNull(result);
            // A brand-new kingdom (just created by EnsureKingdom during bootstrap)
            // has no decisions yet -- an empty array is the correct, well-formed result.
            Assert.AreEqual(0, result.Length);
        }

        /// <summary>
        /// Same technique as BackendSyncCoordinatorDuelTests's
        /// RequestDuel_CalledBeforeKingdomReady_ExercisesRetryPathAndSettles (see that
        /// test's own doc comment for the full rationale) -- proves RequestHistory's
        /// separate implementation of the same refresh-then-kingdomReady ordering
        /// doesn't reintroduce the bug that was found and fixed in RequestDuel.
        /// </summary>
        [UnityTest]
        public IEnumerator RequestHistory_CalledBeforeKingdomReady_ExercisesRetryPathAndSettles()
        {
            var freshCoordinatorObject = new GameObject("FreshCoordinator");
            try
            {
                var coordinator = freshCoordinatorObject.AddComponent<BackendSyncCoordinator>();
                coordinator.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
                coordinator.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";
                coordinator.BackendBaseUrl = "http://localhost:3000";

                FieldInfo sessionField = typeof(BackendSyncCoordinator).GetField("currentSession", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo kingdomReadyField = typeof(BackendSyncCoordinator).GetField("kingdomReady", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(sessionField, "currentSession field not found -- BackendSyncCoordinator internals changed");
                Assert.IsNotNull(kingdomReadyField, "kingdomReady field not found -- BackendSyncCoordinator internals changed");

                float deadline = Time.realtimeSinceStartup + 10f;
                while (sessionField.GetValue(coordinator) == null && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                bool sessionWasReady = sessionField.GetValue(coordinator) != null;
                bool observedRetryWindow = sessionWasReady && !(bool)kingdomReadyField.GetValue(coordinator);

                DecisionHistoryEntry[] result = null;
                string error = null;
                coordinator.RequestHistory(10, r => result = r, err => error = err);

                yield return new WaitUntil(() => result != null || error != null);

                Assert.IsTrue(result != null || error != null, "RequestHistory never settled");

                if (observedRetryWindow)
                {
                    Assert.IsNull(error, $"Expected the kingdomReady retry path to succeed, got error: {error}");
                    Assert.IsNotNull(result);
                }
                else if (!sessionWasReady)
                {
                    Assert.AreEqual("No session available yet -- try again in a moment.", error);
                }
            }
            finally
            {
                Object.DestroyImmediate(freshCoordinatorObject);
            }
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

With `server/` running locally, run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform PlayMode -testFilter "BackendSyncCoordinatorHistoryTests" -testResults "C:\Users\rajes\understudy-kingdom\test-results-task3-fail.xml"
```
Expected: FAIL — `RequestHistory` does not exist (compile error).

- [ ] **Step 3: Add `RequestHistory` to `BackendSyncCoordinator`**

In `Assets/Scripts/Backend/BackendSyncCoordinator.cs`, add these two methods (all existing methods, including `RequestDuel`/`EnsureKingdomThenSendDuel`/`SendDuelRequest`, are unchanged):

```csharp
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
```

(This reuses the same `kingdomReady` field `RequestDuel` already maintains — a single shared flag, not a duplicate.)

- [ ] **Step 4: Run the tests to verify they pass**

With `server/` still running locally, run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform PlayMode -testFilter "BackendSyncCoordinatorHistoryTests" -testResults "C:\Users\rajes\understudy-kingdom\test-results-task3-pass.xml"
```
Expected: PASS, both tests green.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Backend/BackendSyncCoordinator.cs Assets/Tests/PlayMode/BackendSyncCoordinatorHistoryTests.cs
git commit -m "feat: add BackendSyncCoordinator.RequestHistory"
```

---

### Task 4: `HistoryRowFormatter`

**Files:**
- Create: `Assets/Scripts/UI/HistoryRowFormatter.cs`
- Test: `Assets/Tests/EditMode/HistoryRowFormatterTests.cs`

**Interfaces:**
- Consumes: `DecisionHistoryEntry` (Task 1).
- Produces: `HistoryRowFormatter.Format(DecisionHistoryEntry entry) : string`. Task 5 consumes this.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/HistoryRowFormatterTests.cs`:
```csharp
using NUnit.Framework;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    public class HistoryRowFormatterTests
    {
        [Test]
        public void Format_AcceptedDecision_ProducesExpectedLine()
        {
            var entry = new DecisionHistoryEntry
            {
                cycleNumber = 2,
                playerRecommendation = new PlayerRecommendationDto { army = 40, trade = 30, religion = 30 },
                rulerOutcome = new RulerOutcomeDto { mood = 55, loyalty = 60 },
                overridden = false
            };

            string result = HistoryRowFormatter.Format(entry);

            Assert.AreEqual("Cycle 2: Army 40 / Trade 30 / Religion 30 -> Accepted (Mood 55, Loyalty 60)", result);
        }

        [Test]
        public void Format_OverriddenDecision_ProducesExpectedLine()
        {
            var entry = new DecisionHistoryEntry
            {
                cycleNumber = 1,
                playerRecommendation = new PlayerRecommendationDto { army = 70, trade = 15, religion = 15 },
                rulerOutcome = new RulerOutcomeDto { mood = 40, loyalty = 45 },
                overridden = true
            };

            string result = HistoryRowFormatter.Format(entry);

            Assert.AreEqual("Cycle 1: Army 70 / Trade 15 / Religion 15 -> Overridden (Mood 40, Loyalty 45)", result);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform EditMode -testFilter "HistoryRowFormatterTests" -testResults "C:\Users\rajes\understudy-kingdom\test-results-task4-fail.xml"
```
Expected: FAIL — `HistoryRowFormatter` does not exist (compile error).

- [ ] **Step 3: Create `HistoryRowFormatter`**

`Assets/Scripts/UI/HistoryRowFormatter.cs`:
```csharp
using UnderstudyKingdom.Backend;

namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Formats one decision history entry into a single display line. Kept
    /// separate from DialogueTemplateEngine -- that engine's templates are
    /// single-purpose flavor narration for the moment a decision resolves, not a
    /// data-dense summary row. See
    /// docs/superpowers/specs/2026-09-02-decision-history-design.md.
    /// </summary>
    public static class HistoryRowFormatter
    {
        public static string Format(DecisionHistoryEntry entry)
        {
            string outcome = entry.overridden ? "Overridden" : "Accepted";
            return $"Cycle {entry.cycleNumber}: Army {entry.playerRecommendation.army} / " +
                   $"Trade {entry.playerRecommendation.trade} / Religion {entry.playerRecommendation.religion} " +
                   $"-> {outcome} (Mood {entry.rulerOutcome.mood}, Loyalty {entry.rulerOutcome.loyalty})";
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run the same command as Step 2 (new results file name).
Expected: PASS, both tests green.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/HistoryRowFormatter.cs Assets/Scripts/UI/HistoryRowFormatter.cs.meta Assets/Tests/EditMode/HistoryRowFormatterTests.cs
git commit -m "feat: add HistoryRowFormatter"
```

---

### Task 5: `HistoryPanelController`

**Files:**
- Create: `Assets/Scripts/UI/HistoryPanelController.cs`
- Test: `Assets/Tests/PlayMode/HistoryPanelControllerTests.cs`
- Test: `Assets/Tests/PlayMode/HistoryPanelControllerRealDataTests.cs`

**Interfaces:**
- Consumes: `BackendSyncCoordinator.RequestHistory` (Task 3), `HistoryRowFormatter.Format` (Task 4).
- Produces: `HistoryPanelController` (`MonoBehaviour`, `Initialize(Button viewHistoryButton, GameObject panelRoot, Button closeButton, TextMeshProUGUI[] rowTexts, BackendSyncCoordinator coordinator, Slider armySlider, Slider tradeSlider, Slider religionSlider, Button submitButton, Button challengeButton)`). Task 6 (scene wiring) instantiates and configures this component.

**External dependency (for `HistoryPanelControllerRealDataTests.cs` only):** requires `server/` running locally and real internet access.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/PlayMode/HistoryPanelControllerTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    public class HistoryPanelControllerTests
    {
        private GameObject coordinatorObject;
        private GameObject controllerObject;
        private GameObject canvasObject;
        private GameObject panelRootObject;
        private Slider armySlider;
        private Slider tradeSlider;
        private Slider religionSlider;
        private Button submitButton;
        private Button challengeButton;
        private Button viewHistoryButton;
        private Button closeButton;
        private TextMeshProUGUI[] rowTexts;

        [SetUp]
        public void SetUp()
        {
            // Built inactive so Start() never runs on the coordinator --
            // currentSession stays null, giving RequestHistory's synchronous
            // no-session error path with zero network dependency. The real
            // network paths are covered by BackendSyncCoordinatorHistoryTests
            // and HistoryPanelControllerRealDataTests.
            coordinatorObject = new GameObject("Coordinator");
            coordinatorObject.SetActive(false);
            var coordinator = coordinatorObject.AddComponent<BackendSyncCoordinator>();

            canvasObject = new GameObject("Canvas", typeof(Canvas));

            armySlider = CreateSlider("ArmySlider", 40);
            tradeSlider = CreateSlider("TradeSlider", 30);
            religionSlider = CreateSlider("ReligionSlider", 30);

            var submitButtonObject = new GameObject("SubmitButton", typeof(Image), typeof(Button));
            submitButtonObject.transform.SetParent(canvasObject.transform, false);
            submitButton = submitButtonObject.GetComponent<Button>();

            var challengeButtonObject = new GameObject("ChallengeButton", typeof(Image), typeof(Button));
            challengeButtonObject.transform.SetParent(canvasObject.transform, false);
            challengeButton = challengeButtonObject.GetComponent<Button>();

            var viewHistoryButtonObject = new GameObject("ViewHistoryButton", typeof(Image), typeof(Button));
            viewHistoryButtonObject.transform.SetParent(canvasObject.transform, false);
            viewHistoryButton = viewHistoryButtonObject.GetComponent<Button>();

            panelRootObject = new GameObject("PanelRoot");
            panelRootObject.transform.SetParent(canvasObject.transform, false);

            var closeButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            closeButtonObject.transform.SetParent(panelRootObject.transform, false);
            closeButton = closeButtonObject.GetComponent<Button>();

            rowTexts = new TextMeshProUGUI[10];
            for (int i = 0; i < rowTexts.Length; i++)
            {
                var rowObject = new GameObject($"Row{i}", typeof(TextMeshProUGUI));
                rowObject.transform.SetParent(panelRootObject.transform, false);
                rowTexts[i] = rowObject.GetComponent<TextMeshProUGUI>();
            }

            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<HistoryPanelController>();
            controller.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, coordinator,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(coordinatorObject);
        }

        private Slider CreateSlider(string name, float initialValue)
        {
            var sliderObject = new GameObject(name, typeof(Slider));
            sliderObject.transform.SetParent(canvasObject.transform, false);
            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(initialValue);
            return slider;
        }

        [Test]
        public void ViewHistory_WithNoSessionYet_DisablesControlsAndShowsMessage()
        {
            viewHistoryButton.onClick.Invoke();

            Assert.IsFalse(armySlider.interactable);
            Assert.IsFalse(tradeSlider.interactable);
            Assert.IsFalse(religionSlider.interactable);
            Assert.IsFalse(submitButton.interactable);
            Assert.IsFalse(challengeButton.interactable);
            Assert.IsTrue(panelRootObject.activeSelf);
            Assert.AreEqual("No session available yet -- try again in a moment.", rowTexts[0].text);
        }

        [Test]
        public void Close_ReEnablesControlsAndHidesPanel()
        {
            viewHistoryButton.onClick.Invoke();
            closeButton.onClick.Invoke();

            Assert.IsTrue(armySlider.interactable);
            Assert.IsTrue(tradeSlider.interactable);
            Assert.IsTrue(religionSlider.interactable);
            Assert.IsTrue(submitButton.interactable);
            Assert.IsTrue(challengeButton.interactable);
            Assert.IsFalse(panelRootObject.activeSelf);
        }
    }
}
```

`Assets/Tests/PlayMode/HistoryPanelControllerRealDataTests.cs`:
```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    /// <summary>
    /// Real end-to-end: real Supabase sign-in, real local server/, a real submitted
    /// decision, real history fetch, asserts on the actual rendered row text.
    /// </summary>
    public class HistoryPanelControllerRealDataTests
    {
        private GameObject coordinatorObject;
        private GameObject controllerObject;
        private GameObject canvasObject;
        private GameObject panelRootObject;
        private GameObject directApiClientObject;
        private Slider armySlider;
        private Slider tradeSlider;
        private Slider religionSlider;
        private Button submitButton;
        private Button challengeButton;
        private Button viewHistoryButton;
        private Button closeButton;
        private TextMeshProUGUI[] rowTexts;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            coordinatorObject = new GameObject("Coordinator");
            var coordinator = coordinatorObject.AddComponent<BackendSyncCoordinator>();
            coordinator.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            coordinator.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";
            coordinator.BackendBaseUrl = "http://localhost:3000";

            // Wait for the coordinator's own session bootstrap + EnsureKingdom.
            yield return new WaitForSeconds(2f);

            // The coordinator doesn't expose its session, so read back the file it
            // just persisted via SessionStore.Save, and use a separate BackendApiClient
            // to submit one real decision directly -- giving this panel real history
            // to render without needing BackendSyncCoordinator to expose internals it
            // has no other reason to expose.
            SessionData session = SessionStore.Load();
            Assert.IsNotNull(session, "Coordinator did not persist a session during bootstrap");

            directApiClientObject = new GameObject("DirectApiClient");
            var directApiClient = directApiClientObject.AddComponent<BackendApiClient>();
            directApiClient.BackendBaseUrl = "http://localhost:3000";

            var dto = new DecisionSyncRequest
            {
                cycle_number = 1,
                player_recommendation = new PlayerRecommendationDto { army = 40, trade = 30, religion = 30 },
                ruler_outcome = new RulerOutcomeDto { mood = 55, loyalty = 60 },
                overridden = false
            };
            bool posted = false;
            directApiClient.PostDecision(session.AccessToken, dto, _ => posted = true, err => Assert.Fail($"PostDecision failed: {err}"));
            yield return new WaitUntil(() => posted);

            canvasObject = new GameObject("Canvas", typeof(Canvas));

            armySlider = CreateSlider("ArmySlider", 40);
            tradeSlider = CreateSlider("TradeSlider", 30);
            religionSlider = CreateSlider("ReligionSlider", 30);

            var submitButtonObject = new GameObject("SubmitButton", typeof(Image), typeof(Button));
            submitButtonObject.transform.SetParent(canvasObject.transform, false);
            submitButton = submitButtonObject.GetComponent<Button>();

            var challengeButtonObject = new GameObject("ChallengeButton", typeof(Image), typeof(Button));
            challengeButtonObject.transform.SetParent(canvasObject.transform, false);
            challengeButton = challengeButtonObject.GetComponent<Button>();

            var viewHistoryButtonObject = new GameObject("ViewHistoryButton", typeof(Image), typeof(Button));
            viewHistoryButtonObject.transform.SetParent(canvasObject.transform, false);
            viewHistoryButton = viewHistoryButtonObject.GetComponent<Button>();

            panelRootObject = new GameObject("PanelRoot");
            panelRootObject.transform.SetParent(canvasObject.transform, false);

            var closeButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            closeButtonObject.transform.SetParent(panelRootObject.transform, false);
            closeButton = closeButtonObject.GetComponent<Button>();

            rowTexts = new TextMeshProUGUI[10];
            for (int i = 0; i < rowTexts.Length; i++)
            {
                var rowObject = new GameObject($"Row{i}", typeof(TextMeshProUGUI));
                rowObject.transform.SetParent(panelRootObject.transform, false);
                rowTexts[i] = rowObject.GetComponent<TextMeshProUGUI>();
            }

            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<HistoryPanelController>();
            controller.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, coordinator,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(coordinatorObject);
            Object.DestroyImmediate(directApiClientObject);
            SessionStore.Clear();
        }

        private Slider CreateSlider(string name, float initialValue)
        {
            var sliderObject = new GameObject(name, typeof(Slider));
            sliderObject.transform.SetParent(canvasObject.transform, false);
            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(initialValue);
            return slider;
        }

        [UnityTest]
        public IEnumerator ViewHistory_WithRealSubmittedDecision_RendersRealRowText()
        {
            viewHistoryButton.onClick.Invoke();

            yield return new WaitUntil(() => !string.IsNullOrEmpty(rowTexts[0].text));

            Assert.AreEqual(
                "Cycle 1: Army 40 / Trade 30 / Religion 30 -> Accepted (Mood 55, Loyalty 60)",
                rowTexts[0].text);
            Assert.IsFalse(rowTexts[1].gameObject.activeSelf);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform PlayMode -testFilter "HistoryPanelControllerTests|HistoryPanelControllerRealDataTests" -testResults "C:\Users\rajes\understudy-kingdom\test-results-task5-fail.xml"
```
Expected: FAIL — `HistoryPanelController` does not exist (compile error).

- [ ] **Step 3: Create `HistoryPanelController`**

`Assets/Scripts/UI/HistoryPanelController.cs`:
```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Backend;

namespace UnderstudyKingdom.UI
{
    /// <summary>
    /// Modal history panel: while open, the core loop's sliders/Submit/Challenge
    /// are non-interactive. Single fixed page (up to MaxRows), no scrolling, no
    /// "Load More" -- see
    /// docs/superpowers/specs/2026-09-02-decision-history-design.md.
    /// </summary>
    public class HistoryPanelController : MonoBehaviour
    {
        private const int MaxRows = 10;
        private const string NoKingdomErrorMessage = "No kingdom found for this user";
        private const string EmptyHistoryMessage = "No decisions yet -- submit your first recommendation!";

        [SerializeField] private Button viewHistoryButton;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI[] rowTexts;
        [SerializeField] private BackendSyncCoordinator coordinator;
        [SerializeField] private Slider armySlider;
        [SerializeField] private Slider tradeSlider;
        [SerializeField] private Slider religionSlider;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button challengeButton;

        private void Start()
        {
            Bind();
        }

        /// <summary>
        /// Mirrors CoreLoopScreenController/DuelButtonController's Initialize pattern
        /// -- called by Start() in the real scene, and callable directly by tests to
        /// bypass Unity lifecycle timing.
        /// </summary>
        public void Initialize(
            Button viewHistoryButton,
            GameObject panelRoot,
            Button closeButton,
            TextMeshProUGUI[] rowTexts,
            BackendSyncCoordinator coordinator,
            Slider armySlider,
            Slider tradeSlider,
            Slider religionSlider,
            Button submitButton,
            Button challengeButton)
        {
            this.viewHistoryButton = viewHistoryButton;
            this.panelRoot = panelRoot;
            this.closeButton = closeButton;
            this.rowTexts = rowTexts;
            this.coordinator = coordinator;
            this.armySlider = armySlider;
            this.tradeSlider = tradeSlider;
            this.religionSlider = religionSlider;
            this.submitButton = submitButton;
            this.challengeButton = challengeButton;

            Bind();
        }

        private void Bind()
        {
            viewHistoryButton.onClick.RemoveAllListeners();
            viewHistoryButton.onClick.AddListener(OnViewHistory);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnClose);

            panelRoot.SetActive(false);
        }

        private void OnViewHistory()
        {
            SetCoreLoopControlsInteractable(false);
            panelRoot.SetActive(true);
            coordinator.RequestHistory(MaxRows, HandleResult, HandleError);
        }

        private void HandleResult(DecisionHistoryEntry[] entries)
        {
            if (entries.Length == 0)
            {
                rowTexts[0].gameObject.SetActive(true);
                rowTexts[0].text = EmptyHistoryMessage;
                for (int i = 1; i < rowTexts.Length; i++)
                {
                    rowTexts[i].gameObject.SetActive(false);
                }
                return;
            }

            for (int i = 0; i < rowTexts.Length; i++)
            {
                if (i < entries.Length)
                {
                    rowTexts[i].gameObject.SetActive(true);
                    rowTexts[i].text = HistoryRowFormatter.Format(entries[i]);
                }
                else
                {
                    rowTexts[i].gameObject.SetActive(false);
                }
            }
        }

        private void HandleError(string error)
        {
            // A fresh player who's never had a kingdom created yet gets the same
            // friendly empty-state message as a kingdom with zero decisions -- the
            // player doesn't need to know which case it is, both mean "nothing to
            // show yet." Any other error is shown verbatim.
            rowTexts[0].gameObject.SetActive(true);
            rowTexts[0].text = error == NoKingdomErrorMessage ? EmptyHistoryMessage : error;

            for (int i = 1; i < rowTexts.Length; i++)
            {
                rowTexts[i].gameObject.SetActive(false);
            }
        }

        private void OnClose()
        {
            panelRoot.SetActive(false);
            SetCoreLoopControlsInteractable(true);
        }

        private void SetCoreLoopControlsInteractable(bool interactable)
        {
            armySlider.interactable = interactable;
            tradeSlider.interactable = interactable;
            religionSlider.interactable = interactable;
            submitButton.interactable = interactable;
            challengeButton.interactable = interactable;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

With `server/` running locally, run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform PlayMode -testFilter "HistoryPanelControllerTests|HistoryPanelControllerRealDataTests" -testResults "C:\Users\rajes\understudy-kingdom\test-results-task5-pass.xml"
```
Expected: PASS, all 3 tests green (2 in `HistoryPanelControllerTests`, 1 in `HistoryPanelControllerRealDataTests`).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/HistoryPanelController.cs Assets/Scripts/UI/HistoryPanelController.cs.meta Assets/Tests/PlayMode/HistoryPanelControllerTests.cs Assets/Tests/PlayMode/HistoryPanelControllerRealDataTests.cs
git commit -m "feat: add HistoryPanelController"
```

---

### Task 6: Scene wiring + full-suite regression + manual verification

**Files:**
- Modify: `Assets/Editor/CoreLoopSceneBuilder.cs`
- Modify (regenerated): `Assets/Scenes/CoreLoop.unity`

**Interfaces:**
- Consumes: `HistoryPanelController` (Task 5).
- Produces: nothing further consumed by later tasks — this is the final task.

- [ ] **Step 1: Increase the canvas's reference height and add the history UI**

In `Assets/Editor/CoreLoopSceneBuilder.cs`, change:
```csharp
            canvasScaler.referenceResolution = new Vector2(800f, 1400f);
```
(changed from `1200f` — the existing content already extends to y=-540 (`DuelResultText`); this task adds a button at y=-600, both of which need to stay within the canvas's visible half-height. 1400 gives ±700, comfortable margin.)

Then, immediately after the existing `duelController.Initialize(armySlider, tradeSlider, religionSlider, duelButton, duelResultText, backendCoordinator);` line and before `canvasObject.GetComponent<RectTransform>().localScale = Vector3.one;`, add:

```csharp
            var viewHistoryButtonObject = new GameObject("ViewHistoryButton", typeof(Image), typeof(Button));
            viewHistoryButtonObject.transform.SetParent(canvasObject.transform, false);
            var viewHistoryButtonRect = viewHistoryButtonObject.GetComponent<RectTransform>();
            viewHistoryButtonRect.anchoredPosition = new Vector2(0f, -600f);
            viewHistoryButtonRect.sizeDelta = new Vector2(220f, 44f);
            viewHistoryButtonObject.GetComponent<Image>().color = new Color(0.3f, 0.6f, 0.4f, 1f);
            var viewHistoryButton = viewHistoryButtonObject.GetComponent<Button>();
            TextMeshProUGUI viewHistoryLabel = CreateLabel(viewHistoryButtonObject.transform, "Text", 0f, "View History");
            var viewHistoryLabelRect = viewHistoryLabel.GetComponent<RectTransform>();
            viewHistoryLabelRect.anchorMin = Vector2.zero;
            viewHistoryLabelRect.anchorMax = Vector2.one;
            viewHistoryLabelRect.sizeDelta = Vector2.zero;
            viewHistoryLabelRect.anchoredPosition = Vector2.zero;

            var panelRootObject = new GameObject("HistoryPanel", typeof(Image));
            panelRootObject.transform.SetParent(canvasObject.transform, false);
            var panelRect = panelRootObject.GetComponent<RectTransform>();
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(700f, 800f);
            panelRootObject.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            var closeButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            closeButtonObject.transform.SetParent(panelRootObject.transform, false);
            var closeButtonRect = closeButtonObject.GetComponent<RectTransform>();
            closeButtonRect.anchoredPosition = new Vector2(310f, 360f);
            closeButtonRect.sizeDelta = new Vector2(60f, 40f);
            closeButtonObject.GetComponent<Image>().color = new Color(0.6f, 0.3f, 0.3f, 1f);
            var closeButton = closeButtonObject.GetComponent<Button>();
            TextMeshProUGUI closeLabel = CreateLabel(closeButtonObject.transform, "Text", 0f, "X");
            var closeLabelRect = closeLabel.GetComponent<RectTransform>();
            closeLabelRect.anchorMin = Vector2.zero;
            closeLabelRect.anchorMax = Vector2.one;
            closeLabelRect.sizeDelta = Vector2.zero;
            closeLabelRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI titleLabel = CreateLabel(panelRootObject.transform, "Title", 0f, "Your Reign So Far");
            titleLabel.fontSize = 28f;
            titleLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 340f);

            var rowTexts = new TextMeshProUGUI[10];
            for (int i = 0; i < rowTexts.Length; i++)
            {
                TextMeshProUGUI row = CreateLabel(panelRootObject.transform, $"Row{i}", 0f, string.Empty);
                row.fontSize = 18f;
                row.alignment = TextAlignmentOptions.Left;
                var rowRect = row.GetComponent<RectTransform>();
                rowRect.sizeDelta = new Vector2(640f, 50f);
                rowRect.anchoredPosition = new Vector2(0f, 280f - i * 55f);
                rowTexts[i] = row;
            }

            var historyControllerObject = new GameObject("HistoryPanelController");
            var historyController = historyControllerObject.AddComponent<HistoryPanelController>();
            historyController.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, backendCoordinator,
                armySlider, tradeSlider, religionSlider, button, duelButton);
```

`Assets/Editor/CoreLoopSceneBuilder.cs` already has `using UnderstudyKingdom.UI;` and `using UnderstudyKingdom.Backend;` — no new usings needed. `button` (Submit) and `duelButton` (Challenge) are the same local variables already in scope from earlier in `Build()`.

- [ ] **Step 2: Regenerate `CoreLoop.unity`**

Run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Build -projectPath "C:\Users\rajes\understudy-kingdom" -quit
```
Expected: console output ends with `CoreLoopSceneBuilder: saved scene to Assets/Scenes/CoreLoop.unity`, no errors. (`-quit` is correct here — this is `-executeMethod`, not `-runTests`.)

- [ ] **Step 3: Verify the scene via the existing sanity check**

Run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Verify -projectPath "C:\Users\rajes\understudy-kingdom" -quit
```
Expected: `CoreLoopSceneBuilder.Verify: scene opened and controller found successfully.`, exit code 0.

- [ ] **Step 4: Run the full EditMode + PlayMode suites**

With `server/` running locally, run:
```
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform EditMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-history-final-editmode.xml"
"C:\Users\rajes\UnityEditors\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -runTests -projectPath "C:\Users\rajes\understudy-kingdom" -testPlatform PlayMode -testResults "C:\Users\rajes\understudy-kingdom\test-results-history-final-playmode.xml"
```
Expected: PASS, zero failures in both. Read the real XML — do not trust exit codes alone.

- [ ] **Step 5: Manual verification — real Play Mode run against the real local backend**

With `server/` running locally (`cd server && npm run dev`, confirm it logs listening on port 3000):
1. Open the Unity Editor on this project (not batch mode), open `Assets/Scenes/CoreLoop.unity`, press Play, switch to the Game view.
2. Confirm a new "View History" button is visible and not clipped, below "Challenge a Rival Kingdom."
3. Click "Submit Recommendation" once or twice to create real decision history.
4. Click "View History." A panel should appear showing the decision(s) just submitted, formatted per `HistoryRowFormatter`; the sliders/Submit/Challenge buttons underneath should be non-interactive while the panel is open.
5. Click the close button ("X"). The panel should close and the underlying controls should become interactive again.
6. Confirm the Console shows no red errors.
7. Stop Play Mode.

Record the result of this manual check before moving to the whole-branch review — this project's established "definition of done" bar (see milestones #3, #4, #5), not just green automated tests.

- [ ] **Step 6: Commit**

```bash
git add Assets/Editor/CoreLoopSceneBuilder.cs Assets/Scenes/CoreLoop.unity
git commit -m "feat: wire HistoryPanelController into the CoreLoop scene"
```
