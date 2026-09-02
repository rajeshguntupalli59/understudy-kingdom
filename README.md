# Understudy Kingdom

> You never rule. You advise a flawed monarch who sometimes ignores you — and lives with the consequences either way.

## Overview

Understudy Kingdom is a mobile strategy/RPG hybrid-casual game. Instead of
commanding a kingdom directly, the player is the royal advisor: each decision
cycle they prep a strategic recommendation for an NPC ruler with a persistent,
evolving personality (mood, loyalty, agenda). The ruler doesn't always listen —
and the story is what happens when they don't.

It's positioned in the two highest-revenue mobile genres (Strategy ~$17.5B/yr
+ RPG ~$16.8B/yr) but differentiated against the current chart-toppers'
biggest complaints: no unwinnable-without-spending events, no matchmaking
bots (PvP is async), no home-screen pop-up stacking. See
[`docs/COMPETITOR_ANALYSIS.md`](docs/COMPETITOR_ANALYSIS.md) for the specific
pain points this design counters.

## Features (planned)

- Prep→ruler-decision core loop with a persistent, moody NPC monarch
- Behavior-tree-driven NPC "realism" (no heavy on-device ML — see
  [`docs/NPC_PERFORMANCE_NOTES.md`](docs/NPC_PERFORMANCE_NOTES.md))
- Council (guild) system with shared milestones
- Asynchronous advisor-vs-advisor PvP — no live matchmaking, no bots
- Weekly live-ops events with a guaranteed F2P-completable reward tier
- Cosmetic-only monetization for the "court" customization layer

## Documentation

| Doc | Purpose |
|---|---|
| [`docs/PROJECT_PLAN.md`](docs/PROJECT_PLAN.md) | Full BA spec — requirements, data model, API shape, business rules, definition of done |
| [`docs/COMPETITOR_ANALYSIS.md`](docs/COMPETITOR_ANALYSIS.md) | Player pain points from current top-grossing games, mapped to the requirement that counters each |
| [`docs/GAME_CONCEPTS.md`](docs/GAME_CONCEPTS.md) | The 18-concept brainstorm this design was selected from, and why |
| [`docs/NPC_PERFORMANCE_NOTES.md`](docs/NPC_PERFORMANCE_NOTES.md) | Google Play's Feb 2027 memory rules and how to hit realistic NPCs within budget |

## Dev Tooling

This repo has three Claude Code project-scoped skills/integrations wired in:

- **[superpowers](https://github.com/obra/superpowers)** (MIT) — TDD, systematic debugging, planning, and code-review workflow skills. Registered as a proper plugin marketplace in `.claude/settings.json` (`extraKnownMarketplaces` + `enabledPlugins`), so it loads through Claude Code's plugin system on a fresh session/restart. Also vendored under `.claude/skills/` as a working fallback for sessions that predate the marketplace registration. License at `.claude/skills/SUPERPOWERS_LICENSE`.
- **[codegraph](https://github.com/colbymchenry/codegraph)** — local semantic code index (MCP server, config in `.mcp.json`). Run `codegraph init` after cloning to build the index (gitignored, not checked in).
- **[graphify](https://github.com/graphify-dev/graphify)** — codebase knowledge graph (`.claude/skills/graphify/`). Run `graphify .` to build it; add an LLM API key (`GEMINI_API_KEY`, `ANTHROPIC_API_KEY`, etc.) to also index the markdown docs, or pass `--code-only` to skip that. Output is gitignored.

## Status

**Pre-production — Unity project skeleton only, no gameplay logic yet.**
Script stubs under `Assets/Scripts/` map 1:1 to the functional requirements
in `docs/PROJECT_PLAN.md` (each has a `TODO(FR-xx)` comment). Open decisions
before real implementation starts are tracked in `docs/PROJECT_PLAN.md` §9.

## Getting Started (Unity)

1. Clone this repo.
2. Open Unity Hub, add the project folder. It should prompt for **Unity 6
   LTS (6000.3.23f1)** — the version pinned in `ProjectSettings/ProjectVersion.txt`.
   Install that editor version if you don't have it.
3. On first open, Unity will generate `Library/`, additional
   `ProjectSettings/` files, and resolve packages from `Packages/manifest.json`
   — all gitignored, this is expected and can take a few minutes.

> **Note:** this scaffold was hand-built (folder structure, manifest, script
> stubs) in an environment without a Unity Editor available, so it has *not*
> been opened or compiled in-editor yet. Treat the first local open as the
> real verification step — if Unity reports any issue with the manifest or
> project structure, that's the first thing to fix.

## Proposed Stack

- Client: Unity 6 LTS (6000.3) (C#), ASTC texture compression, Addressables
- Backend: Node.js + PostgreSQL + Redis
- Target floor: 4GB-RAM-class Android devices (Google Play's Feb 2027
  enforcement caps foreground RSS+Swap at 2GB on that tier)

## License

TBD.
