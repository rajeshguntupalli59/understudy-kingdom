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

  it('handles two concurrent POST /api/v1/kingdoms requests for the same user without a bare kingdom (I-2/I-3)', async () => {
    // Fire both requests "simultaneously" -- both promises are created and
    // handed to Promise.all without awaiting either individually first, so
    // their underlying DB work genuinely overlaps instead of running one
    // after the other. This is what would have caught the pre-fix I-2 race
    // (kingdom insert and ruler_npc insert as two separate autocommitted
    // statements): the losing request could re-select a kingdom whose
    // ruler_npc hadn't committed yet and 500 out of getRulerNpcOrThrow.
    const [first, second] = await Promise.all([
      app.inject({
        method: 'POST',
        url: '/api/v1/kingdoms',
        headers: { authorization: `Bearer ${jwt}` },
      }),
      app.inject({
        method: 'POST',
        url: '/api/v1/kingdoms',
        headers: { authorization: `Bearer ${jwt}` },
      }),
    ]);

    // Exactly one request created the kingdom (201) and the other found it
    // already there (200) -- but which one is non-deterministic, so assert
    // on the multiset of status codes rather than which promise "won".
    expect([first.statusCode, second.statusCode].sort()).toEqual([200, 201]);

    const firstBody = first.json();
    const secondBody = second.json();

    expect(firstBody.kingdom.id).toBeDefined();
    expect(secondBody.kingdom.id).toBe(firstBody.kingdom.id);

    // The specific invariant Fix I-2's transaction is supposed to
    // guarantee: both responses see a fully-formed ruler, never a kingdom
    // without one.
    expect(firstBody.rulerNpc).toBeTruthy();
    expect(secondBody.rulerNpc).toBeTruthy();
    expect(secondBody.rulerNpc.kingdomId).toBe(firstBody.kingdom.id);
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

  it('rejects requests with a syntactically-invalid bearer token', async () => {
    const response = await app.inject({
      method: 'GET',
      url: '/api/v1/kingdoms/me',
      headers: { authorization: 'Bearer not.a.real.jwt' },
    });

    expect(response.statusCode).toBe(401);
  });
});
