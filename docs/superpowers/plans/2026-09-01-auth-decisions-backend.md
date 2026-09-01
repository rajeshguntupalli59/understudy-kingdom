# Auth + Decisions Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement user authentication (Google/Apple OAuth + anonymous device-id fallback) and the `/api/v1/decisions` endpoint as real, running Fastify server code with a real Postgres-backed test suite.

**Architecture:** Fastify server with Knex-backed Postgres persistence. Auth issues short-lived JWT access tokens + longer-lived refresh tokens. Decision-cycle scoring logic stays client-authoritative for this pass (per the design spec) — this endpoint records outcomes, it does not compute them.

**Tech Stack:** Node.js (ESM, plain JavaScript — no TypeScript build step, to minimize toolchain risk; Fastify's JSON-schema validation covers most of the type-safety benefit without a compile step), Fastify, Knex, `pg`, `jsonwebtoken`, `bcrypt`, `google-auth-library`, `jose` (for Apple JWKS verification), Vitest for tests.

## Global Constraints

- All new code lives under `backend/` in this repo (monorepo alongside the Unity client), per the design spec's scope decision.
- Tables implemented: exactly `users`, `kingdoms`, `ruler_npcs`, `decisions` — matching `docs/PROJECT_PLAN.md` §6's DDL. Do not create `councils`/`events`/`pvp_duels`/`purchases` tables in this plan — out of scope.
- Auth middleware attaches `request.userId` on success; every protected route reads it from there, never re-verifies the token itself.
- `kingdom_id` ownership must be checked with a 403 (not 404) on mismatch — never leak whether a kingdom exists to a caller who doesn't own it.
- OAuth verification (Google/Apple) must be dependency-injected (a `verifier` parameter with a real default), so tests can supply a fake verifier instead of making live network calls to Google/Apple during the test suite.
- **This backend CAN be tested for real** — Postgres 16 is running locally (`understudy_kingdom_test` database, role `understudy_kingdom` / password `devpassword`), Node 22 and npm are available. Every "run tests" step in this plan must actually be run, with real output pasted into the task's report — this is a hard departure from the Unity client plan, where hand-tracing was the only option. A report that hand-traces instead of running `npm test` for this plan is a plan violation, not an acceptable substitute.

---

### Task 1: Backend scaffolding + Knex migrations

**Files:**
- Create: `backend/package.json`
- Create: `backend/knexfile.js`
- Create: `backend/src/db/migrations/20260901000001_create_users.js`
- Create: `backend/src/db/migrations/20260901000002_create_kingdoms.js`
- Create: `backend/src/db/migrations/20260901000003_create_ruler_npcs.js`
- Create: `backend/src/db/migrations/20260901000004_create_decisions.js`
- Create: `backend/src/db/knex.js`
- Create: `backend/.env.example`
- Create: `backend/test/migrations.test.js`

**Interfaces:**
- Produces: `backend/src/db/knex.js` exports a default Knex instance configured from env vars (`DATABASE_URL` or discrete `PGHOST`/`PGUSER`/etc). Later tasks import this for all DB access.

- [ ] **Step 1: Create package.json**

```json
{
  "name": "understudy-kingdom-backend",
  "version": "0.1.0",
  "type": "module",
  "scripts": {
    "test": "vitest run",
    "migrate": "knex migrate:latest",
    "migrate:rollback": "knex migrate:rollback"
  },
  "dependencies": {
    "fastify": "^5.1.0",
    "knex": "^3.1.0",
    "pg": "^8.13.1",
    "jsonwebtoken": "^9.0.2",
    "bcrypt": "^5.1.1",
    "google-auth-library": "^9.15.0",
    "jose": "^5.9.6",
    "dotenv": "^16.4.7"
  },
  "devDependencies": {
    "vitest": "^2.1.8"
  }
}
```

- [ ] **Step 2: Create knexfile.js**

```javascript
import 'dotenv/config';

const base = {
  client: 'pg',
  migrations: { directory: './src/db/migrations' },
};

export default {
  development: {
    ...base,
    connection: process.env.DATABASE_URL
      || 'postgres://understudy_kingdom:devpassword@localhost:5432/understudy_kingdom_dev',
  },
  test: {
    ...base,
    connection: process.env.TEST_DATABASE_URL
      || 'postgres://understudy_kingdom:devpassword@localhost:5432/understudy_kingdom_test',
  },
};
```

- [ ] **Step 3: Create backend/.env.example**

```
DATABASE_URL=postgres://understudy_kingdom:devpassword@localhost:5432/understudy_kingdom_dev
TEST_DATABASE_URL=postgres://understudy_kingdom:devpassword@localhost:5432/understudy_kingdom_test
JWT_ACCESS_SECRET=change-me-in-production
JWT_REFRESH_SECRET=change-me-too
GOOGLE_CLIENT_ID=your-google-oauth-client-id
```

- [ ] **Step 4: Create backend/src/db/knex.js**

```javascript
import 'dotenv/config';
import knexLib from 'knex';
import config from '../../knexfile.js';

const env = process.env.NODE_ENV === 'test' ? 'test' : 'development';

export default knexLib(config[env]);
```

- [ ] **Step 5: Write the four migration files**

`backend/src/db/migrations/20260901000001_create_users.js`:
```javascript
export function up(knex) {
  return knex.schema.createTable('users', (table) => {
    table.uuid('id').primary().defaultTo(knex.raw('gen_random_uuid()'));
    table.string('device_id').unique();
    table.string('device_secret_hash');
    table.string('google_sub').unique();
    table.string('apple_sub').unique();
    table.string('email');
    table.timestamp('created_at').defaultTo(knex.fn.now());
    table.string('country_code');
  });
}

export function down(knex) {
  return knex.schema.dropTable('users');
}
```

`backend/src/db/migrations/20260901000002_create_kingdoms.js`:
```javascript
export function up(knex) {
  return knex.schema.createTable('kingdoms', (table) => {
    table.uuid('id').primary().defaultTo(knex.raw('gen_random_uuid()'));
    table.uuid('user_id').notNullable().references('id').inTable('users');
    table.timestamp('founded_at').defaultTo(knex.fn.now());
  });
}

export function down(knex) {
  return knex.schema.dropTable('kingdoms');
}
```

`backend/src/db/migrations/20260901000003_create_ruler_npcs.js`:
```javascript
export function up(knex) {
  return knex.schema.createTable('ruler_npcs', (table) => {
    table.uuid('id').primary().defaultTo(knex.raw('gen_random_uuid()'));
    table.uuid('kingdom_id').notNullable().references('id').inTable('kingdoms');
    table.integer('mood').notNullable().defaultTo(50);
    table.integer('loyalty').notNullable().defaultTo(50);
    table.string('agenda').notNullable().defaultTo('Expansionist');
    table.integer('trait_seed');
  });
}

export function down(knex) {
  return knex.schema.dropTable('ruler_npcs');
}
```

`backend/src/db/migrations/20260901000004_create_decisions.js`:
```javascript
export function up(knex) {
  return knex.schema.createTable('decisions', (table) => {
    table.uuid('id').primary().defaultTo(knex.raw('gen_random_uuid()'));
    table.uuid('kingdom_id').notNullable().references('id').inTable('kingdoms');
    table.integer('cycle_number').notNullable();
    table.jsonb('player_recommendation').notNullable();
    table.jsonb('ruler_outcome').notNullable();
    table.boolean('overridden').notNullable();
    table.timestamp('created_at').defaultTo(knex.fn.now());
    table.unique(['kingdom_id', 'cycle_number']);
  });
}

export function down(knex) {
  return knex.schema.dropTable('decisions');
}
```

- [ ] **Step 6: Install dependencies and run migrations against the real test DB**

```bash
cd backend && npm install
NODE_ENV=test npx knex migrate:latest
```
Expected: `npm install` completes without error; migration output lists 4 batch entries (users, kingdoms, ruler_npcs, decisions) applied.

- [ ] **Step 7: Write and run a real migration test**

Create `backend/test/migrations.test.js`:
```javascript
import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import knex from '../src/db/knex.js';

describe('migrations', () => {
  afterAll(async () => {
    await knex.destroy();
  });

  it('creates the users, kingdoms, ruler_npcs, and decisions tables', async () => {
    for (const table of ['users', 'kingdoms', 'ruler_npcs', 'decisions']) {
      const exists = await knex.schema.hasTable(table);
      expect(exists).toBe(true);
    }
  });

  it('enforces one decision per kingdom per cycle_number', async () => {
    const [user] = await knex('users').insert({ device_id: 'test-device-1' }).returning('id');
    const [kingdom] = await knex('kingdoms').insert({ user_id: user.id }).returning('id');
    await knex('decisions').insert({
      kingdom_id: kingdom.id, cycle_number: 1,
      player_recommendation: {}, ruler_outcome: {}, overridden: false,
    });
    await expect(
      knex('decisions').insert({
        kingdom_id: kingdom.id, cycle_number: 1,
        player_recommendation: {}, ruler_outcome: {}, overridden: false,
      })
    ).rejects.toThrow();
  });
});
```

Run: `cd backend && NODE_ENV=test npx vitest run test/migrations.test.js`
Expected: **REAL** output, 2 tests passing. Paste the actual terminal output into your report — this is not a hand-trace step.

- [ ] **Step 8: Commit**

```bash
git add backend/package.json backend/package-lock.json backend/knexfile.js backend/.env.example \
  backend/src/db/knex.js backend/src/db/migrations backend/test/migrations.test.js
git commit -m "feat: scaffold backend, add Knex migrations for users/kingdoms/ruler_npcs/decisions"
```

---

### Task 2: Auth core module (JWT + device-secret verification, no HTTP yet)

**Files:**
- Create: `backend/src/auth/tokens.js`
- Create: `backend/src/auth/deviceAuth.js`
- Create: `backend/src/auth/middleware.js`
- Create: `backend/test/auth/tokens.test.js`
- Create: `backend/test/auth/deviceAuth.test.js`

**Interfaces:**
- Produces: `issueTokenPair(userId)` → `{accessToken, refreshToken}`. `verifyAccessToken(token)` → `{userId}` or throws. `verifyRefreshToken(token)` → `{userId}` or throws. `hashDeviceSecret(secret)` → bcrypt hash string. `verifyDeviceSecret(secret, hash)` → boolean. `authMiddleware` — a Fastify `preHandler` function, attaches `request.userId`, replies 401 on failure.

- [ ] **Step 1: Write failing tests for tokens.js**

`backend/test/auth/tokens.test.js`:
```javascript
import { describe, it, expect } from 'vitest';
import { issueTokenPair, verifyAccessToken, verifyRefreshToken } from '../../src/auth/tokens.js';

describe('tokens', () => {
  it('issues an access token that verifies back to the same userId', () => {
    const { accessToken } = issueTokenPair('user-123');
    const result = verifyAccessToken(accessToken);
    expect(result.userId).toBe('user-123');
  });

  it('issues a refresh token that verifies back to the same userId', () => {
    const { refreshToken } = issueTokenPair('user-123');
    const result = verifyRefreshToken(refreshToken);
    expect(result.userId).toBe('user-123');
  });

  it('rejects an access token passed to verifyRefreshToken', () => {
    const { accessToken } = issueTokenPair('user-123');
    expect(() => verifyRefreshToken(accessToken)).toThrow();
  });
});
```

Run: `cd backend && npx vitest run test/auth/tokens.test.js`
Expected: fails — `tokens.js` doesn't exist yet.

- [ ] **Step 2: Implement tokens.js**

```javascript
import jwt from 'jsonwebtoken';

const ACCESS_SECRET = process.env.JWT_ACCESS_SECRET || 'dev-access-secret';
const REFRESH_SECRET = process.env.JWT_REFRESH_SECRET || 'dev-refresh-secret';
const ACCESS_EXPIRES_IN = '15m';
const REFRESH_EXPIRES_IN = '30d';

export function issueTokenPair(userId) {
  const accessToken = jwt.sign({ userId, type: 'access' }, ACCESS_SECRET, { expiresIn: ACCESS_EXPIRES_IN });
  const refreshToken = jwt.sign({ userId, type: 'refresh' }, REFRESH_SECRET, { expiresIn: REFRESH_EXPIRES_IN });
  return { accessToken, refreshToken };
}

export function verifyAccessToken(token) {
  const payload = jwt.verify(token, ACCESS_SECRET);
  if (payload.type !== 'access') throw new Error('not an access token');
  return { userId: payload.userId };
}

export function verifyRefreshToken(token) {
  const payload = jwt.verify(token, REFRESH_SECRET);
  if (payload.type !== 'refresh') throw new Error('not a refresh token');
  return { userId: payload.userId };
}
```

- [ ] **Step 3: Run tokens.test.js again, confirm real pass**

Run: `cd backend && npx vitest run test/auth/tokens.test.js`
Expected: 3/3 passing. Paste real output.

- [ ] **Step 4: Write failing tests for deviceAuth.js**

`backend/test/auth/deviceAuth.test.js`:
```javascript
import { describe, it, expect } from 'vitest';
import { hashDeviceSecret, verifyDeviceSecret } from '../../src/auth/deviceAuth.js';

describe('deviceAuth', () => {
  it('hashes a secret and verifies the same secret against it', async () => {
    const hash = await hashDeviceSecret('my-secret-123');
    const result = await verifyDeviceSecret('my-secret-123', hash);
    expect(result).toBe(true);
  });

  it('rejects a wrong secret against a hash', async () => {
    const hash = await hashDeviceSecret('my-secret-123');
    const result = await verifyDeviceSecret('wrong-secret', hash);
    expect(result).toBe(false);
  });
});
```

Run: `cd backend && npx vitest run test/auth/deviceAuth.test.js`
Expected: fails — module doesn't exist.

- [ ] **Step 5: Implement deviceAuth.js**

```javascript
import bcrypt from 'bcrypt';

const SALT_ROUNDS = 10;

export function hashDeviceSecret(secret) {
  return bcrypt.hash(secret, SALT_ROUNDS);
}

export function verifyDeviceSecret(secret, hash) {
  return bcrypt.compare(secret, hash);
}
```

- [ ] **Step 6: Run deviceAuth.test.js again, confirm real pass**

Run: `cd backend && npx vitest run test/auth/deviceAuth.test.js`
Expected: 2/2 passing. Paste real output.

- [ ] **Step 7: Implement middleware.js (no test file — covered indirectly by Task 3's route tests, per the brief's own interface contract)**

```javascript
import { verifyAccessToken } from './tokens.js';

export function authMiddleware(request, reply, done) {
  const header = request.headers.authorization;
  if (!header || !header.startsWith('Bearer ')) {
    reply.code(401).send({ error: 'UNAUTHORIZED' });
    return;
  }
  try {
    const { userId } = verifyAccessToken(header.slice('Bearer '.length));
    request.userId = userId;
    done();
  } catch {
    reply.code(401).send({ error: 'UNAUTHORIZED' });
  }
}
```

- [ ] **Step 8: Run the full auth test directory once, confirm no regressions**

Run: `cd backend && npx vitest run test/auth/`
Expected: 5/5 passing across both files. Paste real output.

- [ ] **Step 9: Commit**

```bash
git add backend/src/auth backend/test/auth
git commit -m "feat: add JWT token issuing/verification and device-secret hashing"
```

---

### Task 3: POST /api/v1/auth/device endpoint

**Files:**
- Create: `backend/src/server.js`
- Create: `backend/src/routes/auth.js` (device route only this task — google/apple/refresh come in later tasks, appending to this same file)
- Create: `backend/test/routes/auth.device.test.js`
- Create: `backend/test/helpers/testDb.js`

**Interfaces:**
- Consumes: `hashDeviceSecret`/`verifyDeviceSecret` (Task 2), `issueTokenPair` (Task 2), `knex` (Task 1).
- Produces: `backend/src/server.js` exports `buildServer()` returning a configured Fastify instance (not started — tests use `.inject()`, a real start happens only outside tests). `POST /api/v1/auth/device` registered on it.

- [ ] **Step 1: Create a test-DB cleanup helper**

`backend/test/helpers/testDb.js`:
```javascript
import knex from '../../src/db/knex.js';

export async function truncateAll() {
  await knex.raw('TRUNCATE TABLE decisions, ruler_npcs, kingdoms, users RESTART IDENTITY CASCADE');
}

export { knex };
```

- [ ] **Step 2: Write the failing endpoint test**

`backend/test/routes/auth.device.test.js`:
```javascript
import { describe, it, expect, afterEach, afterAll } from 'vitest';
import { buildServer } from '../../src/server.js';
import { truncateAll, knex } from '../helpers/testDb.js';

describe('POST /api/v1/auth/device', () => {
  afterEach(async () => {
    await truncateAll();
  });

  afterAll(async () => {
    await knex.destroy();
  });

  it('creates a new user on first call with a device_id/secret pair', async () => {
    const app = buildServer();
    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/auth/device',
      payload: { device_id: 'device-abc', secret: 'first-secret' },
    });
    expect(response.statusCode).toBe(200);
    const body = response.json();
    expect(body.access_token).toBeTruthy();
    expect(body.refresh_token).toBeTruthy();

    const users = await knex('users').where({ device_id: 'device-abc' });
    expect(users).toHaveLength(1);
  });

  it('logs in an existing device_id with the correct secret', async () => {
    const app = buildServer();
    await app.inject({
      method: 'POST', url: '/api/v1/auth/device',
      payload: { device_id: 'device-xyz', secret: 'correct-secret' },
    });
    const response = await app.inject({
      method: 'POST', url: '/api/v1/auth/device',
      payload: { device_id: 'device-xyz', secret: 'correct-secret' },
    });
    expect(response.statusCode).toBe(200);

    const users = await knex('users').where({ device_id: 'device-xyz' });
    expect(users).toHaveLength(1); // no duplicate created on second call
  });

  it('rejects an existing device_id with the wrong secret', async () => {
    const app = buildServer();
    await app.inject({
      method: 'POST', url: '/api/v1/auth/device',
      payload: { device_id: 'device-wrong', secret: 'right-secret' },
    });
    const response = await app.inject({
      method: 'POST', url: '/api/v1/auth/device',
      payload: { device_id: 'device-wrong', secret: 'bad-secret' },
    });
    expect(response.statusCode).toBe(401);
  });

  it('rejects a malformed request missing device_id', async () => {
    const app = buildServer();
    const response = await app.inject({
      method: 'POST', url: '/api/v1/auth/device',
      payload: { secret: 'only-secret' },
    });
    expect(response.statusCode).toBe(400);
  });
});
```

Run: `cd backend && NODE_ENV=test npx vitest run test/routes/auth.device.test.js`
Expected: fails — `server.js`/route don't exist yet.

- [ ] **Step 3: Implement server.js and the device route**

`backend/src/server.js`:
```javascript
import Fastify from 'fastify';
import { registerAuthRoutes } from './routes/auth.js';

export function buildServer() {
  const app = Fastify({ logger: false });
  registerAuthRoutes(app);
  return app;
}
```

`backend/src/routes/auth.js`:
```javascript
import knex from '../db/knex.js';
import { hashDeviceSecret, verifyDeviceSecret } from '../auth/deviceAuth.js';
import { issueTokenPair } from '../auth/tokens.js';

const deviceAuthSchema = {
  body: {
    type: 'object',
    required: ['device_id', 'secret'],
    properties: {
      device_id: { type: 'string', minLength: 1 },
      secret: { type: 'string', minLength: 1 },
    },
  },
};

export function registerAuthRoutes(app) {
  app.post('/api/v1/auth/device', { schema: deviceAuthSchema }, async (request, reply) => {
    const { device_id: deviceId, secret } = request.body;

    const existing = await knex('users').where({ device_id: deviceId }).first();

    if (existing) {
      const valid = await verifyDeviceSecret(secret, existing.device_secret_hash);
      if (!valid) {
        reply.code(401).send({ error: 'INVALID_DEVICE_SECRET' });
        return;
      }
      const tokens = issueTokenPair(existing.id);
      reply.send({ access_token: tokens.accessToken, refresh_token: tokens.refreshToken });
      return;
    }

    const hash = await hashDeviceSecret(secret);
    const [user] = await knex('users')
      .insert({ device_id: deviceId, device_secret_hash: hash })
      .returning('id');
    const tokens = issueTokenPair(user.id);
    reply.code(200).send({ access_token: tokens.accessToken, refresh_token: tokens.refreshToken });
  });
}
```

- [ ] **Step 4: Run the test again, confirm real pass**

Run: `cd backend && NODE_ENV=test npx vitest run test/routes/auth.device.test.js`
Expected: 4/4 passing. Paste real output.

- [ ] **Step 5: Run the full test suite so far, confirm no regressions**

Run: `cd backend && NODE_ENV=test npx vitest run`
Expected: all tests across Tasks 1-3 passing (migrations + auth unit tests + device route tests). Paste real output.

- [ ] **Step 6: Commit**

```bash
git add backend/src/server.js backend/src/routes/auth.js backend/test/routes/auth.device.test.js \
  backend/test/helpers/testDb.js
git commit -m "feat: add POST /api/v1/auth/device endpoint"
```

---

### Task 4: POST /api/v1/auth/refresh endpoint

**Files:**
- Modify: `backend/src/routes/auth.js` (append the refresh route to the existing `registerAuthRoutes`)
- Create: `backend/test/routes/auth.refresh.test.js`

**Interfaces:**
- Consumes: `verifyRefreshToken`, `issueTokenPair` (Task 2).
- Produces: `POST /api/v1/auth/refresh` registered alongside the device route.

- [ ] **Step 1: Write the failing test**

`backend/test/routes/auth.refresh.test.js`:
```javascript
import { describe, it, expect, afterEach, afterAll } from 'vitest';
import { buildServer } from '../../src/server.js';
import { truncateAll, knex } from '../helpers/testDb.js';

describe('POST /api/v1/auth/refresh', () => {
  afterEach(async () => {
    await truncateAll();
  });

  afterAll(async () => {
    await knex.destroy();
  });

  it('issues a fresh access token for a valid refresh token', async () => {
    const app = buildServer();
    const loginResponse = await app.inject({
      method: 'POST', url: '/api/v1/auth/device',
      payload: { device_id: 'device-refresh', secret: 'a-secret' },
    });
    const { refresh_token: refreshToken } = loginResponse.json();

    const response = await app.inject({
      method: 'POST', url: '/api/v1/auth/refresh',
      payload: { refresh_token: refreshToken },
    });
    expect(response.statusCode).toBe(200);
    expect(response.json().access_token).toBeTruthy();
  });

  it('rejects a malformed/invalid refresh token', async () => {
    const app = buildServer();
    const response = await app.inject({
      method: 'POST', url: '/api/v1/auth/refresh',
      payload: { refresh_token: 'not-a-real-token' },
    });
    expect(response.statusCode).toBe(401);
  });
});
```

Run: `cd backend && NODE_ENV=test npx vitest run test/routes/auth.refresh.test.js`
Expected: fails — route doesn't exist.

- [ ] **Step 2: Add the refresh route to auth.js**

Add to `backend/src/routes/auth.js`, inside `registerAuthRoutes`, after the device route (also add `verifyRefreshToken` to the existing `tokens.js` import at the top of the file):

```javascript
  app.post('/api/v1/auth/refresh', {
    schema: { body: { type: 'object', required: ['refresh_token'], properties: { refresh_token: { type: 'string' } } } },
  }, async (request, reply) => {
    try {
      const { userId } = verifyRefreshToken(request.body.refresh_token);
      const tokens = issueTokenPair(userId);
      reply.send({ access_token: tokens.accessToken, refresh_token: tokens.refreshToken });
    } catch {
      reply.code(401).send({ error: 'INVALID_REFRESH_TOKEN' });
    }
  });
```

- [ ] **Step 3: Run the test again, confirm real pass**

Run: `cd backend && NODE_ENV=test npx vitest run test/routes/auth.refresh.test.js`
Expected: 2/2 passing. Paste real output.

- [ ] **Step 4: Run the full suite, confirm no regressions**

Run: `cd backend && NODE_ENV=test npx vitest run`
Expected: all tests across Tasks 1-4 passing. Paste real output.

- [ ] **Step 5: Commit**

```bash
git add backend/src/routes/auth.js backend/test/routes/auth.refresh.test.js
git commit -m "feat: add POST /api/v1/auth/refresh endpoint"
```

---

### Task 5: Google/Apple OAuth verification + endpoints

**Files:**
- Create: `backend/src/auth/oauthVerify.js`
- Modify: `backend/src/routes/auth.js` (append google/apple routes)
- Create: `backend/test/auth/oauthVerify.test.js`
- Create: `backend/test/routes/auth.oauth.test.js`

**Interfaces:**
- Produces: `verifyGoogleIdToken(idToken, verifier)` → `{sub, email}` or throws; `verifier` defaults to a real `google-auth-library` call, injectable for tests. `verifyAppleIdToken(idToken, verifier)` → `{sub, email}` or throws; same pattern with `jose`. `POST /api/v1/auth/google` and `POST /api/v1/auth/apple`, both upsert a `users` row by `google_sub`/`apple_sub` and return a token pair — same response shape as the device route.

- [ ] **Step 1: Write the failing unit tests for oauthVerify.js (using fake verifiers, no live network calls)**

`backend/test/auth/oauthVerify.test.js`:
```javascript
import { describe, it, expect } from 'vitest';
import { verifyGoogleIdToken, verifyAppleIdToken } from '../../src/auth/oauthVerify.js';

describe('oauthVerify', () => {
  it('verifyGoogleIdToken returns sub/email from the injected verifier', async () => {
    const fakeVerifier = async () => ({ sub: 'google-user-1', email: 'a@example.com' });
    const result = await verifyGoogleIdToken('fake-id-token', fakeVerifier);
    expect(result).toEqual({ sub: 'google-user-1', email: 'a@example.com' });
  });

  it('verifyGoogleIdToken propagates a verifier rejection', async () => {
    const failingVerifier = async () => { throw new Error('invalid token'); };
    await expect(verifyGoogleIdToken('bad-token', failingVerifier)).rejects.toThrow();
  });

  it('verifyAppleIdToken returns sub/email from the injected verifier', async () => {
    const fakeVerifier = async () => ({ sub: 'apple-user-1', email: 'b@example.com' });
    const result = await verifyAppleIdToken('fake-id-token', fakeVerifier);
    expect(result).toEqual({ sub: 'apple-user-1', email: 'b@example.com' });
  });
});
```

Run: `cd backend && npx vitest run test/auth/oauthVerify.test.js`
Expected: fails — module doesn't exist.

- [ ] **Step 2: Implement oauthVerify.js**

```javascript
import { OAuth2Client } from 'google-auth-library';
import { createRemoteJWKSet, jwtVerify } from 'jose';

const googleClient = new OAuth2Client(process.env.GOOGLE_CLIENT_ID);

async function defaultGoogleVerifier(idToken) {
  const ticket = await googleClient.verifyIdToken({ idToken, audience: process.env.GOOGLE_CLIENT_ID });
  const payload = ticket.getPayload();
  return { sub: payload.sub, email: payload.email };
}

export async function verifyGoogleIdToken(idToken, verifier = defaultGoogleVerifier) {
  return verifier(idToken);
}

const appleJwks = createRemoteJWKSet(new URL('https://appleid.apple.com/auth/keys'));

async function defaultAppleVerifier(idToken) {
  const { payload } = await jwtVerify(idToken, appleJwks, { issuer: 'https://appleid.apple.com' });
  return { sub: payload.sub, email: payload.email };
}

export async function verifyAppleIdToken(idToken, verifier = defaultAppleVerifier) {
  return verifier(idToken);
}
```

- [ ] **Step 3: Run oauthVerify.test.js again, confirm real pass**

Run: `cd backend && npx vitest run test/auth/oauthVerify.test.js`
Expected: 3/3 passing. Paste real output.

- [ ] **Step 4: Write the failing route tests (still using fake verifiers via a test-only injection point — see implementation step for how the routes expose this)**

`backend/test/routes/auth.oauth.test.js`:
```javascript
import { describe, it, expect, afterEach, afterAll } from 'vitest';
import { buildServer } from '../../src/server.js';
import { truncateAll, knex } from '../helpers/testDb.js';

describe('POST /api/v1/auth/google', () => {
  afterEach(async () => {
    await truncateAll();
  });
  afterAll(async () => {
    await knex.destroy();
  });

  it('creates a user on first Google sign-in and returns tokens', async () => {
    const app = buildServer({
      googleVerifier: async () => ({ sub: 'google-sub-1', email: 'user@example.com' }),
    });
    const response = await app.inject({
      method: 'POST', url: '/api/v1/auth/google', payload: { id_token: 'fake' },
    });
    expect(response.statusCode).toBe(200);
    expect(response.json().access_token).toBeTruthy();

    const users = await knex('users').where({ google_sub: 'google-sub-1' });
    expect(users).toHaveLength(1);
  });

  it('reuses the same user on a second sign-in with the same google_sub', async () => {
    const app = buildServer({
      googleVerifier: async () => ({ sub: 'google-sub-2', email: 'user2@example.com' }),
    });
    await app.inject({ method: 'POST', url: '/api/v1/auth/google', payload: { id_token: 'fake' } });
    await app.inject({ method: 'POST', url: '/api/v1/auth/google', payload: { id_token: 'fake' } });

    const users = await knex('users').where({ google_sub: 'google-sub-2' });
    expect(users).toHaveLength(1);
  });

  it('returns 401 when the verifier rejects the token', async () => {
    const app = buildServer({
      googleVerifier: async () => { throw new Error('invalid'); },
    });
    const response = await app.inject({
      method: 'POST', url: '/api/v1/auth/google', payload: { id_token: 'bad' },
    });
    expect(response.statusCode).toBe(401);
  });
});
```

Run: `cd backend && NODE_ENV=test npx vitest run test/routes/auth.oauth.test.js`
Expected: fails — `buildServer` doesn't accept a `googleVerifier` option yet, and the route doesn't exist.

- [ ] **Step 5: Modify server.js to accept injectable verifiers, and add the google/apple routes to auth.js**

Replace `backend/src/server.js` in full:
```javascript
import Fastify from 'fastify';
import { registerAuthRoutes } from './routes/auth.js';

export function buildServer(options = {}) {
  const app = Fastify({ logger: false });
  registerAuthRoutes(app, options);
  return app;
}
```

In `backend/src/routes/auth.js`: change the `registerAuthRoutes` signature to `registerAuthRoutes(app, options = {})`, import `verifyGoogleIdToken`/`verifyAppleIdToken` from `../auth/oauthVerify.js` at the top, and add these two routes (after the refresh route):

```javascript
  app.post('/api/v1/auth/google', {
    schema: { body: { type: 'object', required: ['id_token'], properties: { id_token: { type: 'string' } } } },
  }, async (request, reply) => {
    let profile;
    try {
      profile = await verifyGoogleIdToken(request.body.id_token, options.googleVerifier);
    } catch {
      reply.code(401).send({ error: 'INVALID_TOKEN' });
      return;
    }

    let user = await knex('users').where({ google_sub: profile.sub }).first();
    if (!user) {
      const [inserted] = await knex('users')
        .insert({ google_sub: profile.sub, email: profile.email })
        .returning('id');
      user = inserted;
    }
    const tokens = issueTokenPair(user.id);
    reply.send({ access_token: tokens.accessToken, refresh_token: tokens.refreshToken });
  });

  app.post('/api/v1/auth/apple', {
    schema: { body: { type: 'object', required: ['id_token'], properties: { id_token: { type: 'string' } } } },
  }, async (request, reply) => {
    let profile;
    try {
      profile = await verifyAppleIdToken(request.body.id_token, options.appleVerifier);
    } catch {
      reply.code(401).send({ error: 'INVALID_TOKEN' });
      return;
    }

    let user = await knex('users').where({ apple_sub: profile.sub }).first();
    if (!user) {
      const [inserted] = await knex('users')
        .insert({ apple_sub: profile.sub, email: profile.email })
        .returning('id');
      user = inserted;
    }
    const tokens = issueTokenPair(user.id);
    reply.send({ access_token: tokens.accessToken, refresh_token: tokens.refreshToken });
  });
```

- [ ] **Step 6: Run the oauth route test again, confirm real pass**

Run: `cd backend && NODE_ENV=test npx vitest run test/routes/auth.oauth.test.js`
Expected: 3/3 passing. Paste real output.

- [ ] **Step 7: Run the full suite, confirm no regressions**

Run: `cd backend && NODE_ENV=test npx vitest run`
Expected: all tests across Tasks 1-5 passing. Paste real output.

- [ ] **Step 8: Commit**

```bash
git add backend/src/auth/oauthVerify.js backend/src/server.js backend/src/routes/auth.js \
  backend/test/auth/oauthVerify.test.js backend/test/routes/auth.oauth.test.js
git commit -m "feat: add Google/Apple OAuth verification and auth endpoints"
```

---

### Task 6: POST /api/v1/decisions endpoint

**Files:**
- Create: `backend/src/routes/decisions.js`
- Modify: `backend/src/server.js` (register the new route module)
- Create: `backend/test/routes/decisions.test.js`

**Interfaces:**
- Consumes: `authMiddleware` (Task 2), `knex` (Task 1).
- Produces: `POST /api/v1/decisions`, Bearer-auth-protected, per the design spec's contract.

- [ ] **Step 1: Write the failing tests**

`backend/test/routes/decisions.test.js`:
```javascript
import { describe, it, expect, afterEach, afterAll } from 'vitest';
import { buildServer } from '../../src/server.js';
import { issueTokenPair } from '../../src/auth/tokens.js';
import { truncateAll, knex } from '../helpers/testDb.js';

async function seedUserAndKingdom() {
  const [user] = await knex('users').insert({ device_id: `dev-${Date.now()}-${Math.random()}` }).returning('id');
  const [kingdom] = await knex('kingdoms').insert({ user_id: user.id }).returning('id');
  return { user, kingdom };
}

describe('POST /api/v1/decisions', () => {
  afterEach(async () => {
    await truncateAll();
  });
  afterAll(async () => {
    await knex.destroy();
  });

  it('records a decision for a kingdom the caller owns', async () => {
    const { user, kingdom } = await seedUserAndKingdom();
    const { accessToken } = issueTokenPair(user.id);
    const app = buildServer();

    const response = await app.inject({
      method: 'POST', url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${accessToken}` },
      payload: {
        kingdom_id: kingdom.id, cycle_number: 1,
        recommendation: { army: 40, trade: 30, religion: 30 },
        ruler_outcome: { mood_delta: 5, loyalty_delta: 3 },
        overridden: false,
      },
    });

    expect(response.statusCode).toBe(201);
    const body = response.json();
    expect(body.decision_id).toBeTruthy();
    expect(body.overridden).toBe(false);

    const rows = await knex('decisions').where({ kingdom_id: kingdom.id, cycle_number: 1 });
    expect(rows).toHaveLength(1);
  });

  it('returns 401 with no Bearer token', async () => {
    const { kingdom } = await seedUserAndKingdom();
    const app = buildServer();
    const response = await app.inject({
      method: 'POST', url: '/api/v1/decisions',
      payload: { kingdom_id: kingdom.id, cycle_number: 1, recommendation: {}, ruler_outcome: {}, overridden: false },
    });
    expect(response.statusCode).toBe(401);
  });

  it('returns 403 when the caller does not own the kingdom', async () => {
    const { kingdom } = await seedUserAndKingdom(); // kingdom belongs to user A
    const [userB] = await knex('users').insert({ device_id: `dev-b-${Date.now()}` }).returning('id');
    const { accessToken } = issueTokenPair(userB.id); // token for user B

    const app = buildServer();
    const response = await app.inject({
      method: 'POST', url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${accessToken}` },
      payload: { kingdom_id: kingdom.id, cycle_number: 1, recommendation: {}, ruler_outcome: {}, overridden: false },
    });
    expect(response.statusCode).toBe(403);
  });

  it('returns 409 when cycle_number is already resolved for that kingdom', async () => {
    const { user, kingdom } = await seedUserAndKingdom();
    const { accessToken } = issueTokenPair(user.id);
    const app = buildServer();

    const payload = { kingdom_id: kingdom.id, cycle_number: 1, recommendation: {}, ruler_outcome: {}, overridden: false };
    await app.inject({ method: 'POST', url: '/api/v1/decisions', headers: { authorization: `Bearer ${accessToken}` }, payload });

    const response = await app.inject({
      method: 'POST', url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${accessToken}` }, payload,
    });
    expect(response.statusCode).toBe(409);
  });

  it('returns 400 for a malformed request missing kingdom_id', async () => {
    const { user } = await seedUserAndKingdom();
    const { accessToken } = issueTokenPair(user.id);
    const app = buildServer();
    const response = await app.inject({
      method: 'POST', url: '/api/v1/decisions',
      headers: { authorization: `Bearer ${accessToken}` },
      payload: { cycle_number: 1, recommendation: {}, ruler_outcome: {}, overridden: false },
    });
    expect(response.statusCode).toBe(400);
  });
});
```

Run: `cd backend && NODE_ENV=test npx vitest run test/routes/decisions.test.js`
Expected: fails — route doesn't exist.

- [ ] **Step 2: Implement decisions.js**

```javascript
import knex from '../db/knex.js';
import { authMiddleware } from '../auth/middleware.js';

