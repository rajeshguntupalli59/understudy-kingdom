# Design: Backend Service — Auth + Decision History Sync (Milestone #3)

**Date:** 2026-09-02 | **Status:** Approved, pending implementation plan

## Purpose

Milestones #1-2 built the Unity client's core loop entirely offline — the
ruler AI runs locally, state persists to a local JSON file. Per
`docs/PROJECT_PLAN.md` §7, the intended architecture also includes a backend
service (auth, councils, async PvP, purchase verification, live-ops config)
that milestone #1's design doc explicitly deferred ("separate planning
pass"). This milestone is that pass — scoped down to the two pieces that
don't require any other unbuilt feature first: user authentication and
syncing decision history to a real database, laying the foundation FR-06
("relationship history log") and FR-09 ("async PvP", which needs
server-side decision records to judge against) will build on later.

## Scope Decisions

- **Backend-only, not wired to the client yet.** This milestone builds and
  tests `server/` in complete isolation. The Unity client is not modified —
  it keeps using local `OverrideEvaluator`/`SaveService` exactly as today.
  Actually connecting the client (a sign-in screen, replacing/supplementing
  local save with sync calls) is a separate follow-up milestone. This keeps
  the blast radius contained: a new top-level folder, zero changes to
  `Assets/`, independently testable via its own test suite.
- **Auth + decision sync only — not purchase verification.** "Backend
  service" in the original milestone list bundled auth, a decisions API,
  and purchase verification. Purchase verification has no client-side
  monetization UI to call it yet (none exists in the project), so building
  it now would mean untested-by-real-usage code sitting idle. Deferred to
  whenever real monetization UI is planned.
- **Same repository, new top-level folder (`server/`).** Not a separate
  repo — one git history, easier to keep client and server changes in sync
  during early development. Revisit if the team/deploy pipeline ever
  requires a split.
- **Supabase for both Postgres hosting and auth**, not a separately
  self-hosted Postgres + hand-rolled OAuth. Supabase Auth already
  implements Google/Apple sign-in and anonymous accounts — exactly the
  auth requirement in `docs/PROJECT_PLAN.md` §7 — so building our own
  OAuth flow would be reimplementing a solved, well-tested piece for no
  benefit. We still own our own REST API surface and our own
  `kingdoms`/`ruler_npcs`/`decisions` schema — Supabase is not used as an
  auto-generated backend, only as (a) a managed Postgres host and (b) an
  auth provider whose issued JWTs our API verifies.
- **Simplified data model.** `docs/PROJECT_PLAN.md` §6's schema has a
  redundant circular FK (`kingdoms.ruler_npc_id` and
  `ruler_npcs.kingdom_id` both pointing at each other for what's a strict
  1:1 relationship). This design keeps only `ruler_npcs.kingdom_id` — one
  ruler per kingdom, looked up by that FK, no redundant reverse pointer.
  No separate `users` table of our own — Supabase's own `auth.users` table
  already is the user identity table; `kingdoms.user_id` references it
  directly.
- **No rate limiting, CORS configuration, or request logging
  infrastructure in this pass.** Not required by "auth + decision sync"
  functioning correctly; add when a real deployment target makes them
  concrete requirements rather than speculative ones.

## Approach

**Runtime/language:** Node.js (already installed on the dev machine,
v24) + TypeScript, run via `tsx` — no build-step/bundler complexity for a
service this size yet.

**Framework:** Fastify over Express. Two reasons: built-in JSON Schema
request/response validation (matches this API's small, well-defined
payload shapes), and Fastify's `.inject()` gives clean, fast endpoint
tests without spinning up a real HTTP server or adding `supertest`.

**Database access:** Drizzle ORM, connecting directly to Supabase's
Postgres connection string via standard `pg`. Not Supabase's
auto-generated PostgREST data API — we want real, reviewable SQL
migrations and full control over query shape, matching
`docs/PROJECT_PLAN.md` §7's framing of this as *our* REST API, not a thin
proxy over Supabase's.

**Auth verification:** Supabase Auth issues a JWT on sign-in (handled
entirely client-side by whatever eventually calls this API — out of scope
here). Our Fastify service verifies that JWT's signature and expiry on
every protected route (an `onRequest` hook reads `Authorization: Bearer
<token>`, verifies it, and attaches the verified `sub` claim — the
Supabase user id — to the request). No sign-in/OAuth flow is implemented
in this service; it only ever *verifies* tokens someone else obtained.

