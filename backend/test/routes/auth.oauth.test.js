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
