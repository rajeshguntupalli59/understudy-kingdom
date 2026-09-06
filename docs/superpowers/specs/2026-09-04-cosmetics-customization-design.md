# Design: Cosmetics Customization (Milestone #11)

**Date:** 2026-09-04 | **Status:** Approved, pending implementation plan

## Purpose

`docs/PROJECT_PLAN.md` lists FR-12 as the last unbuilt cosmetic/collection
requirement: "The user can customize their court/advisory chamber with
unlockable non-gameplay-affecting cosmetics." This milestone builds a
scoped-down slice of that — confirmed interactively before any design work
began — because the game currently has **zero visual art assets of any
kind**: every existing screen (`Assets/Editor/CoreLoopSceneBuilder.cs`) is
built entirely from plain Unity `Image`/`TextMeshProUGUI`/`Slider`
primitives, with no sprites, textures, or 3D content anywhere in the repo
(`Assets/Art/` exists but contains only a placeholder `.gitkeep`). This
session has no way to originate new art. "Customizing the chamber" is
therefore scoped to what's actually buildable: a color theme applied to
the game's existing persistent visual surfaces (the modal panel
backgrounds), not new artwork.

## Scope Decisions

Confirmed interactively:

- **Color themes only, no new art.** The three existing modal panel
  backgrounds (History/Council/Events, all currently the identical navy
  `Color(0.1, 0.1, 0.15, 0.95)`) are the closest thing this game has to a
  persistent "chamber" — they're the surfaces the player returns to every
  session. A theme recolors all three together for a cohesive look.
  Individual action buttons (Submit, Challenge, History, Council, Events)
  keep their current distinct functional colors — those are navigation
  cues, not decor, and recoloring them would hurt usability.
- **Two unlockable themes, tied to existing `RulerState` milestone flags,
  no currency system.** This project has twice already (FR-11, and
  before that) deliberately avoided introducing a currency/points system
  where a simpler composition of existing signals would do. `RulerState`
  already tracks `CouncilRewardApplied` and a non-empty
  `ClaimedEventWeekId` — both real signs of engagement with an existing
  system. A **Council theme** unlocks via the former, an **Event theme**
  via the latter. `TutorialCompleted` is deliberately NOT used as an
  unlock gate — it flips almost immediately for every player (even
  skipping the tutorial sets it), so gating a cosmetic behind it
  wouldn't feel like an unlock. A **Default theme** (the current navy) is
  always available. Rejected alternative: decision-cycle-count
  thresholds (e.g. 10/25/50 decisions) — would need a new public getter
  on `DecisionCycleManager`, and decouples unlocks from actually engaging
  with Council/Events, letting a player unlock everything by pure
  grinding without ever touching either system.

## Approach

### New component: `Assets/Scripts/UI/CosmeticsPanelController.cs`

