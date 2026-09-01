import knex from '../db/knex.js';
import { hashDeviceSecret, verifyDeviceSecret } from '../auth/deviceAuth.js';
import { issueTokenPair, verifyRefreshToken } from '../auth/tokens.js';

const deviceAuthSchema = {
  body: {
    type: 'object',
    required: ['device_id', 'secret'],
    properties: {
      device_id: { type: 'string', minLength: 1 },
      secret: { type: 'string', minLength: 1 },
    },
  },
};

export function registerAuthRoutes(app) {
  app.post('/api/v1/auth/device', { schema: deviceAuthSchema }, async (request, reply) => {
    const { device_id: deviceId, secret } = request.body;

    const existing = await knex('users').where({ device_id: deviceId }).first();

    if (existing) {
      const valid = await verifyDeviceSecret(secret, existing.device_secret_hash);
      if (!valid) {
        reply.code(401).send({ error: 'INVALID_DEVICE_SECRET' });
        return;
      }
      const tokens = issueTokenPair(existing.id);
      reply.send({ access_token: tokens.accessToken, refresh_token: tokens.refreshToken });
      return;
    }

    const hash = await hashDeviceSecret(secret);
    let user;
    try {
      const [inserted] = await knex('users')
        .insert({ device_id: deviceId, device_secret_hash: hash })
        .returning('id');
      user = inserted;
    } catch (err) {
      if (err.code === '23505') {
        // Lost a race to create this device_id -- another concurrent request won.
        // Fall back to the login path against whichever row won.
        const winner = await knex('users').where({ device_id: deviceId }).first();
        if (!winner) throw err; // shouldn't happen, but don't swallow a genuinely different error
        const valid = await verifyDeviceSecret(secret, winner.device_secret_hash);
        if (!valid) {
          reply.code(401).send({ error: 'INVALID_DEVICE_SECRET' });
          return;
        }
        const tokens = issueTokenPair(winner.id);
        reply.send({ access_token: tokens.accessToken, refresh_token: tokens.refreshToken });
        return;
      }
      throw err;
    }

    const tokens = issueTokenPair(user.id);
    reply.code(200).send({ access_token: tokens.accessToken, refresh_token: tokens.refreshToken });
  });

  app.post('/api/v1/auth/refresh', {
    schema: { body: { type: 'object', required: ['refresh_token'], properties: { refresh_token: { type: 'string' } } } },
  }, async (request, reply) => {
    try {
      const { userId } = verifyRefreshToken(request.body.refresh_token);
      const tokens = issueTokenPair(userId);
      reply.send({ access_token: tokens.accessToken, refresh_token: tokens.refreshToken });
    } catch {
      reply.code(401).send({ error: 'INVALID_REFRESH_TOKEN' });
    }
  });
}
