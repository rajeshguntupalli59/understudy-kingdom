# Design: Button Icons

**Date:** 2026-09-07 | **Status:** Approved, reduced scope, pending implementation plan

## Purpose

Fourth and final increment of the visual-art phase (see `docs/PROJECT_PLAN.md`'s
roadmap discussion; the first three were milestone #12's ruler portrait,
#13's scene backgrounds, #14's panel art). Today every button in the game
is a flat-color rectangle with a text label and nothing else. This gives
each of the 15 buttons a small painted icon next to its existing label,
closing out the visual-art roadmap.

## Scope Decisions

Confirmed interactively before any design work began:

- **All 15 buttons were meant to get icons**, not just the 6 primary
  CoreLoop action-bar buttons -- includes secondary/modal buttons (Close
  x4, Claim Reward, Create/Join Council, Skip/Next). **Reduced after art
  generation began** (see "Reduced Scope" below): only 4 of the 12 unique
  icons were generated before Hugging Face's monthly credit cap was hit
  again -- Submit Recommendation, Challenge a Rival Kingdom, View
  History, and Council. This pass implements icons for those 4 buttons
  only. The remaining 9 button slots (Events, Customize, Claim, Create,
  Join, Skip, Next, and the 4 shared-Close buttons) keep their current
  text-only appearance until more generation credit is available -- no
  code changes needed to add them later, just generating the art and
  wiring it the same way this pass wires the first 4.
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
`CoreLoopSceneBuilder.Build()` receives each button as a primary element).
**This pass only implements the 4 rows marked (this pass)** -- see Scope
Decisions above for why; the rest is the full original design, kept here
as the reference for whoever picks up the deferred 9 later.

| Controller | Icons it owns | Count | This pass? |
|---|---|---|---|
| `CoreLoopScreenController` | Submit Recommendation | 1 | Yes |
| `DuelButtonController` | Challenge a Rival Kingdom | 1 | Yes |
| `HistoryPanelController` | View History, its own Close | 2 | View History only -- its Close icon is deferred (shared Close art not generated) |
| `CouncilPanelController` | Council, its own Close, Create Council, Join Council | 4 | Council only -- Close/Create/Join deferred |
| `EventPanelController` | This Week's Event, its own Close, Claim Reward | 3 | Deferred entirely |
| `CosmeticsPanelController` | Customize, its own Close | 2 | Deferred entirely |
| `TutorialOverlayController` | Skip, Next | 2 | Deferred entirely |

This pass: 4 `Image` fields across 4 controllers
(`CoreLoopScreenController`, `DuelButtonController`,
`HistoryPanelController`, `CouncilPanelController` -- each gaining
exactly one new icon field, not the 2/4 they'll eventually own). No
cross-controller duplication (no controller needs an icon reference for
a button it doesn't own, even though several already hold pass-through
`Button` references to other controllers' buttons for
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

| Button(s) | Subject | Generated? |
|---|---|---|
| Submit Recommendation | A wax seal being pressed onto a decree | Yes -- `submit.png` |
| Challenge a Rival Kingdom | Crossed swords | Yes -- `challenge.png` |
| View History | An open book | Yes -- `history.png` |
| Council | A round table | Yes -- `council.png` |
| This Week's Event | A hanging banner | Deferred -- HF credit cap hit |
| Customize | A paintbrush and palette | Deferred -- HF credit cap hit |
| Claim Reward | A treasure chest | Deferred -- HF credit cap hit |
| Create Council | A banner with a rising seal | Deferred -- HF credit cap hit |
| Join Council | An open castle gate | Deferred -- HF credit cap hit |
| Skip (tutorial) | A double forward chevron | Deferred -- HF credit cap hit |
| Next (tutorial) | A single forward chevron | Deferred -- HF credit cap hit |
| Close (shared, all 4 panels) | A broken wax seal / X mark | Deferred -- HF credit cap hit |

Generated square (matching the ruler portrait's precedent of a
fixed-aspect small UI element, not the tall 1024x1792 used for
backgrounds/panels which exists purely to match phone-portrait
full-screen/large-panel proportions -- icons are small and square, no
reason to inherit that aspect). The first icon (Submit Recommendation) was
generated and shown to the user for approval on style/composition before
the other 11 were attempted, matching the pattern all three prior
increments used. Only 3 of the remaining 11 (Challenge, History, Council)
generated before the credit cap was hit -- see Scope Decisions.

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
loader. This pass loads exactly 4 files:
`Assets/Art/ButtonIcons/submit.png`,
`Assets/Art/ButtonIcons/challenge.png`,
`Assets/Art/ButtonIcons/history.png`,
`Assets/Art/ButtonIcons/council.png`.

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
one new assertion per icon it owns this pass (4 total, one each in
`CoreLoopScreenControllerTests`, `DuelButtonControllerTests`,
`HistoryPanelControllerTests`, `CouncilPanelControllerTests`):
`Assert.AreSame(expectedIconSprite, iconImage.sprite)` after
`Initialize(...)`, mirroring how the themed art features extended their
own controllers' existing tests rather than adding parallel test files.

**Verify() and regression test.** `CoreLoopSceneBuilder.Verify()` gains a
non-null check for each of the 4 icon `Image` references this pass adds
(via the same reflection pattern used for `sceneBackgroundImage`) -- since
icons have no "intentionally missing" slot the way `event_event.png` did,
a full non-null check (not just presence/length) is correct here. One new
PlayMode regression test loads the real scene and confirms all 4 icon
`Image`s have non-null sprites, mirroring the existing
`LoadedCoreLoopScene_SceneBackground_HasNonNullSpriteOnLoad`-style tests.

## Explicitly Out of Scope for This Pass

- The other 11 buttons/8 remaining unique icons (Events, Customize,
  Claim, Create, Join, Skip, Next, shared Close) -- HF credit cap hit
  mid-generation, only 4 of 12 unique icons exist. Revisit once free
  art-generation credits are available again; no code-shape changes
  needed to add them, just generating the art and repeating this same
  pass's wiring pattern for each remaining controller/button in the
  Ownership Breakdown table above.
- Any dynamic/theme-based icon variation -- the controller-level wiring
  exists so a future milestone *could* add this without another interface
  change, but no theming logic is implemented now.
- Icon animation/transition on press -- instant, matching how this
  project's buttons have never had press animation.
- A shared icon-size design token/constant beyond the fixed 32x32 used
  here -- if a future feature needs a different icon size, that's a
  new decision then, not solved speculatively now.
