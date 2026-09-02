# Backend Service (Auth + Decision History Sync) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up a real, runnable Fastify + TypeScript backend service (`server/`) that authenticates requests via Supabase-issued JWTs and lets an authenticated user create a kingdom and sync their ruler-decision history to a real Postgres database.

**Architecture:** Fastify app (`server/src/app.ts`) exposes an unauthenticated `/health` route and, inside an encapsulated child scope with an auth `onRequest` hook, two authenticated route groups (`kingdoms`, `decisions`) backed by Drizzle ORM queries against Supabase's hosted Postgres. The service only verifies JWTs Supabase already issued — it never implements sign-in itself.

**Tech Stack:** Node.js v24 (already installed) + TypeScript 5.9.3, run via `tsx` (no build step). Fastify 5.12.1, Drizzle ORM 0.45.2 + `pg` 8.23.0, `jose` 6.2.10 for JWT verification, `@supabase/supabase-js` 2.114.0 (test helper only, to create real anonymous test users), Vitest 4.1.11.

## Global Constraints

- This plan touches only the new `server/` folder — no changes anywhere under `Assets/` (the Unity client), per `docs/superpowers/specs/2026-09-02-backend-service-design.md`.
- No purchase verification, councils, PvP, or live-ops endpoints — auth + kingdoms + decisions only.
- No rate limiting, CORS config, or deployment/hosting setup for the Node process itself.
- One Supabase project, one environment (no dev/staging/prod split).
- `ruler_npcs.kingdom_id` is `UNIQUE` (enforces 1:1 with `kingdoms` at the DB level). No Drizzle-level FK from `kingdoms.user_id` to Supabase's `auth.users` (different Postgres schema, application-enforced only).
- Integration tests hit the real Supabase Postgres project using a real Supabase Auth anonymous user's real JWT — never mocked.
- Every git commit message ends with:
  ```
  Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_017JNi1ThZutdGUt6toaGBmq
  ```

---

## Task 1: Project scaffolding + Supabase project setup + verified connection

**Files:**
- Create: `server/package.json`
- Create: `server/tsconfig.json`
- Create: `server/.env.example`
- Create: `server/.gitignore`
- Create: `server/src/db/client.ts`
- Modify: `.gitignore` (repo root — add `server/node_modules/`, `server/.env`, `server/dist/` if not already covered by a broad `node_modules/` rule)

**Interfaces:**
- Consumes: nothing (first task).
- Produces: `server/src/db/client.ts` exports `db` — a Drizzle instance connected via `pg.Pool` to `process.env.DATABASE_URL`, importable as `import { db } from './db/client'` (or `'../db/client'` from `src/routes/`). Every later task's database access goes through this.

- [ ] **Step 1: Create a free Supabase project (human step — cannot be automated)**

If you don't already have one: go to https://supabase.com, sign up (free), click "New Project". Pick any name/region/database password (save the password — you'll need it for the connection string). Wait for provisioning to finish (~2 minutes).

Once created, enable anonymous sign-ins (needed for this plan's integration tests, which authenticate as a real anonymous user): in the Supabase dashboard, go to **Authentication → Sign In / Providers**, find **"Allow anonymous sign-ins"**, and turn it on.

Then collect three values from the dashboard:
- **`DATABASE_URL`**: Project Settings → Database → Connection string → URI (choose the "Session pooler" or direct connection string; it looks like `postgresql://postgres.[ref]:[password]@aws-0-[region].pooler.supabase.com:5432/postgres` or `postgresql://postgres:[password]@db.[ref].supabase.co:5432/postgres`). Substitute in the database password you set at project creation.
- **`SUPABASE_URL`**: Project Settings → API → Project URL (looks like `https://[ref].supabase.co`).
- **`SUPABASE_ANON_KEY`**: Project Settings → API → Project API keys → `anon` `public` key.
No JWT secret needs to be collected: this plan's auth verification uses JWKS-based asymmetric (ES256) verification, resolved automatically from `SUPABASE_URL` at `{SUPABASE_URL}/auth/v1/.well-known/jwks.json` — there's no shared secret to provision, store, or rotate. (An earlier draft of this plan called for collecting a `SUPABASE_JWT_SECRET`/"Legacy JWT Secret" for HS256 verification; that assumption didn't match how this project's Supabase instance actually signs tokens — see the "Design Correction" section of `docs/superpowers/specs/2026-09-02-backend-service-design.md`.)

If anything in this step doesn't match what's described (e.g. no anonymous sign-in toggle), **stop and report back** with what you actually see in the dashboard rather than guessing — the exact Supabase dashboard layout changes over time and this plan's instructions may be stale.

- [ ] **Step 2: Scaffold the Node project**

Create `server/package.json`:

```json
{
  "name": "understudy-kingdom-server",
  "version": "0.1.0",
  "private": true,
  "type": "module",
  "scripts": {
    "dev": "tsx watch src/server.ts",
    "start": "tsx src/server.ts",
    "test": "vitest run",
    "test:watch": "vitest",
    "db:generate": "drizzle-kit generate",
    "db:migrate": "drizzle-kit migrate"
  },
  "dependencies": {
    "@supabase/supabase-js": "2.114.0",
    "dotenv": "17.4.2",
    "drizzle-orm": "0.45.2",
    "fastify": "5.12.1",
    "fastify-plugin": "6.0.0",
    "jose": "6.2.10",
    "pg": "8.23.0"
  },
  "devDependencies": {
    "@types/node": "26.4.1",
    "@types/pg": "8.23.1",
    "drizzle-kit": "0.31.10",
    "tsx": "4.23.13",
    "typescript": "5.9.3",
    "vitest": "4.1.11"
  }
}
```

Create `server/tsconfig.json`:

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "NodeNext",
    "moduleResolution": "NodeNext",
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "outDir": "dist",
    "types": ["node"]
  },
  "include": ["src", "test", "drizzle.config.ts", "vitest.config.ts"]
}
```

Create `server/.env.example`:

```
DATABASE_URL=postgresql://postgres:[YOUR-PASSWORD]@db.[YOUR-PROJECT-REF].supabase.co:5432/postgres
SUPABASE_URL=https://[YOUR-PROJECT-REF].supabase.co
SUPABASE_ANON_KEY=your-anon-public-key
PORT=3000
```

Create `server/.gitignore`:

```
node_modules/
.env
dist/
drizzle/
```

Create `server/.env` (NOT committed — copy from `.env.example` and fill in the three real values from Step 1):

```bash
cd "C:\Users\rajes\understudy-kingdom\server"
cp .env.example .env
```

Then manually edit `server/.env` to replace the three placeholder values with the real ones from Step 1. If you (the implementer) don't have these values because a human hasn't completed Step 1 yet, **stop here and report NEEDS_CONTEXT** — do not fabricate placeholder credentials and proceed, since Step 4 below requires a real, working connection.

- [ ] **Step 3: Install dependencies**

```bash
cd "C:\Users\rajes\understudy-kingdom\server"
npm install
```

Expected: completes with no errors, creates `server/node_modules/` and `server/package-lock.json`.

- [ ] **Step 4: Write the DB client and verify a real connection**

Create `server/src/db/client.ts`:

```typescript
import 'dotenv/config';
import { drizzle } from 'drizzle-orm/node-postgres';
import { Pool } from 'pg';

