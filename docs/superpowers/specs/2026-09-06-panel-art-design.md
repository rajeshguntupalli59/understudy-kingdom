# Design: Themed Panel Art

**Date:** 2026-09-06 | **Status:** Approved, pending implementation plan

## Purpose

Third increment of the visual-art phase (see `docs/PROJECT_PLAN.md`'s
roadmap discussion; the first two were milestone #12's ruler portrait
and milestone #13's scene backgrounds). Today the History/Council/Events
modal panels are flat-colored rectangles -- `CosmeticsPanelController.ApplyTheme`
already recolors them to match the selected theme. This gives each panel
real painted art, per theme, so opening any panel feels like entering a
themed room rather than a colored overlay.

## Scope Decisions

Confirmed interactively before any design work began:

- **3 panels (History/Council/Events), not 4.** Customize was originally
  scoped to gain theme-reactivity too (4 panels x 3 themes = 12 images),
  but the art generation session hit a real, hard blocker partway
  through: the free Hugging Face inference tier has a genuine monthly
  credit cap (distinct from the earlier anonymous-tier per-session quota
  wall, which resets), and it was exhausted after 8 of the planned 12
  images -- all 3 Customize variants and the Events/Harvest-Hall variant
  never got generated. Rather than block this whole increment on a
  monthly reset or a different paid art source, the user chose to ship
  with what generated successfully: Customize stays exactly as it is
  today (fixed navy, not theme-reactive -- literally untouched by this
  plan), and the one missing panel/theme combination
  (`event_event`, i.e. the Events panel under the Harvest Hall theme)
  is deliberately left unset and relies on the fallback mechanism
  already built for exactly this situation (falls back to
  `event_default`'s Default-theme art) rather than blocking the other 8
  on it.
- **Art varies per theme, matching the scene-background precedent** --
  not one fixed image per panel. 3 panels x 3 themes = 9 needed sprite
  slots, 8 with real art, 1 intentionally exercising the fallback path.
- **Extends `CosmeticsPanelController`**, the existing single owner of
  theme-switching, rather than a new controller -- same reasoning
  milestones #12 and #13 both used.
- **Consolidates the sprite-loading helper.** The final review on
  milestone #13 already flagged `LoadBackgroundSprite`/`LoadPortraitSprite`
  as near-duplicate code. Rather than writing a 3rd and 4th near-copy for
  three more panels, this plan generalizes into one
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
| Events | A seasonal banner hall | Cool stone hall with banners | Burgundy banner hall | **Not generated (HF credits exhausted) -- falls back to Default's art via the existing fallback mechanism** |

Customize is out of scope this pass (see Scope Decisions) -- stays its
current fixed navy, no art.

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

**Data: panel x theme -> sprite.** Each of the 3 panels gets its own
`Sprite[3]` field, index-aligned to the existing `Themes` array exactly
like `backgroundSprites` already is:

```csharp
[SerializeField] private Sprite[] historyPanelSprites;
[SerializeField] private Sprite[] councilPanelSprites;
[SerializeField] private Sprite[] eventPanelSprites;
```

All three reuse the existing `GetBackgroundSprite(themeId, sprites)`
lookup unmodified -- it already takes any 3-element array and the
theme id, with no assumption about which panel it's for. This is also
why the missing `event_event` image needs no special handling: `sprites[2]`
(the Event slot) will simply be null after loading, and
`GetBackgroundSprite`'s existing `sprites[i] != null ? sprites[i] :
sprites[0]` fallback already resolves that to `sprites[0]` (Default) --
the exact mechanism this spec's Scope Decisions section is relying on.

**Where it lives.** `Initialize(...)` gains three new trailing
parameters (the three sprite arrays), and `ApplyTheme(themeId)` gains
matching lines:

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
    if (sceneBackgroundImage != null)
    {
        sceneBackgroundImage.sprite = GetBackgroundSprite(themeId, backgroundSprites);
    }
}
```

No new null-Image guards are needed: `eventPanelImage`/`councilPanelImage`/
`historyPanelImage` are pre-existing fields that have never been null
since this controller was first written (unlike `sceneBackgroundImage`,
which was genuinely new when milestone #13 added it).

**Scene wiring.** `CoreLoopSceneBuilder.Build()` passes three
`LoadThemedSprites(...)` results into `Initialize(...)`. The Customize
panel's construction is untouched -- no new Image variable, no new
loader calls for it.

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
`background_<themeid>.png`). The three new panels follow the same shape:
`Assets/Art/PanelArt/history_<themeid>.png`,
`Assets/Art/PanelArt/council_<themeid>.png`,
`Assets/Art/PanelArt/event_<themeid>.png` -- with `event_event.png`
deliberately not present among the committed files (see Scope
Decisions).

`LoadRulerPortraits`/`LoadPortraitSprite` (milestone #12, a different
asset shape -- 15 mood x loyalty images, not theme-keyed) are
deliberately left as-is; consolidating everything into one mega-helper
across both unrelated shapes would be over-generalizing past what this
feature needs.

## Error Handling

Same fallback shape used throughout: a missing/null sprite for a given
theme falls back to index 0 (Default). This is not a hypothetical for
this pass -- `event_event` genuinely is missing and exercises this
exact path in the shipped scene, not just in a test.

## Testing

**PlayMode:** extend `CosmeticsPanelControllerTests.cs`'s existing two
theme-assertion tests (`ApplyTheme_Unlocked_RecolorsAllThreePanelsAndPersists`,
`Initialize_WithPreviouslySelectedTheme_ReappliesItImmediately`) with
`Assert.AreSame` checks for all three new sprite arrays, mirroring the
existing `backgroundSprites` assertion added in milestone #13.

**Verify() and regression test.** `Verify()` gains the same reflection-
based null/length/element check for each of the three new sprite arrays
(NOT per-element null checks the way `backgroundSprites`/`rulerPortraits`
get them -- this feature deliberately ships with one known-missing
element, `eventPanelSprites[2]`, so a per-element check would fail
`Verify()` on a perfectly-intended state; check array presence/length
only, not full population). A new PlayMode test in `CoreLoopSceneTests.cs`
confirms all three panel Images have non-null sprites after the real
scene loads (note: this passes because of the fallback, not because
every slot has real art -- the test name/docstring should say so
explicitly), mirroring `LoadedCoreLoopScene_SceneBackground_HasNonNullSpriteOnLoad`.

## Explicitly Out of Scope for This Pass

- Customize panel art (see Scope Decisions -- HF credits exhausted
  mid-generation; panel stays exactly as it is today).
- The missing `event_event` image -- revisit once free art-generation
  credits are available again; no code changes needed to add it later,
  just committing the file at the expected path.
- Button icons (separate, not-yet-started phase item).
- Any animation/transition on panel art swap -- instant, matching the
  existing color swap.
- Aspect-ratio safety net: milestone #13's final review recommended (but
  did not require) an EditMode test asserting all sprites in an art
  folder share one `rect.size`. Still not built here -- each generation
  is visually dimension-checked by hand instead, same as the two prior
  increments.