const decisionSchema = {
  body: {
    type: 'object',
    required: ['kingdom_id', 'cycle_number', 'recommendation', 'ruler_outcome', 'overridden'],
    properties: {
      kingdom_id: { type: 'string' },
      cycle_number: { type: 'integer' },
      recommendation: { type: 'object' },
      ruler_outcome: { type: 'object' },
      overridden: { type: 'boolean' },
    },
  },
};

export function registerDecisionsRoutes(app) {
  app.post('/api/v1/decisions', { preHandler: authMiddleware, schema: decisionSchema }, async (request, reply) => {
    const { kingdom_id: kingdomId, cycle_number: cycleNumber, recommendation, ruler_outcome: rulerOutcome, overridden } = request.body;

    const kingdom = await knex('kingdoms').where({ id: kingdomId }).first();
    if (!kingdom || kingdom.user_id !== request.userId) {
      reply.code(403).send({ error: 'FORBIDDEN' });
      return;
    }

    const existing = await knex('decisions').where({ kingdom_id: kingdomId, cycle_number: cycleNumber }).first();
    if (existing) {
      reply.code(409).send({ error: 'CYCLE_ALREADY_RESOLVED' });
      return;
    }

    const [decision] = await knex('decisions')
      .insert({
        kingdom_id: kingdomId,
        cycle_number: cycleNumber,
        player_recommendation: JSON.stringify(recommendation),
        ruler_outcome: JSON.stringify(rulerOutcome),
        overridden,
      })
      .returning(['id', 'overridden']);

    reply.code(201).send({ decision_id: decision.id, ruler_outcome: rulerOutcome, overridden: decision.overridden });
  });
}
```

Update `backend/src/server.js` to register it — replace in full:
```javascript
import Fastify from 'fastify';
import { registerAuthRoutes } from './routes/auth.js';
import { registerDecisionsRoutes } from './routes/decisions.js';