const pool = new Pool({ connectionString: process.env.DATABASE_URL });

export const db = drizzle(pool);
export const pgPool = pool;
```

Create a throwaway verification script `server/verify-connection.mjs`:

```javascript
import 'dotenv/config';
import { Pool } from 'pg';

const pool = new Pool({ connectionString: process.env.DATABASE_URL });

try {
  const result = await pool.query('SELECT 1 as ok');
  console.log('Connection successful:', result.rows);
  await pool.end();
  process.exit(0);
} catch (err) {
  console.error('Connection FAILED:', err.message);
  process.exit(1);
}
```

Run it:

```bash
cd "C:\Users\rajes\understudy-kingdom\server"
node verify-connection.mjs
```

Expected: `Connection successful: [ { ok: 1 } ]`, exit code 0. If this fails, the `DATABASE_URL` in `.env` is wrong (bad password, wrong host, or the project isn't fully provisioned yet) — do not proceed to Task 2 until this passes with a real result.

Once it passes, delete the throwaway script:

```bash
rm "C:\Users\rajes\understudy-kingdom\server\verify-connection.mjs"
```

- [ ] **Step 5: Commit**

```bash
cd "C:\Users\rajes\understudy-kingdom"
git add server/package.json server/package-lock.json server/tsconfig.json server/.env.example server/.gitignore server/src/db/client.ts .gitignore
git commit -m "feat: scaffold backend service project and verify Supabase connection

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_017JNi1ThZutdGUt6toaGBmq"
```

(`server/.env` is gitignored and must NOT be committed — verify with `git status --short` that it does not appear as staged.)

---

## Task 2: Database schema + migration

**Files:**
- Create: `server/src/db/schema.ts`
- Create: `server/drizzle.config.ts`

**Interfaces:**
- Consumes: `server/src/db/client.ts`'s `db` and `pgPool` exports (Task 1).
- Produces: `server/src/db/schema.ts` exports `kingdoms`, `rulerNpcs`, `decisions` (Drizzle `pgTable` objects). Later tasks import these directly: `import { kingdoms, rulerNpcs, decisions } from '../db/schema'`. Columns (camelCase in TS, snake_case in Postgres): `kingdoms.id` (uuid), `kingdoms.userId` (uuid), `kingdoms.foundedAt` (timestamp); `rulerNpcs.id` (uuid), `rulerNpcs.kingdomId` (uuid, unique), `rulerNpcs.mood`/`loyalty` (int, default 50), `rulerNpcs.agenda` (text, default `'Expansionist'`), `rulerNpcs.createdAt` (timestamp); `decisions.id` (uuid), `decisions.kingdomId` (uuid), `decisions.cycleNumber` (int), `decisions.playerRecommendation`/`rulerOutcome` (jsonb), `decisions.overridden` (boolean), `decisions.createdAt` (timestamp).

- [ ] **Step 1: Write the schema**

Create `server/src/db/schema.ts`:

```typescript
import { pgTable, uuid, integer, text, boolean, jsonb, timestamp, unique } from 'drizzle-orm/pg-core';

/**
 * kingdoms.userId references Supabase's own auth.users(id) -- a table this
 * project doesn't own or migrate, so there is deliberately no Drizzle-level
 * foreign key here (application-enforced only). See
 * docs/superpowers/specs/2026-09-02-backend-service-design.md.
 */
export const kingdoms = pgTable('kingdoms', {
  id: uuid('id').primaryKey().defaultRandom(),
  userId: uuid('user_id').notNull(),
  foundedAt: timestamp('founded_at', { withTimezone: true }).notNull().defaultNow(),
});

