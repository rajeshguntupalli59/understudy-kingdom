import { describe, it, expect } from 'vitest';
import { SignJWT } from 'jose';
import { verifySupabaseJwt, TokenVerificationError } from '../../src/auth/verifyToken';

const TEST_SECRET = 'test-secret-at-least-32-bytes-long-for-hs256!!';
const encodedSecret = new TextEncoder().encode(TEST_SECRET);

async function makeToken(overrides: { sub?: string; expiresInSeconds?: number } = {}): Promise<string> {
  const jwt = new SignJWT({})
    .setProtectedHeader({ alg: 'HS256' })
    .setSubject(overrides.sub ?? 'test-user-id')
    .setIssuedAt();

  const expiresInSeconds = overrides.expiresInSeconds ?? 3600;
  jwt.setExpirationTime(Math.floor(Date.now() / 1000) + expiresInSeconds);

  return jwt.sign(encodedSecret);
}

describe('verifySupabaseJwt', () => {
  it('accepts a validly signed, unexpired token and returns its userId', async () => {
    const token = await makeToken({ sub: 'user-123' });

    const result = await verifySupabaseJwt(token, TEST_SECRET);

    expect(result.userId).toBe('user-123');
  });

  it('rejects an expired token', async () => {
    const token = await makeToken({ expiresInSeconds: -60 });

    await expect(verifySupabaseJwt(token, TEST_SECRET)).rejects.toThrow(TokenVerificationError);
  });

  it('rejects a token signed with a different secret (tampered/wrong signature)', async () => {
    const wrongSecret = new TextEncoder().encode('a-completely-different-secret-32-bytes!');
    const token = await new SignJWT({})
      .setProtectedHeader({ alg: 'HS256' })
      .setSubject('user-123')
      .setIssuedAt()
      .setExpirationTime(Math.floor(Date.now() / 1000) + 3600)
      .sign(wrongSecret);

    await expect(verifySupabaseJwt(token, TEST_SECRET)).rejects.toThrow(TokenVerificationError);
  });

  it('rejects a malformed token string', async () => {
    await expect(verifySupabaseJwt('not-a-jwt', TEST_SECRET)).rejects.toThrow(TokenVerificationError);
  });

  it('rejects an empty token string', async () => {
    await expect(verifySupabaseJwt('', TEST_SECRET)).rejects.toThrow(TokenVerificationError);
  });
});
