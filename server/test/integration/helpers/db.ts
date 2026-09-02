import { sql } from 'drizzle-orm';
import { db } from '../../../src/db/client';

// Guards against running a real TRUNCATE against whatever DATABASE_URL
// happens to be configured. Harmless today (no real user data exists yet),
// but once a future milestone has the Unity client syncing real player data
// to this same database, an unguarded TRUNCATE in every test's afterEach
// would be a standing landmine. Requires an explicit opt-in rather than
// e.g. sniffing the DB name/URL for "test", since that kind of heuristic is
// easy to spoof or misconfigure.
export async function truncateTables(): Promise<void> {
  if (process.env.ALLOW_TEST_DB_TRUNCATE !== 'true') {
    throw new Error(
      'Refusing to TRUNCATE: ALLOW_TEST_DB_TRUNCATE is not set to "true". ' +
        'This guard exists because DATABASE_URL points at a real database -- ' +
        'set ALLOW_TEST_DB_TRUNCATE=true in server/.env only when it is safe ' +
        'to destroy all rows in decisions/ruler_npcs/kingdoms (e.g. a ' +
        'dedicated test project, never one holding real player data).',
    );
  }

  await db.execute(sql`TRUNCATE TABLE pvp_duels, decisions, ruler_npcs, kingdoms RESTART IDENTITY CASCADE`);
}
