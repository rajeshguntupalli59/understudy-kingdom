import { OAuth2Client } from 'google-auth-library';
import { createRemoteJWKSet, jwtVerify } from 'jose';
import { requireEnv } from '../config/requireEnv.js';

const GOOGLE_CLIENT_ID = requireEnv('GOOGLE_CLIENT_ID', 'test-google-client-id');
const APPLE_CLIENT_ID = requireEnv('APPLE_CLIENT_ID', 'test-apple-client-id');

const googleClient = new OAuth2Client(GOOGLE_CLIENT_ID);

async function defaultGoogleVerifier(idToken) {
  const ticket = await googleClient.verifyIdToken({ idToken, audience: GOOGLE_CLIENT_ID });
  const payload = ticket.getPayload();
  return { sub: payload.sub, email: payload.email };
}

export async function verifyGoogleIdToken(idToken, verifier = defaultGoogleVerifier) {
  return verifier(idToken);
}

const appleJwks = createRemoteJWKSet(new URL('https://appleid.apple.com/auth/keys'));

async function defaultAppleVerifier(idToken) {
  const { payload } = await jwtVerify(idToken, appleJwks, {
    issuer: 'https://appleid.apple.com',
    audience: APPLE_CLIENT_ID,
  });
  return { sub: payload.sub, email: payload.email };
}

export async function verifyAppleIdToken(idToken, verifier = defaultAppleVerifier) {
  return verifier(idToken);
}
