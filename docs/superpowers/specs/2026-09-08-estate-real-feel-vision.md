# Design: Estate "Real-Feel" Interaction Vision

## Purpose

The user's standing directive (given right after Estate Phase 3 merged):
every core Estate interaction should make the player feel like they're
doing the real thing, not tapping through an abstract state change. This
document is not a buildable spec — it establishes the shared design
language that each Estate subsystem's own spec should inherit, so future
phases don't reinvent the interaction philosophy from scratch.

This document intentionally stays conceptual for subsystems not yet
scoped for implementation. The first buildable application of this
language is `2026-09-08-estate-crop-animation-design.md` (crops +
watering). Shops and animals get their own specs, written when their turn
comes, that read this document first.

## The Principle

Every core interaction has a visible beginning, a visible middle (when one
exists), and a visible end -- never a bare instant flip from one UI state
to another. Where a real-world action has a natural physical shape (a seed
going into soil, a building going up, an animal being fed), the
interaction should evoke that shape, using procedural animation on
existing or cheaply-generated art rather than requiring an animation
engine or hand-authored frame sequences this project doesn't have tooling
for.

This does not mean every interaction needs elaborate animation. A one-shot
0.2-0.4s tween (the existing `ColorFlash`/`ScaleBounce`/`HarvestFly`
pattern already in `EstatePanelController.cs`) is often enough -- the
principle is about *presence*, not *duration*.

## Per-Subsystem Application (conceptual only, except Crops)

**Crops & watering (this phase, fully specified separately):** planting
visibly places a seed, watering visibly waters, growth is visibly
progressing (not just correct on next look), harvest visibly collects to
where the reward actually lands.

**Shops (future phase, not yet specified):** the user's concrete
description was shops built as physical structures directly on the land,
with the land visibly expanding to make room, and a real construction
animation playing as a shop goes up -- replacing the current list-row
Unlock/Start/Collect UI in the Shops tab. This is a materially bigger
change than crop animation: it needs a land-expansion mechanic that
doesn't exist yet (today's 8-plot grid is fixed), and a decision about
whether shops occupy plot-grid space or a separate spatial area. Not
scoped here -- its own brainstorm when this phase is reached.

**Animals (future Phase 2 of the economy roadmap, not yet built at all):**
no design exists yet for the underlying animal-care mechanic itself, so
there is nothing to apply this language to yet. When Phase 2 is
brainstormed, it should read this document first so feeding/care
interactions are animated from day one rather than shipped abstract and
retrofitted later (avoiding a repeat of what happened with crops/shops).

## Non-Goals

- No new animation engine, rigging tool, or frame-sequence art pipeline --
  procedural tweens on static sprites only, matching what's already
  established in this codebase.
- No redesign of any already-shipped subsystem's underlying state model
  (save format, growth timing, production timing) -- this vision is about
  the presentation layer only, in every phase it eventually touches.
- Not a commitment to build shops-on-land or animal animations now --
  purely a shared reference for when those phases are brainstormed.
