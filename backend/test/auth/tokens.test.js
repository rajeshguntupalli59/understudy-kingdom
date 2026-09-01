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
});
