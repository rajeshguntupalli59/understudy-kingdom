import { OAuth2Client } from 'google-auth-library';
import { createRemoteJWKSet, jwtVerify } from 'jose';

const googleClient = new OAuth2Client(process.env.GOOGLE_CLIENT_ID);

async function defaultGoogleVerifier(idToken) {
  const ticket = await googleClient.verifyIdToken({ idToken, audience: process.env.GOOGLE_CLIENT_ID });
  const payload = ticket.getPayload();
  return { sub: payload.sub, email: payload.email };
}

export async function verifyGoogleIdToken(idToken, verifier = defaultGoogleVerifier) {
  return verifier(idToken);
}

const appleJwks = createRemoteJWKSet(new URL('https://appleid.apple.com/auth/keys'));

async function defaultAppleVerifier(idToken) {
  const { payload } = await jwtVerify(idToken, appleJwks, { issuer: 'https://appleid.apple.com' });
  return { sub: payload.sub, email: payload.email };
}

export async function verifyAppleIdToken(idToken, verifier = defaultAppleVerifier) {
  return verifier(idToken);
}
