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
