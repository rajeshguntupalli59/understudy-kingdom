import Fastify from 'fastify';
import { registerAuthRoutes } from './routes/auth.js';
import { registerDecisionsRoutes } from './routes/decisions.js';

export function buildServer(options = {}) {
  const app = Fastify({ logger: false });

  // Global error handler: never let a real 500 echo err.message or raw
  // driver/SQL error text back to the client. Fastify's own validation
  // errors are reduced to a consistent { error: 'CODE', ... } envelope
  // (matching the shape the rest of the API already uses) instead of the
  // library's default ad-hoc body. Route handlers that call
  // reply.code(...).send(...) directly bypass this entirely and are
  // unaffected.
  app.setErrorHandler((err, request, reply) => {
    if (err.validation || err.code === 'FST_ERR_VALIDATION') {
      reply.code(400).send({ error: 'VALIDATION_FAILED', message: err.message });
      return;
    }

    if (err.statusCode && err.statusCode < 500) {
      reply.code(err.statusCode).send({ error: err.code || 'REQUEST_FAILED', message: err.message });
      return;
    }

    // logger is disabled (Fastify({ logger: false }) above), so log
    // server-side directly rather than silently swallowing the failure.
    console.error(err);
    reply.code(500).send({ error: 'INTERNAL_ERROR' });
  });

  registerAuthRoutes(app, options);
  registerDecisionsRoutes(app);
  return app;
}
