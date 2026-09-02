import { describe, it, expect } from 'vitest';
import Fastify from 'fastify';
import { createRemoteJWKSet, generateKeyPair, SignJWT } from 'jose';
import { verifySupabaseJwt, TokenVerificationError } from '../../src/auth/verifyToken';
import { isInfrastructureError } from '../../src/auth/authPlugin';

// Proves the fix for the JWKS-unreachable case: an infrastructure failure
// (can't reach/parse the JWKS document) must surface as 503, not 401 --
// 401 misleads a well-behaved client into treating a transient outage as
// "your session is dead, sign out." This wires a minimal onRequest hook
// using the exact same verifySupabaseJwt + isInfrastructureError
// production uses (see server/src/auth/authPlugin.ts), pointed at a
// hostname that cannot resolve, with a short `timeoutDuration` so the test
// fails fast rather than hanging on a real DNS timeout.
describe('auth: JWKS infrastructure failures', () => {
  it('returns 503 (not 401) when the JWKS endpoint is unreachable', async () => {
    const { privateKey } = await generateKeyPair('ES256');
    const token = await new SignJWT({})
      .setProtectedHeader({ alg: 'ES256', kid: 'irrelevant-kid' })
      .setSubject('user-123')
      .setIssuedAt()
      .setExpirationTime('1h')
      .sign(privateKey);

    const unreachableJwks = createRemoteJWKSet(
      new URL('https://this-does-not-exist-12345.supabase.co/auth/v1/.well-known/jwks.json'),
      { timeoutDuration: 2000 },
    );

    const app = Fastify({ logger: false });
    app.addHook('onRequest', async (request, reply) => {
      try {
        await verifySupabaseJwt(token, unreachableJwks);
      } catch (err) {
        if (err instanceof TokenVerificationError) {
          reply.code(401).send({ error: 'Invalid or expired token' });
          return reply;
        }
        if (isInfrastructureError(err)) {
          reply.code(503).send({ error: 'Authentication service unavailable' });
          return reply;
        }
        throw err;
      }
    });
    app.get('/protected', async () => ({ ok: true }));

    const response = await app.inject({ method: 'GET', url: '/protected' });

    expect(response.statusCode).toBe(503);
  });
});
