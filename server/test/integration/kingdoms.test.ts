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
