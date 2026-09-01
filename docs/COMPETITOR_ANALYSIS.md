# Competitor Analysis — Player Pain Points

**Purpose:** current top-grossing mobile games in Understudy Kingdom's genre
space have well-documented player complaints. Each one below is mapped to
the specific requirement in `PROJECT_PLAN.md` designed to avoid it.

## Market context

| Game | 2026 status |
|---|---|
| Whiteout Survival | Surged to world's #1 grossing mobile game, June 2026 |
| Royal Match | Top-10 grossing, ~55M MAU, puzzle-with-narrative |
| Honor of Kings | Top-10 grossing worldwide, MOBA |
| MONOPOLY GO! | Top-10 grossing, board-game-as-service |
| Gossip Harbor | Top-10 grossing, hidden-object-merge |

*(Source: https://sensortower.com/blog/top-10-worldwide-mobile-games-by-revenue-and-downloads-in-june-2026, https://www.globalgamesforum.com/news-media/the-biggest-mobile-games-of-august-2026)*

## MONOPOLY GO! — reported complaints

| Complaint | Source detail |
|---|---|
| Unwinnable-without-spending events | "Challenges become unwinnable without spending significant real money"; monetization described as harsher than many gacha games |
| Aggressive refill throttling | Dice system requires waiting a full hour for only 5 dice |
| Pop-up spam | Promotional pop-ups every 2–3 minutes |
| Poor customer service | "Zero resolution to complaints" reported |
| Technical bugs | 99% loading errors, mid-roll freezing reported |

**Countered by:** FR-11 (guaranteed F2P reward tier on every event), FR-14
(one interstitial per session cap), FR-15 (no modal stacking), NFR-03
(99.5% crash-free target), NFR-07 (in-app support with visible SLA).

## Honor of Kings — reported complaints

| Complaint | Source detail |
|---|---|
| Broken matchmaking / bots | Players report bot presence in matches; some suggest devs prioritize new skins over fixing gameplay |
| Cluttered home UI | Multiple simultaneous pop-ups for events, login rewards, store bundles |
| Weak onboarding | Game fails to teach basic mechanics, leading to poor teamwork |

**Countered by:** FR-09 (async-only PvP — sidesteps live matchmaking and bot
issues by design rather than patching them), FR-15 (modal stacking ban),
FR-13 (interactive tutorial gates monetization surfaces until the core loop
is taught).

## Structural takeaway

The common thread across both games' complaints is **monetization pressure
crowding out trust and clarity** — refill gates tuned to force spend, pop-up
frequency tuned for impressions over UX, and support that doesn't resolve
issues. Understudy Kingdom's differentiation isn't a new core mechanic — it's
refusing these specific patterns and saying so explicitly at onboarding (see
`PROJECT_PLAN.md` §5, "fair-play pledge").

## Sources

- [Google clamps down on Android app RAM usage amid AI memory crisis](https://www.tomshardware.com/phones/android/google-clamps-down-on-android-app-ram-usage-amid-ai-memory-crisis-developers-have-until-february-2027-to-adapt-to-new-memory-optimizing-rules)
- [Sensor Tower Top 10 Worldwide Mobile Games - June 2026](https://sensortower.com/blog/top-10-worldwide-mobile-games-by-revenue-and-downloads-in-june-2026)
- [Biggest Mobile Games of August 2026](https://www.globalgamesforum.com/news-media/the-biggest-mobile-games-of-august-2026)
- MONOPOLY GO! and Honor of Kings complaint summaries drawn from aggregated
  app store / review-site search results, September 2026
