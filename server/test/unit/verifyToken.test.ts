import { describe, it, expect, beforeAll } from 'vitest';
import { SignJWT, generateKeyPair, type KeyLike } from 'jose';
import { verifySupabaseJwt, TokenVerificationError } from '../../src/auth/verifyToken';

// Supabase signs its real tokens asymmetrically (ES256) -- see
// server/src/auth/verifyToken.ts's doc comment. These tests generate a local
// ES256 keypair and sign/verify against it directly (no network, no remote
// JWKS fetch), keeping the suite fully offline and deterministic while still
// exercising the same asymmetric-verification code path `jwtVerify` uses in
// production (which resolves a key via `createRemoteJWKSet` there instead).
let privateKey: KeyLike;
let publicKey: KeyLike;
let wrongPrivateKey: KeyLike;

beforeAll(async () => {
  const keyPair = await generateKeyPair('ES256');
  privateKey = keyPair.privateKey;
  publicKey = keyPair.publicKey;

  const wrongKeyPair = await generateKeyPair('ES256');
  wrongPrivateKey = wrongKeyPair.privateKey;
});

async function makeToken(overrides: { sub?: string; expiresInSeconds?: number; signWith?: KeyLike } = {}): Promise<string> {
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
