import { describe, it, expect } from 'vitest';
import { verifyGoogleIdToken, verifyAppleIdToken } from '../../src/auth/oauthVerify.js';

describe('oauthVerify', () => {
  it('verifyGoogleIdToken returns sub/email from the injected verifier', async () => {
    const fakeVerifier = async () => ({ sub: 'google-user-1', email: 'a@example.com' });
    const result = await verifyGoogleIdToken('fake-id-token', fakeVerifier);
    expect(result).toEqual({ sub: 'google-user-1', email: 'a@example.com' });
  });

  it('verifyGoogleIdToken propagates a verifier rejection', async () => {
    const failingVerifier = async () => { throw new Error('invalid token'); };
    await expect(verifyGoogleIdToken('bad-token', failingVerifier)).rejects.toThrow();
  });

  it('verifyAppleIdToken returns sub/email from the injected verifier', async () => {
    const fakeVerifier = async () => ({ sub: 'apple-user-1', email: 'b@example.com' });
    const result = await verifyAppleIdToken('fake-id-token', fakeVerifier);
    expect(result).toEqual({ sub: 'apple-user-1', email: 'b@example.com' });
  });
});
