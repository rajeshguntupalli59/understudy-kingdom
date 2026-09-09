# Design: Extract Estate into a Standalone Project ("Homestead")

## Purpose

The user has decided to abandon further work on Understudy Kingdom's
ruler/advisor systems (Council, PvP Duels, Ruler AI, Decision History,
backend auth service -- ~18 shipped milestones) and make the Estate
farming economy (Land/Crops, Shops/Production, Crop Animation) the
standalone product going forward, under a new name: **Homestead**. This
spec covers extracting Estate's current code into a fresh, independent
Unity project. It does NOT cover the paused Shops-on-Land design (that
resumes inside Homestead once this extraction is verified working) or
any new gameplay -- this is a lift-and-clean port of what already exists
and is already tested.

## Scope Decisions

- **Fresh Unity project, not a clone-and-strip.** A brand-new empty Unity
  project at `C:\Users\rajes\Homestead`, hand-carrying only the
  Estate-specific files. Chosen over cloning `understudy-kingdom` and
  deleting the rest, to avoid inheriting now-irrelevant config, docs, and
  dead code paths that would otherwise need a separate cleanup pass.
- **Project name: Homestead.** New private GitHub repo,
  `rajeshguntupalli59/Homestead` -- created but not pushed to until
  explicitly requested, matching this user's established convention.
- **Same Unity version** (`6000.3.23f1`, already installed) -- no new
  Editor install, no version-compatibility risk in ported code.
- **`understudy-kingdom` is left completely untouched.** No files
  deleted, nothing removed from its GitHub repo during this work. Once
  Homestead is verified working end-to-end, the user will be asked
  explicitly, one more time, before the `understudy-kingdom` GitHub repo
  is archived (read-only) -- that action is out of this spec's own scope
  and happens as a separate, explicitly-confirmed step afterward.
