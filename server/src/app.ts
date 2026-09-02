import Fastify, { FastifyInstance } from 'fastify';

export function buildApp(): FastifyInstance {
  const app = Fastify({ logger: false });

  // Any error a route handler doesn't explicitly reply to (a thrown
  // exception, a DB failure) lands here. 4xx errors set by routes/schema
  // validation pass through with their own message; everything else is
  // logged server-side only and replies with a generic message -- never
  // leak internals (query text, stack traces) to the client. See
  // docs/superpowers/specs/2026-09-02-backend-service-design.md's Error
  // Handling section.
  app.setErrorHandler((error, _request, reply) => {
    if (error.statusCode && error.statusCode < 500) {
      reply.code(error.statusCode).send({ error: error.message });
      return;
    }

    console.error(error);
    reply.code(500).send({ error: 'Internal server error' });
  });

  app.get('/health', async () => {
    return { status: 'ok' };
  });

  return app;
}
