# Design: Estate Economy Roadmap (Phases 1-5)

**Date:** 2026-09-07 | **Status:** Approved, Phase 1 detailed spec follows in
`2026-09-07-estate-phase1-land-crops-design.md`

## Purpose

Adds a full land/crop/animal/production/trade economy ("the Estate") to
Understudy Kingdom, on top of the existing advisor/ruler narrative loop
(FR-01–FR-13, all shipped). This is not one feature — it's five, each with
its own hard dependency on the last, so it ships as five separate specs and
implementation plans, the same way milestones #1–15 already shipped one at
a time. This document is the roadmap; each phase gets its own detailed
design doc when its turn comes.

## Market Research (grounding for every scope decision below)

Researched Hay Day, Township, and the wider farm/village-sim genre
(Diandian: ~$1.03B 2025 revenue, proof the genre sustains real revenue
without live-service gimmicks).

**Core loop every competitor converges on:** start with a small plot +
starter coins -> buy seeds -> plant -> real-time grow timer -> harvest ->
sell -> buy more land (escalating cost/time per plot) -> buy animals ->
feed them crops -> collect and sell produce -> production buildings convert
raw goods into higher-value goods (Hay Day: wheat -> flour -> bread).

**Reported player complaints (the pattern to design out, not patch
later):** storage fills faster than players can act, leaving them "stuck
watching crops grow"; slow loops become monotonous; frequent ads;
purchase pressure to skip waits. This is structurally the same complaint
shape already documented in `docs/COMPETITOR_ANALYSIS.md` for Understudy
Kingdom's existing competitors (MONOPOLY GO!, Honor of Kings) — progress
deliberately throttled to create a purchase moment. Same fair-play stance
applies here: design it out from Phase 1, don't patch it in later.

Sources: [Hay Day guide](https://www.haydayfarming.com/) ·
[Township land expansion](https://www.appgamer.com/township/strategy-guide/land-expansion) ·
[Township review](https://www.mmommorpg.com/simulation/township/) ·
[Hay Day review](https://www.playnforge.com/hay-day-review/) ·
[Mobile game revenue stats 2026](https://www.tekrevol.com/blogs/mobile-game-revenue-statistics/)

## Scope Decisions (whole initiative)

- **Single-player only.** No neighbor visits, no player-to-player trading,
  no live multiplayer economy — explicit requirement. The Trade phase
  (Phase 4) is a single-player NPC marketplace, not a social feature. This
  also means Estate work never touches `server/` the way Council/Duel do
  (see per-phase state-ownership decisions below) — it's a different kind
  of feature than this codebase's existing social/PvP work.
- **Reuses the existing single `CoreLoop` scene and panel architecture.**
  Every phase adds a new button + modal panel (matching
  Cosmetics/Council/History/Events), not a new `.unity` scene — this
  project has never loaded a second scene, and there's no reason to start
  now just for Estate.
- **Reuses the existing weekly live-ops event system (FR-10) for seasonal
  content** (limited-time crops/animals) instead of building a parallel
  events system.
- **Differentiation stance, concretely:** no storage cap that blocks
  harvesting; land-plot cost curve is always visible before purchase, never
  opaque; no crop wilting/death mechanic that punishes a missed session;
  automation (the recruitable Shop Seller, Phase 4) is an earned mid-game
  goal, not something gated behind spend.
- **Monetization stays out of scope for every phase below**, consistent
  with FR-14/FR-15 (monetization guardrails) already being deferred,
  undecided project-wide — Estate is a gameplay-depth initiative, not a
  monetization initiative, and nothing here should be read as a decision
  to attach IAP to it.

## Phase Breakdown

### Phase 1 — Land, Coins & Crops (next; see detailed spec)
The foundation everything else depends on: a new `Coins` currency, a grid
of owned/lockable land plots, and the plant -> water -> grow (staged,
sprite-swap) -> harvest loop. Watering is a real mechanical decision (an
unwatered crop stalls, it doesn't wilt or die) rather than Hay Day's
passive wait-and-collect — this is the concrete "beat the genre" hook:
active care instead of a pure timer.

### Phase 2 — Animals & Breeding
Buy animals with Coins, feed them crops harvested in Phase 1, collect
eggs/milk/wool on a timer, sell for Coins. Breeding: spend Coins + two
animals to produce a new (possibly higher-tier) animal.

### Phase 3 — Shops / Production Chains
Buildings (Bakery, Mill, etc.) that convert raw crop/animal goods into
higher-value processed goods (wheat -> flour -> bread, milk -> cheese),
mirroring Hay Day's production-chain depth. Each conversion is a
coin-cost + time-cost decision, never a hard paywall.

### Phase 4 — Trade / Marketplace + Recruitable Shop Seller
A single-player NPC marketplace: sell raw or processed goods, rotating buy
orders for bonus payouts. Adds the **Shop Seller** — an NPC the player can
recruit (Coins + a progression milestone, not real money) who then
automatically sells harvested/produced goods at market rate without the
player manually tapping through every sale. This is the mid-game
automation/idle layer that gives the Estate legs past the first few
sessions instead of demanding constant manual attention forever — and it
doubles as a concrete retention goal (see below).

### Phase 5 — Integration with the Advisor/Kingdom systems
Decides how Estate output connects back to the game's existing identity:
candidates include Council milestones rewarding Estate goods, Customize
unlocks costing Coins instead of being free, and/or Estate tiers
unlocking new ruler-agenda-aligned crop/animal options. Deliberately
decided last, once Phases 1–4 exist to integrate *with* — designing this
before there's a real economy to connect would be speculative.

## Progression & Retention Loop

The piece none of Phases 1-4 answer alone: why does a player open the app
on day 30, not just day 1.

- **Tiers**: better seeds/animals/land unlock as the player's Estate
  levels up (Coins earned + milestones hit) — there's always a concrete
  next upgrade.
- **Seasonal rotation**: limited-time crops/animals riding the
  already-shipped weekly-event system (FR-10), so this reuses existing
  infrastructure instead of a second events system.
- **Automation as an earned mid-game goal**: recruiting the Shop Seller
  (Phase 4), and any later hireable helpers, is itself a milestone worth
  playing toward — not a day-one giveaway, not a spend-to-skip.
- **Estate prestige**: once a plot/region is fully built out, a
  reset-for-permanent-bonus loop (standard in this genre, e.g. Township's
  town-level resets) gives long-term players a reason to keep going
  instead of hitting a hard content wall. Exact shape deferred to whichever
  phase first has enough content to prestige.

## Explicitly Out of Scope (whole initiative, this pass)

- Cross-device/server-authoritative Estate state (Phase 1 decision, may be
  revisited once real playtime data shows reinstall-loses-everything is a
  real complaint — see Phase 1 spec's Scope Decisions).
- Player-to-player trading, neighbor visits, any social/live multiplayer
  economy mechanic.
- Any real-money IAP tied to an Estate mechanic — monetization is a
  separate, still-undecided project-wide decision (FR-14/15).
- Crop wilting/death, weather effects, more than a first-pass content
  volume (crop/animal/building counts) — each phase's own spec sets its
  first-pass content list; expanding it is future-phase work, not this
  roadmap's job to pre-decide.