export const rulerNpcs = pgTable('ruler_npcs', {
  id: uuid('id').primaryKey().defaultRandom(),
  kingdomId: uuid('kingdom_id')
    .notNull()
    .references(() => kingdoms.id)
    .unique(),
  mood: integer('mood').notNull().default(50),
  loyalty: integer('loyalty').notNull().default(50),
  agenda: text('agenda').notNull().default('Expansionist'),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
});

export const decisions = pgTable(
  'decisions',
  {
    id: uuid('id').primaryKey().defaultRandom(),
    kingdomId: uuid('kingdom_id')
      .notNull()
      .references(() => kingdoms.id),
    cycleNumber: integer('cycle_number').notNull(),
    playerRecommendation: jsonb('player_recommendation').notNull(),
    rulerOutcome: jsonb('ruler_outcome').notNull(),
    overridden: boolean('overridden').notNull(),
    createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
  },
  (table) => [unique().on(table.kingdomId, table.cycleNumber)],
);
```

- [ ] **Step 2: Generate and apply the migration**

Create `server/drizzle.config.ts`:

```typescript
import 'dotenv/config';
import { defineConfig } from 'drizzle-kit';

export default defineConfig({
  schema: './src/db/schema.ts',
  out: './drizzle',
  dialect: 'postgresql',
  dbCredentials: {
    url: process.env.DATABASE_URL!,
  },
});
```

Run:

```bash
cd "C:\Users\rajes\understudy-kingdom\server"
npm run db:generate
```

Expected: creates `server/drizzle/0000_<some_name>.sql` and a `server/drizzle/meta/` folder. Read the generated SQL file to confirm it contains `CREATE TABLE "kingdoms"`, `CREATE TABLE "ruler_npcs"`, `CREATE TABLE "decisions"`, and a `UNIQUE` constraint on `ruler_npcs."kingdom_id"` plus a composite unique constraint on `decisions("kingdom_id", "cycle_number")`.

Apply it to the real database:

```bash
npm run db:migrate
```

Expected: completes with no errors.

- [ ] **Step 3: Verify the tables actually exist**

```bash
cd "C:\Users\rajes\understudy-kingdom\server"
node --input-type=module -e "
import 'dotenv/config';
import { Pool } from 'pg';
const pool = new Pool({ connectionString: process.env.DATABASE_URL });
const result = await pool.query(\"SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name\");
console.log(result.rows);
await pool.end();
"
```

Expected: output includes `{ table_name: 'decisions' }`, `{ table_name: 'kingdoms' }`, `{ table_name: 'ruler_npcs' }`.

- [ ] **Step 4: Commit**

```bash
cd "C:\Users\rajes\understudy-kingdom"
git add server/src/db/schema.ts server/drizzle.config.ts server/drizzle/
git commit -m "feat: add kingdoms/ruler_npcs/decisions schema and migration

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_017JNi1ThZutdGUt6toaGBmq"
```

---

## Task 3: JWT verification (pure logic) + unit tests

> **Historical note:** the step-by-step content below (HS256-based test code and implementation) is superseded. The shipped implementation uses JWKS-based ES256 verification, not the HS256/shared-secret approach shown in this task's original steps — see the "Design Correction" section of `docs/superpowers/specs/2026-09-02-backend-service-design.md`. The content is left in place for historical context rather than deleted; see Task 5 below and the current `server/src/auth/verifyToken.ts` for what actually shipped.

**Files:**
- Create: `server/src/auth/verifyToken.ts`
- Create: `server/test/unit/verifyToken.test.ts`
- Create: `server/vitest.config.ts`

**Interfaces:**
- Consumes: nothing new (no DB, no network — pure function).
- Produces: `server/src/auth/verifyToken.ts` exports `verifySupabaseJwt(token: string, key: CryptoKey | JWTVerifyGetKey, options?: VerifySupabaseJwtOptions): Promise<{ userId: string }>` (throws `TokenVerificationError` on any invalid/expired/malformed/missing-subject token) and the `TokenVerificationError` class itself. `VerifySupabaseJwtOptions` is `{ issuer?: string; audience?: string }`. Task 5's auth plugin imports both: `import { verifySupabaseJwt, TokenVerificationError } from '../auth/verifyToken'`.

- [ ] **Step 1: Write the failing tests**

Create `server/vitest.config.ts`:

```typescript
import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'node',
    include: ['test/**/*.test.ts'],
    testTimeout: 15000,
  },
});
```

Create `server/test/unit/verifyToken.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { SignJWT } from 'jose';
import { verifySupabaseJwt, TokenVerificationError } from '../../src/auth/verifyToken';

const TEST_SECRET = 'test-secret-at-least-32-bytes-long-for-hs256!!';
const encodedSecret = new TextEncoder().encode(TEST_SECRET);

async function makeToken(overrides: { sub?: string; expiresInSeconds?: number } = {}): Promise<string> {
  const jwt = new SignJWT({})
    .setProtectedHeader({ alg: 'HS256' })
    .setSubject(overrides.sub ?? 'test-user-id')
    .setIssuedAt();

  const expiresInSeconds = overrides.expiresInSeconds ?? 3600;
  jwt.setExpirationTime(Math.floor(Date.now() / 1000) + expiresInSeconds);

  return jwt.sign(encodedSecret);
}

