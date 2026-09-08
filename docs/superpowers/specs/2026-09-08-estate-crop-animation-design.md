# Design: Estate Crop Animation (Plant / Water / Grow / Harvest)

> Reads `2026-09-08-estate-real-feel-vision.md` first for the shared
> interaction philosophy this spec applies concretely to crops.

## Purpose

Make the existing plant -> water -> grow -> harvest loop (Estate Phase 1,
extended by Phase 3's harvest-to-inventory change) feel like a real,
tactile action at each step, per the user's standing "real-feel"
directive. This is a presentation-layer phase only -- no change to the
underlying save format, growth timing, or crop values.

## Current State (what already exists)

- **Plant:** `OnSeedPicked` already plays a small `ScaleBounce` on the
  newly-planted plot's `stageImage` (`EstatePanelController.cs:323`). No
  seed-specific visual -- the stage sprite just appears and bounces.
- **Water:** `OnPlotTapped`'s water branch plays a 0.2s blue `ColorFlash`
  on the plot (`EstatePanelController.cs:251`). No water-specific visual.
- **Growth:** `GrowthStage` (`EstateState.cs:60`) is a pure, already-tested
  function computing stage 0/1/2 from `WateredAtUnixSeconds` and
  `crop.GrowDurationSeconds`. `RefreshPlots()` swaps `stageImage.sprite`
  instantly whenever it runs -- but it only runs on a tap or panel
  reopen, not continuously, so a stage change is only ever discovered,
  never watched happening.
- **Harvest:** `HarvestFly` (`EstatePanelController.cs:454`) spawns a
  temporary copy of the harvested sprite and arcs it to
  `coinsLabel.transform.position` over 0.4s -- **stale since Phase 3**:
  harvest now fills `Inventory`, not `Coins`, so the animation flies to
  the wrong place.

## Scope Decisions

- **Procedural animation + 2 new generic sprites.** Per user direction:
  build on the existing tween pattern (`ScaleBounce`/`ColorFlash`/
  `HarvestFly`'s spawn-temp-object-and-tween technique), and add exactly
  2 new art assets -- `seed.png`, `water_droplet.png` -- both generic
  (reused across all 3 crop types), not one set per crop, to stay inside
  a realistic single-session Hugging Face credit budget (established at
  ~3-5 successful generations/session). If only one lands this session,
  ship it and defer the other, same partial-batch pattern as every prior
  art-generation pass in this project.
- **All 4 loop moments get real animation:** plant, water, growth
  (live progress bar + stage-change pop), harvest. Per explicit user
  selection -- no moment is left as a bare instant state change.
- **Live progress bar while the panel is open.** Each plot gets a
  `fillAmount`-driven progress `Image`, updated every frame the Estate
  panel is visible, via a new pure `EstateState.GrowthProgress01` helper.
  No change to the underlying passive/real-time growth model (still
  correct even if the app was closed the whole time) -- this only adds a
  *visible* indicator while the player is looking.
- **Stage-change pop, not per-frame stage re-render.** The sprite swap
  itself stays instant (matches `RefreshPlots()`'s existing shape); what's
  new is a `ScaleBounce`-style pop fired exactly once, the frame a cached
  per-plot stage value is observed to increase.
- **Harvest-fly destination fix is in scope and required**, not optional
  polish -- the animation currently visibly lies about where the reward
  goes.

## Data Model

### `Assets/Scripts/Core/EstateState.cs` (modified, additive)

New pure static method, alongside `GrowthStage`:

```csharp
public static float GrowthProgress01(LandPlot plot, CropDefinition crop, long nowUnixSeconds)
```

Returns `0f` if `plot.WateredAtUnixSeconds == 0` (not yet watered -- no
progress to show). Otherwise `(nowUnixSeconds - plot.WateredAtUnixSeconds)
/ (float)crop.GrowDurationSeconds`, clamped to `[0f, 1f]`. Pure function,
no UI dependency, same shape and testability as `GrowthStage` and
`ShopProductionStage`. No changes to `GrowthStage` itself, `LandPlot`,
`Coins`, `Plots`, `Inventory`, or `Shops`.

## Client

### `Assets/Scripts/UI/EstatePanelController.cs` (modified)

**New `Update()`:** no-op unless `panelRoot.activeSelf`. While active,
for each unlocked plot with a planted+watered crop, computes
`GrowthProgress01` and sets `plotViews[i].progressBarImage.fillAmount`.
Also compares the freshly computed `GrowthStage` against a new per-plot
cache (`int[8] lastKnownStage`, initialized in `RefreshPlots()` whenever
the panel opens); on an increase, fires a pop animation
(`StartCoroutine(ScaleBounce(...))`, reusing the existing helper) and
updates the cache. The progress bar hides itself (`fillAmount = 0`,
image inactive) for locked/empty/unwatered/fully-mature plots -- it only
makes sense mid-growth.

**`PlotView` gains one new field:** `public Image progressBarImage;`
alongside the existing `stageImage`/`lockedOverlay`/`lockCostLabel`.

**Plant animation (`OnSeedPicked`):** before the existing `ScaleBounce`,
spawn a temporary `seed.png` sprite (same spawn-temp-object-then-destroy
technique as `HarvestFly`) that appears over the plot and scales down to
nothing (settling into the soil), then the existing sprout-sprite
`ScaleBounce` plays as today. `ScaleBounce` itself is unchanged.

**Water animation (`OnPlotTapped`'s water branch):** replaces the
`ColorFlash` call with a new `WaterDroplet` coroutine -- a temporary
`water_droplet.png` sprite falls from above the plot onto it, then fades
out over ~0.4s. `ColorFlash` stays in the file (still used nowhere else
currently, but left as a general-purpose helper other future animations
may reuse -- not deleted since removing a small, still-correct, reusable
helper isn't this task's job).

**Harvest animation fix (`HarvestFly`):** destination changes from
`coinsLabel.transform.position` to
`inventoryRows[GoodsCatalog.IndexOf(plot.CropId)].root.transform.position`
-- resolved *before* the plot's `CropId` is cleared, matching the
existing call-site ordering in `OnPlotTapped`. Since
`state.Inventory[...] += 1` and `RefreshInventoryAndShops()` (Task 6,
already shipped) both run before `HarvestFly` is invoked, the target
inventory row is guaranteed already active (`root.SetActive(count > 0)`)
by the time the flight starts, regardless of which tab is currently
showing (the inventory strip is a direct panel child, visible on both
tabs).

### `Assets/Editor/CoreLoopSceneBuilder.cs` (modified)

Adds one `Image` (type Filled, horizontal or radial -- final choice made
during implementation based on what reads clearly at the plot tile's
existing 160x160 size) per plot tile, parented under each plot's existing
slot background, positioned so it doesn't obscure the stage sprite
(e.g. a thin bar along one edge). Loads `seed.png`/`water_droplet.png`
the same way `cropStageSprites` are already loaded (if either sprite
doesn't exist yet because this session's HF budget ran out, the loader
logs one warning and the corresponding animation step is skipped
gracefully -- matching this project's established "missing art degrades,
never crashes" pattern from the Phase 1 crop-sprite work). Updates the
`estateController.Initialize(...)` call site with the new
`progressBarImage` per `PlotView` and the 2 new sprite references.

## Data Flow

```
Tap empty unlocked plot -> seed picker -> OnSeedPicked
  -> seed.png spawns, scales to 0 ("planted")
  -> sprout stage sprite ScaleBounce (unchanged)
  -> RefreshPlots(), SaveService.SaveEstate

Tap planted+unwatered plot -> OnPlotTapped water branch
  -> water_droplet.png falls onto plot, fades
  -> WateredAtUnixSeconds = now, RefreshPlots(), SaveEstate

[Panel open, no tap] -> Update() each frame
  -> GrowthProgress01 drives each plot's progress bar fillAmount
  -> on any plot's GrowthStage increasing since last check: pop animation

Tap mature plot -> OnPlotTapped harvest branch
  -> Inventory[goodsIndex] += 1, RefreshInventoryAndShops()
  -> HarvestFly: temp sprite arcs from the plot to the correct
     InventoryRowView.root's position, then destroyed
  -> plot cleared, RefreshPlots(), SaveEstate
```

## Testing

**EditMode (`EstateStateTests.cs`, extended):**
- `GrowthProgress01_BeforeWatering_IsZero`
- `GrowthProgress01_JustWatered_IsZero`
- `GrowthProgress01_AtHalfDuration_IsAboutHalf`
- `GrowthProgress01_AtFullDuration_IsOne`
- `GrowthProgress01_PastFullDuration_StaysClampedAtOne`

**PlayMode (`EstatePanelControllerTests.cs`, extended):**
- Progress bar `fillAmount` reflects `GrowthProgress01` at a sampled time
  after watering (drive via a seeded save file with a known
  `WateredAtUnixSeconds`, matching the existing test pattern for
  `GrowthStage`-dependent tests).
- Stage-change pop fires (assert via a testable side effect -- e.g. a
  counter or last-triggered-plot-index the coroutine sets, avoiding
  asserting on visual tween state directly, matching how this project
  already doesn't test `ScaleBounce`/`ColorFlash`'s actual motion) exactly
  once per real stage increase, not on every `Update()` tick while the
  stage is unchanged.
- `HarvestFly`'s resolved target position matches the correct
  `InventoryRowView.root`'s position for the harvested crop, not the
  coins label.
- Missing-sprite graceful degradation: if `seed.png`/`water_droplet.png`
  aren't present (e.g. deferred to a follow-up art pass), planting/
  watering still correctly update state and don't throw.

Exact test counts and full implementation steps belong in the
implementation plan, not this spec.

## Explicitly Out of Scope for This Phase

- Shops-on-land, land expansion mechanic, animal care animations -- see
  the vision doc; each gets its own future spec.
- Per-crop-type seed/droplet art (wheat seed vs. carrot seed vs. pumpkin
  seed) -- generic assets only, this pass.
- Any change to `GrowthStage`, save format, growth timing, or crop
  values -- presentation layer only.
- Sound effects / haptics -- purely visual this pass.
- Continuous per-frame sprite morphing/skeletal animation -- progress
  communicated via the fill bar, not by animating the stage sprite itself
  between discrete stages.
