import Fastify from 'fastify';
import { registerAuthRoutes } from './routes/auth.js';
import { registerDecisionsRoutes } from './routes/decisions.js';

export function buildServer(options = {}) {
  const app = Fastify({ logger: false });
  registerAuthRoutes(app, options);
  registerDecisionsRoutes(app);
  return app;
}
