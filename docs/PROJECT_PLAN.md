# Project Plan — Understudy Kingdom

**Version:** 1.0 | **Status:** Pre-production | **Last updated:** 2026-09-01

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
users:            id (UUID PK), device_id (unique, nullable), device_secret_hash (nullable),
                  google_sub (unique, nullable), apple_sub (unique, nullable),
                  email (nullable), created_at, country_code

kingdoms:         id (UUID PK), user_id (FK -> users), founded_at
                  -- 1:1 with ruler_npcs; the FK lives on ruler_npcs.kingdom_id
                  -- (single direction, no redundant back-reference -- see
                  -- backend Task 1 review, 2026-09-01)

ruler_npcs:       id (UUID PK), kingdom_id (FK -> kingdoms), mood (int), loyalty (int),
                  agenda (enum), trait_seed (int)

decisions:        id (UUID PK), kingdom_id (FK -> kingdoms), cycle_number (int),
                  player_recommendation (jsonb), ruler_outcome (jsonb),
                  overridden (bool), created_at

councils:         id (UUID PK), name, ruler_alignment (enum)
council_members:  council_id (FK -> councils), user_id (FK -> users), joined_at   -- many-to-many

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
Request:  { kingdom_id: uuid, cycle_number: int, recommendation: object,
            ruler_outcome: object, overridden: bool }
          -- ruler_outcome/overridden are client-reported, not server-computed:
          -- the client stays authoritative for the decision cycle in this pass
          -- (see docs/superpowers/specs/2026-09-01-auth-decisions-backend-design.md
          -- Scope Decisions). Server-authoritative scoring is a Future Phase item.
Response: { decision_id: uuid, ruler_outcome: object, overridden: bool }
Errors:   400 (malformed request, incl. non-UUID kingdom_id), 401 (auth),
          403 (kingdom not owned by caller), 409 (cycle already resolved)
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

## 8. Open Questions

`Q1: Backend hosting provider (AWS/GCP/Azure/managed Postgres service) — Owner: you — Blocking: No, only needed before backend scaffolding starts`

`Q2: Monetization model detail — pure cosmetic IAP vs. also a season pass/subscription — Owner: you — Blocking: No, only needed before the store/payment integration milestone`
