# Design: Onboarding Tutorial (Milestone #8, FR-13)

**Date:** 2026-09-03 | **Status:** Approved, pending implementation plan

## Purpose

`docs/PROJECT_PLAN.md` lists FR-13 ("interactive first-session tutorial
covering the core prep→ruler-decision loop before any monetization prompt
appears") as unstarted. Milestones #1-7 built the full core loop, PvP,
history, and council features, but nothing currently explains any of it to
a brand-new player — the sliders, Submit button, and mood/loyalty/agenda
labels are simply presented with no context. FR-14/FR-15 (monetization
guardrails) don't exist yet, so FR-13's literal "before any monetization
prompt" gating is currently vacuous — there is nothing to gate against —
but the tutorial's actual job (teach the loop on first launch) is still
squarely needed and is entirely client-side.

## Scope Decisions

These were confirmed interactively before any design work began:

- **A short step-by-step overlay, not a forced guided decision or a single
  static panel.** Four dismissible steps, each pointing (by text only, not
  visually) at one part of the UI in the order a player will actually use
  it: sliders → Submit → status labels → other features. The player reads
  it, then submits their own first real recommendation whenever ready —
  no gameplay-blocking "you must submit before this counts as done" logic.
  Rejected alternatives: a guided-first-decision variant (real state-machine
  complexity — a hybrid "seen the overlay" + "submitted for real" completion
  condition — for a game this simple to operate); a single static panel
  (weaker fit for FR-13's own "interactive" wording, and wastes the chance
  to walk through the UI in the order it's actually used).
- **The 7 shared controls (3 sliders, Submit, Challenge, View History,
  Council) are disabled while the tutorial overlay is showing**, reusing
  the exact `SetCoreLoopControlsInteractable` pattern `HistoryPanelController`
  and `CouncilPanelController` already established, just with the overlay
  auto-shown instead of button-triggered. A first-time player tapping
  Challenge or Council before they understand mood/loyalty/agenda would be
  confused regardless of monetization state.
- **Trigger condition is the persisted `TutorialCompleted` flag itself**
  (`!RulerState.TutorialCompleted`, read via `DecisionCycleManager.Ruler.State`
  once `Awake()` has loaded it), not a raw `SaveService.HasSave()` check.
  `HasSave()==false` only detects a truly fresh install; a player who quits
  mid-tutorial without completing or skipping it should see it again on
  their next launch even though a save file may exist by then from an
  unrelated action. The flag is the correct, precise signal; a fresh
  install naturally satisfies it too since a fresh `RulerState`'s bool
  defaults to `false`.
- **No visual pointer/spotlight highlighting the live UI element being
  described.** Real rendering complexity (masks, arrows, dynamic
  positioning against elements the overlay itself sits on top of) with no
  existing precedent in this project. The callout's text names what it's
  about; the player can see the real element underneath is dimmed but not
  literally circled. Revisit only if user feedback says the text-only
  version is genuinely confusing.
- **No replay-from-settings-menu.** No settings menu exists in this
  project yet — out of scope until one does.
- **No localization** — matches every other pass of narration text in
  this project (English only).

## Approach

**Reuse, don't reinvent.** `TutorialOverlayController` is structurally the
fourth panel-shaped controller in this scene, but unlike
`HistoryPanelController`/`CouncilPanelController` (button-triggered
modals) it is nobody's target — it shows itself once on `Start()` based on
persisted state, and nothing else needs a reference to it or a way to
disable it in return, since by construction nothing else is interactable
while it's up.

**Persistence mirrors `CouncilRewardApplied` exactly** (milestone #7):
one new bool on `RulerState`/`RulerSaveData`, threaded through the existing
`SaveService.Save`/`Load` round trip. No new file, no new save path.

**Step content is a fixed array of (title, body) pairs**, not a
data-driven/configurable system — four steps, hardcoded, matching this
project's established "don't build generality nothing asks for yet"
discipline (e.g. `DialogueTemplateEngine`'s own small hardcoded template
dictionary).

**ui-ux-pro-max checklist applied to the overlay UI** (per the user's
standing instruction to always check this project's UI/UX work against
it): `escape-routes` — Skip is visible and reachable on every step, never
buried behind Next; `multi-step-progress` — a step indicator ("Step 2 of
4") is always shown; `touch-target-size` — Next/Skip are 220×44,
matching every other button already in this scene; `progressive-disclosure`
— exactly what choosing the step-by-step approach (over one dense wall of
text) already achieves.

## Components

### `Assets/Scripts/NPC/RulerState.cs` / `Assets/Scripts/Core/RulerSaveData.cs` / `Assets/Scripts/Core/SaveService.cs` (modified)
One new field, `TutorialCompleted` (bool, default `false`), threaded
through `RulerState`, `RulerSaveData`, and both directions of
`SaveService.Save`/`Load` — identical shape to `CouncilRewardApplied`.

### `Assets/Scripts/UI/TutorialOverlayController.cs` (new)
Owns: the full-screen semi-transparent background + centered callout box
(panel root), the step title/body/indicator labels, the Next/Skip
buttons, and references to the 7 shared controls it disables while
showing.

```
Steps (fixed array, index 0-3):
  0. "Your Resources" / "These three sliders control your recommendation:
     Army, Trade, and Religion. They always add up to 100 -- adjust one
     and the others rebalance automatically."
  1. "Submit Your Recommendation" / "Once you're happy with your
     allocation, tap Submit Recommendation. Your ruler will accept or
     override it based on their mood, loyalty, and agenda."
  2. "Reading Your Ruler" / "Mood, Loyalty, and Agenda (top of screen)
     describe your ruler's state -- they shift based on how well your
     recommendations match what your ruler actually wants."
  3. "Beyond the Basics" / "Once you're comfortable with the core loop,
     you can Challenge rival kingdoms, view your History, or join a
     Council with other players."
```

**On `Start()`:** if `manager.Ruler.State.TutorialCompleted`, hide the
panel and return immediately (no controls touched — normal play resumes
untouched). Otherwise show step 0, disable the 7 shared controls.

**On Next:** advance to the next step's text; on step 3, the button's own
label switches to "Done". Tapping it on step 3 calls the same completion
path as Skip.

**On Skip (any step) or Done (step 3):** set
`manager.Ruler.State.TutorialCompleted = true`, persist via
`SaveService.Save(manager.Ruler.State)`, hide the panel, re-enable the 7
shared controls.

### `Assets/Editor/CoreLoopSceneBuilder.cs` (modified)
Adds the full-screen background image, the callout box (title/body/step-
indicator labels, Next/Skip buttons), and wires
`TutorialOverlayController.Initialize(...)` with the panel elements, the
`DecisionCycleManager`, and the 7 shared controls
(`armySlider, tradeSlider, religionSlider, submitButton, challengeButton,
viewHistoryButton, councilButton`). `Verify()` gains a
`TutorialOverlayController` check mirroring the existing three panel
checks.

## Data Flow

```
App launches, CoreLoop scene loads
  -> DecisionCycleManager.Awake() -> LoadPersistedStateIfPresent()
       -> Ruler.State = SaveService.Load() (TutorialCompleted from save,
          or false on a fresh RulerState)
  -> TutorialOverlayController.Start()
       -> if Ruler.State.TutorialCompleted: hide panel, do nothing else
       -> else: show step 0, disable the 7 shared controls

Player taps Next through steps 0-2
  -> step title/body/indicator text updates; no persistence yet

Player taps Next on step 3 (now labeled "Done"), OR taps Skip on any step
  -> TutorialCompleted = true
  -> SaveService.Save(Ruler.State)
  -> hide panel, re-enable the 7 shared controls
  -> normal play resumes; this player will never see the tutorial again
```

## Error Handling

None of this feature makes a network call or can fail in a way that needs
surfacing to the player — it's pure local UI state, same posture as
`SliderRebalancer`/`CoreLoopScreenController`. The only failure mode is
`SaveService.Save` itself (already covered by its own established
defensive behavior from milestone #1), unchanged by this task.

## Testing

**EditMode:** `RulerState`/`SaveService` round-trip for `TutorialCompleted`
(mirrors milestone #7's `CouncilRewardApplied` tests exactly — save/load
round-trips true, defaults false with no save file).

**PlayMode:** `TutorialOverlayController` deterministic tests (no network
dependency, matching `HistoryPanelControllerTests`'/`CouncilPanelControllerTests`'
synchronous-setup pattern, not the real-network `*RealDataTests` pattern
since nothing here touches the backend): overlay shows and disables the 7
controls when `TutorialCompleted` starts false; overlay stays hidden and
leaves controls untouched when it starts true; Next advances through all
4 steps with the button label switching to "Done" on step 3; Skip on an
early step sets `TutorialCompleted` true, persists it, hides the panel,
and re-enables the 7 controls; Done on step 3 does the same.

## Explicitly Out of Scope for This Pass

- Visual pointer/spotlight highlighting live UI elements.
- Replaying the tutorial from a settings menu (none exists).
- Localization.
- Any gating against a monetization prompt (none exists yet — FR-13's
  literal wording is satisfied vacuously; revisit once FR-14/FR-15 land).
- Analytics/telemetry on tutorial completion or skip rate.
