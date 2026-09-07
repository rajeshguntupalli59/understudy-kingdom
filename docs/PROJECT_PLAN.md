# Project Plan — Understudy Kingdom

**Version:** 1.0 | **Status:** In development (7 milestones shipped) | **Last updated:** 2026-09-03

Assumptions: mobile client (Android + iOS), Unity/C# client, lightweight
backend (Node.js + PostgreSQL), F2P with IAP, India-first launch market,
greenfield project.

## 1. Executive Summary

Understudy Kingdom is a strategy/RPG hybrid-casual mobile game where the
player is a royal advisor, not a ruler: they prep strategic options for an
NPC monarch with a persistent personality who sometimes overrides the
"correct" play. Target audience: strategy-game players fatigued by the top
grossing genre's monetization abuses. Business value: differentiate on trust
and narrative depth in the largest-revenue mobile genre combo (Strategy
$17.5B/yr + RPG $16.8B/yr) without competing on raw production budget.

## 2. Functional Requirements

**Core Loop**
- FR-01: The user can prep a strategic recommendation (resource allocation,
  army move, diplomatic choice) for the ruler NPC each decision cycle.
- FR-02: When the ruler NPC's loyalty/agenda state conflicts with the
  player's recommendation, the system shall probabilistically override the
  player's choice and narrate the consequence. (Narrowed from an earlier
  "mood or trait" phrasing during design: the shipped decision table
  weights on loyalty and agenda-alignment only, not mood. Mood is still
  tracked and narrated but does not currently influence the override
  probability — see `docs/superpowers/specs/2026-09-01-core-decision-cycle-design.md`
  approach B. Revisit if mood-weighting becomes a real gameplay need.)
- FR-03: The system shall persist ruler mood, loyalty, and agenda as
  numeric/enum state that evolves from player choices and event outcomes.
  (Dropped "trust" as a separate stat during design — no distinct trust
  mechanic was ever designed; loyalty serves that role. Revisit if a
  distinct trust stat is designed later.)

**Advisor/Ruler AI**
- FR-04: The system shall drive ruler behavior via a lightweight
  utility-AI/behavior-tree (mood, loyalty, agenda variables), not a heavy
  on-device model.
- FR-05: The system shall generate ruler dialogue from templated strings
  with variable slots keyed to current mood/history, avoiding on-device LLM
  inference.
- FR-06: The user can view a "relationship history" log of past decisions
  and how the ruler reacted.

**Social / Alliance**
- FR-07: The user can join a "council" (guild) of advisors serving the same
  or rival ruler.
- FR-08: When a council reaches a shared milestone, the system shall grant
  all members a reward.

**Async PvP**
- FR-09: The user can submit a prepared strategy to be judged asynchronously
  against another player's strategy for the same scenario (no live
  matchmaking — avoids the bot/matchmaking complaints seen in competitor
  games; see `COMPETITOR_ANALYSIS.md`).

**Live-Ops / Events**
- FR-10: The system shall rotate limited-time narrative events on a weekly
  cadence.
- FR-11: Every event shall have a free-to-play-completable reward tier;
  premium spend shall only unlock cosmetic or time-acceleration rewards,
  never an exclusive-required-to-progress outcome.

**Cosmetic / Collection**
- FR-12: The user can customize their court/advisory chamber with
  unlockable non-gameplay-affecting cosmetics.

**Onboarding**
- FR-13: The system shall run an interactive first-session tutorial covering
  the core prep→ruler-decision loop before any monetization prompt appears.

**Monetization Guardrails**
- FR-14: The system shall cap promotional interstitials to one per session.
- FR-15: The system shall never stack more than one modal/pop-up on the
  home screen simultaneously.

## 3. Non-Functional Requirements

