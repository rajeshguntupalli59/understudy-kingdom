import { describe, it, expect } from 'vitest';
import { hashDeviceSecret, verifyDeviceSecret } from '../../src/auth/deviceAuth.js';

describe('deviceAuth', () => {
  it('hashes a secret and verifies the same secret against it', async () => {
    const hash = await hashDeviceSecret('my-secret-123');
    const result = await verifyDeviceSecret('my-secret-123', hash);
    expect(result).toBe(true);
  });

  it('rejects a wrong secret against a hash', async () => {
    const hash = await hashDeviceSecret('my-secret-123');
    const result = await verifyDeviceSecret('wrong-secret', hash);
    expect(result).toBe(false);
  });
});
