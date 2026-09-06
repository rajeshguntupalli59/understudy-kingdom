# Design: Themed Scene Backgrounds

**Date:** 2026-09-06 | **Status:** Approved, pending implementation plan

## Purpose

Second increment of the visual-art phase (see `docs/PROJECT_PLAN.md`'s
roadmap discussion; the first was milestone #12's ruler portrait). Today
the CoreLoop screen has no deliberate background at all -- what players
see behind the sliders/buttons is Unity's default procedural skybox
gradient, rendered by the scene's Camera because nothing was ever set.
This replaces that with a painted throne-room backdrop, and -- per the
scope decision below -- ties it to the theme system milestone #11
already shipped, so unlocking and switching themes changes the whole
scene's mood, not just the three panel interiors.

## Scope Decisions

Confirmed interactively before any design work began:

- **Three backgrounds, one per existing Cosmetics theme** (Default,
  Council Chamber, Harvest Hall), not one fixed background. The user's
  explicit choice over the simpler single-background alternative --
  more art to produce, but it makes theme-switching change the whole
  scene rather than just the panel interiors it already recolors.
- **Composition: grand throne-room interior**, not a castle-exterior
  vista or a war-room map table. Matches the "royal advisor in court"
  framing directly and gives each theme a natural interior redecoration
  (different banners/lighting/finish) rather than three unrelated scenes.
- **Extends `CosmeticsPanelController`**, the existing single owner of
  theme-switching, rather than a new controller class -- exactly the
  same reasoning milestone #12 used for extending
  `CoreLoopScreenController` instead of adding a new portrait
  controller.

## Art Direction

Style: same painterly, hand-painted medieval fantasy anchor as the ruler
portrait (milestone #12) -- warm/muted oil-and-watercolor palette,
visible brushwork -- applied to interior architecture instead of a
character. One version per theme, using each theme's already-shipped
color identity (`CosmeticsPanelController.Themes`) as the lighting/palette
target rather than inventing new colors:

| Theme Id | Display Name | Existing panel color (RGB, 0-1) | Background direction |
|---|---|---|---|
| `Default` | Default | (0.10, 0.10, 0.15) navy | Modest stone throne room, cool blue-grey torchlight |
| `Council` | Council Chamber | (0.22, 0.08, 0.16) burgundy | Ornate council chamber, deep red drapery, candlelit |
| `Event` | Harvest Hall | (0.16, 0.13, 0.04) warm brown/gold | Warm harvest hall, amber light, autumn banners |

Base prompt template (mood/lighting descriptor swapped per theme):

```
Painterly hand-painted interior, grand medieval throne room, wide
architectural view, warm muted oil-painting palette, visible
brushstrokes, storybook illustration style, empty of people, [THEME
LIGHTING/PALETTE DESCRIPTOR].
```

Generated at a tall portrait aspect (~1024x2048, matching this project's
mobile-portrait screen) and stretched to fill rather than
aspect-preserved -- standard practice for a full-screen background,
avoids letterboxing, and unlike the portrait's headshot framing, minor
stretch of a wide architectural scene is not visually objectionable.

The Default theme's background is generated first and shown to the user
for approval before the other two, so the base architecture/style is
locked before palette variants are produced from it.

## Approach

**Data: theme id -> background sprite.** Reuses the existing `Themes`
array (`CosmeticsPanelController.cs:31-54`) as the lookup key -- no new
tier-mapping scheme needed, since themes are already identified by a
string id with a fixed, small set. A parallel `Sprite[] backgroundSprites`
(3 elements, index-aligned to `Themes`) is looked up the same way
`GetThemeColor` already looks up `PanelColor`:

```csharp
private static Sprite GetBackgroundSprite(string themeId, Sprite[] sprites)
{
    for (int i = 0; i < Themes.Length; i++)
    {
        if (Themes[i].Id == themeId)
        {
            return sprites[i] != null ? sprites[i] : sprites[0];
        }
    }
    return sprites[0];
}
```

**Where it lives.** `CosmeticsPanelController` gains two new fields:
`Image sceneBackgroundImage` and `Sprite[] backgroundSprites`, added as
trailing `Initialize(...)` parameters (this project's established
DI convention). `ApplyTheme(themeId)` -- already the single place that
recolors the three panel images -- gains one more line:

```csharp
private void ApplyTheme(string themeId)
{
    Color color = GetThemeColor(themeId);
    eventPanelImage.color = color;
    councilPanelImage.color = color;
    historyPanelImage.color = color;
    sceneBackgroundImage.sprite = GetBackgroundSprite(themeId, backgroundSprites);
}
```

No other method changes -- `ApplyTheme` is already called from `Bind()`
on scene load (re-applying the saved theme) and from `OnApplyTheme`
(when the player picks a new one), so both paths pick up the background
automatically.

**Scene wiring.** `CoreLoopSceneBuilder.Build()` creates one new
full-screen `Image` GameObject as the *first* child under `canvasObject`
(Unity renders uGUI siblings in child order, so being first means
everything else draws on top of it) with a stretch-anchored
`RectTransform` (`anchorMin = Vector2.zero`, `anchorMax = Vector2.one`,
`offsetMin`/`offsetMax = Vector2.zero`) and `preserveAspect = false`. A
new `LoadBackgroundSprites()` helper, matching `LoadRulerPortraits()`'s
shape exactly (including the same texture-import-type fix, since these
too will be added as raw files), loads the 3 sprites from
`Assets/Art/Backgrounds/background_<themeid-lowercase>.png` in the same
order as `Themes`.

## Error Handling

Same fallback shape as `GetThemeColor` and milestone #12's portrait
fallback: a missing/null background sprite for the selected theme falls
back to index 0 (Default) rather than leaving the background blank.

## Testing

**PlayMode:** extend the existing `CosmeticsPanelControllerTests.cs`
(which already asserts panel colors after `OnApplyTheme`/`Initialize`)
with assertions that `sceneBackgroundImage.sprite` matches the expected
theme's sprite in the same test cases that currently check
`eventPanelImage.color` etc. -- mirrors milestone #12's approach of
extending an existing test file's existing test cases rather than adding
a parallel set.

## Explicitly Out of Scope for This Pass

- Button icons and panel-interior art (separate later phases in the
  visual-art roadmap).
- Any animation/transition on background swap -- instant, matching how
  theme color swaps already are.
- Locked-theme backgrounds are unreachable the same way locked panel
  colors already are (a locked theme can't be selected via
  `OnApplyTheme`'s existing `IsUnlocked` guard) -- no new lock-state
  handling needed.
