# Design: Estate Phase 1 — Land, Coins & Crops

**Date:** 2026-09-07 | **Status:** Approved, pending implementation plan

## Purpose

First phase of the Estate economy roadmap (see
`docs/superpowers/specs/2026-09-07-estate-economy-roadmap-design.md`).
Establishes the foundational `Coins` currency, land-plot ownership, and the
plant -> water -> grow -> harvest crop loop every later phase (Animals,
Shops, Trade) depends on. Adds a new "Estate" panel to the existing
`CoreLoop` scene, opened from a new throne-room button — the same
panel-from-a-button pattern as Cosmetics/Council/History/Events.

## Scope Decisions

- **Client-authoritative, locally persisted — no server work this phase.**
  Mirrors how `RulerState` (mood/loyalty/agenda) works today: computed and
  saved locally, never synced server-side. Estate genuinely has no
  multiplayer/shared-state need (single-player only, per the roadmap), so
  there's no reason to introduce a server surface for it. Revisit only if
  real playtime data shows reinstall-loses-everything is an actual player
  complaint — not a speculative concern to design around now.
- **New save file, not a new field on the existing one.** Council's design
  added one bool field to `RulerState`/`RulerSaveData` because it was a
  single small addition to an existing concept. Estate is a genuinely
  separate, much larger subsystem (a whole plot array), and
  `SaveService.Save(RulerState)`/`Load()`'s signatures are used at every
  existing call site (`DecisionCycleManager`, `CoreLoopScreenController`,
  `TutorialOverlayController`, `CouncilPanelController`, every test that
  touches state). Changing those signatures to thread Estate data through
  has a wide, unnecessary blast radius. Instead: a second, independent
  `SaveService.SaveEstate(EstateState)` / `LoadEstate()` pair, writing
  `estate_save.json` alongside the existing `ruler_save.json`. Two small
  independent save files is simpler and lower-risk than one save API
  serving two unrelated concerns.
- **Land grid: 8 total plots, 4 unlocked at start, 4 locked.** Enough
  starting plots for a real crop-rotation decision (fast vs. slow crops)
  without overwhelming a first session — matches Township/Hay Day's small
  starting footprint. 8 is a fixed cap for this pass (no plot-count growth
  beyond it yet); revisit once Phase 2/3 need more surface area.
- **Land-plot unlock cost: geometric, `100 * 1.6^(plotIndex - 4)` coins,
  rounded to the nearest 10, always shown before purchase** (exponent is
  relative to the locked plot's own position — plot index 4 is the first
  locked plot, exponent 0). Plot 4 (first locked) costs 100, plot 5 costs
  160, plot 6 costs 260, plot 7 (last) costs 410. Deliberately predictable
  and always visible — direct response to the "opaque expansion cost"
  pattern in the market research (Township specifically). The 200-coin
  starting balance is sized to afford exactly plot 4 immediately (see
  "Starting balance" below) without needing a single harvest first.
