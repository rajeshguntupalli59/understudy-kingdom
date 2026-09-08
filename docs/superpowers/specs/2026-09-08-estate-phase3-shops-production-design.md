# Design: Estate Phase 3 — Shops & Production Chains

**Date:** 2026-09-08 | **Status:** Approved, pending implementation plan

## Purpose

Third phase of the Estate economy roadmap (see
`docs/superpowers/specs/2026-09-07-estate-economy-roadmap-design.md`),
built directly after Phase 1 (Land, Coins & Crops) rather than Phase 2
(Animals) — explicit user decision, since production chains are what
actually implements "a shop needs a specific crop," and Phase 2's own
production inputs (milk→cheese) don't exist without animals yet, while
Phase 1's 3 crops are enough to build 3 real crop→shop→product chains
right now.

Adds an **Inventory** to the Estate (raw and processed goods, as counts)
and **3 production buildings** (Bakery, Kitchen, Pie House), each tied to
exactly one crop: Wheat→Bread, Carrot→Carrot Soup, Pumpkin→Pumpkin Pie.
This requires changing what harvesting does — see Scope Decisions.

## Scope Decisions

- **Harvesting now adds to Inventory instead of auto-selling.** Explicit
  user decision (confirmed via direct question), matching Hay Day/
  Township's actual model: harvest fills your inventory, selling is a
  separate action. This is the change that makes "Bakery needs Wheat"
  mean anything — Phase 1's harvest-immediately-sells design had no raw
  goods left to feed a shop. `EstatePanelController.OnPlotTapped`'s
  harvest branch changes from `state.Coins += crop.Value.SellValue` to
  adding 1 unit of that crop to `Inventory`.
- **Selling: tap a goods row, sell the whole stack.** One tap sells every
  unit of that goods type currently held, not one-at-a-time — reduces
  tap-grind for a returning player with a full inventory, consistent with
  the project's fair-play stance (no mechanic that rewards excessive
  tapping over genuine decisions).
- **3 shops, each a one-time coin purchase, then reusable forever.**
  Matches the land-plot unlock pattern (a permanent, one-time cost, not a
  recurring fee) and Hay Day's own building-purchase model.
- **One-step chains only this pass: 1 crop in, 1 product out.** No
  multi-building chains (Hay Day's real wheat→flour→bread needs a Mill
  *and* a Bakery) — that's real added complexity (a second building, a
  second timer, an intermediate goods type) not justified without
  playtest data showing single-step chains feel too shallow. Revisit in a
  later phase if needed.
- **Starting a production cycle costs a small coin fee *and* consumes the
  input crop from Inventory, then takes real elapsed time — never a hard
  paywall**, matching Phase 1's exact philosophy for crop growth. The
  coin fee represents "ingredients/labor," not a progress gate: it's
  small relative to what the finished product sells for.
- **New UI stays inside the existing Estate panel — a Land/Shops tab
  switcher, not a new throne-room button.** Adding a whole new button to
  the throne-room list would risk repeating the exact canvas-space
  squeeze that caused Phase 1's final-review Critical bug (the row is
  already at capacity). Toggling which section of the *existing* Estate
  panel is visible avoids that risk entirely.