Verification method (revised from this doc's original plan — see "Design
Correction" below): Supabase signs tokens asymmetrically (ES256), resolved
via the project's JWKS endpoint
(`{SUPABASE_URL}/auth/v1/.well-known/jwks.json`), not a shared HS256
secret. The auth plugin constructs a `jose` `createRemoteJWKSet` once,
lazily on first request (it caches fetched keys internally), and passes it
to `verifySupabaseJwt` on each request. `SUPABASE_JWT_SECRET` is not used.

### Design Correction (found during Task 5 implementation)

This document originally specified HS256 verification against a shared
`SUPABASE_JWT_SECRET` — based on Supabase's older "Legacy JWT Secret"
mechanism, which this project's Supabase instance *has available in its
dashboard* but does not actually use to sign tokens. Empirically decoding
a real token issued by `signInAnonymously()` showed `alg: "ES256"` with a
`kid`, and JWKS-based verification against
`{SUPABASE_URL}/auth/v1/.well-known/jwks.json` succeeds where HS256
verification against the dashboard's "Legacy JWT Secret" value fails. The
*plan* document (a separate file, written after this one) did note that
the dashboard's JWT-secret labeling was worth verifying carefully — but
this design document itself assumed HS256 throughout and never considered
asymmetric signing as a real possibility anywhere in its original text.
The mismatch was not anticipated in design review; it was only caught by
running the plan's Task 5 against the real Supabase project. The fix
(JWKS-based verification) is a net simplification: no secret to provision,
store, or rotate; `jose`'s `createRemoteJWKSet` handles key rotation
automatically.

Rejected alternative: implementing Google/Apple OAuth by hand in Fastify.
Rejected because Supabase Auth already solves this correctly and securely;
reimplementing it is pure risk with no benefit at this project stage.

## Data Model

```sql
-- kingdoms.user_id references Supabase's own auth.users(id) -- not a
-- table we own or migrate.
kingdoms:    id (UUID PK), user_id (UUID, references auth.users), founded_at (timestamptz)

ruler_npcs:  id (UUID PK), kingdom_id (UUID FK -> kingdoms, UNIQUE),
             mood (int), loyalty (int), agenda (text), created_at (timestamptz)

decisions:   id (UUID PK), kingdom_id (UUID FK -> kingdoms), cycle_number (int),
             player_recommendation (jsonb), ruler_outcome (jsonb),
             overridden (boolean), created_at (timestamptz)
```

`ruler_npcs.kingdom_id` is `UNIQUE` to enforce the 1:1 relationship at the
database level, not just in application code.

## Components

### `server/src/db/schema.ts`
Drizzle schema definitions for the three tables above. Source of truth for
migrations (`drizzle-kit generate`).

### `server/src/auth/verifyToken.ts`
Fastify `onRequest` hook: reads `Authorization: Bearer <token>`, verifies
against Supabase's JWKS-resolved ES256 public key (see "Verification
method" above — not a shared JWT secret), rejects with `401` on
missing/invalid/expired token, otherwise attaches `request.userId` (the
verified `sub` claim) for downstream route handlers.

### `server/src/routes/kingdoms.ts`
- `POST /api/v1/kingdoms` — creates a kingdom + ruler_npc row for
  `request.userId` if one doesn't already exist (idempotent: if the user
  already has a kingdom, returns the existing one with `200` rather than
  erroring, since "create my kingdom" is naturally idempotent from a
  client's perspective — it doesn't need to track whether this is the
  first call).
- `GET /api/v1/kingdoms/me` — returns `request.userId`'s kingdom + ruler
  state, or `404` if none exists yet.

### `server/src/routes/decisions.ts`
- `POST /api/v1/decisions` — records one decision for the caller's
  kingdom. Body: `{ cycle_number, player_recommendation, ruler_outcome,
  overridden }`. Returns `409` if `cycle_number` already has a recorded
  decision for this kingdom (matches `docs/PROJECT_PLAN.md` §7's own
  sample endpoint error list).
- `GET /api/v1/decisions?cursor=&limit=` — paginated list of the caller's
  kingdom's decision history, newest first.

### `server/src/app.ts`
Builds and exports the Fastify instance (route registration, the auth
hook, a `GET /health` route requiring no auth) as a function, separate
from the file that actually starts listening — this is what
`.inject()`-based tests import directly, without binding a real port.

## Data Flow

```
Client (out of scope) obtains a Supabase JWT via Supabase Auth
  -> Authorization: Bearer <jwt> on every request to server/
  -> verifyToken hook validates signature+expiry, sets request.userId
  -> route handler queries/writes kingdoms/ruler_npcs/decisions via Drizzle,
     scoped to request.userId (never trusting a client-supplied user id)
  -> JSON response
```

## Error Handling

- Missing/invalid/expired JWT → `401`, generic message (no detail on
  *why* verification failed, to avoid giving an attacker useful signal).
- Malformed request body → `400`, via Fastify's built-in schema
  validation (no hand-written validation code).
- `POST /decisions` with an already-recorded `cycle_number` for that
  kingdom → `409`.
- `GET /kingdoms/me` with no kingdom yet → `404`.
- Any unexpected/database error → `500`, generic message, full detail
  logged server-side only — never leak internals (query text, stack
  traces) to the client.

## Testing

Integration tests use Fastify's `.inject()` against the real app instance
from `server/src/app.ts`, hitting one real (single) Supabase project's
Postgres — no mocked database, since the whole point is verifying real
queries against a real schema. Each protected-route test authenticates as
a real anonymous Supabase Auth user (created via Supabase's Auth API at
test setup), using that user's real issued JWT — this exercises the actual
verification path end-to-end, not a stubbed one. Tables relevant to a test
file are truncated in `afterEach` for isolation between tests; exact
mechanics (a shared test-setup helper, transaction-per-test vs
truncate-per-test) are an implementation-plan decision, not fixed here.

Unit tests (no database) cover: the JWT verification hook's pure
signature/expiry logic in isolation (valid token, expired token, tampered
signature, missing header — each a fast, deterministic test with no
network/DB dependency).

## Explicitly Out of Scope for This Pass

- Any Unity client change — no sign-in screen, no sync calls replacing or
  supplementing `SaveService`. Separate follow-up milestone.
- Purchase verification, councils, async PvP, live-ops event config — all
  separate, later milestones per the original decomposition.
- Rate limiting, CORS policy, structured request logging, deployment
  configuration (hosting the Node service itself, as opposed to the
  Postgres database, which Supabase already hosts) — add when a concrete
  deployment target makes these real requirements.
- Multiple environments (dev/staging/prod Supabase projects) — one project
  for now, matching the project's current solo/greenfield stage.
