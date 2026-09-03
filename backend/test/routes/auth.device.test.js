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
    const body = response.json();
    expect(body.access_token).toBeTruthy();
    expect(body.refresh_token).toBeTruthy();

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

  it('handles two concurrent requests for the same new device_id without a 500', async () => {
    const app = buildServer();
    const payload = { device_id: 'device-race', secret: 'race-secret' };

    const [response1, response2] = await Promise.all([
      app.inject({ method: 'POST', url: '/api/v1/auth/device', payload }),
      app.inject({ method: 'POST', url: '/api/v1/auth/device', payload }),
    ]);

    expect(response1.statusCode).toBe(200);
    expect(response2.statusCode).toBe(200);
    expect(response1.json().access_token).toBeTruthy();
    expect(response2.json().access_token).toBeTruthy();

    const users = await knex('users').where({ device_id: 'device-race' });
    expect(users).toHaveLength(1); // only one user row created despite the race
  });
});
