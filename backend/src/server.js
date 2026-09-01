import Fastify from 'fastify';
import { registerAuthRoutes } from './routes/auth.js';

export function buildServer() {
  const app = Fastify({ logger: false });
  registerAuthRoutes(app);
  return app;
}
