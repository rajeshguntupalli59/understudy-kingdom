# Design: Themed Panel Art

**Date:** 2026-09-06 | **Status:** Approved, pending implementation plan

## Purpose

Third increment of the visual-art phase (see `docs/PROJECT_PLAN.md`'s
roadmap discussion; the first two were milestone #12's ruler portrait
and milestone #13's scene backgrounds). Today the four modal panels
(History, Council, Events, Customize) are flat-colored rectangles --
`CosmeticsPanelController.ApplyTheme` already recolors three of them
(History/Council/Events) to match the selected theme, and the fourth
(Customize itself) is a fixed navy that never changes. This gives each
panel real painted art, per theme, so opening any panel feels like
entering a themed room rather than a colored overlay.

## Scope Decisions

Confirmed interactively before any design work began:

- **All 4 panels, not just the 3 already theme-colored.** Customize
  gains theme-reactivity for the first time (previously fixed navy) --
  the user's explicit choice over leaving it excluded, for visual
  consistency across every panel.
- **Art varies per theme, matching the scene-background precedent** --
  not one fixed image per panel. 4 panels x 3 themes = 12 images, the
  user's explicit choice over the smaller 4-image (one per panel,
  theme-independent) alternative.
- **Extends `CosmeticsPanelController`**, the existing single owner of
  theme-switching, rather than a new controller -- same reasoning
  milestones #12 and #13 both used.
- **Consolidates the sprite-loading helper.** The final review on
  milestone #13 already flagged `LoadBackgroundSprite`/`LoadPortraitSprite`
  as near-duplicate code. Rather than writing a 3rd, 4th, and 5th
  near-copy for four more panels, this plan generalizes into one
  `LoadThemedSprites(folder, fileNamePrefix)` helper and has the
  existing background loader call it too -- a small, justified refactor
  of code this feature is already touching, not unrelated scope creep.

## Art Direction

Style: same painterly, hand-painted medieval fantasy anchor as the
ruler portrait and scene backgrounds. Each panel gets a distinct
architectural/thematic identity, rendered in the same 3 existing theme
palettes (`CosmeticsPanelController.Themes`):

| Panel | Depicts | Default (navy/stone) | Council (burgundy/candlelit) | Event (warm gold/amber) |
|---|---|---|---|---|
| History | An archive/records room -- scrolls, ledgers, shelved records | Cool stone archive | Burgundy-draped records chamber | Warm amber scroll room |
| Council | A round table / heraldry chamber | Stone council room | The same burgundy council chamber style as the scene background | Warm harvest council nook |
| Events | A seasonal banner hall | Cool stone hall with banners | Burgundy banner hall | The same warm Harvest Hall style as the scene background |
| Customize | A tailor's wardrobe / dressing room | Cool stone wardrobe | Burgundy-draped wardrobe | Warm amber wardrobe |

Base prompt template (panel subject and theme descriptor both swapped
per image):

```
Painterly hand-painted interior, [PANEL SUBJECT], wide architectural
view, warm muted oil-painting palette, visible brushstrokes, storybook
illustration style, no people, no human figures, uninhabited empty
room, [THEME LIGHTING/PALETTE DESCRIPTOR].
```

Generated at the same 1024x1792 portrait aspect as the scene
backgrounds (kept identical on purpose, since these render inside
smaller modal panel rects, not full-screen -- a consistent source
aspect avoids re-deriving a different `AspectRatioFitter` ratio per
image set). Given milestone #13's final review found the "no people"
instruction alone wasn't reliable (one image needed 2 regenerations to
actually come out empty), each generation will be visually checked
before being committed, regenerating any panel/theme combination that
comes back with an unwanted figure.

The History/Default combination is generated first and shown to the
user for approval on style/composition before the other 11 are
generated, matching the pattern both prior increments used.

## Approach

**Data: panel x theme -> sprite.** Each panel gets its own `Sprite[3]`
field, index-aligned to the existing `Themes` array exactly like
`backgroundSprites` already is:

```csharp
[SerializeField] private Image cosmeticsPanelImage;
[SerializeField] private Sprite[] historyPanelSprites;
[SerializeField] private Sprite[] councilPanelSprites;
[SerializeField] private Sprite[] eventPanelSprites;
[SerializeField] private Sprite[] cosmeticsPanelSprites;
```

All four reuse the existing `GetBackgroundSprite(themeId, sprites)`
lookup unmodified -- it already takes any 3-element array and the
theme id, with no assumption about which panel it's for.

**Where it lives.** `Initialize(...)` gains five new trailing
parameters (`cosmeticsPanelImage` plus the four sprite arrays), and
`ApplyTheme(themeId)` gains matching lines:

```csharp
private void ApplyTheme(string themeId)
{
    Color color = GetThemeColor(themeId);
    eventPanelImage.color = color;
    councilPanelImage.color = color;
    historyPanelImage.color = color;
    eventPanelImage.sprite = GetBackgroundSprite(themeId, eventPanelSprites);
    councilPanelImage.sprite = GetBackgroundSprite(themeId, councilPanelSprites);
    historyPanelImage.sprite = GetBackgroundSprite(themeId, historyPanelSprites);
    if (cosmeticsPanelImage != null)
    {
        cosmeticsPanelImage.color = color;
        cosmeticsPanelImage.sprite = GetBackgroundSprite(themeId, cosmeticsPanelSprites);
    }
    if (sceneBackgroundImage != null)
    {
        sceneBackgroundImage.sprite = GetBackgroundSprite(themeId, backgroundSprites);
    }
}
```

`cosmeticsPanelImage` gets the same `!= null` guard `sceneBackgroundImage`
already has (a genuinely new field, at risk of the old-scene-deserializes-
null issue both prior milestones hit); `eventPanelImage`/`councilPanelImage`/
`historyPanelImage` do not need the guard -- they are pre-existing fields
that have never been null since this controller was first written.

**Scene wiring.** `CoreLoopSceneBuilder.Build()` passes
`cosmeticsPanelRootObject.GetComponent<Image>()` inline at the
`Initialize(...)` call site, matching exactly how the other three panel
Images (`eventPanelRootObject.GetComponent<Image>()` etc.) are already
passed there today, plus four `LoadThemedSprites(...)` results.

**Loader consolidation.** Replace `LoadBackgroundSprite`/
`LoadBackgroundSprites` with one generic pair:

```csharp
private static Sprite LoadThemedSprite(string path)
{
    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
    if (importer != null && importer.textureType != TextureImporterType.Sprite)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.SaveAndReimport();
    }
    return AssetDatabase.LoadAssetAtPath<Sprite>(path);
}

private static Sprite[] LoadThemedSprites(string folder, string fileNamePrefix)
{
    string[] themeIds = { "default", "council", "event" };
    var sprites = new Sprite[3];
    for (int i = 0; i < themeIds.Length; i++)
    {
        sprites[i] = LoadThemedSprite($"{folder}/{fileNamePrefix}_{themeIds[i]}.png");
    }
    for (int i = 0; i < sprites.Length; i++)
    {
        if (sprites[i] == null)
        {
            Debug.LogError($"CoreLoopSceneBuilder.LoadThemedSprites: failed to load sprite at index {i} for {fileNamePrefix} in {folder}");
        }
    }
    return sprites;
}
```

`Build()`'s existing `Sprite[] backgroundSprites = LoadBackgroundSprites();`
becomes `Sprite[] backgroundSprites = LoadThemedSprites("Assets/Art/Backgrounds", "background");`
(same file paths as before, since the naming convention was already
`background_<themeid>.png`). The four new panels follow the same shape:
`Assets/Art/PanelArt/history_<themeid>.png`,
`Assets/Art/PanelArt/council_<themeid>.png`,
`Assets/Art/PanelArt/event_<themeid>.png`,
`Assets/Art/PanelArt/cosmetics_<themeid>.png`.

`LoadRulerPortraits`/`LoadPortraitSprite` (milestone #12, a different
asset shape -- 15 mood x loyalty images, not theme-keyed) are
deliberately left as-is; consolidating everything into one mega-helper
across both unrelated shapes would be over-generalizing past what this
feature needs.

## Error Handling

Same fallback shape used throughout: a missing/null sprite for a given
theme falls back to index 0 (Default). `cosmeticsPanelImage` null-checked
before use, matching `sceneBackgroundImage`.

## Testing

**PlayMode:** extend `CosmeticsPanelControllerTests.cs`'s existing two
theme-assertion tests (`ApplyTheme_Unlocked_RecolorsAllThreePanelsAndPersists`,
`Initialize_WithPreviouslySelectedTheme_ReappliesItImmediately`) with
`Assert.AreSame` checks for all four new sprite arrays, mirroring the
existing `backgroundSprites` assertion added in milestone #13.

**Verify() and regression test.** `Verify()` gains the same reflection-
based null/length/element check for each of the four new sprite arrays
and `cosmeticsPanelImage`, matching the pattern already used for
`backgroundSprites`/`sceneBackgroundImage`. A new PlayMode test in
`CoreLoopSceneTests.cs` confirms all four panel Images have non-null
sprites after the real scene loads, mirroring
`LoadedCoreLoopScene_SceneBackground_HasNonNullSpriteOnLoad`.

## Explicitly Out of Scope for This Pass

- Button icons (separate, not-yet-started phase item).
- Any animation/transition on panel art swap -- instant, matching the
  existing color swap.
- Aspect-ratio safety net: milestone #13's final review recommended (but
  did not require) an EditMode test asserting all sprites in an art
  folder share one `rect.size`. Still not built here -- each generation
  is visually dimension-checked by hand instead, same as the two prior
  increments.
