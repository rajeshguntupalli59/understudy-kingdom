# Design: Button Icons

**Date:** 2026-09-07 | **Status:** Approved, pending implementation plan

## Purpose

Fourth and final increment of the visual-art phase (see `docs/PROJECT_PLAN.md`'s
roadmap discussion; the first three were milestone #12's ruler portrait,
#13's scene backgrounds, #14's panel art). Today every button in the game
is a flat-color rectangle with a text label and nothing else. This gives
each of the 15 buttons a small painted icon next to its existing label,
closing out the visual-art roadmap.

## Scope Decisions

Confirmed interactively before any design work began:

- **All 15 buttons get icons**, not just the 6 primary CoreLoop action-bar
  buttons -- includes secondary/modal buttons (Close x4, Claim Reward,
  Create/Join Council, Skip/Next).
- **Painterly style, matching the existing three increments** -- same
  warm, hand-painted oil-illustration prompt template already used for
  the ruler portrait/backgrounds/panel art, not a clean vector/flat icon
  set. Keeps one consistent visual identity across the whole game.
- **Icon sits left of the existing text label**, not replacing it and not
  becoming the whole button face. Icon-only buttons without a label are a
  known accessibility anti-pattern; a full-button-face icon (like the
  panel-art approach) risks the exact text-legibility problem milestone
  #14 just spent a fix-and-reverify round solving, on 15 buttons instead
  of 3 panels. The 4 "X" close buttons are the one exception -- an X is
  itself a universally understood icon-only convention, so no separate
  text label exists there to preserve.
- **4 close buttons (Events/Council/History/Customize panels) share one
  icon.** They are functionally and visually identical; generating 4
  near-identical paintings would waste generation budget for no visual
  benefit. This brings the total from 15 button slots down to **12
  unique icon images**.
- **Icons are wired through their owning controller**, not left as
  scene-only decoration. Each button is already constructed by exactly
  one controller (the one whose `Initialize(...)` receives that button as
  a primary UI element, not a cross-reference for interactable-toggling).
  That controller gains a new `Image` field per icon it owns, so a future
  milestone could reskin icons dynamically without another interface
  change to the scene builder. Nothing dynamic is implemented this pass --
  the sprite is assigned once and never touched again -- this is purely
  about which layer owns the reference.

## Ownership Breakdown

Each of the 15 buttons is owned by exactly one controller (confirmed by
tracing which controller's `Initialize(...)` call in
`CoreLoopSceneBuilder.Build()` receives each button as a primary element):

| Controller | Icons it owns | Count |
|---|---|---|
| `CoreLoopScreenController` | Submit Recommendation | 1 |
| `DuelButtonController` | Challenge a Rival Kingdom | 1 |
| `HistoryPanelController` | View History, its own Close | 2 |
| `CouncilPanelController` | Council, its own Close, Create Council, Join Council | 4 |
| `EventPanelController` | This Week's Event, its own Close, Claim Reward | 3 |
| `CosmeticsPanelController` | Customize, its own Close | 2 |
| `TutorialOverlayController` | Skip, Next | 2 |

Total: 15 `Image` fields across 7 controllers, no cross-controller
duplication (no controller needs an icon reference for a button it
doesn't own, even though several already hold pass-through `Button`
references to other controllers' buttons for
`SetCoreLoopControlsInteractable`-style toggling -- those stay `Button`
references only, unchanged).

## Art Direction

Style: identical painterly, hand-painted anchor as the other three
increments -- warm/muted oil-and-watercolor palette, visible brushwork.
Applied to small, single-subject icon paintings instead of a full scene
or portrait.

Base prompt template (subject swapped per icon):

```
Painterly hand-painted icon, [SUBJECT], centered on a plain dark parchment
background, warm muted oil-painting palette, visible brushstrokes,
storybook illustration style, no text, no border, simple and readable
at small size.
```

| Button(s) | Subject |
|---|---|
| Submit Recommendation | A wax seal being pressed onto a decree |
| Challenge a Rival Kingdom | Crossed swords |
| View History | An open book |
| Council | A round table |
| This Week's Event | A hanging banner |
| Customize | A paintbrush and palette |
| Claim Reward | A treasure chest |
| Create Council | A banner with a rising seal |
| Join Council | An open castle gate |
| Skip (tutorial) | A double forward chevron |
| Next (tutorial) | A single forward chevron |
| Close (shared, all 4 panels) | A broken wax seal / X mark |

Generated square (matching the ruler portrait's precedent of a
fixed-aspect small UI element, not the tall 1024x1792 used for
backgrounds/panels which exists purely to match phone-portrait
full-screen/large-panel proportions -- icons are small and square, no
reason to inherit that aspect). The first icon (Submit Recommendation) is
generated and shown to the user for approval on style/composition before
the other 11, matching the pattern all three prior increments used.

## Approach

**Fixed size, fixed position, no theme variation.** Each icon is a 32x32
child `Image` on the left edge of its button, 8px inset from the button's
left border, with the existing text label's `RectTransform` shifted right
by 40px (32px icon + 8px gap) and narrowed by the same amount so the
label doesn't overlap or get pushed off the button. The 4 "X" close
buttons are the exception: no text label exists to shift, so the icon is
centered in the button (not left-inset) and simply replaces the existing
"X" `TextMeshProUGUI` -- the close button's function is already
communicated by an icon-only convention players recognize everywhere.

**Data: one `Sprite` per icon, no array.** Unlike the themed art features
(portrait, backgrounds, panel art), icons don't vary by theme or any
other runtime state -- a plain `Sprite` field per icon, not a `Sprite[3]`
indexed by `Themes`. No `GetBackgroundSprite`-style fallback lookup is
needed since there's only ever one value to assign.

**Where it lives.** Each owning controller (see Ownership Breakdown)
gains one new `[SerializeField] private Image` field per icon it owns,
added as trailing `Initialize(...)` parameters (this project's
established DI convention), assigned in `Initialize` and never touched
again -- no `ApplyTheme`-style method exists on most of these controllers
in the first place, and this pass doesn't add one. `CoreLoopSceneBuilder.Build()`
creates one new icon `Image` child GameObject per button (parented to
that button, `raycastTarget = false` so it can't intercept clicks meant
for the button itself), assigns the loaded sprite, and passes the `Image`
reference into that button's owning controller's `Initialize(...)` call.

**Loader.** A new `LoadIconSprite(string path)` helper (same
idempotent-texture-import-type-fix shape as every other sprite loader in
`CoreLoopSceneBuilder.cs`) loads a single icon from
`Assets/Art/ButtonIcons/<icon-name>.png`. No array/multi-file loader is
needed since icons aren't theme-keyed -- this is a deliberately simpler
shape than `LoadThemedSprites`, matching how `LoadPortraitSprite` already
coexists as its own shape rather than being folded into the themed
loader.

## Error Handling

A failed/missing icon load (`LoadIconSprite` returns null) logs
`Debug.LogError` (matching every other loader in this file) and leaves
the icon `Image`'s `sprite` null -- the icon slot renders as an empty/
invisible square rather than crashing, since `Image` with a null sprite
simply doesn't render anything. No fallback-to-a-different-icon logic is
meaningful here (unlike the themed art's fallback-to-Default, there's no
sensible "default icon" to substitute for a missing Challenge-sword icon).

## Testing

**EditMode/PlayMode:** each owning controller's existing test file gets
one new assertion per icon it owns: `Assert.AreSame(expectedIconSprite,
iconImage.sprite)` after `Initialize(...)`, mirroring how the themed art
features extended their own controllers' existing tests rather than
adding parallel test files.

**Verify() and regression test.** `CoreLoopSceneBuilder.Verify()` gains a
non-null check for each of the 15 icon `Image` references (via the same
reflection pattern used for `sceneBackgroundImage`) -- since icons have no
"intentionally missing" slot the way `event_event.png` did, a full
non-null check (not just presence/length) is correct here. One new
PlayMode regression test loads the real scene and confirms all 15 icon
`Image`s have non-null sprites, mirroring the existing
`LoadedCoreLoopScene_SceneBackground_HasNonNullSpriteOnLoad`-style tests.

## Explicitly Out of Scope for This Pass

- Any dynamic/theme-based icon variation -- the controller-level wiring
  exists so a future milestone *could* add this without another interface
  change, but no theming logic is implemented now.
- Icon animation/transition on press -- instant, matching how this
  project's buttons have never had press animation.
- A shared icon-size design token/constant beyond the fixed 32x32 used
  here -- if a future feature needs a different icon size, that's a
  new decision then, not solved speculatively now.
