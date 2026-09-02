import 'dotenv/config';
import { buildApp } from './app';
import { pgPool } from './db/client';

const app = buildApp();
const port = Number(process.env.PORT) || 3000;

app
  .listen({ port, host: '0.0.0.0' })
  .then(() => {
    console.log(`Server listening on port ${port}`);
  })
  .catch((err) => {
    console.error(err);
    process.exit(1);
  });

// Close the HTTP server before the DB pool so in-flight requests get a
// chance to finish (or at least stop accepting new ones) before their
// connections are yanked out from under them.
async function shutdown(signal: NodeJS.Signals): Promise<void> {
  console.log(`Received ${signal}, shutting down gracefully`);
  try {
    await app.close();
    await pgPool.end();
    process.exit(0);
  } catch (err) {
    console.error('Error during shutdown', err);
    process.exit(1);
  }
}

process.on('SIGTERM', shutdown);
process.on('SIGINT', shutdown);
