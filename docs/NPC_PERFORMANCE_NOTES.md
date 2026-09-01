# NPC Realism vs. Memory Budget

**Purpose:** reconcile "NPCs should feel real" with Google Play's incoming
memory enforcement and general mobile RAM constraints. Written before any
engine work starts so the art/animation pipeline is designed around the
budget from day one, not retrofitted.

## The constraint: Google Play memory enforcement (effective Feb 2027)

- Enforcement is based on **Anonymous RSS + Swap** (foreground memory),
  **Bitmap memory usage**, and **DEX code optimization** — tracked as a
  28-day rolling P90 in Play Console (visible from Nov 2026).
- Limits scale by device RAM tier: **4GB devices → 2GB foreground cap**,
  **8GB devices → 2.25GB cap**. Design for the 4GB tier — it's both the
  binding constraint and the more common tier among the India-first launch
  audience.
- **Bitmap memory is tracked separately and explicitly** — this is exactly
  where "better graphics" and "less RAM" collide, since character/NPC
  textures are usually the largest bitmap consumer in a game.
- Games with DEX code >50MB need ≥25% each on obfuscation, optimization,
  and shrinking rates in submitted App Bundles.

*(Source: https://android-developers.googleblog.com/2026/08/app-quality-memory-optimization-secure-onboarding.html, https://www.tomshardware.com/phones/android/google-clamps-down-on-android-app-ram-usage-amid-ai-memory-crisis-developers-have-until-february-2027-to-adapt-to-new-memory-optimizing-rules)*

## Texture pipeline

- **ASTC compression** for all NPC textures — cross-platform (Android + iOS),
  best compression-to-quality ratio of the standard mobile formats.
- **One shared texture atlas per NPC archetype** (not per individual NPC).
  Visual variety comes from shader-driven recoloring/blending (skin tone,
  hair color, clothing palette) applied to the shared atlas, not from unique
  textures per character.
- **Shared skeleton + shared low-poly base mesh** across NPC types; silhouette
  variation via small mesh deformers rather than separate high-poly models.
- **Impostors/billboards** for background/non-interactive NPCs; full 3D
  detail reserved for NPCs the player is actively interacting with (i.e.,
  the ruler, and any council/advisor NPCs in the current scene).

*(Source: https://unity.com/blog/games/optimize-your-mobile-game-performance-expert-tips-on-graphics-and-assets)*

## Animation pipeline

- Compress animation curves — reduce keyframes, quantize bone rotations.
  Full-fidelity mocap data is a commonly overlooked memory cost.
- Reuse animation rigs across NPC types.
- Facial expression via a small blend-shape set (happy / angry / tired /
  suspicious) driven by the behavior tree's mood state — reads as "alive"
  without per-NPC unique animation data.

## Where "realism" should actually come from: behavior, not polygons

For Understudy Kingdom specifically, behavioral depth is a cheaper and more
differentiated source of "feels real" than graphical fidelity:

- **Lightweight behavior trees / utility AI** — each NPC's mood, loyalty,
  and agenda are a few KB of state, not MB of asset data (see
  `PROJECT_PLAN.md` FR-04).
- **Procedural dialogue templates** with variable slots keyed to mood/
  history (FR-05) — no on-device LLM inference, no voice-acted line
  explosion in asset size.
- **Persistent memory of player choices** (a small state dict per kingdom,
  see `PROJECT_PLAN.md` §6 `decisions` table) reads as more "real" to
  players than higher-poly faces, at near-zero memory cost.

## Working target

Keep NPC-related bitmap memory under a fixed per-archetype budget — one
shared 1024×1024 ASTC atlas per NPC class, not per individual — and spend
the differentiation budget on the behavior/writing layer instead of the
render layer.

## Engine-specific follow-up (deferred)

Concrete Unity pipeline guidance (Addressables grouping strategy, ASTC
import presets, Sprite Atlas setup) is deferred until the project skeleton
milestone — see `PROJECT_PLAN.md` §8 Open Questions.