- NFR-01 (Performance): Foreground Anon RSS + Swap shall stay under 2GB on
  4GB-RAM-class devices, 2.25GB on 8GB-class — matching Google Play's Feb
  2027 enforcement thresholds.
  *(Source: https://android-developers.googleblog.com/2026/08/app-quality-memory-optimization-secure-onboarding.html)*
- NFR-02 (Performance): Bitmap memory per NPC archetype capped at one
  shared 1024×1024 ASTC atlas; no unique per-NPC textures.
- NFR-03 (Reliability): Crash-free session rate ≥ 99.5%.
- NFR-04 (Security): All purchase verification server-side (receipt
  validation against Google Play/App Store); client-reported purchase state
  is never trusted.
- NFR-05 (Compliance): DPDP Act (India) + GDPR-equivalent consent flow for
  telemetry; COPPA-safe design if under-13 users are reachable (no direct
  messaging between minors).
- NFR-06 (Availability): Backend API 99.5% uptime; async PvP and council
  features degrade gracefully offline (queue actions, sync on reconnect)
  rather than hard-failing.
- NFR-07 (Support): In-app support ticket with visible SLA (target: first
  response <24h).

## 4. Acceptance Criteria

```
FR-02 Acceptance:
  Given: player has submitted a recommendation and ruler loyalty < 20
  When:  the decision cycle resolves
  Then:  system has a defined probability of ruler override, weighted by
         loyalty and agenda alignment (see FR-02 note on mood)
  And:   the override outcome is narrated with a templated line referencing
         the current mood state

FR-11 Acceptance:
  Given: a live-ops event is active
  When:  a free-to-play user completes all event objectives using only
         earned currency
  Then:  the user receives 100% of the event's functional reward tier
  And:   only cosmetic/time-skip rewards remain locked behind premium spend

FR-15 Acceptance:
  Given: the home screen is loading
  When:  more than one promotional modal is queued
  Then:  only the highest-priority modal displays
  And:   remaining modals are deferred to the next session, not stacked
```

## 5. Enhancement Opportunities

- `[Recommended]` Transparent "fair-play pledge" surfaced at onboarding (no
  unwinnable-without-spending events) — converts a top competitor complaint
  into a marketing differentiator.
- `[Recommended]` Async-only PvP (FR-09) — architecturally avoids
  matchmaking/bot complaints rather than patching them later.
- `[Nice to Have]` Ruler "relationship history" log (FR-06) doubles as a
  shareable save-file flex feature for social virality.
- `[Future Phase]` Cross-ruler rivalries: two players' rulers can trigger a
  scripted diplomatic event, creating emergent multiplayer narrative without
  live-service infrastructure cost.
- `[Future Phase]` Seasonal "regime change" — ruler NPC periodically
  succeeded, refreshing the meta-narrative by reusing the templated dialogue
  system rather than a full content rebuild.

## 6. Data Model

```sql
users:            id (UUID PK), device_id, email (nullable), created_at, country_code

kingdoms:         id (UUID PK), user_id (FK -> users), ruler_npc_id (FK -> ruler_npcs), founded_at

ruler_npcs:       id (UUID PK), kingdom_id (FK -> kingdoms), mood (int), loyalty (int),
                  agenda (enum), trait_seed (int)

decisions:        id (UUID PK), kingdom_id (FK -> kingdoms), cycle_number (int),
                  player_recommendation (jsonb), ruler_outcome (jsonb),
                  overridden (bool), created_at

councils:         id (UUID PK), name, join_code (unique, 6-char), milestone_threshold (int,
                  default 10), milestone_reached (bool), created_at
council_members:  user_id (PK, FK -> users -- one council per user, DB-enforced),
                  council_id (FK -> councils), joined_at, reward_eligible (bool)
                  -- shipped design: join-by-code, not ruler_alignment matching;
                  -- capped at 20 members; see docs/superpowers/specs/2026-09-03-council-social-design.md

events:           id (UUID PK), name, start_at, end_at, f2p_reward_tier (jsonb),
                  premium_reward_tier (jsonb)

pvp_duels:        id (UUID PK), challenger_kingdom_id (FK), defender_kingdom_id (FK),
                  scenario_id, resolved_at, winner_kingdom_id (nullable)

purchases:        id (UUID PK), user_id (FK -> users), sku, amount_cents,
                  platform_receipt (text), verified (bool), created_at
```

Relationships: one user → one kingdom → one ruler_npc (1:1:1); kingdom →
many decisions (1:many); council ↔ users many:many via council_members;
kingdom → many pvp_duels as challenger or defender.

## 7. Engineering Handoff Spec

**Tech Stack**
- Client: Unity 6 LTS (6000.3) (C#), ASTC texture compression, Addressables for
  asset streaming.
  *(Source: https://unity.com/blog/games/optimize-your-mobile-game-performance-expert-tips-on-graphics-and-assets)*
- Backend: Node.js + Express/Fastify, PostgreSQL, Redis for session/queue state
- Auth: Sign in with Google/Apple + anonymous device-id fallback

**Architecture Overview**
- Client: Unity game loop + local behavior-tree AI for the ruler NPC (no
  network round-trip for the core loop)
- Backend: REST API for councils, async PvP, purchase verification,
  live-ops event config
- DB: PostgreSQL for persistent state; Redis for the PvP duel queue

**API Endpoints (sample)**

```
Method:   POST
Path:     /api/v1/decisions
Auth:     Bearer token required
Request:  { kingdom_id: uuid, cycle_number: int, recommendation: object }
Response: { decision_id: uuid, ruler_outcome: object, overridden: bool }
Errors:   400 (invalid cycle), 401 (auth), 409 (cycle already resolved)
```

```
Method:   POST
Path:     /api/v1/purchases/verify
Auth:     Bearer token required
Request:  { platform: "android"|"ios", receipt: string, sku: string }
Response: { verified: bool, granted_items: array }
Errors:   400 (malformed receipt), 401 (auth), 422 (verification failed)
```

**Business Logic Rules**

```
BL-01: Every live-ops event must define a non-null f2p_reward_tier reachable
       without purchases (FR-11)
BL-02: Purchase grants only apply after server-side receipt verification
       succeeds
BL-03: Ruler override probability is a function of loyalty and agenda
       alignment only (mood is tracked/narrated but not currently a
       weighting input — see FR-02 note) — never influenced by purchase
       history (no pay-to-avoid-override)
BL-04: Async PvP duels resolve via scenario scoring server-side; the client
       never computes the winner
```

**UI/UX Notes**
- States required per screen: loading (skeleton, not blank), empty
  (no council yet → CTA), error (toast, not silent), success
- Home screen: max 1 modal on load (FR-15); tutorial gates all monetization
  surfaces until the first decision cycle completes (FR-13)
- Accessibility: text scaling support, colorblind-safe mood/loyalty
  indicators (not color-only)

**Definition of Done**
- [ ] FR-01 through FR-15 implemented and pass acceptance criteria
- [ ] NFR-01/02 verified via Play Console memory profiling on a 4GB reference device
- [ ] Crash-free session rate ≥99.5% over a 7-day soak test
- [ ] Purchase verification tested against sandbox receipts for both stores
- [ ] Unit + integration tests passing in CI
- [ ] Deployed to internal testing track, smoke tests passed

## 8. Implementation Status

Eight milestones shipped end-to-end (brainstorm → spec → plan →
subagent-driven-development → final whole-branch review → fix → manual
Play Mode checkpoint → merge to `main`), each covered by real Supabase +
real local Postgres integration tests (no mocking):

| Milestone | Branch (merged) | Covers | Status |
|---|---|---|---|
| #1 Core Loop Vertical Slice | `feat/core-loop-...` | FR-01, FR-03 | Done |
| #2 Ruler AI Depth | `feat/ruler-ai-depth` | FR-02, FR-04 | Done |
| #3 Backend Service | `main` (direct, isolated `server/`) | Auth, `decisions` persistence/history endpoint | Done |
| #4 Client-Backend Integration | `feat/client-backend-integration` | Wires client to `server/`; session bootstrap/refresh | Done |
| #5 Async PvP | `feat/async-pvp` | FR-09 | Done |
| #6 Relationship History Log | `feat/decision-history` | FR-06 | Done |
| #7 Council / Social | `feat/council-social` | FR-07, FR-08 | Done |
| #8 Onboarding Tutorial | `feat/onboarding-tutorial` | FR-13 | Done |
| #9 Duel/Modal Gate Fix | `feat/duel-modal-gate` | Shared duel-in-flight/modal gate for History/Council panels | Done |
| #10 Live-Ops Events | `feat/live-ops-events` | FR-10, FR-11 (narrowed) | Done |
| #11 Cosmetics Customization | `feat/cosmetics-customization` | FR-12 | Done |
| #12 Ruler Portrait System | `feat/ruler-portrait` | Visual art phase 1: painted ruler portrait reacting to Mood/Loyalty | Done |
| #13 Themed Scene Backgrounds | `feat/scene-backgrounds` | Visual art phase 2: full-screen throne-room backdrop matching the 3 Cosmetics themes | Done |
| #14 Themed Panel Art | `feat/panel-art` | Visual art phase 3: painted background art for the History/Council/Events modal panels, per theme | Done |
| #15 Button Icons | `feat/button-icons` | Visual art phase 4 (final increment): painted icons on 4 of 15 buttons | Done |

*(Milestones #12-15 are the four increments of the visual-art phase --
all planned increments now have at least a first pass shipped, though
#14 and #15 both ended up scope-reduced by the same Hugging Face credit
cap; see "Known follow-up items" below for what's deferred in each.
Milestone #9 merged after a manual playtest was still outstanding —
same limitation as milestones #10/#11: no automated agent can drive the
interactive Unity Editor UI. Everything automated (server tests,
typecheck, Unity EditMode, Unity PlayMode) is green; see "Known follow-up
items" below for the one pre-existing, non-blocking issue found and
investigated during this merge.)*

**FR status:** FR-01, FR-02 (loyalty/agenda-weighted, not mood — see FR-02
note), FR-03, FR-04, FR-06, FR-07, FR-08, FR-09, FR-10, FR-11 (narrowed —
see below), FR-12, FR-13 implemented and live. FR-05 (templated ruler
dialogue) implemented as part of milestones #1/#2/#5/#6's narration work.
FR-13's "before any monetization prompt" gating is currently vacuous
(FR-14/FR-15 don't exist yet — nothing to gate against); revisit once
they land. FR-11 shipped narrowed to a single, unconditionally
F2P-completable reward per event — the "premium spend unlocks
cosmetic/time-skip rewards" clause is deliberately deferred, since no
currency/IAP system exists yet to attach a premium tier to; see
`docs/superpowers/specs/2026-09-03-live-ops-events-design.md`. FR-12
shipped as a 3-theme color picker recoloring existing panel backgrounds,
zero new art, zero currency system — see
`docs/superpowers/specs/2026-09-04-cosmetics-customization-design.md`.
FR-14, FR-15 (monetization guardrails) not yet started.

**Known follow-up items, deliberately deferred (not bugs):**
- Milestone #5's `defenderRulerSnapshot` is always the schema default
  (`mood:50, loyalty:50, agenda:Expansionist`) because `server/` never
  writes to `ruler_npcs` — a real duel's outcome currently depends only on
  the challenger's own allocation. Documented in `duels.ts`; revisit once
  a milestone actually needs the defender's real ruler state.
- A concurrent-session-refresh race across `BackendSyncCoordinator`'s three
  callers (decision sync, duel, history) was flagged in milestone #6's
  final review (I-4) and fixed post-merge (commit `b5dd265`): all three now
  funnel through one `EnsureFreshSession` chokepoint instead of racing
  independent `RefreshSession` calls.
- Milestone #7's shipped `councils`/`council_members` schema (see §6) diverged
  from this doc's original sketch during design: no `ruler_alignment` enum
  (join-by-code instead of ruler-alignment matching), and `council_members`
  gained `join_code`/`milestone_threshold`/`milestone_reached`/`reward_eligible`
  columns the original sketch didn't anticipate. §6 below is updated to match
  what shipped. No leave/rename/kick-member, no browsing UI, no repeating
  rewards this pass — see
  `docs/superpowers/specs/2026-09-03-council-social-design.md`.
- Milestone #7's final review flagged a duel-in-flight request as sitting
  outside the modal mutual-exclusion gate shared by the History and Council
  panels (`DuelButtonController` only disables its own button, not the
  shared gate) — a pre-existing gap from milestone #6, now duplicated by
  Council. Real fix needs `DuelButtonController` to own a shared in-flight
  flag the panels consult; deferred as larger than a single milestone.
- Milestone #8's final review caught a real soft-lock bug (`CoreLoopSceneBuilder`
  bakes disabled-control state into the committed scene at edit time; the
  tutorial's completed-path never re-enabled it for returning players) —
  fixed and re-verified before merge. Separately, the manual checkpoint
  caught a *third* occurrence of a recurring readability bug (a new label
  shipped under this scene's 24pt text-size convention) across milestones
  #6/#7/#8, each time invisible to automated review since font size doesn't
  show up as a diff-level defect. Fixed, and a permanent rule comment was
  added directly above `CreateLabel()` in `CoreLoopSceneBuilder.cs` so every
  future label call site carries an explicit floor to check against.
- Milestone #10's FR-11 shipped with the premium/IAP reward tier
  deliberately deferred — no currency/IAP system exists yet to attach a
  premium tier to; see
  `docs/superpowers/specs/2026-09-03-live-ops-events-design.md`.
- Milestone #10's final whole-branch review caught a critical bug (C-1):
  `DecisionCycleManager`'s cycle counter was pure in-memory state that reset
  to 0 on every relaunch while the player's kingdom/decisions persisted
  server-side, so a returning player's submissions silently collided with
  cycle numbers already used and were dropped (server's
  `onConflictDoNothing`) -- live-ops event progress could never advance past
  a player's first-ever session. Fixed: the counter is now self-healing via
  a server round-trip (`GET /api/v1/decisions?limit=1`) on session
  bootstrap, seeding the counter up to the most recently *inserted*
  server decision's cycle number (never backward). Not purely local
  anymore. Any player with an out-of-sync install from before this fix
  self-heals automatically on their next launch once this ships.
- The C-1 fix's re-review found 4 non-blocking Low/informational
  follow-ups, none of which weakened the fix itself. **N-1, N-2, N-3
  resolved (commit `94f8e8e`):** (N-1) the seed read the newest-by-
  `created_at` decision rather than `MAX(cycle_number)` -- these coincide
  under normal sequential play, but could theoretically diverge under a
  rare double-refresh-callback race (`DrainPendingRefreshCallbacks`
  firing two queued syncs back-to-back), landing the seed one cycle low.
  Fixed via a new dedicated `GET /api/v1/decisions/latest-cycle` route
  (`MAX(cycle_number)`, sidestepping the race instead of relying on
  insertion-order-adjacent timestamps) and a matching
  `BackendApiClient.GetLatestCycleNumber` on the Unity side, replacing
  the reused `GetDecisionHistory(limit: 1)` call. (N-2) the seed fetch is
  still bootstrap-only with no in-session retry if `EnsureKingdom` fails
  at launch (unlike the duel/history/event request paths, which do retry
  via `EnsureKingdomThenSend*`) -- that part is deliberately NOT fixed
  (a real retry mechanism is more scope than this pass), but the
  previously-misleading log message ("will resync on next attempt," when
  the only next attempt is actually the next app launch) now says so
  correctly. (N-3) `DecisionCycleManagerSessionResumeTests` now calls
  `SessionStore.Clear()` in its own `UnitySetUp` instead of relying on a
  prior test's `TearDown` for isolation. **(N-4) still open, not a bug:**
  a fixed `WaitForSeconds(3f)` for two sequential real round-trips is a
  latent flake source under a slow network, matching this project's
  existing convention for real-data test fixtures elsewhere -- left as
  environmental risk, consistent with how every other real-data test in
  this codebase handles the same tradeoff.
- **Resolved:** `EventPanelController` and `CosmeticsPanelController` are
  now `DuelModalGate`-aware (both had branched before milestone #9 merged,
  so neither originally consulted the shared duel-in-flight/modal-open
  gate History/Council already did). `Initialize(...)` on both gained the
  same trailing `DuelModalGate gate` parameter as History/Council; their
  open/close handlers set `gate.IsModalOpen`; `SetCoreLoopControlsInteractable`
  skips re-enabling `challengeButton` while `gate.IsDuelInFlight` is still
  true — identical shape to the existing History/Council fix. `CoreLoopSceneBuilder.Build()`
  had to be rerun to regenerate the committed `CoreLoop.unity` scene itself
  (not just the builder script) — Unity's scene serializer silently drops
  a `[SerializeField]` reference that was never actually re-baked into the
  scene, which is exactly the C-1 class of bug milestone #9 hit for the
  same reason; `CoreLoopSceneBuilder.Verify()` confirms it's live. New
  `Close_WithDuelInFlight_LeavesChallengeButtonDisabled` tests added to
  both controllers' PlayMode suites, mirroring Council's existing coverage.
  Full regression green: server 70/70 + typecheck clean, Unity EditMode
  74/74, Unity PlayMode 71/71 (69 + 2 new). Manual Play Mode checkpoint
  still outstanding — no automated agent can drive the interactive Editor.
- **Real, confirmed, but currently non-reproducible production-reliability
  gap in `POST /api/v1/decisions`** (found during milestone #9's merge,
  pre-existing since milestone #7, not caused by the merge itself — see
  `docs/HANDOVER_2026-09-03.md` §2a): `BackendApiClient.PostDecision`
  treats HTTP 409 as an accepted/successful outcome for idempotency, and
  two separate full-suite `CouncilPanelControllerRealDataTests` runs were
  directly verified (via a throwaway script querying the real Postgres DB)
  to have reported `posted = true` for all 10 decisions while the DB held
  **zero** rows for that kingdom — i.e. the client can believe a decision
  was recorded when it silently wasn't, under concurrent server load. A
  same-session follow-up investigation (temporary request-level debug
  logging in `decisions.ts`, plus a standalone script firing up to 120
  concurrent `POST /api/v1/decisions` calls across 12 kingdoms outside
  Unity) could **not** reproduce it — 4 additional full Unity PlayMode
  suite runs and the scripted concurrent-load repro all came back clean
  (69/69, no mismatches). The underlying route logic
  (`decisions.ts`, `kingdoms.ts`'s conflict-safe insert, the
  `drizzle-orm` node-postgres transaction wrapper) was read closely and
  looks structurally sound — no leaked/unreleased pool connection, no
  shared mutable request state. Given it reproduced twice under real
  conditions before and zero times since across meaningful additional
  attempts, treat as a real, timing-dependent gap rather than resolved.
  **Partially addressed:** Fastify's request logger is now on
  (`logger: true` in `app.ts`, commit `94f8e8e`) so a future occurrence
  leaves a diagnosable paper trail — this session's own investigation had
  none to work from. The `ALLOW_TEST_DB_TRUNCATE` half of this note was
  stale when written: the env var was already wired
  (`server/test/integration/helpers/db.ts`'s `truncateTables()`, gated
  behind it, called in all 6 integration test files' `afterEach`) and set
  to `true` in this environment's real `.env` — the TypeScript
  integration suite's own test DB already resets between runs and was
  never the unbounded-growth source this note assumed. The real remaining
  risk (Unity PlayMode `*RealDataTests` hitting the same live server
  outside that TS-side truncation) is unquantified — not yet investigated
  further.
- **Milestone #12 (Ruler Portrait System)** shipped the first increment of
  a broader visual-art phase: 15 AI-generated portraits (5 Mood tiers x 3
  Loyalty tiers) that `CoreLoopScreenController.RefreshStatusLabels()`
  swaps in every time those stats change. Generated free via Hugging
  Face (public FLUX.1-schnell Space, then the authenticated router API
  after hitting the anonymous quota wall) rather than Higgsfield (out of
  credits). A final whole-branch review caught a real defect before merge
  — 3 of the 15 images had a different aspect ratio than the other 12,
  which under `preserveAspect = true` made the portrait visibly resize as
  mood changed — fixed by cropping and independently re-verified
  (PNG dimensions decoded byte-for-byte, not just trusted). Remaining
  non-blocking Minor items from that review, not yet acted on: no
  automated guard preventing a future art re-export from reintroducing
  the aspect-ratio bug (an EditMode test asserting all 15 sprites share
  one `rect.size` would close this); `CoreLoopScreenControllerTests`'s
  dummy sprites/textures aren't destroyed in `TearDown` (harmless test
  leak); the portrait's rect has only ~30px of accidental clearance from
  the status labels next to it (`CoreLoopSceneBuilder.cs`), not real
  margin — a longer/localized label could slide under it; all 15 sprites
  are 1024px sources displayed at 200x260 (no mobile-platform texture
  size override yet, worth doing before a store build). See
  `docs/superpowers/specs/2026-09-06-ruler-portrait-design.md` and
  `docs/superpowers/plans/2026-09-06-ruler-portrait-system.md`.
- **Milestone #13 (Themed Scene Backgrounds)** shipped the second
  increment of the visual-art phase: a full-screen throne-room backdrop
  that swaps with the player's selected Cosmetics theme
  (Default/Council Chamber/Harvest Hall), via one new line in
  `CosmeticsPanelController.ApplyTheme()`. This branch's final
  whole-branch review needed **two** fix-and-reverify rounds before
  merging — worth reading in full as a cautionary example:
  - Round 1 fixed 3 Important findings: (a) the shipped art was
    1024x1792, not the ~1024x2048 the design spec's stretch-to-fill
    reasoning assumed, causing a real ~19% horizontal squeeze on typical
    phones under `preserveAspect = false` -- fixed by switching to
    Unity's `AspectRatioFitter` in `EnvelopeParent` mode (crops instead
    of distorting); (b) white status labels measured ~3.8:1 contrast
    against the Harvest Hall background (below WCAG AA's 4.5:1); (c) no
    null/length guard on the new `backgroundSprites` array lookup -- the
    same class of gap that had already cost implementer time twice in
    milestone #12, hit here for a third time.
  - The contrast fix in round 1 **did not work**: it set
    `TextMeshProUGUI.outlineWidth`/`outlineColor`, which only set shader
    properties without enabling TMP's `OUTLINE_ON` keyword the mobile
    shader gates the whole outline render path behind -- the code
    compiled, both test suites stayed green, and the outline was
    genuinely invisible. It also silently created 55 per-label material
    instances, breaking UI batching (0 -> 55 `Material:` blocks in the
    scene). This was only caught because the re-review independently
    decoded the actual shader/material source rather than trusting a
    "tests pass" report -- worth remembering that a passing test suite
    proved nothing about this specific defect, since no test asserted
    anything about rendered outline visibility.
  - Round 2 fixed it by assigning TMP's own shipped, pre-configured
    `LiberationSans SDF - Outline.mat` (already has `OUTLINE_ON`) via
    `fontSharedMaterial` -- one shared material for every label, fixing
    the invisibility and the batching regression together. Re-verified
    by decoding the regenerated scene's serialized material references
    directly (55 GUID references to the shared asset, 0 inline instances)
    rather than trusting the fix report a second time.
  - Non-blocking follow-ups from the round-2 re-review, not yet acted
    on: the outline material load has a silent no-op if the hardcoded
    TMP asset path ever fails to resolve (inconsistent with this file's
    other asset loaders, which all `Debug.LogError` on failure -- exactly
    the kind of silent failure this milestone already got bitten by
    once); the WCAG comment cites the pre-fix ~3.8:1 measurement against
    an outline width that changed (Unity's preset is 0.1, the original
    finding measured against a hypothetical 0.2) and was never
    re-measured; 4 TMP input-field text objects still use the
    non-outlined default material (likely fine, sit on opaque input
    backgrounds, not directly on the scene art); no EditMode test
    guards against a future re-export reintroducing the aspect-ratio
    mismatch (same open recommendation as milestone #12's). See
    `docs/superpowers/specs/2026-09-06-scene-backgrounds-design.md` and
    `docs/superpowers/plans/2026-09-06-scene-backgrounds.md`.
- **Milestone #14 (Themed Panel Art)** shipped the third increment of the
  visual-art phase: painted background art for the History/Council/Events
  modal panels, per theme (3 panels x 3 themes). Two real, unavoidable
  scope reductions from the originally-approved design, both confirmed
  interactively before implementation began:
  - **Customize panel art dropped entirely.** Mid-generation, Hugging
    Face's authenticated router API hit a genuine monthly free-credit cap
    (distinct from the anonymous-tier per-session quota wall used in
    milestones #12-13, which resets much faster) — 8 of the planned 12
    images generated successfully before every remaining request started
    failing with "You have depleted your monthly included credits."
    Customize stays exactly as milestone #11 shipped it (fixed navy, not
    theme-reactive) — literally untouched by this milestone.
  - **`event_event.png` (Events panel, Harvest Hall theme) was never
    generated** — the one image lost to the credit cap among the 8 that
    remained in scope. Left deliberately unset; resolves through the
    pre-existing `GetBackgroundSprite` fallback-to-Default mechanism with
    zero special-case code. A new PlayMode regression test
    (`LoadedCoreLoopScene_PanelArt_HasNonNullSpritesUnderEventTheme`)
    seeds a save file selecting the Event theme before loading the real
    scene, specifically to exercise this fallback path end-to-end, not
    just assert non-null.
  - **5 of the 8 committed images had hallucinated fake artist
    signatures/watermarks** baked in by FLUX.1-schnell (reproducing
    stock-art credits from its training data) — one had a large, fully
    legible fake copyright stamp spanning the image width. Fixed before
    committing: 4 via `ffmpeg` crop (removing the affected edge) +
    rescale back to the original 1024x1792 (negligible stretch, under
    ~3% in most cases), 1 via `ffmpeg delogo` in-place blur (signature
    sat mid-image, not at an edge, so cropping wasn't viable). All 8
    final images independently verified 1024x1792 via `ffprobe` and
    visually reviewed before commit.
  - **The final whole-branch review caught 2 Important, purely
    spec-level defects** — both task-level reviews had passed correctly
    against what the spec/plan actually specified; the gap was that
    neither ever stated what the destination Image's tint or aspect
    ratio should be. (I-1) `ApplyTheme` set both `.color` (the existing
    dark per-theme tint) and `.sprite` (the new art) on the same three
    Images — Unity's UI shader multiplies sprite x color, so the art
    rendered at roughly 10-20% brightness, effectively invisible. (I-2)
    the 1024x1792 art was stretched ~53% horizontally into the 700x800
    panel rects with no aspect correction — worse than milestone #13's
    already-fixed 19% squeeze, using the exact same `AspectRatioFitter`
    mechanism sitting 300 lines up the same file. Both were confirmed via
    the actual serialized scene YAML, not speculation.
  - **Fixed via a dedicated art-layer architecture**, chosen interactively
    over two cheaper-but-worse alternatives (brightening the existing
    Image in place, which would have broken milestone #11's already-shipped
    color-tint tests; or `preserveAspect` letterboxing, which would have
    left transparent gaps at the panel edges exposing the scene background
    underneath). Each panel gained a new child `Image` (`historyArtImage`/
    `councilArtImage`/`eventArtImage`, default Unity white, matching the
    existing `sceneBackgroundImage` precedent exactly) carrying an
    `AspectRatioFitter` in `EnvelopeParent` mode against its *parent*
    panel (not the panel root itself — parenting the fitter to the root
    would have resized the whole 700x800 panel to full-screen, since
    `EnvelopeParent` drives its own RectTransform from its own parent),
    plus a `RectMask2D` on each panel root to clip the resulting overflow
    to the panel's fixed bounds. The original three Images/their
    `.color` assignments/their milestone #11 tests are completely
    unchanged. Independently re-verified against the regenerated scene's
    actual serialized transforms (envelope math confirmed exact to 4
    decimal places, GUID/parenting/sizeDelta all checked directly) rather
    than trusted from the fix report — this project's established
    standard after milestone #13's invisible-outline lesson.
  - Consulted the `ui-ux-pro-max` skill for scrim/contrast guidance before
    finalizing the fix; decided against adding a scrim layer — the
    existing TMP outlined-label material (already proven for the
    visually-similar, equally-busy scene-background case in milestone
    #13) should suffice, and a scrim wasn't clearly warranted by the
    guidance for this specific case. Not verified with a live
    screenshot/device render — the fix's correctness rests on independently-checked
    serialized-scene math (exact envelope ratio, confirmed white tint,
    confirmed child-not-root fitter placement) rather than a rendered
    image. Recommended next step if any panel's readability looks off in
    practice: a real screenshot per theme, and revisit the scrim question
    if labels are hard to read over busy art.
  - Non-blocking Minor items from the fix's re-review, not yet acted on:
    the three new art Images are dereferenced with no null guard in
    `ApplyTheme` (unlike `sceneBackgroundImage` just below them, which is
    guarded) — this is what caused 9 expected PlayMode failures against
    the stale pre-rebuild scene, not a new hazard but worth guarding
    consistently; the new scene-level regression test dereferences the
    `PanelArt` children/their sprites without an `Assert.IsNotNull` first,
    so a missing-child regression would surface as a bare NRE instead of
    a diagnostic message; one stale XML-doc comment describing the test's
    pre-strengthening assertions; a handful of `AspectRatioFitter`
    pivot/`preserveAspect` lines that just restate Unity's own defaults
    (harmless, mirrors the existing scene-background code's style).
    Texture budget (8 more 1024x1792 sources, ~23MB, no mobile
    `maxTextureSize` override) folds into the same open item milestones
    #12-13 already recorded. See
    `docs/superpowers/specs/2026-09-06-panel-art-design.md` and
    `docs/superpowers/plans/2026-09-06-panel-art.md`.
- **Milestone #15 (Button Icons)** shipped the fourth and originally-final
  increment of the visual-art phase: small painted icons on 4 of the
  game's 15 buttons (Submit Recommendation, Challenge a Rival Kingdom,
  View History, Council), sitting left of each button's existing text
  label. The other 11 buttons/8 remaining unique icons are deferred --
  Hugging Face's monthly credit cap was hit again mid-batch, this time
  after only ~5 generations (vs. ~9 the first time it was hit, during
  milestone #14 the day before) -- see the updated "Known limit" section
  of the HF-token memory note for this observation. No code-shape changes
  needed to add the rest later; the pattern (art file, loader call, icon
  child GameObject, label offset, `Initialize` field) is now a clean,
  copy-pasteable template across all 4 landed buttons.
  - **Architecturally distinct from milestones #12-14**: icons are purely
    static (no mood/theme/state variation), so unlike the three prior
    increments there is no `ApplyTheme`-equivalent method and no
    `GetBackgroundSprite`-style fallback lookup -- a plain `Sprite` per
    icon, loaded once by `CoreLoopSceneBuilder.Build()` and assigned to a
    dedicated `Image` child on each button. Confirmed interactively before
    implementation: icon `Image` references are still wired through each
    button's owning controller via a new trailing `Initialize(...)`
    parameter (not left as scene-only decoration), purely so a future
    milestone could add dynamic icon theming without another interface
    change -- nothing reads these fields dynamically yet.
  - **Task 1's diff had to touch 5 test files beyond its own brief's file
    list** (`DuelModalGateInterleavingTests`,
    `HistoryPanelControllerRealDataTests`, `EventPanelControllerTests`,
    `EventPanelControllerRealDataTests`, `CouncilPanelControllerRealDataTests`)
    because they also construct the same 4 controllers directly for
    unrelated test scenarios, and the plan's brief (written before
    implementation) hadn't traced every caller across the whole test
    suite. Each got exactly one appended `null` placeholder argument --
    both the task reviewer and the final whole-branch reviewer
    independently verified (via `git log --all` on each filename) that
    this boundary held for the rest of the branch: those 5 files were
    never touched again once Task 2 wired the real icons.
  - **Final whole-branch review passed clean on the first attempt** (no
    fix-and-reverify round needed, unlike milestones #13 and #14) --
    Ready to merge: Yes, zero Critical/Important findings, only 2 Minor
    notes: `Verify()`'s new icon-check block re-runs `FindFirstObjectByType`
    lookups already done earlier in the same method for other checks
    (matches a pre-existing per-check-block convention in this file, not
    a new anti-pattern); and the icon geometry itself (8px inset, 32x32,
    40px label offset) has no automated test coverage -- the 4 new
    per-controller tests are reflection-based field-storage checks
    (near-tautological, but that's the correct floor given icons have no
    other observable behavior this pass), and the new scene-level
    regression test checks non-null sprites but not position/size. Worth
    adding a `RectTransform` geometry assertion if this layout code is
    ever touched again.
  - **Process note**: the opus-model final review hit a session-wide rate
    limit mid-review (a genuinely small, low-risk 4-icon branch didn't
    need the most expensive model) and had to be retried on sonnet, which
    completed cleanly in under 2 minutes. Going forward, final-review
    model choice should scale with actual diff risk/size rather than
    defaulting to the most capable model for every branch regardless of
    scope.
  - See `docs/superpowers/specs/2026-09-07-button-icons-design.md` and
    `docs/superpowers/plans/2026-09-07-button-icons.md`.
  - **Follow-up same day**: HF credits became available again a short
    while later, so 3 more icons (Events, Customize, Claim Reward) were
    generated and shipped as a direct follow-up commit on `main`
    (`f1afe3d`) rather than through the full subagent-driven-development
    pipeline -- by this point the pattern had shipped clean 4 times in a
    row with zero Important findings across 3 independent reviews, so the
    controller/scene-builder wiring was done directly, self-reviewed
    against the actual scene YAML (RectTransform math and sprite GUID
    references cross-checked by hand, not just trusted), and merged after
    a fresh full-suite pass (EditMode 77/77, PlayMode 81/81). No new
    per-controller reflection tests were added this round (the earlier
    final review had already flagged those as near-tautological); the
    existing scene-level regression test was extended to cover all 7
    shipped icons instead. 7 of 12 unique icons now live (Submit,
    Challenge, View History, Council, Events, Customize, Claim Reward);
    5 icons/6 button slots remain deferred (Create Council, Join Council,
    Skip, Next, shared Close).
- **Resolved (`fix/portrait-array-null-guard`, commit `cc47d60`):**
  the recurring "new `[SerializeField]` deserializes as null/empty on
  the old committed scene, and the field is indexed without a
  null-array guard" pattern from milestones #12/#13 (cost implementer
  time in 3 separate task implementations: ruler-portrait Task 2,
  scene-backgrounds Task 1, and scene-backgrounds Task 2 avoided it only
  because Task 1 already forced the rebuild). The guard had already
  landed for `backgroundSprites`/`historyPanelSprites`/
  `councilPanelSprites`/`eventPanelSprites` (all route through
  `CosmeticsPanelController.GetBackgroundSprite`, which already
  null/length-guards), but a deliberate audit found one remaining gap:
  `CoreLoopScreenController.RefreshStatusLabels()` indexed
  `rulerPortraits[portraitIndex]` directly with no array-level null
  check at all -- a null `rulerPortraits` array NRE'd immediately rather
  than degrading gracefully. Fixed via a new `GetPortraitSprite(...)`
  helper mirroring `GetBackgroundSprite`'s exact fallback shape (null
  array, too-short array, out-of-range index, or a missing sprite at the
  selected index all resolve to the Neutral/Medium fallback, index 7,
  instead of throwing). TDD: a new regression test first captured the
  real `NullReferenceException` (RED), then confirmed the fix (GREEN).
  This was the last unguarded instance of the pattern in the codebase --
  no further audit needed unless a future milestone adds another raw
  array index outside `GetBackgroundSprite`/`GetPortraitSprite`.

Full task-by-task history (every commit, every review verdict, every
fix round) lives in the git-ignored `.superpowers/sdd/progress.md` ledger
for the duration of active development.

## 9. Open Questions

`Q1: Backend hosting provider (AWS/GCP/Azure/managed Postgres service) — Owner: you — Blocking: No, only needed before backend scaffolding starts`

`Q2: Monetization model detail — pure cosmetic IAP vs. also a season pass/subscription — Owner: you — Blocking: No, only needed before the store/payment integration milestone`
