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
