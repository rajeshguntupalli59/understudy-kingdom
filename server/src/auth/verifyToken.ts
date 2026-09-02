import { jwtVerify } from 'jose';

export class TokenVerificationError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'TokenVerificationError';
  }
}

export async function verifySupabaseJwt(token: string, secret: string): Promise<{ userId: string }> {
  if (!token) {
    throw new TokenVerificationError('Token is empty');
  }

  try {
    const { payload } = await jwtVerify(token, new TextEncoder().encode(secret));

    if (typeof payload.sub !== 'string' || payload.sub.length === 0) {
      throw new TokenVerificationError('Token is missing a subject claim');
    }

    return { userId: payload.sub };
  } catch (err) {
    if (err instanceof TokenVerificationError) {
      throw err;
    }
    throw new TokenVerificationError('Token is invalid or expired');
  }
}
