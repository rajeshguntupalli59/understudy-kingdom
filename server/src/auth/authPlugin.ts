import fp from 'fastify-plugin';
import { FastifyPluginAsync } from 'fastify';
import { createRemoteJWKSet, errors as joseErrors } from 'jose';
import { verifySupabaseJwt, TokenVerificationError } from './verifyToken';

declare module 'fastify' {
  interface FastifyRequest {
    userId: string;
  }
}

interface AuthContext {
  jwks: ReturnType<typeof createRemoteJWKSet>;
  issuer: string;
  audience: string;
}

// Built lazily on first use rather than at module load. createRemoteJWKSet
// caches fetched keys internally and is designed to be reused across
// calls, so we still only construct it once -- but doing that eagerly at
// import time meant a missing SUPABASE_URL failed at *import* of this
// module (confusing if anything imports authPlugin before `dotenv/config`
// has run) rather than at first actual request. Supabase signs tokens
// asymmetrically (ES256); this resolves the current public key from the
// project's JWKS endpoint rather than needing a shared secret. See
// docs/superpowers/specs/2026-09-02-backend-service-design.md.
let authContext: AuthContext | undefined;

function getAuthContext(): AuthContext {
  if (!authContext) {
    const supabaseUrl = process.env.SUPABASE_URL;
    if (!supabaseUrl) {
      throw new Error('SUPABASE_URL is not configured');
    }
    authContext = {
      jwks: createRemoteJWKSet(new URL('/auth/v1/.well-known/jwks.json', supabaseUrl)),
      issuer: new URL('/auth/v1', supabaseUrl).toString(),
      audience: 'authenticated',
    };
  }
  return authContext;
}

// Infrastructure-class failures (the JWKS endpoint being unreachable,
// timing out, or returning something that isn't a valid JWKS document) are
// distinct from token-class failures (a bad/expired/tampered/downgraded
// token) -- verifyToken.ts normalizes token-class jose errors into
// TokenVerificationError and lets everything else propagate as its
// original jose error type. A `TypeError` here is what Node's `fetch`
// throws when it can't reach the JWKS endpoint at all (DNS failure,
// connection refused, etc.); the various jose `JOSEError` subclasses cover
// a timeout, a non-200 response, or an unparseable JWKS document.
export function isInfrastructureError(err: unknown): boolean {
  return err instanceof TypeError || err instanceof joseErrors.JOSEError;
}

const authPlugin: FastifyPluginAsync = async (fastify) => {
  fastify.addHook('onRequest', async (request, reply) => {
    const authHeader = request.headers.authorization;

    if (!authHeader || !authHeader.startsWith('Bearer ')) {
      reply.code(401).send({ error: 'Missing or invalid Authorization header' });
      return reply;
    }

    const token = authHeader.slice('Bearer '.length);
    const { jwks, issuer, audience } = getAuthContext();

    try {
      const { userId } = await verifySupabaseJwt(token, jwks, { issuer, audience });
      request.userId = userId;
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
};

export default fp(authPlugin);
