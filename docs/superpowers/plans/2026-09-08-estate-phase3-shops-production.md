# Estate Phase 3 (Shops & Production Chains) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an Inventory to the Estate, change harvesting to fill it
instead of auto-selling, and add 3 one-time-purchase production
buildings (Bakery, Kitchen, Pie House) that each convert one specific
crop into a higher-value product over real time, inside a new Land/Shops
tab switcher in the existing Estate panel.

**Architecture:** Three new static catalogs (`ProductCatalog`,
`GoodsCatalog`, `ShopCatalog`) mirror the existing `CropCatalog` pattern.
`EstateState` gains `Inventory` (flat `int[]`, indexed via
`GoodsCatalog.IndexOf`) and `Shops` (`ShopState[]`, one per
`ShopCatalog.All` entry), following the exact fixed-array-per-catalog
shape `Plots` already uses. Save migration is per-field, not a blanket
reset. All new UI lives inside the existing `EstatePanelController`/
`EstatePanel` — no new throne-room button.

**Tech Stack:** Unity 6000.3.23f1, C#, NUnit EditMode/PlayMode tests. No
new packages.

## Global Constraints

- Harvesting adds 1 unit of the harvested crop to `Inventory` instead of
  awarding coins directly (this changes Phase 1's existing harvest
  behavior in `EstatePanelController.OnPlotTapped`).
- Selling: tapping an inventory entry sells the *entire* stack of that
  goods type in one action, not one unit at a time.
- 3 shops, each a one-time coin unlock, then reusable forever:
  `bakery` (unlock 150, input `wheat` x1, output `bread` x1, start cost
  3, production 60s), `kitchen` (unlock 300, input `carrot` x1, output
  `carrot_soup` x1, start cost 8, production 180s), `pie_house` (unlock
  600, input `pumpkin` x1, output `pumpkin_pie` x1, start cost 20,
  production 420s).