export function buildServer(options = {}) {
  const app = Fastify({ logger: false });
  registerAuthRoutes(app, options);
  registerDecisionsRoutes(app);
  return app;
}
```

- [ ] **Step 3: Run the test again, confirm real pass**

Run: `cd backend && NODE_ENV=test npx vitest run test/routes/decisions.test.js`
Expected: 5/5 passing. Paste real output.

- [ ] **Step 4: Run the FULL suite one final time, confirm everything passes together**

Run: `cd backend && NODE_ENV=test npx vitest run`
Expected: every test across all 6 tasks passing, real output pasted in full.

- [ ] **Step 5: Commit**

```bash
git add backend/src/routes/decisions.js backend/src/server.js backend/test/routes/decisions.test.js
git commit -m "feat: add POST /api/v1/decisions endpoint"
```

---

## Definition of Done

- [ ] All 6 tasks committed with their tests **actually run and passing** (real `npx vitest run` output pasted in every task's report — hand-tracing is not acceptable for this plan, unlike the Unity client plan)
- [ ] Full suite green: `cd backend && NODE_ENV=test npx vitest run`
- [ ] `kingdom_id` ownership check returns 403, never 404, on mismatch (grep to confirm)
- [ ] Google/Apple verification functions accept an injectable verifier and no test makes a live network call to Google/Apple (confirm no test uses the real default verifier)
- [ ] `docs/PROJECT_PLAN.md`'s `/api/v1/decisions` and auth endpoint samples in §7-8 are reflected accurately by what's actually implemented