A 6th modal panel, following the exact `Initialize()`-args
dependency-injection pattern every prior panel controller
(History/Council/Tutorial/Events) uses. Unlike Events, this panel makes
**zero network calls** — everything it needs (`CouncilRewardApplied`,
`ClaimedEventWeekId`, the currently-selected theme) already lives in
`manager.Ruler.State`, read synchronously on open. A `customizeButton`
trigger opens the panel showing all 3 themes: unlocked ones are
selectable (tap to apply immediately, no confirmation), locked ones show
which flag unlocks them (e.g. "Unlocks after your council reaches its
milestone").

Applying a theme:
1. Sets the `Image.color` on all three panel-background `GameObject`s
   (History/Council/Events — this controller holds references to all
   three, passed in via `Initialize()`) to the theme's color.
2. Sets `manager.Ruler.State.SelectedTheme` to the theme's id.
3. `SaveService.Save(manager.Ruler.State)`.

Once at scene `Start()` (so a relaunch shows the previously-selected
theme immediately rather than the default), the currently-saved
`SelectedTheme` is re-applied to all three panels — this is the only
place theme application happens outside an explicit tap (opening the
panel itself only renders the picker's locked/unlocked state, it doesn't
re-apply the theme, since the theme can only ever change via an explicit
tap and is already applied the moment it does), and it runs
unconditionally (a `SelectedTheme` a player is no longer eligible
for, e.g. an edited save file, silently falls back to Default rather than
erroring — the client trusts its own local flags, matching this
project's client-authoritative design for every other reward in this
game).

### Theme definitions

A small hardcoded array of 3 `(id, displayName, color, unlockDescription)`
tuples in `CosmeticsPanelController.cs` itself (no server involvement at
all, so no reason for this to live anywhere else):
- `Default`, always unlocked, `Color(0.1, 0.1, 0.15, 0.95)` (today's
  existing panel color — unchanged for anyone who never opens this panel).
- `Council`, unlocked via `CouncilRewardApplied`, a distinct accent color.
- `Event`, unlocked via `ClaimedEventWeekId != ""`, a distinct accent
  color.

### `RulerState` / persistence

One new field, following the established pattern exactly:
`public string SelectedTheme = "Default";` (never null, same rationale as
`ClaimedEventWeekId`), threaded through `RulerSaveData`/`SaveService`
identically to the three prior additions (including the `?? "Default"`
load-time guard for save files predating this milestone).

### Modal mutual exclusion

`CosmeticsPanelController` joins the existing shared-control-disable web
exactly the way Events joined it in milestone #10: it disables
`historyButton`/`councilButton`/`eventsButton`/`submitButton`/
`challengeButton`/3 sliders while open, and gains a `customizeButton`
field itself. Symmetrically, `HistoryPanelController`, `CouncilPanelController`,
`TutorialOverlayController`, and `EventPanelController` all gain a new
trailing `customizeButton` `Initialize()` parameter and add it to their
own disable sets — mirroring exactly how `eventsButton` was threaded into
all four in milestone #10's Task 8. Like `EventPanelController`, this new
panel is **not** `DuelModalGate`-aware this pass (that fix still lives on
the unmerged `feat/duel-modal-gate` branch) — recorded as the same kind of
known, accepted gap.

### `CoreLoopSceneBuilder.cs`

Adds the `CustomizeButton` (24pt label, 44pt tall, next in the button
column at 60px below Events), the `CosmeticsPanel` GameObject tree
(mirrors the Events/Council panel's 700×800 construction, with 3 theme
rows each showing name + lock-state + an apply button), and wires
`CosmeticsPanelController.Initialize(...)` with references to the 3
existing panel-background `Image` components plus the shared controls.

## Data Flow

```
Scene loads -> CosmeticsPanelController.Start(): re-applies the saved
  SelectedTheme to all 3 panel backgrounds (no-op if still "Default")

Player taps Customize
  -> SetCoreLoopControlsInteractable(false), panel opens
  -> for each of the 3 themes: read the relevant RulerState flag
     synchronously, render as unlocked/locked
  -> player taps an unlocked theme's Apply button:
     recolor all 3 panel backgrounds, SelectedTheme = themeId,
     SaveService.Save, (no reward/mood/loyalty change of any kind)
  -> player closes -> SetCoreLoopControlsInteractable(true)
```

## Error Handling

None of this feature makes a network call — it's pure local state and
UI recoloring. The only failure mode is `SaveService.Save` itself
failing, an existing, unchanged, already-accepted risk shared by every
other reward-applying panel in this game.

## Testing

**EditMode:** `RulerState`/`SaveService` round-trip tests for
`SelectedTheme` (mirroring the 3 prior fields' test shape exactly,
including the "save file predates this field, defaults to Default, never
null" case).

**PlayMode (deterministic, zero network):** `CosmeticsPanelControllerTests.cs`
constructed the same way `EventPanelControllerTests.cs` is (no real
coordinator needed at all here, since this panel never touches the
network) — covering: locked theme's apply action is a no-op /
non-interactable when its unlock flag is false; unlocked theme applies
and recolors all 3 panel-background `Image` components; selecting a
theme leaves `Mood`/`Loyalty`/`Agenda` completely unchanged (the
non-gameplay-affecting guarantee, asserted explicitly); the previously
selected theme is re-applied on a fresh `Start()` (simulating a
relaunch). A new scene smoke test
(`LoadedCoreLoopScene_CustomizeButton_OpensPanelWithoutThrowing`) mirrors
the Events one from milestone #10.

## Explicitly Out of Scope for This Pass

- Any new art assets, sprites, or visual redesign beyond solid-color
  panel backgrounds.
- Recoloring the individual action buttons (Submit/Challenge/History/
  Council/Events/Customize) — those stay as functional navigation cues.
- Any currency/points system, or any cosmetic tied to real-money spend.
- More than 3 themes, or an "editor" for custom colors.
- Threading `DuelModalGate` into `CosmeticsPanelController` (blocked on
  milestone #9 merging first — same tracked follow-up class as
  `EventPanelController`'s).
