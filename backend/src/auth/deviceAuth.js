import bcrypt from 'bcrypt';

const SALT_ROUNDS = 10;

export function hashDeviceSecret(secret) {
  return bcrypt.hash(secret, SALT_ROUNDS);
}

export function verifyDeviceSecret(secret, hash) {
  return bcrypt.compare(secret, hash);
}
