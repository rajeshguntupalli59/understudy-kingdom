// dotenv/config is loaded here directly (not just relied on transitively via
// src/db/knex.js) so this guard behaves the same whether it's imported first
// or last in the module graph -- e.g. from a standalone script that imports
// src/auth/tokens.js without ever touching the db layer.
import 'dotenv/config';

// Boot-time guard for required secrets/config. Throws immediately at import
// time when a required env var is unset and NODE_ENV isn't 'test', so a
// deploy can never silently fall back to an insecure default value baked
// into the repo. In test mode, returns the given placeholder so the suite
// can run without a real .env file.
export function requireEnv(name, testFallback) {
  const value = process.env[name];
  if (value) return value;
  if (process.env.NODE_ENV === 'test') return testFallback;
  throw new Error(`${name} must be set (refusing to start with an insecure default)`);
}
