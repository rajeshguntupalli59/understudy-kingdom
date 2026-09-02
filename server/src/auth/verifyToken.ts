import { jwtVerify, type JWTVerifyGetKey, type KeyLike } from 'jose';

export class TokenVerificationError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'TokenVerificationError';
  }
}

/**
 * Verifies a Supabase-issued JWT. Supabase signs tokens with an asymmetric
 * key (ES256) resolved via its project's JWKS endpoint, not a shared HS256
 * secret -- confirmed empirically against a real token (its header carries
 * `alg: "ES256"` and a `kid`), so `key` is whatever `jose`'s `jwtVerify`
 * accepts as verification key material: a `JWTVerifyGetKey` (e.g.
 * `createRemoteJWKSet(...)`, what the real auth plugin passes) for
 * production, or a local public `KeyLike`/JWK for tests that don't want a
 * network dependency. See
 * docs/superpowers/specs/2026-09-02-backend-service-design.md.
 */
export async function verifySupabaseJwt(
  token: string,
  key: KeyLike | JWTVerifyGetKey,
): Promise<{ userId: string }> {
  if (!token) {
    throw new TokenVerificationError('Token is empty');
  }

  try {
    const { payload } = await jwtVerify(token, key as JWTVerifyGetKey);

    if (typeof payload.sub !== 'string' || payload.sub.length === 0) {
      throw new TokenVerificationError('Token is missing a subject claim');
    }

    return { userId: payload.sub };
  } catch (err) {
    if (err instanceof TokenVerificationError) {
      throw err;
    }
    throw new TokenVerificationError('Token is invalid or expired');
  }
}
