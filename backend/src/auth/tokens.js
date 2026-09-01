import jwt from 'jsonwebtoken';
import { requireEnv } from '../config/requireEnv.js';

const ACCESS_SECRET = requireEnv('JWT_ACCESS_SECRET', 'test-access-secret');
const REFRESH_SECRET = requireEnv('JWT_REFRESH_SECRET', 'test-refresh-secret');
const ACCESS_EXPIRES_IN = '15m';
const REFRESH_EXPIRES_IN = '30d';

export function issueTokenPair(userId) {
  const accessToken = jwt.sign({ userId, type: 'access' }, ACCESS_SECRET, { expiresIn: ACCESS_EXPIRES_IN, algorithm: 'HS256' });
  const refreshToken = jwt.sign({ userId, type: 'refresh' }, REFRESH_SECRET, { expiresIn: REFRESH_EXPIRES_IN, algorithm: 'HS256' });
  return { accessToken, refreshToken };
}

export function verifyAccessToken(token) {
  const payload = jwt.verify(token, ACCESS_SECRET, { algorithms: ['HS256'] });
  if (payload.type !== 'access') throw new Error('not an access token');
  return { userId: payload.userId };
}

export function verifyRefreshToken(token) {
  const payload = jwt.verify(token, REFRESH_SECRET, { algorithms: ['HS256'] });
  if (payload.type !== 'refresh') throw new Error('not a refresh token');
  return { userId: payload.userId };
}
