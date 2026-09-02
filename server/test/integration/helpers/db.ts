import { sql } from 'drizzle-orm';
import { db } from '../../../src/db/client';

export async function truncateTables(): Promise<void> {
  await db.execute(sql`TRUNCATE TABLE decisions, ruler_npcs, kingdoms RESTART IDENTITY CASCADE`);
}
