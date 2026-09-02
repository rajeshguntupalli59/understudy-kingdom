import { describe, it, expect, beforeAll } from 'vitest';
import { SignJWT, generateKeyPair, exportJWK, exportSPKI, createLocalJWKSet, UnsecuredJWT, type JWTVerifyGetKey } from 'jose';
import { verifySupabaseJwt, TokenVerificationError } from '../../src/auth/verifyToken';

// Supabase signs its real tokens asymmetrically (ES256) -- see
// server/src/auth/verifyToken.ts's doc comment. These tests generate a local
// ES256 keypair and sign/verify against it directly (no network, no remote
// JWKS fetch), keeping the suite fully offline and deterministic while still
// exercising the same asymmetric-verification code path `jwtVerify` uses in
// production (which resolves a key via `createRemoteJWKSet` there instead).
let privateKey: CryptoKey;
let publicKey: CryptoKey;
let wrongPrivateKey: CryptoKey;

beforeAll(async () => {
  const keyPair = await generateKeyPair('ES256');
  privateKey = keyPair.privateKey;
  publicKey = keyPair.publicKey;

  const wrongKeyPair = await generateKeyPair('ES256');
  wrongPrivateKey = wrongKeyPair.privateKey;
});

async function makeToken(overrides: { sub?: string; expiresInSeconds?: number; signWith?: CryptoKey } = {}): Promise<string> {
  const jwt = new SignJWT({})
    .setProtectedHeader({ alg: 'ES256' })
    .setSubject(overrides.sub ?? 'test-user-id')
    .setIssuedAt();

  const expiresInSeconds = overrides.expiresInSeconds ?? 3600;
  jwt.setExpirationTime(Math.floor(Date.now() / 1000) + expiresInSeconds);

  return jwt.sign(overrides.signWith ?? privateKey);
}

describe('verifySupabaseJwt', () => {
  it('accepts a validly signed, unexpired token and returns its userId', async () => {
    const token = await makeToken({ sub: 'user-123' });

    const result = await verifySupabaseJwt(token, publicKey);

    expect(result.userId).toBe('user-123');
  });

  it('rejects an expired token', async () => {
    const token = await makeToken({ expiresInSeconds: -60 });

    await expect(verifySupabaseJwt(token, publicKey)).rejects.toThrow(TokenVerificationError);
  });

  it('rejects a token signed with a different key (tampered/wrong signature)', async () => {
    const token = await makeToken({ sub: 'user-123', signWith: wrongPrivateKey });

    await expect(verifySupabaseJwt(token, publicKey)).rejects.toThrow(TokenVerificationError);
  });

  it('rejects a malformed token string', async () => {
    await expect(verifySupabaseJwt('not-a-jwt', publicKey)).rejects.toThrow(TokenVerificationError);
  });

  it('rejects an empty token string', async () => {
    await expect(verifySupabaseJwt('', publicKey)).rejects.toThrow(TokenVerificationError);
  });
});

// Production (authPlugin.ts) passes a JWTVerifyGetKey (createRemoteJWKSet)
// rather than a raw CryptoKey -- a different branch inside jose, and the
// one that actually resolves a key by `kid`/`alg` the way a real JWKS
// lookup does. createLocalJWKSet gives the same JWTVerifyGetKey shape
// built from a local JWK, so these exercise that production code path
// without a network dependency. The JWK below carries kid/alg/use/key_ops
// matching what a real Supabase JWKS entry looks like, so key selection is
// constrained the same way it is in production, not looser than it.
const KID = 'test-kid';

describe('verifySupabaseJwt via JWTVerifyGetKey (createLocalJWKSet)', () => {
  let jwks: JWTVerifyGetKey;

  beforeAll(async () => {
    const jwk = await exportJWK(publicKey);
    jwks = createLocalJWKSet({
      keys: [{ ...jwk, kid: KID, alg: 'ES256', use: 'sig', key_ops: ['verify'] }],
    });
  });

  it('accepts a legitimately ES256-signed token and returns the correct userId', async () => {
    const token = await new SignJWT({})
      .setProtectedHeader({ alg: 'ES256', kid: KID })
      .setSubject('user-456')
      .setIssuedAt()
      .setExpirationTime('1h')
      .sign(privateKey);

    const result = await verifySupabaseJwt(token, jwks);

    expect(result.userId).toBe('user-456');
  });

  it('rejects an alg:"none" unsecured JWT', async () => {
    const token = new UnsecuredJWT({}).setSubject('user-456').encode();

    await expect(verifySupabaseJwt(token, jwks)).rejects.toThrow(TokenVerificationError);
  });

  it('rejects a token forged with alg:"HS256" using the ES256 public key bytes as the HMAC secret', async () => {
    const publicKeyBytes = new TextEncoder().encode(await exportSPKI(publicKey));
    const token = await new SignJWT({})
      .setProtectedHeader({ alg: 'HS256', kid: KID })
      .setSubject('user-456')
      .setIssuedAt()
      .setExpirationTime('1h')
      .sign(publicKeyBytes);

    await expect(verifySupabaseJwt(token, jwks)).rejects.toThrow(TokenVerificationError);
  });

  it('rejects an ES384-signed token at the same kid (algorithm downgrade)', async () => {
    const { privateKey: es384PrivateKey } = await generateKeyPair('ES384');
    const token = await new SignJWT({})
      .setProtectedHeader({ alg: 'ES384', kid: KID })
      .setSubject('user-456')
      .setIssuedAt()
      .setExpirationTime('1h')
      .sign(es384PrivateKey);

    await expect(verifySupabaseJwt(token, jwks)).rejects.toThrow(TokenVerificationError);
  });

  it('rejects an RS256-signed token at the same kid (algorithm downgrade)', async () => {
    const { privateKey: rsaPrivateKey } = await generateKeyPair('RS256');
    const token = await new SignJWT({})
      .setProtectedHeader({ alg: 'RS256', kid: KID })
      .setSubject('user-456')
      .setIssuedAt()
      .setExpirationTime('1h')
      .sign(rsaPrivateKey);

    await expect(verifySupabaseJwt(token, jwks)).rejects.toThrow(TokenVerificationError);
  });
});
