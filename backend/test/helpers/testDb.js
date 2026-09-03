import knex from '../../src/db/knex.js';

export async function truncateAll() {
  await knex.raw('TRUNCATE TABLE decisions, ruler_npcs, kingdoms, users RESTART IDENTITY CASCADE');
}

export { knex };
