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

*(Milestones #12-13 are the first two increments of a broader visual-art
phase — button icons and panel art are still not started, see "Known
follow-up items" below. Milestone #9 merged after a manual playtest was still outstanding —
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
  follow-ups, none of which weaken the fix itself: (N-1) the seed reads
  the newest-by-`created_at` decision rather than `MAX(cycle_number)` --
  these coincide under normal sequential play, but could theoretically
  diverge under a rare double-refresh-callback race
  (`DrainPendingRefreshCallbacks` firing two queued syncs back-to-back),
  landing the seed one cycle low; same silent-drop failure class as C-1
  itself, narrow and self-recovering (one dropped decision, corrected on
  the next launch) but worth switching to an explicit
  `ORDER BY cycle_number DESC` / `MAX(cycle_number)` query before this
  sees meaningful player traffic. (N-2) the seed fetch is bootstrap-only
  with no in-session retry if `EnsureKingdom` fails at launch (unlike the
  duel/history/event request paths, which do retry via
  `EnsureKingdomThenSend*`); its own log message ("will resync on next
  attempt") is misleading since the only next attempt is the next app
  launch. (N-3) the new regression test
  (`DecisionCycleManagerSessionResumeTests`) relies on prior test
  fixtures' teardown for session isolation rather than calling
  `SessionStore.Clear()` itself at setup. (N-4) a fixed
  `WaitForSeconds(3f)` for two sequential real round-trips is a latent
  flake source under a slow network, matching this project's existing
  convention for real-data test fixtures elsewhere.
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
  Recommended before this sees real player traffic: enable Fastify's
  built-in request logger (currently `logger: false` in `app.ts`, which is
  exactly why this session's own investigation had no request trail to
  work from) so a future occurrence leaves a diagnosable paper trail, and
  wire up or remove the currently-unwired `ALLOW_TEST_DB_TRUNCATE` env var
  so the ever-growing, never-reset shared test DB doesn't keep making
  `maybeAdvanceCouncilMilestone`'s unbounded join slower over time.
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
- **Recurring pattern across milestones #12 and #13, not yet fixed:**
  the "new `[SerializeField]` deserializes as null/empty on the old
  committed scene, and the field is indexed without a null-array guard"
  failure has now cost implementer time in 3 separate task
  implementations (ruler-portrait Task 2, scene-backgrounds Task 1, and
  scene-backgrounds Task 2 avoided it only because Task 1 already forced
  the rebuild). Each time, the immediate fix was "regenerate the scene
  one task early" rather than "guard the array access" -- the guard
  itself only landed for scene-backgrounds' `backgroundSprites` (as
  Important finding I-3), not more broadly. Worth a deliberate pass
  before a 3rd visual-art milestone: either add null-array guards
  wherever a plan prescribes an unguarded `SerializeField` array/object
  index (not just null-element fallbacks), or explicitly design future
  plans to regenerate the scene inside the first task that adds any new
  serialized field, rather than treating each occurrence as a
  surprise.

Full task-by-task history (every commit, every review verdict, every
fix round) lives in the git-ignored `.superpowers/sdd/progress.md` ledger
for the duration of active development.

## 9. Open Questions

`Q1: Backend hosting provider (AWS/GCP/Azure/managed Postgres service) — Owner: you — Blocking: No, only needed before backend scaffolding starts`

`Q2: Monetization model detail — pure cosmetic IAP vs. also a season pass/subscription — Owner: you — Blocking: No, only needed before the store/payment integration milestone`