describe('verifySupabaseJwt', () => {
  it('accepts a validly signed, unexpired token and returns its userId', async () => {
    const token = await makeToken({ sub: 'user-123' });

    const result = await verifySupabaseJwt(token, TEST_SECRET);

    expect(result.userId).toBe('user-123');
  });

  it('rejects an expired token', async () => {
    const token = await makeToken({ expiresInSeconds: -60 });

    await expect(verifySupabaseJwt(token, TEST_SECRET)).rejects.toThrow(TokenVerificationError);
  });

  it('rejects a token signed with a different secret (tampered/wrong signature)', async () => {
    const wrongSecret = new TextEncoder().encode('a-completely-different-secret-32-bytes!');
    const token = await new SignJWT({})
      .setProtectedHeader({ alg: 'HS256' })
      .setSubject('user-123')
      .setIssuedAt()
      .setExpirationTime(Math.floor(Date.now() / 1000) + 3600)
      .sign(wrongSecret);

    await expect(verifySupabaseJwt(token, TEST_SECRET)).rejects.toThrow(TokenVerificationError);
  });

  it('rejects a malformed token string', async () => {
    await expect(verifySupabaseJwt('not-a-jwt', TEST_SECRET)).rejects.toThrow(TokenVerificationError);
  });

  it('rejects an empty token string', async () => {
    await expect(verifySupabaseJwt('', TEST_SECRET)).rejects.toThrow(TokenVerificationError);
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
cd "C:\Users\rajes\understudy-kingdom\server"
npm test -- test/unit/verifyToken.test.ts
```

Expected: fails because `server/src/auth/verifyToken.ts` doesn't exist yet — the log should show a module-resolution error, not individual assertion failures.

- [ ] **Step 3: Write the minimal implementation**

Create `server/src/auth/verifyToken.ts`:

```typescript
import { jwtVerify } from 'jose';

export class TokenVerificationError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'TokenVerificationError';
  }
}

export async function verifySupabaseJwt(token: string, secret: string): Promise<{ userId: string }> {
  if (!token) {
    throw new TokenVerificationError('Token is empty');
  }

  try {
    const { payload } = await jwtVerify(token, new TextEncoder().encode(secret));

    if (typeof payload.sub !== 'string' || payload.sub.length === 0) {
      throw new TokenVerificationError('Token is missing a subject claim');
    }

    return { userId: payload.sub };
  } catch (err) {
    if (err instanceof TokenVerificationError) {
      throw err;
    }
    throw new TokenVerificationError('Token is invalid or expired');
  }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
cd "C:\Users\rajes\understudy-kingdom\server"
npm test -- test/unit/verifyToken.test.ts
```

Expected: exit code 0, all 5 tests pass.

- [ ] **Step 5: Commit**

```bash
cd "C:\Users\rajes\understudy-kingdom"
git add server/src/auth/verifyToken.ts server/test/unit/verifyToken.test.ts server/vitest.config.ts
git commit -m "feat: add pure JWT verification logic with unit tests

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_017JNi1ThZutdGUt6toaGBmq"
```

---

## Task 4: Fastify app skeleton + health route

**Files:**
- Create: `server/src/app.ts`
- Create: `server/test/unit/health.test.ts`

**Interfaces:**
- Consumes: nothing new.
- Produces: `server/src/app.ts` exports `buildApp(): FastifyInstance` — builds and returns a Fastify instance with `GET /health` registered, without calling `.listen()`. Task 5, 6, and 7 all import this: `import { buildApp } from '../src/app'` (tests) or `import { buildApp } from './app'` (Task 7's `server.ts`, same folder).

- [ ] **Step 1: Write the failing test**

Create `server/test/unit/health.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { buildApp } from '../../src/app';

describe('GET /health', () => {
  it('returns 200 with status ok, no authentication required', async () => {
    const app = buildApp();

    const response = await app.inject({ method: 'GET', url: '/health' });

    expect(response.statusCode).toBe(200);
    expect(response.json()).toEqual({ status: 'ok' });
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
cd "C:\Users\rajes\understudy-kingdom\server"
npm test -- test/unit/health.test.ts
```

Expected: fails — `server/src/app.ts` doesn't exist yet.

- [ ] **Step 3: Write the minimal implementation**

Create `server/src/app.ts`:

```typescript
import Fastify, { FastifyInstance } from 'fastify';

export function buildApp(): FastifyInstance {
  const app = Fastify({ logger: false });

  // Any error a route handler doesn't explicitly reply to (a thrown
  // exception, a DB failure) lands here. 4xx errors set by routes/schema
  // validation pass through with their own message; everything else is
  // logged server-side only and replies with a generic message -- never
  // leak internals (query text, stack traces) to the client. See
  // docs/superpowers/specs/2026-09-02-backend-service-design.md's Error
  // Handling section.
  app.setErrorHandler((error, _request, reply) => {
    if (error.statusCode && error.statusCode < 500) {
      reply.code(error.statusCode).send({ error: error.message });
      return;
    }

    console.error(error);
    reply.code(500).send({ error: 'Internal server error' });
  });

  app.get('/health', async () => {
    return { status: 'ok' };
  });

  return app;
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
cd "C:\Users\rajes\understudy-kingdom\server"
npm test -- test/unit/health.test.ts
```

Expected: exit code 0, 1 test passes.

- [ ] **Step 5: Commit**

```bash
cd "C:\Users\rajes\understudy-kingdom"
git add server/src/app.ts server/test/unit/health.test.ts
git commit -m "feat: add Fastify app skeleton with health route

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_017JNi1ThZutdGUt6toaGBmq"
```

---

## Task 5: Auth plugin + kingdoms routes + integration tests

**Files:**
- Create: `server/src/auth/authPlugin.ts`
- Create: `server/src/routes/kingdoms.ts`
- Modify: `server/src/app.ts`
- Create: `server/test/integration/helpers/testUser.ts`
- Create: `server/test/integration/helpers/db.ts`
- Create: `server/test/integration/kingdoms.test.ts`

**Interfaces:**
- Consumes: `verifySupabaseJwt`/`TokenVerificationError` (Task 3), `db` (Task 1), `kingdoms`/`rulerNpcs` (Task 2), `buildApp` (Task 4, modified here).
- Produces: `server/src/auth/authPlugin.ts` default-exports a Fastify plugin (wrapped with `fastify-plugin`) that adds an `onRequest` hook setting `request.userId` (a new `string` property on `FastifyRequest`, declared via TypeScript module augmentation in this file) or replying `401`. `server/test/integration/helpers/testUser.ts` exports `createTestUser(): Promise<{ userId: string; jwt: string }>`. `server/test/integration/helpers/db.ts` exports `truncateTables(): Promise<void>`. Task 6 imports both helpers with the same signatures.

- [ ] **Step 1: Write the failing integration tests**

Create `server/test/integration/helpers/testUser.ts`:

```typescript
import { createClient } from '@supabase/supabase-js';

export async function createTestUser(): Promise<{ userId: string; jwt: string }> {
  const supabaseUrl = process.env.SUPABASE_URL;
  const supabaseAnonKey = process.env.SUPABASE_ANON_KEY;

  if (!supabaseUrl || !supabaseAnonKey) {
    throw new Error('SUPABASE_URL and SUPABASE_ANON_KEY must be set in server/.env to run integration tests');
  }

  const client = createClient(supabaseUrl, supabaseAnonKey);
  const { data, error } = await client.auth.signInAnonymously();

  if (error || !data.session || !data.user) {
    throw new Error(
      `Failed to create anonymous test user: ${error?.message ?? 'no session returned'}. ` +
        'Confirm "Allow anonymous sign-ins" is enabled in Supabase Authentication settings.',
    );
  }

  return { userId: data.user.id, jwt: data.session.access_token };
}
```

Create `server/test/integration/helpers/db.ts`:

```typescript
import { sql } from 'drizzle-orm';
import { db } from '../../../src/db/client';

export async function truncateTables(): Promise<void> {
  await db.execute(sql`TRUNCATE TABLE decisions, ruler_npcs, kingdoms RESTART IDENTITY CASCADE`);
}
```

Create `server/test/integration/kingdoms.test.ts`:

```typescript
import { describe, it, expect, beforeAll, afterEach } from 'vitest';
import { buildApp } from '../../src/app';
import { createTestUser } from './helpers/testUser';
import { truncateTables } from './helpers/db';

describe('kingdoms routes', () => {
  const app = buildApp();
  let jwt: string;

  beforeAll(async () => {
    const user = await createTestUser();
    jwt = user.jwt;
  });

  afterEach(async () => {
    await truncateTables();
  });

  it('POST /api/v1/kingdoms creates a new kingdom and ruler on first call', async () => {
    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/kingdoms',
      headers: { authorization: `Bearer ${jwt}` },
    });

    expect(response.statusCode).toBe(201);
    const body = response.json();
    expect(body.kingdom.id).toBeDefined();
    expect(body.rulerNpc.kingdomId).toBe(body.kingdom.id);
    expect(body.rulerNpc.mood).toBe(50);
  });

  it('POST /api/v1/kingdoms is idempotent -- returns the existing kingdom on a second call', async () => {
    const first = await app.inject({
      method: 'POST',
      url: '/api/v1/kingdoms',
      headers: { authorization: `Bearer ${jwt}` },
    });
    const second = await app.inject({
      method: 'POST',
      url: '/api/v1/kingdoms',
      headers: { authorization: `Bearer ${jwt}` },
    });

    expect(second.statusCode).toBe(200);
    expect(second.json().kingdom.id).toBe(first.json().kingdom.id);
  });

  it('GET /api/v1/kingdoms/me returns 404 when no kingdom exists yet', async () => {
    const response = await app.inject({
      method: 'GET',
      url: '/api/v1/kingdoms/me',
      headers: { authorization: `Bearer ${jwt}` },
    });

    expect(response.statusCode).toBe(404);
  });

  it('GET /api/v1/kingdoms/me returns the kingdom after one has been created', async () => {
    await app.inject({
      method: 'POST',
      url: '/api/v1/kingdoms',
      headers: { authorization: `Bearer ${jwt}` },
    });

    const response = await app.inject({
      method: 'GET',
      url: '/api/v1/kingdoms/me',
      headers: { authorization: `Bearer ${jwt}` },
    });

    expect(response.statusCode).toBe(200);
    expect(response.json().kingdom.userId).toBeDefined();
  });

  it('rejects requests with no Authorization header', async () => {
    const response = await app.inject({ method: 'GET', url: '/api/v1/kingdoms/me' });

    expect(response.statusCode).toBe(401);
  });

  it('rejects requests with a malformed Authorization header', async () => {
    const response = await app.inject({
      method: 'GET',
      url: '/api/v1/kingdoms/me',
      headers: { authorization: 'NotBearer sometoken' },
    });

    expect(response.statusCode).toBe(401);
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
cd "C:\Users\rajes\understudy-kingdom\server"
npm test -- test/integration/kingdoms.test.ts
```

Expected: fails at import/collection time — `server/src/routes/kingdoms.ts` doesn't exist yet, and `/api/v1/kingdoms` isn't registered on the app built by the current `buildApp()`.

- [ ] **Step 3: Write the minimal implementation**

Create `server/src/auth/authPlugin.ts` (uses JWKS-based verification, not a shared secret — see the "Design Correction" section of `docs/superpowers/specs/2026-09-02-backend-service-design.md`; `jose`'s `createRemoteJWKSet` resolves and caches Supabase's current ES256 public key from `{SUPABASE_URL}/auth/v1/.well-known/jwks.json`):

```typescript
import fp from 'fastify-plugin';
import { FastifyPluginAsync } from 'fastify';
import { createRemoteJWKSet } from 'jose';
import { verifySupabaseJwt, TokenVerificationError } from './verifyToken';

declare module 'fastify' {
  interface FastifyRequest {
    userId: string;
  }
}

function getJwks() {
  const supabaseUrl = process.env.SUPABASE_URL;
  if (!supabaseUrl) {
    throw new Error('SUPABASE_URL is not configured');
  }
  return createRemoteJWKSet(new URL('/auth/v1/.well-known/jwks.json', supabaseUrl));
}

const authPlugin: FastifyPluginAsync = async (fastify) => {
  const jwks = getJwks();

  fastify.addHook('onRequest', async (request, reply) => {
    const authHeader = request.headers.authorization;

    if (!authHeader || !authHeader.startsWith('Bearer ')) {
      reply.code(401).send({ error: 'Missing or invalid Authorization header' });
      return reply;
    }

    const token = authHeader.slice('Bearer '.length);

    try {
      const { userId } = await verifySupabaseJwt(token, jwks);
      request.userId = userId;
    } catch (err) {
      if (err instanceof TokenVerificationError) {
        reply.code(401).send({ error: 'Invalid or expired token' });
        return reply;
      }
      throw err;
    }
  });
};

export default fp(authPlugin);
```

(The shipped version of this file also distinguishes JWKS-infrastructure failures — the endpoint being unreachable or timing out — from bad-token failures, replying `503` rather than `401` for the former, and builds the JWKS getter lazily on first request rather than at module load. See the actual `server/src/auth/authPlugin.ts` for the current implementation rather than treating this snippet as authoritative.)

Create `server/src/routes/kingdoms.ts`:

```typescript
import { FastifyPluginAsync } from 'fastify';
import { eq } from 'drizzle-orm';
import { db } from '../db/client';
import { kingdoms, rulerNpcs } from '../db/schema';

const kingdomsRoutes: FastifyPluginAsync = async (fastify) => {
  fastify.post('/api/v1/kingdoms', async (request, reply) => {
    const existingRows = await db.select().from(kingdoms).where(eq(kingdoms.userId, request.userId)).limit(1);

    if (existingRows.length > 0) {
      const kingdom = existingRows[0];
      const rulerRows = await db.select().from(rulerNpcs).where(eq(rulerNpcs.kingdomId, kingdom.id)).limit(1);
      reply.code(200);
      return { kingdom, rulerNpc: rulerRows[0] };
    }

    const [kingdom] = await db.insert(kingdoms).values({ userId: request.userId }).returning();
    const [rulerNpc] = await db.insert(rulerNpcs).values({ kingdomId: kingdom.id }).returning();

    reply.code(201);
    return { kingdom, rulerNpc };
  });

  fastify.get('/api/v1/kingdoms/me', async (request, reply) => {
    const rows = await db.select().from(kingdoms).where(eq(kingdoms.userId, request.userId)).limit(1);

    if (rows.length === 0) {
      reply.code(404);
      return { error: 'No kingdom found for this user' };
    }

    const kingdom = rows[0];
    const rulerRows = await db.select().from(rulerNpcs).where(eq(rulerNpcs.kingdomId, kingdom.id)).limit(1);

    return { kingdom, rulerNpc: rulerRows[0] };
  });
};

export default kingdomsRoutes;
```

Modify `server/src/app.ts` to (adds the `dotenv/config` import needed once route handlers read `process.env`, and registers `kingdomsRoutes` inside the authenticated child scope — the error handler and `/health` route from Task 4 are unchanged, keep them):

```typescript
import 'dotenv/config';
import Fastify, { FastifyInstance } from 'fastify';
import authPlugin from './auth/authPlugin';
import kingdomsRoutes from './routes/kingdoms';

export function buildApp(): FastifyInstance {
  const app = Fastify({ logger: false });

  app.setErrorHandler((error, _request, reply) => {
    if (error.statusCode && error.statusCode < 500) {
      reply.code(error.statusCode).send({ error: error.message });
      return;
    }

    console.error(error);
    reply.code(500).send({ error: 'Internal server error' });
  });

  app.get('/health', async () => {
    return { status: 'ok' };
  });

  app.register(async (protectedRoutes) => {
    await protectedRoutes.register(authPlugin);
    await protectedRoutes.register(kingdomsRoutes);
  });

  return app;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
cd "C:\Users\rajes\understudy-kingdom\server"
npm test -- test/integration/kingdoms.test.ts
```

Expected: exit code 0, all 6 tests pass. This makes real network calls to Supabase (creating an anonymous auth user, real JWKS fetch, then real Postgres queries) — if it hangs or times out, check `server/.env` has the correct `SUPABASE_URL`/`SUPABASE_ANON_KEY` and that anonymous sign-ins are enabled (Task 1, Step 1).

- [ ] **Step 5: Run the full test suite so far to confirm no regression**

```bash
cd "C:\Users\rajes\understudy-kingdom\server"
npm test
```

Expected: exit code 0, all tests across `test/unit/` and `test/integration/` pass (5 + 1 + 6 = 12 at this point).

- [ ] **Step 6: Commit**

```bash
cd "C:\Users\rajes\understudy-kingdom"
git add server/src/auth/authPlugin.ts server/src/routes/kingdoms.ts server/src/app.ts server/test/integration/
git commit -m "feat: add JWT auth plugin and kingdoms routes with integration tests

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_017JNi1ThZutdGUt6toaGBmq"
```

---

## Task 6: Decisions routes + integration tests

**Files:**
- Create: `server/src/routes/decisions.ts`
- Modify: `server/src/app.ts`
- Create: `server/test/integration/decisions.test.ts`

**Interfaces:**
- Consumes: `db` (Task 1), `kingdoms`/`decisions` (Task 2), `createTestUser`/`truncateTables` (Task 5), `buildApp` (Task 4/5, modified here).
- Produces: nothing new consumed by later tasks (Task 7 only needs `buildApp`, already available).

- [ ] **Step 1: Write the failing integration tests**

Create `server/test/integration/decisions.test.ts`:

```typescript
import { describe, it, expect, beforeAll, afterEach } from 'vitest';
import { buildApp } from '../../src/app';
import { createTestUser } from './helpers/testUser';
import { truncateTables } from './helpers/db';

describe('decisions routes', () => {
  const app = buildApp();
  let jwt: string;

  beforeAll(async () => {
    const user = await createTestUser();
    jwt = user.jwt;
  });

  afterEach(async () => {
    await truncateTables();
  });

  async function createKingdom(): Promise<void> {
    await app.inject({
      method: 'POST',
      url: '/api/v1/kingdoms',
      headers: { authorization: `Bearer ${jwt}` },
    });
  }

  it('POST /api/v1/decisions returns 404 if the caller has no kingdom yet', async () => {
    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${jwt}` },
      payload: {
        cycle_number: 1,
        player_recommendation: { army: 40, trade: 30, religion: 30 },
        ruler_outcome: {},
        overridden: false,
      },
    });

    expect(response.statusCode).toBe(404);
  });

  it('POST /api/v1/decisions records a decision', async () => {
    await createKingdom();

    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${jwt}` },
      payload: {
        cycle_number: 1,
        player_recommendation: { army: 40, trade: 30, religion: 30 },
        ruler_outcome: { mood: 55 },
        overridden: false,
      },
    });

    expect(response.statusCode).toBe(201);
    expect(response.json().decision.cycleNumber).toBe(1);
  });

  it('POST /api/v1/decisions returns 409 for a duplicate cycle_number', async () => {
    await createKingdom();
    const payload = { cycle_number: 1, player_recommendation: {}, ruler_outcome: {}, overridden: false };

    await app.inject({ method: 'POST', url: '/api/v1/decisions', headers: { authorization: `Bearer ${jwt}` }, payload });
    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${jwt}` },
      payload,
    });

    expect(response.statusCode).toBe(409);
  });

  it('POST /api/v1/decisions returns 400 for a malformed body', async () => {
    await createKingdom();

    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${jwt}` },
      payload: { cycle_number: 'not-a-number' },
    });

    expect(response.statusCode).toBe(400);
  });

  it('GET /api/v1/decisions returns this user\'s decisions newest-first', async () => {
    await createKingdom();
    for (let cycle = 1; cycle <= 3; cycle++) {
      await app.inject({
        method: 'POST',
        url: '/api/v1/decisions',
        headers: { authorization: `Bearer ${jwt}` },
        payload: { cycle_number: cycle, player_recommendation: {}, ruler_outcome: {}, overridden: false },
      });
    }

    const response = await app.inject({
      method: 'GET',
      url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${jwt}` },
    });

    expect(response.statusCode).toBe(200);
    const body = response.json();
    expect(body.decisions).toHaveLength(3);
    expect(body.decisions[0].cycleNumber).toBe(3);
    expect(body.decisions[2].cycleNumber).toBe(1);
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
cd "C:\Users\rajes\understudy-kingdom\server"
npm test -- test/integration/decisions.test.ts
```

Expected: fails — `server/src/routes/decisions.ts` doesn't exist yet, `/api/v1/decisions` isn't registered.

- [ ] **Step 3: Write the minimal implementation**

Create `server/src/routes/decisions.ts`:

```typescript
import { FastifyPluginAsync } from 'fastify';
import { and, desc, eq, lt } from 'drizzle-orm';
import { db } from '../db/client';
import { kingdoms, decisions } from '../db/schema';

const createDecisionSchema = {
  body: {
    type: 'object',
    required: ['cycle_number', 'player_recommendation', 'ruler_outcome', 'overridden'],
    properties: {
      cycle_number: { type: 'integer' },
      player_recommendation: { type: 'object' },
      ruler_outcome: { type: 'object' },
      overridden: { type: 'boolean' },
    },
  },
} as const;

interface CreateDecisionBody {
  cycle_number: number;
  player_recommendation: unknown;
  ruler_outcome: unknown;
  overridden: boolean;
}

const decisionsRoutes: FastifyPluginAsync = async (fastify) => {
  fastify.post<{ Body: CreateDecisionBody }>(
    '/api/v1/decisions',
    { schema: createDecisionSchema },
    async (request, reply) => {
      const kingdomRows = await db.select().from(kingdoms).where(eq(kingdoms.userId, request.userId)).limit(1);
      if (kingdomRows.length === 0) {
        reply.code(404);
        return { error: 'No kingdom found for this user' };
      }
      const kingdom = kingdomRows[0];

      const existing = await db
        .select()
        .from(decisions)
        .where(and(eq(decisions.kingdomId, kingdom.id), eq(decisions.cycleNumber, request.body.cycle_number)))
        .limit(1);

      if (existing.length > 0) {
        reply.code(409);
        return { error: 'This cycle_number already has a recorded decision' };
      }

      const [decision] = await db
        .insert(decisions)
        .values({
          kingdomId: kingdom.id,
          cycleNumber: request.body.cycle_number,
          playerRecommendation: request.body.player_recommendation,
          rulerOutcome: request.body.ruler_outcome,
          overridden: request.body.overridden,
        })
        .returning();

      reply.code(201);
      return { decision };
    },
  );

  fastify.get<{ Querystring: { cursor?: string; limit?: string } }>(
    '/api/v1/decisions',
    async (request, reply) => {
      const limit = Math.min(Math.max(parseInt(request.query.limit ?? '20', 10) || 20, 1), 100);

      const kingdomRows = await db.select().from(kingdoms).where(eq(kingdoms.userId, request.userId)).limit(1);
      if (kingdomRows.length === 0) {
        reply.code(404);
        return { error: 'No kingdom found for this user' };
      }
      const kingdom = kingdomRows[0];

      const conditions = [eq(decisions.kingdomId, kingdom.id)];
      if (request.query.cursor) {
        conditions.push(lt(decisions.createdAt, new Date(request.query.cursor)));
      }

      const rows = await db
        .select()
        .from(decisions)
        .where(and(...conditions))
        .orderBy(desc(decisions.createdAt))
        .limit(limit);

      const nextCursor = rows.length === limit ? rows[rows.length - 1].createdAt.toISOString() : null;

      return { decisions: rows, nextCursor };
    },
  );
};

export default decisionsRoutes;
```

Modify `server/src/app.ts` to register the new route (error handler and `/health` unchanged from Task 4, keep them):

```typescript
import 'dotenv/config';
import Fastify, { FastifyInstance } from 'fastify';
import authPlugin from './auth/authPlugin';
import kingdomsRoutes from './routes/kingdoms';
import decisionsRoutes from './routes/decisions';

export function buildApp(): FastifyInstance {
  const app = Fastify({ logger: false });

  app.setErrorHandler((error, _request, reply) => {
    if (error.statusCode && error.statusCode < 500) {
      reply.code(error.statusCode).send({ error: error.message });
      return;
    }

    console.error(error);
    reply.code(500).send({ error: 'Internal server error' });
  });

  app.get('/health', async () => {
    return { status: 'ok' };
  });

  app.register(async (protectedRoutes) => {
    await protectedRoutes.register(authPlugin);
    await protectedRoutes.register(kingdomsRoutes);
    await protectedRoutes.register(decisionsRoutes);
  });

  return app;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
cd "C:\Users\rajes\understudy-kingdom\server"
npm test -- test/integration/decisions.test.ts
```

Expected: exit code 0, all 5 tests pass.

- [ ] **Step 5: Run the full test suite to confirm no regression**

```bash
cd "C:\Users\rajes\understudy-kingdom\server"
npm test
```

Expected: exit code 0, all tests pass (12 from before + 5 new = 17).

- [ ] **Step 6: Commit**

```bash
cd "C:\Users\rajes\understudy-kingdom"
git add server/src/routes/decisions.ts server/src/app.ts server/test/integration/decisions.test.ts
git commit -m "feat: add decisions routes with integration tests

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_017JNi1ThZutdGUt6toaGBmq"
```

---

## Task 7: Server entrypoint + manual end-to-end verification

**Files:**
- Create: `server/src/server.ts`

**Interfaces:**
- Consumes: `buildApp` (Task 4-6).
- Produces: a runnable entrypoint. Nothing else consumes this (final task).

- [ ] **Step 1: Write the entrypoint**

Create `server/src/server.ts`:

```typescript
import 'dotenv/config';
import { buildApp } from './app';

const app = buildApp();
const port = Number(process.env.PORT) || 3000;

app
  .listen({ port, host: '0.0.0.0' })
  .then(() => {
    console.log(`Server listening on port ${port}`);
  })
  .catch((err) => {
    console.error(err);
    process.exit(1);
  });
```

- [ ] **Step 2: Start the server and verify it's genuinely reachable over real HTTP**

```bash
cd "C:\Users\rajes\understudy-kingdom\server"
npm start &
sleep 2
curl -s http://localhost:3000/health
echo ""
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:3000/api/v1/kingdoms/me
```

Expected: first curl prints `{"status":"ok"}`; second prints `401` (no Authorization header, matches the auth plugin's behavior). This is the plan's genuine end-to-end check — passing tests alone don't prove the service is actually runnable as a standalone process; this does.

Stop the server:

```bash
kill %1
```

- [ ] **Step 3: Run the complete test suite one final time**

```bash
cd "C:\Users\rajes\understudy-kingdom\server"
npm test
```

Expected: exit code 0, 17/17 tests passing.

- [ ] **Step 4: Commit**

```bash
cd "C:\Users\rajes\understudy-kingdom"
git add server/src/server.ts
git commit -m "feat: add server entrypoint

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_017JNi1ThZutdGUt6toaGBmq"
```
