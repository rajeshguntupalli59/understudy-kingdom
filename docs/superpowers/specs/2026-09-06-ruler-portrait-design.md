# Design: Ruler Portrait System

**Date:** 2026-09-06 | **Status:** Approved, pending implementation plan

## Purpose

Every visual element in the CoreLoop scene today is a plain colored UI rectangle
with text -- no art. This is the first step of a broader "visual art" phase
(itself the first of several planned post-milestone-#9 phases: visual art,
deeper gameplay, UX polish, real deployment, store readiness -- see
`docs/PROJECT_PLAN.md`'s roadmap discussion). Within visual art, the ruler
portrait was chosen as the starting point because it's the single asset most
tied to the actual gameplay loop: the ruler NPC's Mood and Loyalty already
drive real behavior (override probability, narration, milestone rewards) --
giving that NPC a face that visibly reacts turns an abstract number pair into
something the player actually reads and responds to.

## Scope Decisions

Confirmed interactively before any design work began:

- **Full matrix, not layered composition.** Every Mood tier x Loyalty tier
  combination gets its own fully painted portrait (5 x 3 = 15 images), rather
  than compositing a mood-expression layer with a separate loyalty-frame
  overlay. More art assets to produce and keep visually consistent than a
  layered approach, but each portrait is a single cohesive painting rather
  than a runtime composite -- the user's explicit choice over the
  layered-overlay alternative that was proposed alongside it.
- **Mood drives expression, Loyalty drives regalia/framing** -- not two
  independent axes layered arbitrarily. Within the matrix, Mood changes the
  ruler's facial expression; Loyalty changes the visual richness of the
  ruler's presentation (regalia condition, framing/vignette treatment). This
  keeps all 15 paintings readable as "the same ruler, two things changing
  about him" rather than an arbitrary 15-way grid.
- **AI-generated now, this session**, using a single approved reference
  portrait as the visual anchor for the other 14, rather than placeholder art
  with real art dropped in later. Ensures the ruler is recognizably the same
  character across all 15 states instead of 15 unrelated paintings.
- **Style: painterly hand-painted medieval fantasy**, anchored to a concrete
  external reference (see Art Direction below) rather than an abstract style
  description alone.

## Art Direction

Style anchor: "Medieval Painterly Avatars" (kalponic-studio.itch.io) --
hand-painted, warm/muted oil-and-watercolor palette, visible brushwork,
realistic (not cartoonish) faces with soft/subtle expressions, head-and-
shoulders portrait framing, Ghibli/storybook-illustration influenced.

Base prompt template (shared across all 15 generations, only the bracketed
mood/loyalty descriptors change per image):

```
Painterly hand-painted portrait, medieval fantasy king, head-and-shoulders,
warm muted oil-painting palette, visible brushstrokes, realistic soft facial
features, storybook illustration style, [MOOD EXPRESSION], [LOYALTY REGALIA].
```

| Mood tier (0-100) | Expression descriptor |
|---|---|
| Furious (0-20) | furious, glaring, clenched jaw, thunderous expression |
| Displeased (21-40) | displeased, frowning, cold disapproving stare |
| Neutral (41-60) | neutral, composed, unreadable expression |
| Pleased (61-80) | pleased, faint approving smile, warm eyes |
| Delighted (81-100) | delighted, broad genuine smile, radiant expression |

| Loyalty tier (0-100) | Regalia descriptor |
|---|---|
| Low (0-33) | tarnished plain crown, worn simple attire, dim/cold lighting |
| Medium (34-66) | modest silver crown, respectable attire, neutral lighting |
| High (67-100) | ornate gilded crown, rich regal attire, warm golden lighting |

The first generation (Neutral x Medium, the character's default/starting
state) is the reference portrait -- generated first and shown to the user for
approval on the character design itself before the remaining 14 are generated
using it as visual reference, so the ruler stays recognizably the same person
throughout.

## Approach

**Data: Mood/Loyalty -> tier index.** Two small pure functions map a 0-100
int to a tier index:

```csharp
private static int GetMoodTier(int mood)
{
    if (mood <= 20) return 0; // Furious
    if (mood <= 40) return 1; // Displeased
    if (mood <= 60) return 2; // Neutral
    if (mood <= 80) return 3; // Pleased
    return 4;                 // Delighted
}

private static int GetLoyaltyTier(int loyalty)
{
    if (loyalty <= 33) return 0; // Low
    if (loyalty <= 66) return 1; // Medium
    return 2;                    // High
}
```

Combined index into the flat 15-element array: `moodTier * 3 + loyaltyTier`.

**Where it lives.** `CoreLoopScreenController` already owns the single
established refresh point for Mood/Loyalty display: `RefreshStatusLabels()`,
called whenever either stat changes (from itself, `CouncilPanelController`,
and `EventPanelController`). Rather than introducing a new controller class
for one more piece of ruler-status UI, this extends that existing one:

- New serialized field: `Image rulerPortraitImage`.
- New serialized field: `Sprite[] rulerPortraits` (15 elements, mood-major
  order matching the tables above -- index 0 = Furious/Low ... index 14 =
  Delighted/High).
- `Initialize(...)` gains two new trailing parameters (`Image
  rulerPortraitImage, Sprite[] rulerPortraits`), matching this project's
  established Initialize()-args dependency-injection convention.
- `RefreshStatusLabels()` computes both tiers and sets
  `rulerPortraitImage.sprite = rulerPortraits[moodTier * 3 + loyaltyTier]` in
  the same place it already updates `moodLabel.text` / `loyaltyLabel.text`.

**Scene wiring.** `CoreLoopSceneBuilder.cs` adds one new `Image` GameObject
near the existing mood/loyalty/agenda labels. This is the first time the
scene builder loads external image assets rather than generating everything
in code -- the 15 sprites are loaded via
`AssetDatabase.LoadAssetAtPath<Sprite>(path)` for each of the 15 known file
paths under `Assets/Art/RulerPortraits/`, iterated in mood-major order to
populate the `rulerPortraits` array passed into
`CoreLoopScreenController.Initialize(...)`.

## Error Handling

If any of the 15 array slots is ever null (e.g. an asset failed to import, or
a future edit shrinks the array), fall back to the Neutral/Medium portrait
(index 7) rather than leaving `rulerPortraitImage` blank -- matches this
project's existing pattern for unrecognized/missing state (see
`CosmeticsPanelController.GetThemeColor`'s fallback to the Default theme).

## Testing

**EditMode:** a new test file for the pure tier-mapping functions, covering
every boundary value from the two tables above (e.g. mood 20 vs 21, 40 vs 41,
60 vs 61, 80 vs 81; loyalty 33 vs 34, 66 vs 67) plus the combined-index
formula for a few representative (mood, loyalty) pairs.

**PlayMode:** extend the existing `CoreLoopScreenControllerTests.cs` (or
sibling real-data test, matching whichever already exercises
`RefreshStatusLabels()`) with a case asserting `rulerPortraitImage.sprite`
equals the expected array entry after a mood/loyalty change, and a case
covering the null-slot fallback.

## Explicitly Out of Scope for This Pass

- No animation/transition on portrait swap -- instant, matching how the
  mood/loyalty text labels already update instantly.
- Portrait is not clickable/interactive.
- Agenda (Expansionist, etc.) does not affect the art this pass -- only
  Mood and Loyalty.
- Scene/background art (the throne room backdrop behind the whole CoreLoop
  screen) is a separate later phase, not part of this spec.
- Button icons and panel-specific art are likewise separate later phases.