- **Numbers this pass are a first-tuning-pass, not balanced data**
  (matches Phase 1's own disclaimer for crop costs/durations): unlock
  costs 150/300/600 (Bakery/Kitchen/Pie House, roughly tracking each
  crop's own cost tier), start-cost 3/8/20, production time 60s/180s/420s
  (1/3/7 minutes), product sell values 35/100/275 (roughly 2.5-3x the
  input crop's own raw sell value, so processing is worth the wait but
  doesn't trivialize the raw-sell option).
- **Save migration, not a wipe.** Phase 1 already shipped a real save
  file (`estate_save.json`) with `Coins`/`Plots`. Adding `Inventory`/
  `Shops` fields must not discard an existing player's coins or plot
  state just because those two fields are new/missing on an old file --
  `LoadEstate`'s validation moves from "any problem -> fresh whole
  `EstateState`" to per-field: keep `Coins`/`Plots` as loaded, default
  only `Inventory`/`Shops` individually if missing or the wrong length.

## Approach

Two new static catalogs mirror `CropCatalog`'s existing shape:
`ProductCatalog` (the 3 processed goods) and `ShopCatalog` (the 3
buildings, each referencing one crop id and one product id by string,
resolved at runtime the same way `CropCatalog.Find`/`IndexOf` already
work). A new `GoodsCatalog` static class unifies crop and product ids
into one flat inventory index space (crops first, products after) so
`EstateState.Inventory` can be a single fixed-length `int[]` — same
fixed-array-per-catalog-entry pattern `Plots` already uses for
`LandPlot`, not a `Dictionary` (JsonUtility can't serialize those
directly, and a flat array indexed by a known-at-compile-time catalog
needs no dynamic growth).

Shop production state (`ShopState[]`, one per `ShopCatalog.All` entry)
mirrors `LandPlot`'s own shape: a single `ProductionStartedAtUnixSeconds`
timestamp (`0` = idle), with stage computed on read from elapsed
wall-clock time — exactly `EstateState.GrowthStage`'s pattern, reused
rather than reinvented (`EstateState.ShopProductionStage`, 0=idle/
1=in-progress/2=ready-to-collect, mirroring the crop growth 0/1/2 shape).

## Data Model

```csharp
// Assets/Scripts/Core/ProductCatalog.cs (new) -- mirrors CropCatalog.cs exactly
public readonly struct ProductDefinition
{
    public readonly string Id;
    public readonly string DisplayName;
    public readonly int SellValue;

    public ProductDefinition(string id, string displayName, int sellValue) { ... }
}

public static class ProductCatalog
{
    public static readonly ProductDefinition[] All =
    {
        new ProductDefinition("bread", "Bread", sellValue: 35),
        new ProductDefinition("carrot_soup", "Carrot Soup", sellValue: 100),
        new ProductDefinition("pumpkin_pie", "Pumpkin Pie", sellValue: 275),
    };

    public static ProductDefinition? Find(string id) { /* linear scan, mirrors CropCatalog.Find */ }
    public static int IndexOf(string id) { /* mirrors CropCatalog.IndexOf */ }
}
```

```csharp
// Assets/Scripts/Core/GoodsCatalog.cs (new) -- unifies crop + product ids
// into one flat inventory index space: crops first (0..CropCatalog.All.Length-1),
// products after (CropCatalog.All.Length..CropCatalog.All.Length+ProductCatalog.All.Length-1).
public static class GoodsCatalog
{
    public static readonly int Count = CropCatalog.All.Length + ProductCatalog.All.Length; // 6

    public static int IndexOf(string goodsId)
    {
        int cropIndex = CropCatalog.IndexOf(goodsId);
        if (cropIndex >= 0) return cropIndex;
        int productIndex = ProductCatalog.IndexOf(goodsId);
        return productIndex >= 0 ? CropCatalog.All.Length + productIndex : -1;
    }

    public static string DisplayName(string goodsId) { /* checks CropCatalog.Find then ProductCatalog.Find */ }
    public static int SellValue(string goodsId) { /* same lookup pattern */ }
}
```

```csharp
// Assets/Scripts/Core/ShopCatalog.cs (new)
public readonly struct ShopDefinition
{
    public readonly string Id;
    public readonly string DisplayName;
    public readonly int UnlockCost;
    public readonly string InputGoodsId;   // crop id consumed, 1 unit per cycle
    public readonly string OutputGoodsId;  // product id produced, 1 unit per cycle
    public readonly int StartCost;         // coins spent to begin one cycle
    public readonly int ProductionSeconds;

    public ShopDefinition(string id, string displayName, int unlockCost, string inputGoodsId, string outputGoodsId, int startCost, int productionSeconds) { ... }
}

public static class ShopCatalog
{
    public static readonly ShopDefinition[] All =
    {
        new ShopDefinition("bakery", "Bakery", unlockCost: 150, inputGoodsId: "wheat", outputGoodsId: "bread", startCost: 3, productionSeconds: 60),
        new ShopDefinition("kitchen", "Kitchen", unlockCost: 300, inputGoodsId: "carrot", outputGoodsId: "carrot_soup", startCost: 8, productionSeconds: 180),
        new ShopDefinition("pie_house", "Pie House", unlockCost: 600, inputGoodsId: "pumpkin", outputGoodsId: "pumpkin_pie", startCost: 20, productionSeconds: 420),
    };

    public static ShopDefinition? Find(string id) { ... }
    public static int IndexOf(string id) { ... }
}
```

```csharp
// Assets/Scripts/Core/EstateState.cs (modified -- additive only, existing
// Coins/Plots/GrowthStage/UnlockCost members and their behavior untouched)
[Serializable]
public class ShopState
{
    public bool Unlocked;
    public long ProductionStartedAtUnixSeconds; // 0 = idle
}

public class EstateState
{
    // existing: Coins, Plots ...
    public int[] Inventory;   // length GoodsCatalog.Count, index per GoodsCatalog.IndexOf
    public ShopState[] Shops; // length ShopCatalog.All.Length, index matches ShopCatalog.All order

    public EstateState()
    {
        // existing Plots init ...
        Inventory = new int[GoodsCatalog.Count];
        Shops = new ShopState[ShopCatalog.All.Length];
        for (int i = 0; i < Shops.Length; i++)
        {
            Shops[i] = new ShopState();
        }
    }

    // new static helper, mirrors GrowthStage's exact shape:
    public static int ShopProductionStage(ShopState shop, ShopDefinition def, long nowUnixSeconds)
    {
        if (shop.ProductionStartedAtUnixSeconds == 0)
        {
            return 0; // idle
        }
        long elapsed = nowUnixSeconds - shop.ProductionStartedAtUnixSeconds;
        return elapsed >= def.ProductionSeconds ? 2 : 1; // 1 = in progress, 2 = ready
    }
}
```

## Client

### `Assets/Scripts/Core/ProductCatalog.cs`, `GoodsCatalog.cs`, `ShopCatalog.cs` (new)
Static catalogs as above.

### `Assets/Scripts/Core/EstateState.cs` (modified)
Adds `ShopState`, `Inventory`, `Shops`, `ShopProductionStage` as above.
Existing `LandPlot`/`Coins`/`Plots`/`GrowthStage`/`UnlockCost` members are
untouched.

### `Assets/Scripts/Core/EstateSaveData.cs` / `SaveService.cs` (modified)
`EstateSaveData` gains `Inventory` (`int[]`) and `Shops` (`ShopState[]`)
fields, both reusing the runtime types directly (same "no separate DTO
needed, `LandPlot` has no enum-encoding issue" reasoning `EstateSaveData`
already established for `Plots`). `LoadEstate`'s validation becomes
per-field rather than blanket-reset: if `data.Inventory` is `null` or the
wrong length, default *only* that field to a fresh `int[GoodsCatalog.Count]`;
same per-field treatment for `Shops` against `ShopCatalog.All.Length` --
`Coins`/`Plots` load exactly as they do today regardless of whether
`Inventory`/`Shops` needed defaulting. This is what makes an existing
Phase-1-only save file upgrade cleanly instead of losing progress.

### `Assets/Scripts/UI/EstatePanelController.cs` (modified)
**Harvest branch (`OnPlotTapped`)**: replace `state.Coins +=
crop.Value.SellValue` with `state.Inventory[GoodsCatalog.IndexOf(plot.CropId)]++`
before clearing the plot -- the plot-clearing and harvest-tween code is
unchanged.

**New: Land/Shops tab switcher.** Two new buttons at the top of the
panel body (below the coins label, above the existing plot grid). Tapping
"Shops" hides the plot-grid root and seed-picker root, shows a new
shop-list root; tapping "Land" reverses it. Default tab on open: Land
(unchanged first-open experience). The coins label and a new inventory
strip stay visible regardless of active tab (both tabs need to see
current goods/coins to decide what to do).

**New: Inventory strip.** A row of up to `GoodsCatalog.Count` (6) small
entries, one per goods id with `Inventory[index] > 0`, each showing
`"{DisplayName} x{count}"` and a "Sell" button. Tapping Sell: `Coins +=
count * GoodsCatalog.SellValue(goodsId)`, `Inventory[index] = 0`, save,
refresh. Entries for goods currently at 0 are hidden (not shown as
greyed placeholders) -- keeps the strip uncluttered early on when the
player has little variety yet.

**New: Shop list (Shops tab).** One row per `ShopCatalog.All` entry:
- **Locked:** show `"{DisplayName} -- Unlock: {UnlockCost}"`, an Unlock
  button (disabled if `Coins < UnlockCost`). Tap: deduct `UnlockCost`,
  set `Shops[i].Unlocked = true`.
- **Unlocked, idle (stage 0):** show `"{DisplayName}: tap to start
  ({InputGoodsId} x1 + {StartCost} coins)"`, a Start button (disabled if
  `Inventory[cropIndex] < 1` or `Coins < StartCost`). Tap: deduct
  `StartCost`, decrement `Inventory[cropIndex]` by 1, set
  `Shops[i].ProductionStartedAtUnixSeconds = now`.
- **Unlocked, in progress (stage 1):** show `"{DisplayName}: producing
  {OutputGoodsId}..."`, no interactive button (matches crop-growth's own
  "no tap effect while growing" contract).
- **Unlocked, ready (stage 2):** show `"{DisplayName}: {OutputGoodsId}
  ready!"`, a Collect button. Tap: increment
  `Inventory[productIndex]` by 1, reset
  `Shops[i].ProductionStartedAtUnixSeconds = 0` (back to idle,
  immediately startable again if the player has another input unit).

**Save timing:** unchanged pattern from Phase 1 -- save on every
state-mutating action (sell, start, collect, unlock), consistent with
the per-mutation save fix already shipped for crop actions, not just on
panel close.

### `Assets/Editor/CoreLoopSceneBuilder.cs` (modified)
Adds the tab-switcher buttons, inventory strip container, and shop-list
container to the existing `EstatePanel` construction block (search for
`estatePanelRootObject`) -- same `CreateLabel`/button-creation patterns
already used throughout this file. No new throne-room button, no new
`Verify()` controller-presence check (still the same one
`EstatePanelController`, now with more `[SerializeField]` fields wired
through its `Initialize(...)` signature, which gains new trailing
parameters for the tab roots / inventory row prefabs-equivalent /
shop row prefabs-equivalent, following the exact `PlotView[]`
array-of-a-nested-class pattern already established for the 8 plots).

## Data Flow

```
Player harvests a mature Wheat plot (existing Phase 1 flow)
  -> Inventory[GoodsCatalog.IndexOf("wheat")] += 1 (was: Coins += 12)
  -> plot cleared, harvest-tween plays, panel refreshes

Player switches to the Shops tab, taps "Unlock" on Bakery (Coins >= 150)
  -> Coins -= 150; Shops[0].Unlocked = true

Player taps "Start" on the now-unlocked, idle Bakery
  (Inventory["wheat"] >= 1, Coins >= 3)
  -> Coins -= 3; Inventory["wheat"] -= 1
  -> Shops[0].ProductionStartedAtUnixSeconds = now

Player reopens the panel 60+ seconds later
  -> ShopProductionStage returns 2 (ready) for Bakery
  -> Shops tab shows "Bread ready!" with a Collect button

Player taps Collect
  -> Inventory["bread"] += 1
  -> Shops[0].ProductionStartedAtUnixSeconds = 0 (idle again)

Player switches to the Inventory strip, taps "Sell" on Bread x1
  -> Coins += 35; Inventory["bread"] = 0
```

## Testing

**EditMode:**
- `ProductCatalog`/`ShopCatalog`: entry counts, positive values, `Find`/
  `IndexOf` known-and-unknown-id cases -- mirrors `CropCatalogTests.cs`
  exactly.
- `GoodsCatalog.IndexOf`: crop ids resolve to 0-2, product ids resolve to
  3-5, unknown id resolves to -1; `DisplayName`/`SellValue` resolve
  correctly for both a crop id and a product id.
- `EstateState.ShopProductionStage`: idle (0 timestamp) always returns 0
  regardless of elapsed time; in-progress and ready thresholds at each
  shop's own `ProductionSeconds` -- mirrors `GrowthStage`'s existing test
  shape (including the odd-duration regression-test lesson from Phase
  1's own review: use the same `elapsed >= duration` / no intermediate
  division pattern already fixed there, not the original buggy shape).
- `SaveService.SaveEstate`/`LoadEstate` round-trip for the new
  `Inventory`/`Shops` fields; a save file missing those keys entirely
  (simulating an old Phase-1-only save) loads with `Coins`/`Plots` intact
  and `Inventory`/`Shops` defaulted, not the whole state reset -- this is
  the concrete regression test proving the migration scope decision.

**PlayMode:**
- `EstatePanelController`: harvesting adds to inventory instead of
  coins; selling a stack awards the correct total and zeroes the count;
  shop unlock deducts cost and is blocked when unaffordable; starting
  production deducts the start cost and the input crop and is blocked
  when either is insufficient; a shop mid-production shows no
  interactive action; collecting a ready shop adds the output good and
  returns the shop to idle; tab switching shows/hides the correct
  sections. All via real `SaveService` round-trips, matching Phase 1's
  established test convention.
- Scene-level regression test (extends `CoreLoopSceneTests.cs`): the
  Estate panel's Shops tab is reachable and shows all 3 shops in their
  locked state on a fresh save -- matches the existing
  `LoadedCoreLoopScene_EstateButton_...` test's shape.

## Explicitly Out of Scope for This Phase

- Multi-step production chains (e.g. an intermediate Mill/Flour step).
- Animals/breeding (Phase 2) -- still not started; this phase's shops
  only consume the 3 existing crops, nothing animal-derived.
- The recruitable Shop Seller / automated selling (Phase 4) -- selling
  stays a manual per-stack tap this phase.
- More than 3 shops or products, or any shop taking more than 1 input
  type per cycle.
- Any rebalancing of Phase 1's existing crop seed costs/grow durations/
  raw sell values -- this phase only adds new numbers, doesn't touch the
  ones already shipped and tested.
