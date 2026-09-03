import { verifyAccessToken } from './tokens.js';

export function authMiddleware(request, reply, done) {
  const header = request.headers.authorization;
  if (!header || !header.startsWith('Bearer ')) {
    reply.code(401).send({ error: 'UNAUTHORIZED' });
    return;
  }
  try {
    const { userId } = verifyAccessToken(header.slice('Bearer '.length));
    request.userId = userId;
    done();
  } catch {
    reply.code(401).send({ error: 'UNAUTHORIZED' });
  }
}
