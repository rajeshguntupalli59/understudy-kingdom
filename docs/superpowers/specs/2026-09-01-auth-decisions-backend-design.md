# Design: Auth + Decisions Backend Slice

**Date:** 2026-09-01 | **Status:** Approved, pending implementation plan

## Purpose

Build the first backend slice for Understudy Kingdom: user authentication and
the `/api/v1/decisions` endpoint, per `docs/PROJECT_PLAN.md` §7-8. This is
the server-side counterpart to the already-built client-side decision-cycle
loop (PR #1) — until now the client only persists to a local JSON file
(`SaveService`); this backend gives it a real account and a server record of
each decision cycle, laying the foundation everything else (purchases,
councils, PvP) will build on.

## Scope Decisions

- **Location: `backend/` subdirectory of this repo** (monorepo alongside the
  Unity client), not a separate repo. No strong reason to split, and it keeps
  the whole game's history in one place. Flagged as an assumption, not
  dictated by PROJECT_PLAN.md — revisit if the user wants it split later.
- **Two auth paths, both producing the same JWT pair:** Google/Apple OAuth
  (verify their ID token server-side, no password to manage) and an
  anonymous device-id fallback (client generates a random secret at first
  launch; server stores it bcrypt-hashed keyed to `device_id`).
- **Decisions endpoint scope: contract + persistence only.** The server does
  NOT re-implement `OverrideEvaluator`'s decision logic in this pass — the
  client remains authoritative for the resource-allocation decision cycle,
  per the approved client-side design. This endpoint records what happened
  (`player_recommendation`, `ruler_outcome`, `overridden`) rather than
  computing it. Server-authoritative scoring is a `Future Phase` item, not
  in scope here — the client can currently lie about outcomes; that's an
  accepted risk for this pass, not a gap this design silently papers over.
- **No purchases, councils, or PvP endpoints in this pass** — those are
  separate, already-identified backend slices (see the earlier scoping
  question this session).

## Approach: Framework and Data Access

Three approaches were considered:

| # | Approach | Verdict |
|---|---|---|
| A | Express + raw `pg` (node-postgres), hand-written parameterized SQL | Rejected — minimal abstraction is nice, but more CRUD boilerplate than this project needs, and no built-in request validation for a mobile client's requests |
| **B** | **Fastify + Knex query builder** | **Chosen** — Fastify's built-in JSON-schema request/response validation catches malformed client requests before they reach a handler; Knex keeps SQL close to `PROJECT_PLAN.md`'s existing DDL while cutting boilerplate, without a full ORM's code-generation step |
| C | Fastify + Prisma ORM | Rejected for now — strong type safety and auto-migrations, but the codegen step and abstraction overhead aren't worth it for a handful of tables; revisit if the schema grows substantially |

## Components

### `backend/src/db/migrations/` (Knex migrations)
Implements the `users`, `kingdoms`, `ruler_npcs`, `decisions` tables exactly
as specified in `docs/PROJECT_PLAN.md` §6 — no new schema invented here,
just the migration files. `councils`/`council_members`/`events`/
`pvp_duels`/`purchases` tables are NOT created in this pass (out of scope,
per Scope Decisions above) — creating them now would be schema speculation
ahead of the endpoints that need them.

### `backend/src/auth/` — auth module
- `verifyGoogleToken(idToken)` / `verifyAppleToken(idToken)` — call the
  respective provider's public key endpoint to verify an OAuth ID token,
  return the provider's user id + email.
- `verifyDeviceSecret(deviceId, secret)` — bcrypt-compares against the
  stored hash for that `device_id`.
- `issueTokenPair(userId)` — signs a short-lived access JWT (~15 min) and a
  longer-lived refresh JWT (~30 days), both carrying `userId` as the
  subject claim.
- `authMiddleware` — Fastify `preHandler` hook: verifies the Bearer access
  JWT, attaches `request.userId`, returns 401 on missing/invalid/expired
  token.

### `backend/src/routes/auth.ts`
- `POST /api/v1/auth/google` — body `{id_token}` → verifies via
  `verifyGoogleToken`, upserts a `users` row, returns a token pair.
- `POST /api/v1/auth/apple` — same shape, Apple path.
- `POST /api/v1/auth/device` — body `{device_id, secret}` (secret generated
  client-side on first launch) → creates the user + stores the bcrypt hash
  on first call, verifies on subsequent calls, returns a token pair.
- `POST /api/v1/auth/refresh` — body `{refresh_token}` → verifies and
  issues a fresh access token.

### `backend/src/routes/decisions.ts`
- `POST /api/v1/decisions` — Bearer auth required (via `authMiddleware`).
  Body `{kingdom_id, cycle_number, recommendation}`. Verifies the
  authenticated user owns `kingdom_id` (403 if not, not a 404 — don't leak
  existence of another user's kingdom). Verifies `cycle_number` isn't
  already resolved for that kingdom (409 on conflict — mirrors
  `PROJECT_PLAN.md`'s sample error list). Inserts a `decisions` row with the
  client-reported `player_recommendation`/`ruler_outcome`/`overridden`.
  Returns `{decision_id, ruler_outcome, overridden}`.

## Data Flow

```
Client (Unity)
  -> POST /api/v1/auth/{google|apple|device}
  -> receives {access_token, refresh_token}
  -> POST /api/v1/decisions (Bearer access_token)
       -> authMiddleware verifies JWT, sets request.userId
       -> handler verifies kingdom ownership (403 if not owned)
       -> handler verifies cycle_number not already resolved (409 if so)
       -> INSERT INTO decisions (...)
       -> 201 {decision_id, ruler_outcome, overridden}
```

## Error Handling

- Malformed request body (wrong types, missing required fields): Fastify's
  JSON-schema validation rejects with 400 before the handler runs.
- Missing/invalid/expired Bearer token: `authMiddleware` returns 401.
- `kingdom_id` not owned by the authenticated user: 403 (not 404 — avoids
  leaking whether the kingdom exists at all to an unauthorized caller).
- `cycle_number` already resolved for that kingdom: 409.
- OAuth ID token fails provider verification: 401 with a specific
  `INVALID_TOKEN` error code (distinct from the JWT-auth 401, so the client
  can tell "your login attempt failed" from "your session expired").
- Device-secret mismatch on an existing `device_id`: 401 — does not reveal
  whether the `device_id` exists (same response shape as "wrong secret").

## Testing

Fastify's `app.inject()` for endpoint tests against a real test Postgres
instance (via Knex's test-database + migration-then-truncate pattern
between tests) — no real HTTP server needed, matching the client-side
project's discipline of testing real behavior, not mocks, wherever
practical. Auth module functions (`verifyDeviceSecret`, `issueTokenPair`)
get direct unit tests independent of the HTTP layer.

## Explicitly Out of Scope for This Pass

- Purchase verification, receipt validation (separate backend slice)
- Council/guild endpoints (separate backend slice)
- Async PvP duel endpoints (separate backend slice)
- Server-authoritative decision scoring (Future Phase — client is
  authoritative for now, an accepted risk stated above, not silently
  assumed away)
- Rate limiting / abuse prevention on the auth endpoints (should exist
  before real launch, but not blocking this pass — flag as a follow-up)
- Deployment/hosting configuration (Docker, CI/CD, actual Postgres/Redis
  hosting choice) — a separate concern from the API code itself
