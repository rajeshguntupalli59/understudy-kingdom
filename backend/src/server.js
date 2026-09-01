import Fastify from 'fastify';
import { registerAuthRoutes } from './routes/auth.js';

export function buildServer(options = {}) {
  const app = Fastify({ logger: false });
  registerAuthRoutes(app, options);
  return app;
}