- **`EstatePanelController` and the scene-building code are cleaned up
  during the port, not copy-pasted verbatim.** Estate currently reaches
  into non-Estate systems it doesn't need once it's the whole game:
  `Initialize(...)`'s `armySlider/tradeSlider/religionSlider/
  submitButton/challengeButton/viewHistoryButton/councilButton/
  eventsButton/customizeButton` parameters and the `DuelModalGate gate`
  parameter exist only because Estate used to be one panel among several
  in a shared scene; `SetCoreLoopControlsInteractable` exists only to
  disable/enable those *other* panels' entry buttons while Estate is
  open. None of that has a reason to exist once Estate is the only thing
  in the scene -- all of it is dropped, not ported.
- **`SaveService` becomes Estate-only.** The new project's `SaveService`
  keeps only `SaveEstate`/`LoadEstate`/`EstateSavePath` -- the entire
  `RulerState`/`RulerSaveData` save path is dropped, since that concept
  doesn't exist in Homestead at all.
- **`CoreLoopSceneBuilder.cs` is rewritten as a new, smaller
  `HomesteadSceneBuilder.cs`**, not ported -- it only ever needs to build
  the Estate content, sized as a full-screen root (Estate is now the
  entire game, not a modal panel opened by a button). Reuses the same
  generic helper patterns (`CreateLabel`, `LoadIconSprite`, directory-
  guarded art loading) since those have no Estate-vs-ruler coupling.
- **Art:** `Assets/Art/Crops/` and `Assets/Art/Estate/` copy over
  directly. No ruler-portrait, background, panel-art, or button-icon art
  is ported -- none of it is used by Estate.
- **TMP Essential Resources must be imported explicitly during setup**,
  verified by a smoke test that a TMP label actually renders text --
  this project's own history has a real recorded incident (Understudy
  Kingdom Milestone #1's final review) of a fresh scene shipping with
  blank TMP labels because this step was skipped. Not repeating that.

## New Project Setup

1. Create via Unity's project-creation flow (2D Mobile template,
   matching how `understudy-kingdom` itself was set up) at
   `C:\Users\rajes\Homestead`.
2. `git init`, first commit of the empty-but-buildable project before any
   ported code lands, so history starts clean.
3. Import TextMesh Pro Essential Resources; verify with a smoke test.
4. Folder structure: `Assets/Scripts/Core/`, `Assets/Scripts/UI/`,
   `Assets/Editor/`, `Assets/Art/`, `Assets/Tests/EditMode/`,
   `Assets/Tests/PlayMode/`, `Assets/Scenes/Homestead.unity` (not
   `CoreLoop.unity` -- that name only made sense next to the ruler/
   advisor "core loop" it no longer exists alongside).
5. Unity Test Framework package added, EditMode/PlayMode assemblies
   configured the same known-working way as `understudy-kingdom`.

## File-by-File Migration

**Ported verbatim (zero ruler/advisor dependency already):**
`CropCatalog.cs`, `ShopCatalog.cs`, `ProductCatalog.cs`,
`GoodsCatalog.cs`, `EstateState.cs`, `EstateSaveData.cs`, and their
EditMode tests (`ProductCatalogTests.cs`, `ShopCatalogTests.cs`,
`GoodsCatalogTests.cs`, the `EstateState`-related tests in
`EstateStateTests.cs`).

**New, trimmed `SaveService.cs`:** only `SaveEstate`, `LoadEstate`,
`EstateSavePath`, and the per-field `Inventory`/`Shops` migration logic
already established -- no `RulerState` methods/fields at all.
`SaveServiceEstateTests.cs` ports over (adjusted only if any test
depended on `SaveService`'s now-removed Ruler-side methods, none should).

**`EstatePanelController.cs`:** ported with the `Initialize(...)`
cleanup from Scope Decisions -- final parameter list keeps only what
Estate logic actually reads: `panelRoot, coinsLabel, plotViews,
seedPickerRoot, seedButtons, seedCostLabels, cropStageSprites,
landTabButton, shopsTabButton, landTabRoot, shopsTabRoot, inventoryRows,
shopRows, seedSprite, waterDropletSprite`. `estateButton`/`closeButton`
are dropped -- Estate is the whole game now, not a panel opened/closed
by buttons over other content, so `OnEstateButtonClicked`/`OnClose`
collapse into simple `Start()`-time initialization (load state, refresh
UI) with no open/close lifecycle. `SetCoreLoopControlsInteractable` and
the `gate` parameter/field are deleted outright.
`EstatePanelControllerTests.cs` ports over, `SetUp` adjusted to match
the trimmed constructor and the collapsed open/close lifecycle.

**New `HomesteadSceneBuilder.cs`** (replaces `CoreLoopSceneBuilder.cs`):
a fresh Editor script, only the Estate-panel-building code (8-plot grid,
seed picker, Land/Shops tabs, inventory strip, shop rows, progress bars),
full-screen sized rather than a 700x800 modal. `Build()`/`Verify()` both
call `EditorApplication.Exit()` on their success/failure paths from day
one (this project's own hard-won lesson, ported forward instead of
re-learned).

**Art:** `Assets/Art/Crops/*.png` (+ `.meta`), `Assets/Art/Estate/*.png`
(+ `.meta`) copy over directly, already complete or near-complete
(`carrot_mature.png`/`pumpkin_*.png` remain the same known gap as before,
unaffected by this move).

**New `HomesteadSceneTests.cs`** (replaces `CoreLoopSceneTests.cs`):
ports only the Estate-relevant scene-level regression assertions (plot
grid, tabs, progress bars, shop rows) -- drops every non-Estate check.

## Data Flow

No change to Estate's own internal data flow (plant/water/grow/harvest,
shop unlock/start/collect, save/load) -- this is a structural move, not a
behavior change. The only real flow change: `EstatePanelController` no
longer has an open/closed modal state (`panelRoot.SetActive`/gate calls)
-- it initializes once at scene load and is simply always the visible
content, since there's nothing else in the scene to show or hide it for.

## Testing

Full EditMode + PlayMode suite ported and green in the new project,
using the same batch-mode Unity CLI pattern already established in this
session (`-runTests`, never combined with `-quit`). Expected counts will
be lower than `understudy-kingdom`'s current totals since only
Estate-relevant tests port over -- exact numbers determined during
implementation (count what actually ports, don't guess in advance).
`Build()`/`Verify()` rebuild-and-check pattern verified working from the
first scene build. One manual Play Mode check (open the game, plant/
water/harvest a crop, unlock/use a shop, confirm the UI renders as a
full-screen game with no dead modal-panel space) before considering the
port complete.

## Explicitly Out of Scope for This Extraction

- Any new gameplay feature (Shops-on-Land, land expansion, construction
  animation, animals) -- resumes as its own brainstorm inside Homestead
  once this extraction is verified working.
- Archiving the `understudy-kingdom` GitHub repo -- a separate,
  explicitly-confirmed action after this extraction is verified, not
  part of this plan's own tasks.
- Any change to `understudy-kingdom` itself -- it is not touched,
  modified, or deleted by this work.
- Pushing the new `Homestead` repo to GitHub -- created, not pushed,
  until explicitly requested.
- Any ruler/advisor/duel/council/backend code -- none of it is examined,
  modified, or referenced by this extraction.