- **3 starting crops — Wheat (fast/cheap), Carrot (mid), Pumpkin
  (slow/high-value)** — the fast/mid/slow tiering every researched
  competitor uses (Hay Day's own wheat is the fast tier too). Three is
  enough for a real rotation decision without a large content-authoring
  lift for a first pass; more crops are future-phase content, not a Phase
  1 blocker.
- **Watering, not a wilt/timer-decay mechanic.** A planted crop stays at
  growth stage 0 (sprout) indefinitely until watered — no timer runs, no
  penalty for leaving it, no missed-session punishment. Watering starts the
  real-time growth timer. Re-watering mid-growth has no mechanical effect
  this pass (avoids needing a stacking-bonus balance model with no data to
  tune it against). This is the concrete differentiation from Hay Day's
  passive "just wait" loop: watering is an active decision that unblocks
  progress, not a chore layered on top of a timer that runs regardless.
- **"Animation" scope: staged sprite-swap growth + short UI tweens, not a
  new animation pipeline.** This project's art pipeline is static image
  generation (Hugging Face FLUX.1-schnell), already used for the ruler
  portrait's 9-sprite mood/loyalty tier grid
  (`CoreLoopScreenController.GetPortraitSprite`) — no skeletal rigging or
  frame-by-frame animation tooling exists or is being introduced. Each crop
  gets 3 stage sprites (sprout/growing/mature), swapped exactly like the
  portrait grid already is. "Full length animation" for the plant/water/
  harvest actions means short procedural UI tweens (`IEnumerator`
  coroutines animating `RectTransform`/`Image` over a fixed duration —
  matching this codebase's existing zero-third-party-animation-dependency
  convention, no DOTween/LeanTween): a scale-bounce on planting, a
  splash-tint flash on watering, a pickup-arc-to-the-coin-counter on
  harvest. Flagging this explicitly so it isn't read later as a request for
  hand-animated character/sprite art this project has no tooling for.
- **Starting balance: 200 coins.** Enough to unlock exactly one extra plot
  immediately, or buy several rounds of seeds — a first-pass number, not a
  tuned economy (real balancing needs real playtime data this project
  doesn't have yet).

## Approach

Same "controller owns a panel + a scene-builder wires it" architecture as
every existing panel (`CouncilPanelController`, `HistoryPanelController`,
`CosmeticsPanelController`). `EstatePanelController` owns the Estate
button, the panel root, the 8-plot grid, the coin-balance label, and the
plant/water/harvest/unlock interactions. All state lives in a new
`EstateState` class (mirrors `RulerState`'s role/shape), persisted through
the new `SaveService.SaveEstate`/`LoadEstate` pair. Crop definitions are a
static, code-defined catalog (`CropCatalog`) — not save data — the same
"static content table vs. save data" split the ruler dialogue templates
already use.

Growth is computed on read, not ticked by a running timer: each time the
panel refreshes (on open, and after any plant/water/harvest action), every
watered-but-not-mature plot's stage is recomputed from
`(now - WateredAtUnixSeconds) / CropDefinition.GrowDurationSeconds`. This
means no background coroutine has to run while the panel is closed or the
app is backgrounded — growth simply reflects real elapsed wall-clock time
whenever the player next looks, matching how `DecisionCycleManager`
resolves everything synchronously on the triggering action rather than
running a background clock.

## Data Model

```csharp
// Assets/Scripts/Core/EstateState.cs (new)
[Serializable]
public class EstateState
{
    public int Coins = 200;
    public LandPlot[] Plots;  // fixed length 8; index 0-3 start Unlocked=true

    public EstateState()
    {
        Plots = new LandPlot[8];
        for (int i = 0; i < Plots.Length; i++)
        {
            Plots[i] = new LandPlot { Unlocked = i < 4 };
        }
    }
}

[Serializable]
public class LandPlot
{
    public bool Unlocked;
    public string CropId;              // null/empty = empty plot
    public long PlantedAtUnixSeconds;  // 0 = not planted
    public long WateredAtUnixSeconds;  // 0 = planted but never watered (stalled at stage 0)
}
```

```csharp
// Assets/Scripts/Core/CropCatalog.cs (new) — static, not persisted
public readonly struct CropDefinition
{
    public readonly string Id;
    public readonly string DisplayName;
    public readonly int SeedCost;
    public readonly int GrowDurationSeconds;
    public readonly int SellValue;
    // StageSprites resolved by CropId + stage index via a scene-builder-time
    // LoadIconSprite-style lookup (Assets/Art/Crops/{Id}_{stage}.png),
    // mirroring how button icons and ruler portraits are already loaded --
    // not stored on this struct itself, kept out of EditMode-testable data.
}

public static class CropCatalog
{
    public static readonly CropDefinition[] All =
    {
        new CropDefinition("wheat",   "Wheat",   seedCost: 5,  growDurationSeconds: 30,  sellValue: 12),
        new CropDefinition("carrot",  "Carrot",  seedCost: 15, growDurationSeconds: 120, sellValue: 40),
        new CropDefinition("pumpkin", "Pumpkin", seedCost: 40, growDurationSeconds: 300, sellValue: 110),
    };

    public static CropDefinition? Find(string id) { /* linear scan, 3 entries */ }
}
```
Grow durations above are real-time seconds for a first playable pass
(30s/2min/5min) — short enough to test and feel in a single session;
final tuning is a balance pass against real playtime, not a design
question this spec answers.

## Client

### `Assets/Scripts/Core/EstateState.cs` (new)
Data class as above. `GrowthStage(LandPlot, CropDefinition, long nowUnixSeconds)`
static helper: returns `0` if `WateredAtUnixSeconds == 0` (stalled);
otherwise `Mathf.Clamp((now - WateredAt) / (GrowDurationSeconds / 2), 0, 2)`
mapped so stage 2 (mature) is reached exactly at `GrowDurationSeconds`
elapsed, stage 1 at the halfway point. Pure function, no `UnityEngine`
dependency beyond `Mathf` (matches `OverrideEvaluator`'s
`UnityEngine`-light precedent) — fully EditMode-testable.

### `Assets/Scripts/Core/CropCatalog.cs` (new)
Static catalog as above.

### `Assets/Scripts/Core/SaveService.cs` (modified)
Adds `SaveEstate(EstateState)` / `LoadEstate()`, following the exact same
shape as the existing `Save`/`Load` (a private `EstateSaveData` DTO,
`JsonUtility`, the same corrupt-file-returns-fresh-default handling,
written to `Path.Combine(Application.persistentDataPath, "estate_save.json")`).
The existing `Save(RulerState)`/`Load()` pair and `SavePath` are untouched.

### `Assets/Scripts/UI/EstatePanelController.cs` (new)
Owns: the Estate button, panel root (hidden by default), 8 plot-slot UI
elements (each: background, crop-stage image, a locked-overlay + unlock
button/cost label for locked plots), and the coin-balance label.

**On open:** disable shared controls (joins `SetCoreLoopControlsInteractable`'s
existing set, matching Council/History), show panel, call
`RefreshPlots()` (recomputes every plot's growth stage from wall-clock
time and updates each slot's sprite/label).

**Tap an empty unlocked plot:** show a seed picker (3 buttons, one per
`CropCatalog.All` entry, each showing cost; disabled if `Coins < SeedCost`).
Picking a seed: deduct `SeedCost`, set `CropId`/`PlantedAtUnixSeconds = now`,
`WateredAtUnixSeconds = 0`, play the plant-tween, refresh that slot's sprite
to stage-0.

**Tap a planted-but-unwatered plot:** set `WateredAtUnixSeconds = now`, play
the water-tween, refresh that slot.

**Tap a mature (stage 2) plot:** harvest — `Coins += SellValue`, clear the
plot's `CropId`/`PlantedAtUnixSeconds`/`WateredAtUnixSeconds` back to empty,
play the harvest-tween (arcs to the coin-balance label), refresh coin label
and that slot.

**Tap a locked plot:** show its unlock cost
(`100 * 1.6^(plotIndex - 4)`, rounded to nearest 10); tap Unlock (disabled
if insufficient coins) deducts coins, sets `Unlocked = true`, refreshes.

**On close:** `SaveService.SaveEstate(state)`, hide panel, re-enable shared
controls. (Saving on close, not on every micro-action, matches this
codebase having no existing "save on every mutation" precedent to follow —
`DecisionCycleManager` is the only place that saves per-action today, and
that's because a decision is itself the unit of progress; here the panel
session is the natural unit.)

### `Assets/Editor/CoreLoopSceneBuilder.cs` (modified)
Adds an "Estate" button (same `CreateSlider`/`CreateLabel`-style creation
pattern as every existing button) wired to a new `EstatePanelController`,
which needs references to the panel root, the 8 plot-slot GameObjects
(background Image, stage Image, locked-overlay, cost label, per slot), the
seed-picker sub-view, and the coin-balance label. `Verify()` gains an
`EstatePanelController` check mirroring the existing panel-controller
checks.

## Data Flow

```
Player taps "Estate"
  -> EstatePanelController: disable shared controls, show panel
  -> RefreshPlots(): for each Unlocked plot with a CropId, recompute stage
     from wall-clock elapsed time; update sprites/labels; show Coins

Player taps an empty unlocked plot
  -> show seed picker (Wheat/Carrot/Pumpkin, costs shown, unaffordable ones disabled)
  -> pick Wheat (Coins >= 5)
  -> Coins -= 5; plot.CropId="wheat"; PlantedAt=now; WateredAt=0
  -> plant-tween; slot shows stage-0 (sprout) sprite

Player taps that same plot again (still WateredAt==0)
  -> plot.WateredAt = now
  -> water-tween; slot's growth timer conceptually starts

Player reopens the panel 30+ seconds later
  -> RefreshPlots() recomputes: elapsed >= GrowDurationSeconds -> stage 2 (mature)
  -> slot shows mature sprite

Player taps the mature plot
  -> Coins += 12; plot cleared back to empty
  -> harvest-tween arcing to the coin label; coin label updates

Player taps a locked plot (index 5, cost 160)
  -> Coins < 160: Unlock button shown disabled, cost label visible
  -> (later, once Coins >= 160) tap Unlock -> Coins -= 160; Unlocked=true

Player closes the panel
  -> SaveService.SaveEstate(state); shared controls re-enabled
```

## Error Handling

No network calls this phase, so no server-error surface exists. The only
failure modes are local-file-read issues (corrupt/missing
`estate_save.json`), handled identically to `SaveService.Load()`'s
existing pattern: any parse failure or missing file returns a fresh
default `EstateState` (200 coins, 4 unlocked empty plots) rather than
throwing.

## Testing

**EditMode:**
- `EstateState.GrowthStage(...)`: stage 0 when `WateredAtUnixSeconds == 0`
  regardless of elapsed time (the stalled case); stage 0/1/2 at the
  correct elapsed-time thresholds once watered; clamps at stage 2 past
  full duration (no stage 3).
- `CropCatalog`: all 3 entries have positive `SeedCost`/`GrowDurationSeconds`/`SellValue`;
  `Find` returns null for an unknown id.
- `SaveService.SaveEstate`/`LoadEstate` round-trip (coins, plot
  unlocked/crop/planted/watered fields all survive a save+load cycle);
  corrupt-file and missing-file cases both return a fresh default
  `EstateState`, matching `Load()`'s existing corruption-handling test
  shape.
- Land-unlock cost formula: exact expected value at plot index 4 (100)
  and index 7 (410), guarding the rounding behavior.

**PlayMode:**
- `EstatePanelController`: planting deducts coins and is blocked when
  unaffordable; watering an unwatered plot starts growth (stage becomes
  reachable); harvesting a mature plot awards `SellValue` coins and clears
  the plot; tapping a non-mature plot does not harvest; unlocking a locked
  plot deducts the correct cost and is blocked when unaffordable; closing
  the panel persists state (reopen a fresh controller instance backed by
  the same file and confirm it reflects prior changes) — mirrors
  `CosmeticsPanelController`'s existing persistence-across-reopen test
  shape.
- Scene-level regression test (extends `CoreLoopSceneTests.cs`): the
  `CoreLoop` scene loads with an `EstatePanelController` present, the
  Estate button opens the panel, and the panel starts with 4 unlocked +
  4 locked plots — matching the existing "button opens panel" pattern
  already proven for Council/History/Cosmetics/Events.

## Explicitly Out of Scope for This Phase

- Animals, breeding, shops/production chains, trade/marketplace, the
  recruitable Shop Seller (Phases 2-4).
- Server sync / cross-device persistence of Estate state (see Scope
  Decisions).
- Re-watering bonuses, crop wilting/death, weather effects.
- More than 3 crop types or 8 total plots (first-pass content volume).
- Any currency conversion or interaction between `Coins` and the existing
  Mood/Loyalty/Agenda stats — that connection is explicitly Phase 5's job,
  once there's a real economy to connect.
