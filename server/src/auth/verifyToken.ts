import { jwtVerify, errors as joseErrors, type JWTVerifyGetKey } from 'jose';

export class TokenVerificationError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'TokenVerificationError';
  }
}

/**
 * jose error subclasses that indicate a problem with the token itself
 * (bad/downgraded algorithm, bad signature, expired, malformed, unknown
 * key id) as opposed to the JWKS infrastructure being unreachable or
 * misconfigured. These are normalized into `TokenVerificationError` below
 * so callers get one consistent "bad token" contract. Anything else --
 * notably a `TypeError` from a failed `fetch` to the JWKS endpoint, or a
 * `JOSEError`/`JWKSTimeout`/`JWKSInvalid` indicating the JWKS document
 * itself couldn't be retrieved or parsed -- propagates as its original
 * jose error type instead of being swallowed here, so `authPlugin.ts` can
 * tell an infrastructure outage (503) apart from a bad token (401).
 */
const TOKEN_CLASS_ERRORS = [
  joseErrors.JWSSignatureVerificationFailed,
  joseErrors.JWTExpired,
  joseErrors.JWKSNoMatchingKey,
  joseErrors.JOSENotSupported,
  joseErrors.JOSEAlgNotAllowed,
  joseErrors.JWSInvalid,
  joseErrors.JWTInvalid,
  joseErrors.JWTClaimValidationFailed,
];

function isTokenClassError(err: unknown): boolean {
  return TOKEN_CLASS_ERRORS.some((ErrorType) => err instanceof ErrorType);
}

export interface VerifySupabaseJwtOptions {
  /** Expected `iss` claim, e.g. `${SUPABASE_URL}/auth/v1`. Skipped if omitted. */
  issuer?: string;
  /** Expected `aud` claim, e.g. `'authenticated'`. Skipped if omitted. */
  audience?: string;
}

/**
 * Verifies a Supabase-issued JWT. Supabase signs tokens with an asymmetric
 * key (ES256) resolved via its project's JWKS endpoint, not a shared HS256
 * secret -- confirmed empirically against a real token (its header carries
 * `alg: "ES256"` and a `kid`), so `key` is whatever `jose`'s `jwtVerify`
 * accepts as verification key material: a `JWTVerifyGetKey` (e.g.
 * `createRemoteJWKSet(...)`/`createLocalJWKSet(...)`, what the real auth
 * plugin passes) for production, or a local public `CryptoKey` for tests
 * that don't want a network dependency. `algorithms: ['ES256']` is pinned
 * explicitly rather than relying purely on `jose`'s own internals plus
 * whatever a remote JWKS document happens to contain -- this is what
 * rejects `alg: "none"`, HS256-forged, and ES384/RS256 key-confusion
 * downgrade attempts. See
 * docs/superpowers/specs/2026-09-02-backend-service-design.md.
 */
export async function verifySupabaseJwt(
  token: string,
  key: CryptoKey | JWTVerifyGetKey,
  options: VerifySupabaseJwtOptions = {},
): Promise<{ userId: string }> {
  if (!token) {
    throw new TokenVerificationError('Token is empty');
  }

  try {
    const { payload } = await jwtVerify(token, key, {
      algorithms: ['ES256'],
      issuer: options.issuer,
      audience: options.audience,
    });

    if (typeof payload.sub !== 'string' || payload.sub.length === 0) {
      throw new TokenVerificationError('Token is missing a subject claim');
    }

    return { userId: payload.sub };
  } catch (err) {
    if (err instanceof TokenVerificationError) {
      throw err;
    }
    if (isTokenClassError(err)) {
      throw new TokenVerificationError('Token is invalid or expired');
    }
    throw err;
  }
}
