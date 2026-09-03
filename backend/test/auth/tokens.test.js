import jwt from 'jsonwebtoken';
import { describe, it, expect } from 'vitest';
import { issueTokenPair, verifyAccessToken, verifyRefreshToken } from '../../src/auth/tokens.js';

describe('tokens', () => {
  it('issues an access token that verifies back to the same userId', () => {
    const { accessToken } = issueTokenPair('user-123');
    const result = verifyAccessToken(accessToken);
    expect(result.userId).toBe('user-123');
  });

  it('issues a refresh token that verifies back to the same userId', () => {
    const { refreshToken } = issueTokenPair('user-123');
    const result = verifyRefreshToken(refreshToken);
    expect(result.userId).toBe('user-123');
  });

  it('rejects an access token passed to verifyRefreshToken', () => {
    const { accessToken } = issueTokenPair('user-123');
    expect(() => verifyRefreshToken(accessToken)).toThrow();
  });

  it('rejects a token with the correct refresh secret but wrong type claim', () => {
    // Signed with the SAME secret verifyRefreshToken checks against (tokens.js's
    // NODE_ENV==='test' fallback, since JWT_REFRESH_SECRET is unset here) but
    // type:'access' instead of type:'refresh' -- this isolates the type-check
    // itself as the thing doing the rejecting, unlike the existing cross-type
    // test which passes only because of a signature mismatch (see
    // backend-task-2 review).
    const wrongTypeToken = jwt.sign({ userId: 'user-123', type: 'access' }, 'test-refresh-secret', { algorithm: 'HS256' });
    expect(() => verifyRefreshToken(wrongTypeToken)).toThrow();
  });
});
