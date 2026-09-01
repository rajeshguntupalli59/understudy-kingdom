import { describe, it, expect, afterEach, afterAll } from 'vitest';
import { buildServer } from '../../src/server.js';
import { truncateAll, knex } from '../helpers/testDb.js';

// Shared afterEach/afterAll at file scope (not per-describe): the two
// describe blocks below share the same singleton `knex` connection pool,
// and destroying it in one describe's afterAll would break the sibling
// describe's tests, which run afterward in the same file.
afterEach(async () => {
  await truncateAll();
});
afterAll(async () => {
  await knex.destroy();
});

describe('POST /api/v1/auth/google', () => {
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

  it('handles two concurrent first-sign-ins for the same google_sub without a 500', async () => {
    const app = buildServer({
      googleVerifier: async () => ({ sub: 'google-sub-race', email: 'race@example.com' }),
    });
    const [response1, response2] = await Promise.all([
      app.inject({ method: 'POST', url: '/api/v1/auth/google', payload: { id_token: 'fake' } }),
      app.inject({ method: 'POST', url: '/api/v1/auth/google', payload: { id_token: 'fake' } }),
    ]);
    expect(response1.statusCode).toBe(200);
    expect(response2.statusCode).toBe(200);
    expect(response1.json().access_token).toBeTruthy();
    expect(response2.json().access_token).toBeTruthy();

    const users = await knex('users').where({ google_sub: 'google-sub-race' });
    expect(users).toHaveLength(1);
  });
});

describe('POST /api/v1/auth/apple', () => {
  it('creates a user on first Apple sign-in and returns tokens', async () => {
    const app = buildServer({
      appleVerifier: async () => ({ sub: 'apple-sub-1', email: 'user@example.com' }),
    });
    const response = await app.inject({
      method: 'POST', url: '/api/v1/auth/apple', payload: { id_token: 'fake' },
    });
    expect(response.statusCode).toBe(200);
    expect(response.json().access_token).toBeTruthy();

    const users = await knex('users').where({ apple_sub: 'apple-sub-1' });
    expect(users).toHaveLength(1);
  });

  it('reuses the same user on a second sign-in with the same apple_sub', async () => {
    const app = buildServer({
      appleVerifier: async () => ({ sub: 'apple-sub-2', email: 'user2@example.com' }),
    });
    await app.inject({ method: 'POST', url: '/api/v1/auth/apple', payload: { id_token: 'fake' } });
    await app.inject({ method: 'POST', url: '/api/v1/auth/apple', payload: { id_token: 'fake' } });

    const users = await knex('users').where({ apple_sub: 'apple-sub-2' });
    expect(users).toHaveLength(1);
  });

  it('returns 401 when the verifier rejects the token', async () => {
    const app = buildServer({
      appleVerifier: async () => { throw new Error('invalid'); },
    });
    const response = await app.inject({
      method: 'POST', url: '/api/v1/auth/apple', payload: { id_token: 'bad' },
    });
    expect(response.statusCode).toBe(401);
  });

  it('handles two concurrent first-sign-ins for the same apple_sub without a 500', async () => {
    const app = buildServer({
      appleVerifier: async () => ({ sub: 'apple-sub-race', email: 'race@example.com' }),
    });
    const [response1, response2] = await Promise.all([
      app.inject({ method: 'POST', url: '/api/v1/auth/apple', payload: { id_token: 'fake' } }),
      app.inject({ method: 'POST', url: '/api/v1/auth/apple', payload: { id_token: 'fake' } }),
    ]);
    expect(response1.statusCode).toBe(200);
    expect(response2.statusCode).toBe(200);
    expect(response1.json().access_token).toBeTruthy();
    expect(response2.json().access_token).toBeTruthy();

    const users = await knex('users').where({ apple_sub: 'apple-sub-race' });
    expect(users).toHaveLength(1);
  });
});
