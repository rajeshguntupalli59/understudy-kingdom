import fp from 'fastify-plugin';
import { FastifyPluginAsync } from 'fastify';
import { createRemoteJWKSet } from 'jose';
import { verifySupabaseJwt, TokenVerificationError } from './verifyToken';

declare module 'fastify' {
  interface FastifyRequest {
    userId: string;
  }
}

// Created once at module load, not per-request -- createRemoteJWKSet caches
// the fetched keys internally and is designed to be reused across calls.
// Supabase signs tokens asymmetrically (ES256); this resolves the current
// public key from the project's JWKS endpoint rather than needing a shared
// secret. See docs/superpowers/specs/2026-09-02-backend-service-design.md.
function getJwks() {
  const supabaseUrl = process.env.SUPABASE_URL;
  if (!supabaseUrl) {
    throw new Error('SUPABASE_URL is not configured');
  }
  return createRemoteJWKSet(new URL(`${supabaseUrl}/auth/v1/.well-known/jwks.json`));
}

const jwks = getJwks();

const authPlugin: FastifyPluginAsync = async (fastify) => {
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