- Product sell values: `bread` 35, `carrot_soup` 100, `pumpkin_pie` 275.
- One-step chains only (1 input, 1 output) -- no multi-building chains.
- Starting production costs a small coin fee *and* consumes the input
  crop, then takes real elapsed time -- never a hard paywall (matches
  Phase 1's crop-growth philosophy exactly).
- New UI stays inside the existing `EstatePanel` (a Land/Shops tab
  switcher) -- no new throne-room button, to avoid repeating Phase 1's
  final-review Critical bug (canvas-space exhaustion on that button row).
- Save migration must be per-field: an existing save missing
  `Inventory`/`Shops` keeps its `Coins`/`Plots` exactly as loaded and
  defaults only the two new fields, never a whole-state reset.
- This project's C# convention: no comments explaining WHAT code does;
  only comments for non-obvious WHY.
- Full spec: `docs/superpowers/specs/2026-09-08-estate-phase3-shops-production-design.md`.
  Roadmap context: `docs/superpowers/specs/2026-09-07-estate-economy-roadmap-design.md`.

---

### Task 1: ProductCatalog

**Files:**
- Create: `Assets/Scripts/Core/ProductCatalog.cs`
- Test: `Assets/Tests/EditMode/ProductCatalogTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `ProductDefinition` (readonly struct: `Id`, `DisplayName`,
  `SellValue`, constructor `ProductDefinition(string id, string
  displayName, int sellValue)`), `ProductCatalog.All` (static
  `ProductDefinition[]`, 3 entries: `bread`(35), `carrot_soup`(100),
  `pumpkin_pie`(275)), `ProductCatalog.Find(string id) ->
  ProductDefinition?`, `ProductCatalog.IndexOf(string id) -> int` (-1 if
  not found).

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/ProductCatalogTests.cs
using NUnit.Framework;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Tests
{
    public class ProductCatalogTests
    {
        [Test]
        public void All_HasExactlyThreeProducts()
        {
            Assert.AreEqual(3, ProductCatalog.All.Length);
        }

        [Test]
        public void All_EveryProductHasPositiveSellValue()
        {
            foreach (ProductDefinition product in ProductCatalog.All)
            {
                Assert.Greater(product.SellValue, 0, $"{product.Id} SellValue");
                Assert.IsFalse(string.IsNullOrEmpty(product.DisplayName), $"{product.Id} DisplayName");
            }
        }

        [Test]
        public void All_ExactValues_MatchDesignSpec()
        {
            Assert.AreEqual(35, ProductCatalog.Find("bread").Value.SellValue);
            Assert.AreEqual(100, ProductCatalog.Find("carrot_soup").Value.SellValue);
            Assert.AreEqual(275, ProductCatalog.Find("pumpkin_pie").Value.SellValue);
        }

        [Test]
        public void Find_UnknownId_ReturnsNull()
        {
            Assert.IsNull(ProductCatalog.Find("does-not-exist"));
        }

        [Test]
        public void IndexOf_KnownIds_ReturnsCatalogOrder()
        {
            Assert.AreEqual(0, ProductCatalog.IndexOf("bread"));
            Assert.AreEqual(1, ProductCatalog.IndexOf("carrot_soup"));
            Assert.AreEqual(2, ProductCatalog.IndexOf("pumpkin_pie"));
        }

        [Test]
        public void IndexOf_UnknownId_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, ProductCatalog.IndexOf("does-not-exist"));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testResults <path>.xml -logFile <path>.log`
(no `-quit` flag -- it self-quits after tests). Expected: FAIL with
"ProductCatalog does not exist" (compile error).

- [ ] **Step 3: Write the implementation**

```csharp
// Assets/Scripts/Core/ProductCatalog.cs
namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// Static processed-goods content, mirrors CropCatalog's exact shape.
    /// See docs/superpowers/specs/2026-09-08-estate-phase3-shops-production-design.md.
    /// </summary>
    public readonly struct ProductDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly int SellValue;

        public ProductDefinition(string id, string displayName, int sellValue)
        {
            Id = id;
            DisplayName = displayName;
            SellValue = sellValue;
        }
    }

    public static class ProductCatalog
    {
        public static readonly ProductDefinition[] All =
        {
            new ProductDefinition("bread", "Bread", sellValue: 35),
            new ProductDefinition("carrot_soup", "Carrot Soup", sellValue: 100),
            new ProductDefinition("pumpkin_pie", "Pumpkin Pie", sellValue: 275),
        };

        public static ProductDefinition? Find(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return All[i];
                }
            }
            return null;
        }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Same command. Expected: all `ProductCatalogTests` PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/ProductCatalog.cs Assets/Scripts/Core/ProductCatalog.cs.meta Assets/Tests/EditMode/ProductCatalogTests.cs Assets/Tests/EditMode/ProductCatalogTests.cs.meta
git commit -m "feat: add ProductCatalog static processed-goods content"
```

---

### Task 2: ShopCatalog

**Files:**
- Create: `Assets/Scripts/Core/ShopCatalog.cs`
- Test: `Assets/Tests/EditMode/ShopCatalogTests.cs`

**Interfaces:**
- Consumes: nothing (references crop/product ids as plain strings only --
  no compile-time dependency on `CropCatalog`/`ProductCatalog`).
- Produces: `ShopDefinition` (readonly struct: `Id`, `DisplayName`,
  `UnlockCost`, `InputGoodsId`, `OutputGoodsId`, `StartCost`,
  `ProductionSeconds`, constructor with all 7 params in that order),
  `ShopCatalog.All` (static `ShopDefinition[]`, 3 entries -- exact
  values in Global Constraints), `ShopCatalog.Find(string id) ->
  ShopDefinition?`, `ShopCatalog.IndexOf(string id) -> int`.

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/ShopCatalogTests.cs
using NUnit.Framework;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Tests
{
    public class ShopCatalogTests
    {
        [Test]
        public void All_HasExactlyThreeShops()
        {
            Assert.AreEqual(3, ShopCatalog.All.Length);
        }

        [Test]
        public void All_EveryShopHasPositiveValues()
        {
            foreach (ShopDefinition shop in ShopCatalog.All)
            {
                Assert.Greater(shop.UnlockCost, 0, $"{shop.Id} UnlockCost");
                Assert.Greater(shop.StartCost, 0, $"{shop.Id} StartCost");
                Assert.Greater(shop.ProductionSeconds, 0, $"{shop.Id} ProductionSeconds");
                Assert.IsFalse(string.IsNullOrEmpty(shop.InputGoodsId), $"{shop.Id} InputGoodsId");
                Assert.IsFalse(string.IsNullOrEmpty(shop.OutputGoodsId), $"{shop.Id} OutputGoodsId");
            }
        }

        [Test]
        public void All_ExactValues_MatchDesignSpec()
        {
            ShopDefinition bakery = ShopCatalog.Find("bakery").Value;
            Assert.AreEqual(150, bakery.UnlockCost);
            Assert.AreEqual("wheat", bakery.InputGoodsId);
            Assert.AreEqual("bread", bakery.OutputGoodsId);
            Assert.AreEqual(3, bakery.StartCost);
            Assert.AreEqual(60, bakery.ProductionSeconds);

            ShopDefinition kitchen = ShopCatalog.Find("kitchen").Value;
            Assert.AreEqual(300, kitchen.UnlockCost);
            Assert.AreEqual("carrot", kitchen.InputGoodsId);
            Assert.AreEqual("carrot_soup", kitchen.OutputGoodsId);
            Assert.AreEqual(8, kitchen.StartCost);
            Assert.AreEqual(180, kitchen.ProductionSeconds);

            ShopDefinition pieHouse = ShopCatalog.Find("pie_house").Value;
            Assert.AreEqual(600, pieHouse.UnlockCost);
            Assert.AreEqual("pumpkin", pieHouse.InputGoodsId);
            Assert.AreEqual("pumpkin_pie", pieHouse.OutputGoodsId);
            Assert.AreEqual(20, pieHouse.StartCost);
            Assert.AreEqual(420, pieHouse.ProductionSeconds);
        }

        [Test]
        public void Find_UnknownId_ReturnsNull()
        {
            Assert.IsNull(ShopCatalog.Find("does-not-exist"));
        }

        [Test]
        public void IndexOf_KnownIds_ReturnsCatalogOrder()
        {
            Assert.AreEqual(0, ShopCatalog.IndexOf("bakery"));
            Assert.AreEqual(1, ShopCatalog.IndexOf("kitchen"));
            Assert.AreEqual(2, ShopCatalog.IndexOf("pie_house"));
        }

        [Test]
        public void IndexOf_UnknownId_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, ShopCatalog.IndexOf("does-not-exist"));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Same EditMode command. Expected: FAIL with "ShopCatalog does not exist".

- [ ] **Step 3: Write the implementation**

```csharp
// Assets/Scripts/Core/ShopCatalog.cs
namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// Static production-building content. InputGoodsId/OutputGoodsId are
    /// plain strings resolved at runtime via GoodsCatalog -- no compile-time
    /// dependency on CropCatalog/ProductCatalog, same reasoning
    /// LandPlot.CropId already uses. See
    /// docs/superpowers/specs/2026-09-08-estate-phase3-shops-production-design.md.
    /// </summary>
    public readonly struct ShopDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly int UnlockCost;
        public readonly string InputGoodsId;
        public readonly string OutputGoodsId;
        public readonly int StartCost;
        public readonly int ProductionSeconds;

        public ShopDefinition(string id, string displayName, int unlockCost, string inputGoodsId, string outputGoodsId, int startCost, int productionSeconds)
        {
            Id = id;
            DisplayName = displayName;
            UnlockCost = unlockCost;
            InputGoodsId = inputGoodsId;
            OutputGoodsId = outputGoodsId;
            StartCost = startCost;
            ProductionSeconds = productionSeconds;
        }
    }

    public static class ShopCatalog
    {
        public static readonly ShopDefinition[] All =
        {
            new ShopDefinition("bakery", "Bakery", unlockCost: 150, inputGoodsId: "wheat", outputGoodsId: "bread", startCost: 3, productionSeconds: 60),
            new ShopDefinition("kitchen", "Kitchen", unlockCost: 300, inputGoodsId: "carrot", outputGoodsId: "carrot_soup", startCost: 8, productionSeconds: 180),
            new ShopDefinition("pie_house", "Pie House", unlockCost: 600, inputGoodsId: "pumpkin", outputGoodsId: "pumpkin_pie", startCost: 20, productionSeconds: 420),
        };

        public static ShopDefinition? Find(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return All[i];
                }
            }
            return null;
        }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Same command. Expected: all `ShopCatalogTests` PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/ShopCatalog.cs Assets/Scripts/Core/ShopCatalog.cs.meta Assets/Tests/EditMode/ShopCatalogTests.cs Assets/Tests/EditMode/ShopCatalogTests.cs.meta
git commit -m "feat: add ShopCatalog static production-building content"
```

---

### Task 3: GoodsCatalog

**Files:**
- Create: `Assets/Scripts/Core/GoodsCatalog.cs`
- Test: `Assets/Tests/EditMode/GoodsCatalogTests.cs`

**Interfaces:**
- Consumes: `CropCatalog`/`CropDefinition` (existing, Phase 1),
  `ProductCatalog`/`ProductDefinition` (Task 1).
- Produces: `GoodsCatalog.Count` (static `int`, = `CropCatalog.All.Length
  + ProductCatalog.All.Length` = 6), `GoodsCatalog.IndexOf(string
  goodsId) -> int` (crop ids resolve to 0..2, product ids to 3..5,
  unknown to -1), `GoodsCatalog.DisplayName(string goodsId) -> string`,
  `GoodsCatalog.SellValue(string goodsId) -> int`.

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/GoodsCatalogTests.cs
using NUnit.Framework;
using UnderstudyKingdom.Core;

namespace UnderstudyKingdom.Tests
{
    public class GoodsCatalogTests
    {
        [Test]
        public void Count_IsCropsPlusProducts()
        {
            Assert.AreEqual(6, GoodsCatalog.Count);
        }

        [Test]
        public void IndexOf_CropIds_ResolveToZeroThroughTwo()
        {
            Assert.AreEqual(0, GoodsCatalog.IndexOf("wheat"));
            Assert.AreEqual(1, GoodsCatalog.IndexOf("carrot"));
            Assert.AreEqual(2, GoodsCatalog.IndexOf("pumpkin"));
        }

        [Test]
        public void IndexOf_ProductIds_ResolveToThreeThroughFive()
        {
            Assert.AreEqual(3, GoodsCatalog.IndexOf("bread"));
            Assert.AreEqual(4, GoodsCatalog.IndexOf("carrot_soup"));
            Assert.AreEqual(5, GoodsCatalog.IndexOf("pumpkin_pie"));
        }

        [Test]
        public void IndexOf_UnknownId_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, GoodsCatalog.IndexOf("does-not-exist"));
        }

        [Test]
        public void DisplayNameAndSellValue_ResolveForCropId()
        {
            Assert.AreEqual("Wheat", GoodsCatalog.DisplayName("wheat"));
            Assert.AreEqual(12, GoodsCatalog.SellValue("wheat"));
        }

        [Test]
        public void DisplayNameAndSellValue_ResolveForProductId()
        {
            Assert.AreEqual("Bread", GoodsCatalog.DisplayName("bread"));
            Assert.AreEqual(35, GoodsCatalog.SellValue("bread"));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Same command. Expected: FAIL with "GoodsCatalog does not exist" (do Task
1 before this task -- `ProductCatalog` must already exist for this to
even compile).

- [ ] **Step 3: Write the implementation**

```csharp
// Assets/Scripts/Core/GoodsCatalog.cs
namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// Unifies CropCatalog (raw goods) and ProductCatalog (processed goods)
    /// into one flat inventory index space -- crops first (0..CropCatalog.All.Length-1),
    /// products after. Lets EstateState.Inventory be a single fixed-length
    /// int[] instead of a Dictionary (JsonUtility can't serialize those). See
    /// docs/superpowers/specs/2026-09-08-estate-phase3-shops-production-design.md.
    /// </summary>
    public static class GoodsCatalog
    {
        public static readonly int Count = CropCatalog.All.Length + ProductCatalog.All.Length;

        public static int IndexOf(string goodsId)
        {
            int cropIndex = CropCatalog.IndexOf(goodsId);
            if (cropIndex >= 0)
            {
                return cropIndex;
            }
            int productIndex = ProductCatalog.IndexOf(goodsId);
            return productIndex >= 0 ? CropCatalog.All.Length + productIndex : -1;
        }

        public static string DisplayName(string goodsId)
        {
            CropDefinition? crop = CropCatalog.Find(goodsId);
            if (crop != null)
            {
                return crop.Value.DisplayName;
            }
            ProductDefinition? product = ProductCatalog.Find(goodsId);
            return product?.DisplayName;
        }

        public static int SellValue(string goodsId)
        {
            CropDefinition? crop = CropCatalog.Find(goodsId);
            if (crop != null)
            {
                return crop.Value.SellValue;
            }
            ProductDefinition? product = ProductCatalog.Find(goodsId);
            return product?.SellValue ?? 0;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Same command. Expected: all `GoodsCatalogTests` PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/GoodsCatalog.cs Assets/Scripts/Core/GoodsCatalog.cs.meta Assets/Tests/EditMode/GoodsCatalogTests.cs Assets/Tests/EditMode/GoodsCatalogTests.cs.meta
git commit -m "feat: add GoodsCatalog unifying crops and products into one inventory index space"
```

---

### Task 4: EstateState -- ShopState, Inventory, Shops, ShopProductionStage

**Files:**
- Modify: `Assets/Scripts/Core/EstateState.cs`
- Modify: `Assets/Tests/EditMode/EstateStateTests.cs`

**Interfaces:**
- Consumes: `GoodsCatalog.Count` (Task 3), `ShopCatalog.All` (Task 2).
- Produces: `ShopState` (new `[Serializable]` class: `Unlocked bool`,
  `ProductionStartedAtUnixSeconds long`), `EstateState.Inventory` (new
  `int[]` field, length `GoodsCatalog.Count`, all zero at construction),
  `EstateState.Shops` (new `ShopState[]` field, length
  `ShopCatalog.All.Length`, all `Unlocked=false` at construction),
  `EstateState.ShopProductionStage(ShopState shop, ShopDefinition def,
  long nowUnixSeconds) -> int` (0=idle, 1=in-progress, 2=ready). Existing
  `Coins`/`Plots`/`GrowthStage`/`UnlockCost` are untouched.

- [ ] **Step 1: Write the failing tests**

Add to the EXISTING `Assets/Tests/EditMode/EstateStateTests.cs` (same
file, same class -- do not create a new file):

```csharp
        [Test]
        public void NewEstateState_HasEmptyInventoryOfCorrectLength()
        {
            var state = new EstateState();

            Assert.AreEqual(GoodsCatalog.Count, state.Inventory.Length);
            foreach (int count in state.Inventory)
            {
                Assert.AreEqual(0, count);
            }
        }

        [Test]
        public void NewEstateState_HasAllShopsLockedAndIdle()
        {
            var state = new EstateState();

            Assert.AreEqual(ShopCatalog.All.Length, state.Shops.Length);
            foreach (ShopState shop in state.Shops)
            {
                Assert.IsFalse(shop.Unlocked);
                Assert.AreEqual(0, shop.ProductionStartedAtUnixSeconds);
            }
        }

        [Test]
        public void ShopProductionStage_Idle_IsStageZeroRegardlessOfElapsedTime()
        {
            var shop = new ShopState { Unlocked = true, ProductionStartedAtUnixSeconds = 0 };
            ShopDefinition bakery = ShopCatalog.Find("bakery").Value;

            int stage = EstateState.ShopProductionStage(shop, bakery, nowUnixSeconds: 999999);

            Assert.AreEqual(0, stage);
        }

        [Test]
        public void ShopProductionStage_JustStarted_IsStageOne()
        {
            var shop = new ShopState { Unlocked = true, ProductionStartedAtUnixSeconds = 1000 };
            ShopDefinition bakery = ShopCatalog.Find("bakery").Value; // 60s production

            int stage = EstateState.ShopProductionStage(shop, bakery, nowUnixSeconds: 1001);

            Assert.AreEqual(1, stage);
        }

        [Test]
        public void ShopProductionStage_AtFullDuration_IsStageTwo()
        {
            var shop = new ShopState { Unlocked = true, ProductionStartedAtUnixSeconds = 1000 };
            ShopDefinition bakery = ShopCatalog.Find("bakery").Value;

            int stage = EstateState.ShopProductionStage(shop, bakery, nowUnixSeconds: 1060);

            Assert.AreEqual(2, stage);
        }

        [Test]
        public void ShopProductionStage_PastFullDuration_StaysStageTwo()
        {
            var shop = new ShopState { Unlocked = true, ProductionStartedAtUnixSeconds = 1000 };
            ShopDefinition bakery = ShopCatalog.Find("bakery").Value;

            int stage = EstateState.ShopProductionStage(shop, bakery, nowUnixSeconds: 999999);

            Assert.AreEqual(2, stage);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Same command. Expected: FAIL with "Inventory/Shops/ShopProductionStage
does not exist" (compile error) -- Tasks 2 and 3 must already exist for
this to compile at all.

- [ ] **Step 3: Write the implementation**

Add to `Assets/Scripts/Core/EstateState.cs`, inside the existing
`namespace UnderstudyKingdom.Core` block, alongside the existing
`LandPlot`/`EstateState` classes -- add the new `ShopState` class right
after `LandPlot`'s closing brace:

```csharp
    [Serializable]
    public class ShopState
    {
        public bool Unlocked;
        public long ProductionStartedAtUnixSeconds;
    }
```

Then, inside the existing `EstateState` class: add two new fields right
after the existing `public LandPlot[] Plots;`:

```csharp
        public int[] Inventory;
        public ShopState[] Shops;
```

In the existing constructor, after the existing `Plots` initialization
loop, add:

```csharp
            Inventory = new int[GoodsCatalog.Count];
            Shops = new ShopState[ShopCatalog.All.Length];
            for (int i = 0; i < Shops.Length; i++)
            {
                Shops[i] = new ShopState();
            }
```

Add the new static method after the existing `UnlockCost` method:

```csharp
        /// <summary>
        /// Mirrors GrowthStage's exact shape -- 0 while idle (never
        /// started), 1 while producing, 2 once ProductionSeconds has
        /// fully elapsed. No intermediate-division truncation bug (see
        /// GrowthStage's own fix history) -- compares elapsed directly
        /// against the full duration only, nothing to truncate.
        /// </summary>
        public static int ShopProductionStage(ShopState shop, ShopDefinition def, long nowUnixSeconds)
        {
            if (shop.ProductionStartedAtUnixSeconds == 0)
            {
                return 0;
            }

            long elapsed = nowUnixSeconds - shop.ProductionStartedAtUnixSeconds;
            if (elapsed < 0)
            {
                return 0;
            }
            return elapsed >= def.ProductionSeconds ? 2 : 1;
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Same command. Expected: all `EstateStateTests` (existing + new) PASS,
full EditMode suite grows by 6 tests with 0 failures.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/EstateState.cs Assets/Tests/EditMode/EstateStateTests.cs
git commit -m "feat: add ShopState, Inventory, Shops, ShopProductionStage to EstateState"
```

---

### Task 5: SaveService per-field migration for Inventory/Shops

**Files:**
- Modify: `Assets/Scripts/Core/EstateSaveData.cs`
- Modify: `Assets/Scripts/Core/SaveService.cs`
- Modify: `Assets/Tests/EditMode/SaveServiceEstateTests.cs`

**Interfaces:**
- Consumes: `EstateState.Inventory`/`Shops` (Task 4).
- Produces: `EstateSaveData.Inventory` (`int[]`), `EstateSaveData.Shops`
  (`ShopState[]`) fields. `SaveService.LoadEstate()`'s validation changes
  from "any problem -> fresh whole EstateState" to per-field: `Coins`/
  `Plots` load exactly as before; `Inventory`/`Shops` are defaulted
  individually only if missing/wrong-length.

- [ ] **Step 1: Write the failing tests**

Add to the EXISTING `Assets/Tests/EditMode/SaveServiceEstateTests.cs`
(same file, same class):

```csharp
        [Test]
        public void SaveEstateThenLoadEstate_RoundTripsInventoryAndShops()
        {
            var original = new EstateState { Coins = 100 };
            original.Inventory[0] = 3; // wheat x3
            original.Shops[0].Unlocked = true;
            original.Shops[0].ProductionStartedAtUnixSeconds = 555;

            SaveService.SaveEstate(original);
            var loaded = SaveService.LoadEstate();

            Assert.AreEqual(3, loaded.Inventory[0]);
            Assert.IsTrue(loaded.Shops[0].Unlocked);
            Assert.AreEqual(555, loaded.Shops[0].ProductionStartedAtUnixSeconds);
        }

        [Test]
        public void LoadEstate_SaveFileMissingInventoryAndShops_KeepsCoinsAndPlotsDefaultsRest()
        {
            // Simulates a real Phase-1-only save file, written before
            // Inventory/Shops existed -- literal JSON with no such keys.
            // Must NOT reset Coins/Plots just because the new fields are
            // missing (the whole point of per-field migration).
            System.IO.File.WriteAllText(SaveService.EstateSavePath,
                "{\"Version\":1,\"Coins\":250,\"Plots\":[" +
                "{\"Unlocked\":true,\"CropId\":null,\"PlantedAtUnixSeconds\":0,\"WateredAtUnixSeconds\":0}," +
                "{\"Unlocked\":true,\"CropId\":null,\"PlantedAtUnixSeconds\":0,\"WateredAtUnixSeconds\":0}," +
                "{\"Unlocked\":true,\"CropId\":null,\"PlantedAtUnixSeconds\":0,\"WateredAtUnixSeconds\":0}," +
                "{\"Unlocked\":true,\"CropId\":null,\"PlantedAtUnixSeconds\":0,\"WateredAtUnixSeconds\":0}," +
                "{\"Unlocked\":false,\"CropId\":null,\"PlantedAtUnixSeconds\":0,\"WateredAtUnixSeconds\":0}," +
                "{\"Unlocked\":false,\"CropId\":null,\"PlantedAtUnixSeconds\":0,\"WateredAtUnixSeconds\":0}," +
                "{\"Unlocked\":false,\"CropId\":null,\"PlantedAtUnixSeconds\":0,\"WateredAtUnixSeconds\":0}," +
                "{\"Unlocked\":false,\"CropId\":null,\"PlantedAtUnixSeconds\":0,\"WateredAtUnixSeconds\":0}]}");

            var loaded = SaveService.LoadEstate();

            Assert.AreEqual(250, loaded.Coins);
            Assert.IsTrue(loaded.Plots[0].Unlocked);
            Assert.IsFalse(loaded.Plots[4].Unlocked);
            Assert.AreEqual(GoodsCatalog.Count, loaded.Inventory.Length);
            Assert.AreEqual(0, loaded.Inventory[0]);
            Assert.AreEqual(ShopCatalog.All.Length, loaded.Shops.Length);
            Assert.IsFalse(loaded.Shops[0].Unlocked);
        }

        [Test]
        public void LoadEstate_WrongInventoryLength_DefaultsOnlyInventory()
        {
            var corrupted = new EstateSaveData
            {
                Coins = 50,
                Plots = new EstateState().Plots,
                Inventory = new int[2], // wrong length
                Shops = new EstateState().Shops
            };
            System.IO.File.WriteAllText(SaveService.EstateSavePath, UnityEngine.JsonUtility.ToJson(corrupted));

            var loaded = SaveService.LoadEstate();

            Assert.AreEqual(50, loaded.Coins);
            Assert.AreEqual(GoodsCatalog.Count, loaded.Inventory.Length);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Same command. Expected: FAIL -- `EstateSaveData.Inventory`/`Shops` don't
exist yet (compile error).

- [ ] **Step 3: Write the implementation**

Edit `Assets/Scripts/Core/EstateSaveData.cs` -- add two fields after the
existing `Plots`:

```csharp
        public int[] Inventory;
        public ShopState[] Shops;
```

Edit `Assets/Scripts/Core/SaveService.cs`'s `SaveEstate` method -- add
two lines to the `EstateSaveData` object initializer, after the existing
`Plots = state.Plots`:

```csharp
                Inventory = state.Inventory,
                Shops = state.Shops
```

Edit `LoadEstate`'s body. Currently it returns a whole fresh
`EstateState()` if `data.Plots` is null or the wrong length. Change this
so that check applies ONLY to `Plots` (as today), and add SEPARATE,
independent per-field checks for `Inventory`/`Shops` that patch just
those two fields onto the otherwise-loaded state rather than discarding
everything:

```csharp
                var data = JsonUtility.FromJson<EstateSaveData>(raw);

                if (data.Plots == null || data.Plots.Length != EstateState.PlotCount)
                {
                    return new EstateState();
                }

                int[] inventory = (data.Inventory != null && data.Inventory.Length == GoodsCatalog.Count)
                    ? data.Inventory
                    : new int[GoodsCatalog.Count];

                ShopState[] shops;
                if (data.Shops != null && data.Shops.Length == ShopCatalog.All.Length)
                {
                    shops = data.Shops;
                }
                else
                {
                    shops = new ShopState[ShopCatalog.All.Length];
                    for (int i = 0; i < shops.Length; i++)
                    {
                        shops[i] = new ShopState();
                    }
                }

                return new EstateState
                {
                    Coins = data.Coins,
                    Plots = data.Plots,
                    Inventory = inventory,
                    Shops = shops
                };
```

This replaces the existing `return new EstateState { Coins = data.Coins,
Plots = data.Plots };` line -- find it and replace the whole block above
it through that return, matching the pattern shown (the `Plots`
null/wrong-length check stays exactly where it is and keeps returning a
whole fresh `EstateState()`, per Global Constraints -- only `Inventory`/
`Shops` get the new per-field treatment).

- [ ] **Step 4: Run tests to verify they pass**

Same command. Expected: all `SaveServiceEstateTests` (existing + new 3)
PASS, and every pre-existing `SaveServiceEstateTests`/`SaveServiceTests`
test still passes unmodified.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/EstateSaveData.cs Assets/Scripts/Core/SaveService.cs Assets/Tests/EditMode/SaveServiceEstateTests.cs
git commit -m "feat: per-field save migration for Inventory/Shops"
```

---

### Task 6: EstatePanelController -- harvest-to-inventory, sell, tabs, shops

**Files:**
- Modify: `Assets/Scripts/UI/EstatePanelController.cs`
- Modify: `Assets/Tests/PlayMode/EstatePanelControllerTests.cs`

**Interfaces:**
- Consumes: `GoodsCatalog`/`ProductCatalog`/`ShopCatalog` (Tasks 1-3),
  `EstateState.Inventory`/`Shops`/`ShopProductionStage` (Task 4).
- Produces: new nested class `EstatePanelController.InventoryRowView`
  (`GameObject root`, `TextMeshProUGUI label`, `Button sellButton`), new
  nested class `EstatePanelController.ShopRowView` (`TextMeshProUGUI
  statusLabel`, `Button actionButton`, `TextMeshProUGUI actionButtonLabel`),
  new `Initialize(...)` trailing parameters: `Button landTabButton, Button
  shopsTabButton, GameObject landTabRoot, GameObject shopsTabRoot,
  InventoryRowView[] inventoryRows, ShopRowView[] shopRows` (6 new
  trailing params, appended after the existing `DuelModalGate gate`
  parameter -- consumed by Task 7's scene wiring).

This task has many small behaviors -- each is its own TDD cycle within
the same file, matching Phase 1's Task 4 structure.

- [ ] **Step 1: Write the failing tests**

Add these fields/setup/tests to the EXISTING
`Assets/Tests/PlayMode/EstatePanelControllerTests.cs` (same file, same
class). Add new fields near the existing ones:

```csharp
        private Button landTabButton;
        private Button shopsTabButton;
        private GameObject landTabRoot;
        private GameObject shopsTabRoot;
        private EstatePanelController.InventoryRowView[] inventoryRows;
        private EstatePanelController.ShopRowView[] shopRows;
```

Add construction in `SetUp`, right before the `controller.Initialize(...)`
call, and update that call to pass the 6 new arguments:

```csharp
            landTabButton = CreateButton("LandTabButton");
            shopsTabButton = CreateButton("ShopsTabButton");
            landTabRoot = new GameObject("LandTabRoot");
            landTabRoot.transform.SetParent(canvasObject.transform, false);
            shopsTabRoot = new GameObject("ShopsTabRoot");
            shopsTabRoot.transform.SetParent(canvasObject.transform, false);

            inventoryRows = new EstatePanelController.InventoryRowView[GoodsCatalog.Count];
            for (int i = 0; i < inventoryRows.Length; i++)
            {
                var rowRoot = new GameObject($"InventoryRow{i}");
                rowRoot.transform.SetParent(canvasObject.transform, false);
                inventoryRows[i] = new EstatePanelController.InventoryRowView
                {
                    root = rowRoot,
                    label = CreateLabel($"InventoryLabel{i}"),
                    sellButton = CreateButton($"SellButton{i}")
                };
            }

            shopRows = new EstatePanelController.ShopRowView[ShopCatalog.All.Length];
            for (int i = 0; i < shopRows.Length; i++)
            {
                var actionButton = CreateButton($"ShopActionButton{i}");
                shopRows[i] = new EstatePanelController.ShopRowView
                {
                    statusLabel = CreateLabel($"ShopStatusLabel{i}"),
                    actionButton = actionButton,
                    actionButtonLabel = CreateLabel($"ShopActionLabel{i}")
                };
            }
```

Update the existing `controller.Initialize(...)` call to append the 6
new arguments after `gate` (the existing last argument):

```csharp
                viewHistoryButton, councilButton, eventsButton, customizeButton, gate,
                landTabButton, shopsTabButton, landTabRoot, shopsTabRoot, inventoryRows, shopRows);
```

Add the new tests (same class, after the existing tests):

```csharp
        [Test]
        public void EstateButton_OnOpen_DefaultsToLandTab()
        {
            estateButton.onClick.Invoke();

            Assert.IsTrue(landTabRoot.activeSelf);
            Assert.IsFalse(shopsTabRoot.activeSelf);
        }

        [Test]
        public void TapShopsTab_ShowsShopsTabHidesLandTab()
        {
            estateButton.onClick.Invoke();

            shopsTabButton.onClick.Invoke();

            Assert.IsFalse(landTabRoot.activeSelf);
            Assert.IsTrue(shopsTabRoot.activeSelf);
        }

        [Test]
        public void TapLandTabAfterShops_ShowsLandTabAgain()
        {
            estateButton.onClick.Invoke();
            shopsTabButton.onClick.Invoke();

            landTabButton.onClick.Invoke();

            Assert.IsTrue(landTabRoot.activeSelf);
            Assert.IsFalse(shopsTabRoot.activeSelf);
        }

        [Test]
        public void HarvestMaturePlot_AddsToInventoryInsteadOfCoins()
        {
            var seeded = new EstateState { Coins = 200 };
            seeded.Plots[0].CropId = "wheat";
            seeded.Plots[0].PlantedAtUnixSeconds = 1;
            seeded.Plots[0].WateredAtUnixSeconds = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3600;
            SaveService.SaveEstate(seeded);

            estateButton.onClick.Invoke();
            plotViews[0].tapButton.onClick.Invoke(); // harvest

            closeButton.onClick.Invoke();
            var saved = SaveService.LoadEstate();
            Assert.AreEqual(200, saved.Coins); // unchanged -- no auto-sell
            Assert.AreEqual(1, saved.Inventory[GoodsCatalog.IndexOf("wheat")]);
        }

        [Test]
        public void SellInventoryStack_AwardsCoinsForWholeStackAndZeroesCount()
        {
            var seeded = new EstateState { Coins = 100 };
            seeded.Inventory[GoodsCatalog.IndexOf("wheat")] = 4; // 4 x 12 = 48
            SaveService.SaveEstate(seeded);

            estateButton.onClick.Invoke();
            int wheatIndex = GoodsCatalog.IndexOf("wheat");
            inventoryRows[wheatIndex].sellButton.onClick.Invoke();

            Assert.AreEqual("Coins: 148", coinsLabel.text);
            closeButton.onClick.Invoke();
            var saved = SaveService.LoadEstate();
            Assert.AreEqual(0, saved.Inventory[wheatIndex]);
        }

        [Test]
        public void UnlockShop_WithEnoughCoins_DeductsUnlockCostAndUnlocks()
        {
            var seeded = new EstateState { Coins = 200 }; // bakery costs 150
            SaveService.SaveEstate(seeded);

            estateButton.onClick.Invoke();
            shopsTabButton.onClick.Invoke();
            shopRows[0].actionButton.onClick.Invoke(); // bakery unlock

            Assert.AreEqual("Coins: 50", coinsLabel.text);
            closeButton.onClick.Invoke();
            var saved = SaveService.LoadEstate();
            Assert.IsTrue(saved.Shops[0].Unlocked);
        }

        [Test]
        public void StartProduction_WithInputAndCoins_DeductsBothAndStartsTimer()
        {
            var seeded = new EstateState { Coins = 200 };
            seeded.Shops[0].Unlocked = true; // bakery, already unlocked
            seeded.Inventory[GoodsCatalog.IndexOf("wheat")] = 2;
            SaveService.SaveEstate(seeded);

            estateButton.onClick.Invoke();
            shopsTabButton.onClick.Invoke();
            shopRows[0].actionButton.onClick.Invoke(); // start production

            Assert.AreEqual("Coins: 197", coinsLabel.text); // 200 - 3 (bakery StartCost)
            closeButton.onClick.Invoke();
            var saved = SaveService.LoadEstate();
            Assert.AreEqual(1, saved.Inventory[GoodsCatalog.IndexOf("wheat")]); // 2 - 1
            Assert.AreNotEqual(0, saved.Shops[0].ProductionStartedAtUnixSeconds);
        }

        [Test]
        public void CollectReadyShop_AddsOutputAndResetsToIdle()
        {
            var seeded = new EstateState { Coins = 200 };
            seeded.Shops[0].Unlocked = true;
            seeded.Shops[0].ProductionStartedAtUnixSeconds = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3600; // long past bakery's 60s
            SaveService.SaveEstate(seeded);

            estateButton.onClick.Invoke();
            shopsTabButton.onClick.Invoke();
            shopRows[0].actionButton.onClick.Invoke(); // collect

            closeButton.onClick.Invoke();
            var saved = SaveService.LoadEstate();
            Assert.AreEqual(1, saved.Inventory[GoodsCatalog.IndexOf("bread")]);
            Assert.AreEqual(0, saved.Shops[0].ProductionStartedAtUnixSeconds);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Same PlayMode command shape as Phase 1's Task 4 (no `-quit`). Expected:
FAIL with compile errors (`InventoryRowView`/`ShopRowView` don't exist,
`Initialize` doesn't accept 6 more args).

- [ ] **Step 3: Write the implementation**

In `Assets/Scripts/UI/EstatePanelController.cs`:

Add two new nested public classes, alongside the existing `PlotView`:

```csharp
        [Serializable]
        public class InventoryRowView
        {
            public GameObject root;
            public TextMeshProUGUI label;
            public Button sellButton;
        }

        [Serializable]
        public class ShopRowView
        {
            public TextMeshProUGUI statusLabel;
            public Button actionButton;
            public TextMeshProUGUI actionButtonLabel;
        }
```

Add 6 new `[SerializeField]` fields after the existing `DuelModalGate gate;`:

```csharp
        [SerializeField] private Button landTabButton;
        [SerializeField] private Button shopsTabButton;
        [SerializeField] private GameObject landTabRoot;
        [SerializeField] private GameObject shopsTabRoot;
        [SerializeField] private InventoryRowView[] inventoryRows;
        [SerializeField] private ShopRowView[] shopRows;
```

Extend `Initialize(...)`'s parameter list, adding the same 6 as new
trailing parameters after `DuelModalGate gate`, and assign them in the
body (`this.landTabButton = landTabButton;` etc., same pattern as every
existing assignment), then call `Bind()` at the end as before -- no
change to the existing `Initialize` body's structure otherwise.

Extend `Bind()`: add tab-button listeners and initialize inventory/shop
row listeners, alongside the existing `plotViews`/`seedButtons` wiring:

```csharp
            landTabButton.onClick.RemoveAllListeners();
            landTabButton.onClick.AddListener(() => SetActiveTab(true));
            shopsTabButton.onClick.RemoveAllListeners();
            shopsTabButton.onClick.AddListener(() => SetActiveTab(false));

            for (int i = 0; i < inventoryRows.Length; i++)
            {
                int goodsIndex = i;
                inventoryRows[i].sellButton.onClick.RemoveAllListeners();
                inventoryRows[i].sellButton.onClick.AddListener(() => OnSellStack(goodsIndex));
            }

            for (int i = 0; i < shopRows.Length; i++)
            {
                int shopIndex = i;
                shopRows[i].actionButton.onClick.RemoveAllListeners();
                shopRows[i].actionButton.onClick.AddListener(() => OnShopActionTapped(shopIndex));
            }
```

Modify `OnEstateButtonClicked()`: after the existing `panelRoot.SetActive(true);`,
add `SetActiveTab(true);` (default to Land tab on open, per spec), then
keep the existing `RefreshPlots();` call and add a new
`RefreshInventoryAndShops();` call right after it.

Add the new private methods (place these near the end of the class, after
the existing private helpers):

```csharp
        private void SetActiveTab(bool land)
        {
            landTabRoot.SetActive(land);
            shopsTabRoot.SetActive(!land);
        }

        private void OnSellStack(int goodsIndex)
        {
            int count = state.Inventory[goodsIndex];
            if (count <= 0)
            {
                return;
            }

            string goodsId = GoodsIdForIndex(goodsIndex);
            state.Coins += count * GoodsCatalog.SellValue(goodsId);
            state.Inventory[goodsIndex] = 0;
            SaveService.SaveEstate(state);
            RefreshInventoryAndShops();
            coinsLabel.text = $"Coins: {state.Coins}";
        }

        private void OnShopActionTapped(int shopIndex)
        {
            ShopDefinition def = ShopCatalog.All[shopIndex];
            ShopState shop = state.Shops[shopIndex];

            if (!shop.Unlocked)
            {
                if (state.Coins < def.UnlockCost)
                {
                    return;
                }
                state.Coins -= def.UnlockCost;
                shop.Unlocked = true;
                SaveService.SaveEstate(state);
                RefreshInventoryAndShops();
                coinsLabel.text = $"Coins: {state.Coins}";
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int stage = EstateState.ShopProductionStage(shop, def, now);

            if (stage == 0)
            {
                int inputIndex = GoodsCatalog.IndexOf(def.InputGoodsId);
                if (state.Coins < def.StartCost || state.Inventory[inputIndex] < 1)
                {
                    return;
                }
                state.Coins -= def.StartCost;
                state.Inventory[inputIndex] -= 1;
                shop.ProductionStartedAtUnixSeconds = now;
                SaveService.SaveEstate(state);
                RefreshInventoryAndShops();
                coinsLabel.text = $"Coins: {state.Coins}";
                return;
            }

            if (stage == 2)
            {
                int outputIndex = GoodsCatalog.IndexOf(def.OutputGoodsId);
                state.Inventory[outputIndex] += 1;
                shop.ProductionStartedAtUnixSeconds = 0;
                SaveService.SaveEstate(state);
                RefreshInventoryAndShops();
                return;
            }

            // stage == 1 (in progress): no-op, matches crop-growth's own
            // "tapping a growing plot does nothing" contract.
        }

        private static string GoodsIdForIndex(int goodsIndex)
        {
            return goodsIndex < CropCatalog.All.Length
                ? CropCatalog.All[goodsIndex].Id
                : ProductCatalog.All[goodsIndex - CropCatalog.All.Length].Id;
        }

        private void RefreshInventoryAndShops()
        {
            for (int i = 0; i < inventoryRows.Length; i++)
            {
                int count = state.Inventory[i];
                inventoryRows[i].root.SetActive(count > 0);
                if (count > 0)
                {
                    string goodsId = GoodsIdForIndex(i);
                    inventoryRows[i].label.text = $"{GoodsCatalog.DisplayName(goodsId)} x{count}";
                }
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            for (int i = 0; i < shopRows.Length; i++)
            {
                ShopDefinition def = ShopCatalog.All[i];
                ShopState shop = state.Shops[i];

                if (!shop.Unlocked)
                {
                    shopRows[i].statusLabel.text = $"{def.DisplayName} -- Unlock: {def.UnlockCost}";
                    shopRows[i].actionButtonLabel.text = "Unlock";
                    shopRows[i].actionButton.interactable = state.Coins >= def.UnlockCost;
                    continue;
                }

                int stage = EstateState.ShopProductionStage(shop, def, now);
                if (stage == 0)
                {
                    int inputIndex = GoodsCatalog.IndexOf(def.InputGoodsId);
                    shopRows[i].statusLabel.text = $"{def.DisplayName}: {def.InputGoodsId} x1 + {def.StartCost} coins";
                    shopRows[i].actionButtonLabel.text = "Start";
                    shopRows[i].actionButton.interactable = state.Coins >= def.StartCost && state.Inventory[inputIndex] >= 1;
                }
                else if (stage == 1)
                {
                    shopRows[i].statusLabel.text = $"{def.DisplayName}: producing {def.OutputGoodsId}...";
                    shopRows[i].actionButtonLabel.text = "...";
                    shopRows[i].actionButton.interactable = false;
                }
                else
                {
                    shopRows[i].statusLabel.text = $"{def.DisplayName}: {def.OutputGoodsId} ready!";
                    shopRows[i].actionButtonLabel.text = "Collect";
                    shopRows[i].actionButton.interactable = true;
                }
            }
        }
```

Modify the existing `OnPlotTapped`'s harvest branch: find the lines

```csharp
            state.Coins += crop.Value.SellValue;
            plot.CropId = null;
```

and replace with:

```csharp
            state.Inventory[GoodsCatalog.IndexOf(plot.CropId)] += 1;
            plot.CropId = null;
```

(the rest of that branch -- clearing `PlantedAtUnixSeconds`/
`WateredAtUnixSeconds`, the harvest-tween coroutine, `RefreshPlots()` --
is unchanged). Also add `RefreshInventoryAndShops();` right after that
branch's existing `RefreshPlots();` call, so the inventory strip updates
immediately after a harvest even while still on the Land tab.

Add `using UnderstudyKingdom.Core;` is already present (Task 4/prior
tasks' types are all in that namespace, already imported).

- [ ] **Step 4: Run tests to verify they pass**

Same PlayMode command. Expected: all `EstatePanelControllerTests`
(existing + new) PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/EstatePanelController.cs Assets/Tests/PlayMode/EstatePanelControllerTests.cs
git commit -m "feat: harvest-to-inventory, sell, Land/Shops tabs, shop production in EstatePanelController"
```

---

### Task 7: CoreLoopSceneBuilder wiring

**Files:**
- Modify: `Assets/Editor/CoreLoopSceneBuilder.cs`

**Interfaces:**
- Consumes: `EstatePanelController`'s new `Initialize(...)` signature
  (Task 6) -- read the real file first to confirm exact parameter order
  before writing this call site.

- [ ] **Step 1: Add the Land/Shops tab buttons**

Find the existing `estateController.Initialize(...)` call (search for
`UnderstudyKingdom.UI.EstatePanelController`). Immediately before it, add:

```csharp
            var estateLandTabButtonObject = new GameObject("LandTabButton", typeof(Image), typeof(Button));
            estateLandTabButtonObject.transform.SetParent(estatePanelRootObject.transform, false);
            var estateLandTabButtonRect = estateLandTabButtonObject.GetComponent<RectTransform>();
            estateLandTabButtonRect.anchoredPosition = new Vector2(-80f, 255f);
            estateLandTabButtonRect.sizeDelta = new Vector2(140f, 40f);
            estateLandTabButtonObject.GetComponent<Image>().color = new Color(0.35f, 0.55f, 0.3f, 1f);
            var estateLandTabButton = estateLandTabButtonObject.GetComponent<Button>();
            TextMeshProUGUI estateLandTabLabel = CreateLabel(estateLandTabButtonObject.transform, "Text", 0f, "Land");
            var estateLandTabLabelRect = estateLandTabLabel.GetComponent<RectTransform>();
            estateLandTabLabelRect.anchorMin = Vector2.zero;
            estateLandTabLabelRect.anchorMax = Vector2.one;
            estateLandTabLabelRect.sizeDelta = Vector2.zero;
            estateLandTabLabelRect.anchoredPosition = Vector2.zero;

            var estateShopsTabButtonObject = new GameObject("ShopsTabButton", typeof(Image), typeof(Button));
            estateShopsTabButtonObject.transform.SetParent(estatePanelRootObject.transform, false);
            var estateShopsTabButtonRect = estateShopsTabButtonObject.GetComponent<RectTransform>();
            estateShopsTabButtonRect.anchoredPosition = new Vector2(80f, 255f);
            estateShopsTabButtonRect.sizeDelta = new Vector2(140f, 40f);
            estateShopsTabButtonObject.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.7f, 1f);
            var estateShopsTabButton = estateShopsTabButtonObject.GetComponent<Button>();
            TextMeshProUGUI estateShopsTabLabel = CreateLabel(estateShopsTabButtonObject.transform, "Text", 0f, "Shops");
            var estateShopsTabLabelRect = estateShopsTabLabel.GetComponent<RectTransform>();
            estateShopsTabLabelRect.anchorMin = Vector2.zero;
            estateShopsTabLabelRect.anchorMax = Vector2.one;
            estateShopsTabLabelRect.sizeDelta = Vector2.zero;
            estateShopsTabLabelRect.anchoredPosition = Vector2.zero;
```

- [ ] **Step 2: Wrap the existing plot grid + seed picker in a "LandTabRoot" container**

The existing plot-grid and seed-picker GameObjects (`estatePlotViews`'
slot backgrounds and `estateSeedPickerObject`) are currently direct
children of `estatePanelRootObject`. Add a new empty container BEFORE
those blocks are constructed (find where the 8-plot-grid loop begins,
search for `for (int i = 0; i < 8; i++)` under the Estate section), and
reparent the plot grid and seed picker under it instead of directly
under the panel:

```csharp
            var estateLandTabRootObject = new GameObject("LandTabRoot");
            estateLandTabRootObject.transform.SetParent(estatePanelRootObject.transform, false);
```

Then change every `slotBackgroundObject.transform.SetParent(estatePanelRootObject.transform, false);`
inside the 8-plot loop to
`slotBackgroundObject.transform.SetParent(estateLandTabRootObject.transform, false);`,
and change `estateSeedPickerObject.transform.SetParent(estatePanelRootObject.transform, false);`
to `estateSeedPickerObject.transform.SetParent(estateLandTabRootObject.transform, false);`.
(Only the `SetParent` target changes -- every position/size value inside
the plot grid and seed picker blocks stays exactly as it already is,
since `false` for `worldPositionStays` means positions stay relative to
the new parent's local space, which is still centered the same way.)

- [ ] **Step 3: Add the ShopsTabRoot with the inventory strip and 3 shop rows**

Add this block right after the seed-picker construction (before the
crop-sprite-loading block):

```csharp
            var estateShopsTabRootObject = new GameObject("ShopsTabRoot");
            estateShopsTabRootObject.transform.SetParent(estatePanelRootObject.transform, false);
            estateShopsTabRootObject.SetActive(false);

            var estateShopRows = new UnderstudyKingdom.UI.EstatePanelController.ShopRowView[3];
            string[] estateShopIds = { "bakery", "kitchen", "pie_house" };
            for (int i = 0; i < 3; i++)
            {
                float rowY = 140f - i * 120f;

                var rowBackgroundObject = new GameObject($"ShopRow_{estateShopIds[i]}", typeof(Image));
                rowBackgroundObject.transform.SetParent(estateShopsTabRootObject.transform, false);
                var rowBackgroundRect = rowBackgroundObject.GetComponent<RectTransform>();
                rowBackgroundRect.anchoredPosition = new Vector2(0f, rowY);
                rowBackgroundRect.sizeDelta = new Vector2(640f, 100f);
                rowBackgroundObject.GetComponent<Image>().color = new Color(0.15f, 0.2f, 0.15f, 1f);

                TextMeshProUGUI statusLabel = CreateLabel(rowBackgroundObject.transform, "StatusLabel", 0f, string.Empty);
                statusLabel.fontSize = 20f;
                var statusLabelRect = statusLabel.GetComponent<RectTransform>();
                statusLabelRect.anchoredPosition = new Vector2(-80f, 15f);
                statusLabelRect.sizeDelta = new Vector2(460f, 50f);

                var actionButtonObject = new GameObject("ActionButton", typeof(Image), typeof(Button));
                actionButtonObject.transform.SetParent(rowBackgroundObject.transform, false);
                var actionButtonRect = actionButtonObject.GetComponent<RectTransform>();
                actionButtonRect.anchoredPosition = new Vector2(250f, -15f);
                actionButtonRect.sizeDelta = new Vector2(120f, 44f);
                actionButtonObject.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.7f, 1f);
                var actionButton = actionButtonObject.GetComponent<Button>();
                TextMeshProUGUI actionButtonLabel = CreateLabel(actionButtonObject.transform, "Text", 0f, string.Empty);
                var actionButtonLabelRect = actionButtonLabel.GetComponent<RectTransform>();
                actionButtonLabelRect.anchorMin = Vector2.zero;
                actionButtonLabelRect.anchorMax = Vector2.one;
                actionButtonLabelRect.sizeDelta = Vector2.zero;
                actionButtonLabelRect.anchoredPosition = Vector2.zero;

                estateShopRows[i] = new UnderstudyKingdom.UI.EstatePanelController.ShopRowView
                {
                    statusLabel = statusLabel,
                    actionButton = actionButton,
                    actionButtonLabel = actionButtonLabel
                };
            }

            // Inventory strip -- visible regardless of active tab, so it
            // is a direct child of estatePanelRootObject, not either tab root.
            var estateInventoryRows = new UnderstudyKingdom.UI.EstatePanelController.InventoryRowView[UnderstudyKingdom.Core.GoodsCatalog.Count];
            for (int i = 0; i < estateInventoryRows.Length; i++)
            {
                float rowX = -300f + i * 120f;

                var invRowObject = new GameObject($"InventoryRow{i}", typeof(Image));
                invRowObject.transform.SetParent(estatePanelRootObject.transform, false);
                var invRowRect = invRowObject.GetComponent<RectTransform>();
                invRowRect.anchoredPosition = new Vector2(rowX, -360f);
                invRowRect.sizeDelta = new Vector2(110f, 50f);
                invRowObject.GetComponent<Image>().color = new Color(0.2f, 0.25f, 0.15f, 1f);
                invRowObject.SetActive(false);

                TextMeshProUGUI invLabel = CreateLabel(invRowObject.transform, "Label", 0f, string.Empty);
                invLabel.fontSize = 14f;
                var invLabelRect = invLabel.GetComponent<RectTransform>();
                invLabelRect.anchoredPosition = new Vector2(0f, 10f);
                invLabelRect.sizeDelta = new Vector2(105f, 24f);

                var sellButtonObject = new GameObject("SellButton", typeof(Image), typeof(Button));
                sellButtonObject.transform.SetParent(invRowObject.transform, false);
                var sellButtonRect = sellButtonObject.GetComponent<RectTransform>();
                sellButtonRect.anchoredPosition = new Vector2(0f, -12f);
                sellButtonRect.sizeDelta = new Vector2(90f, 22f);
                sellButtonObject.GetComponent<Image>().color = new Color(0.6f, 0.3f, 0.3f, 1f);
                var sellButton = sellButtonObject.GetComponent<Button>();
                TextMeshProUGUI sellLabel = CreateLabel(sellButtonObject.transform, "Text", 0f, "Sell");
                sellLabel.fontSize = 14f;
                var sellLabelRect = sellLabel.GetComponent<RectTransform>();
                sellLabelRect.anchorMin = Vector2.zero;
                sellLabelRect.anchorMax = Vector2.one;
                sellLabelRect.sizeDelta = Vector2.zero;
                sellLabelRect.anchoredPosition = Vector2.zero;

                estateInventoryRows[i] = new UnderstudyKingdom.UI.EstatePanelController.InventoryRowView
                {
                    root = invRowObject,
                    label = invLabel,
                    sellButton = sellButton
                };
            }
```

Note the row math: 3 shop rows at y = 140, 20, -100 (120-unit pitch,
100-tall rows -- spans from y=190 down to y=-150, comfortably inside the
panel's usable area, well clear of the tab buttons at y=255 above and
the inventory strip at y=-360 below). 6 inventory entries at x = -300,
-180, -60, 60, 180, 300 (120-unit pitch, 110 wide each -- spans
[-355,355], just inside the 700-wide panel's [-350,350] bound with 5
units to spare on the outer two -- **double-check this against the real
panel width during Build/Verify; narrow to 100-unit pitch (six slots
spanning [-315,315]) if Verify or a visual check shows any overflow,
same lesson as Phase 1's plot-grid overflow bug**).

- [ ] **Step 4: Update the `estateController.Initialize(...)` call site**

Append the 6 new arguments after the existing final argument
(`duelModalGate`):

```csharp
                armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton, councilButton,
                eventsButton, customizeButton, duelModalGate,
                estateLandTabButton, estateShopsTabButton, estateLandTabRootObject, estateShopsTabRootObject,
                estateInventoryRows, estateShopRows);
```

- [ ] **Step 5: Rebuild and verify the scene**

Run:
```
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Build -logFile <path>.log
```
(no `-quit` needed -- `Build()` now calls `EditorApplication.Exit()` on
its own success path). Expected: `CoreLoopSceneBuilder: saved scene to
Assets/Scenes/CoreLoop.unity`, no new errors beyond the already-expected
`carrot_mature.png`/`pumpkin_*.png` missing-art warnings.

Then:
```
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -executeMethod UnderstudyKingdom.EditorTools.CoreLoopSceneBuilder.Verify -logFile <path>.log
```
Expected: `CoreLoopSceneBuilder.Verify: scene opened and controller found
successfully.` (no new `Verify()` check is needed this task -- the
existing `EstatePanelController` presence check already covers this,
since it's still the same one controller, just with more fields wired).

- [ ] **Step 6: Run the full test suite once**

EditMode: same command shape as prior tasks, `-testPlatform EditMode`.
Expected: 129 EditMode tests pass, 0 failures -- 102 Phase-1 baseline +
6 (Task 1, ProductCatalog) + 6 (Task 2, ShopCatalog) + 6 (Task 3,
GoodsCatalog) + 6 (Task 4, EstateState) + 3 (Task 5, SaveService) = 129.
PlayMode: `-testPlatform PlayMode`. Expected: 105 PlayMode tests pass,
0 failures -- 97 Phase-1 baseline + 8 (Task 6, EstatePanelController).
Task 8 (not yet run at this point) adds the 106th.

- [ ] **Step 7: Commit**

```bash
git add Assets/Editor/CoreLoopSceneBuilder.cs Assets/Scenes/CoreLoop.unity
git commit -m "feat: wire Land/Shops tabs, inventory strip, and shop rows into CoreLoop scene"
```

---

### Task 8: Scene-level regression test

**Files:**
- Modify: `Assets/Tests/PlayMode/CoreLoopSceneTests.cs`

**Interfaces:**
- Consumes: the built `CoreLoop.unity` scene (Task 7).

- [ ] **Step 1: Write the failing test**

Add to `Assets/Tests/PlayMode/CoreLoopSceneTests.cs` (same file/class as
every other scene-level test, following the existing
`LoadedCoreLoopScene_...` naming and `FindButton`/`FindChildByName`
helpers):

```csharp
        [UnityTest]
        public IEnumerator LoadedCoreLoopScene_EstateShopsTab_ShowsAllThreeShopsLocked()
        {
            yield return SceneManager.LoadSceneAsync("CoreLoop");
            yield return null;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Canvas not found in the loaded CoreLoop scene.");

            Button estateButton = FindButton(canvas, "EstateButton");
            Assert.IsNotNull(estateButton, "EstateButton not found in the loaded CoreLoop scene.");
            estateButton.onClick.Invoke();

            Button shopsTabButton = FindButton(canvas, "ShopsTabButton");
            Assert.IsNotNull(shopsTabButton, "ShopsTabButton not found in the loaded CoreLoop scene.");
            shopsTabButton.onClick.Invoke();

            GameObject shopsTabRoot = FindChildByName(canvas.transform, "ShopsTabRoot");
            Assert.IsNotNull(shopsTabRoot, "ShopsTabRoot not found in the loaded CoreLoop scene.");
            Assert.IsTrue(shopsTabRoot.activeSelf, "Expected ShopsTabRoot to become active after ShopsTabButton is clicked.");

            GameObject landTabRoot = FindChildByName(canvas.transform, "LandTabRoot");
            Assert.IsNotNull(landTabRoot, "LandTabRoot not found in the loaded CoreLoop scene.");
            Assert.IsFalse(landTabRoot.activeSelf, "Expected LandTabRoot to become inactive after switching to the Shops tab.");

            string[] shopRowNames = { "ShopRow_bakery", "ShopRow_kitchen", "ShopRow_pie_house" };
            foreach (string rowName in shopRowNames)
            {
                GameObject row = FindChildByName(shopsTabRoot.transform, rowName);
                Assert.IsNotNull(row, $"{rowName} not found under ShopsTabRoot.");
            }
        }
```

- [ ] **Step 2: Run test to verify it fails**

Same PlayMode command as Task 6. Expected: FAIL -- `ShopsTabButton`/
`ShopsTabRoot`/`LandTabRoot`/shop row names not found (this test runs
against the committed scene, so it fails until Task 7's `Build` step has
actually run and been committed; if Task 7 is already done, this should
instead PASS immediately -- if so, skip to Step 4).

- [ ] **Step 3: N/A**

No implementation step -- Task 7 already built the scene. This task
only adds the regression test proving it stays correct on future scene
rebuilds.

- [ ] **Step 4: Run test to verify it passes**

Same command. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Tests/PlayMode/CoreLoopSceneTests.cs
git commit -m "test: add scene-level regression test for Estate Shops tab"
```

---

## Final Verification (after all 8 tasks)

Run the full suite once more:

```
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform EditMode -testResults <path>.xml -logFile <path>.log
"/c/Users/rajes/UnityEditors/6000.3.23f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\rajes\understudy-kingdom" -runTests -testPlatform PlayMode -testResults <path>.xml -logFile <path>.log
```

Expected: 100% pass, both platforms. Then follow
`understudy-kingdom:finishing-a-development-branch` to merge.

**Given Phase 1's final review found a Critical bug (an off-canvas
button) that every task-level review missed**, the final whole-branch
review for this plan should specifically hand-verify: (1) the two new
tab buttons' and all inventory-row/shop-row positions actually fall
within the Estate panel's own bounds (panel is 700x800, centered at
origin -- so local x range [-350,350], y range [-400,400]), not just
trust the placement math in this plan's own text; (2) the Land/Shops tab
switcher actually hides/shows the right content (no leftover visible
plot-grid or seed-picker elements bleeding through when the Shops tab is
active, or vice versa).

## Explicitly Out of Scope for This Plan

- `carrot_mature.png`/`pumpkin_*.png` crop art, and any product art
  (`bread.png` etc.) -- code references paths where relevant; actual art
  generation is a separate follow-up pass, same split as every prior
  milestone. (Note: this plan does not add any new sprite-loading code
  for shops/products -- shop rows and inventory rows are text+color only
  this pass, no icons, to keep this plan's scope to the economy logic
  itself.)
- Phase 2 (Animals & Breeding), Phase 4 (Trade + recruitable Shop
  Seller), Phase 5 (Integration with Council/Customize).
- Multi-step production chains, more than 3 shops/products, automated
  selling.
- Any APK rebuild/on-device verification -- follow the same
  build-and-visually-verify process already established for Phase 1,
  as its own follow-up step once this plan's code is merged.
