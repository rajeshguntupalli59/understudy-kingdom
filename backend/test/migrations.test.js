import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import knex from '../src/db/knex.js';

describe('migrations', () => {
  afterAll(async () => {
    await knex.destroy();
  });

  it('creates the users, kingdoms, ruler_npcs, and decisions tables', async () => {
    for (const table of ['users', 'kingdoms', 'ruler_npcs', 'decisions']) {
      const exists = await knex.schema.hasTable(table);
      expect(exists).toBe(true);
    }
  });

  it('enforces one decision per kingdom per cycle_number', async () => {
    const [user] = await knex('users').insert({ device_id: 'test-device-1' }).returning('id');
    const [kingdom] = await knex('kingdoms').insert({ user_id: user.id }).returning('id');
    await knex('decisions').insert({
      kingdom_id: kingdom.id, cycle_number: 1,
      player_recommendation: {}, ruler_outcome: {}, overridden: false,
    });
    await expect(
      knex('decisions').insert({
        kingdom_id: kingdom.id, cycle_number: 1,
        player_recommendation: {}, ruler_outcome: {}, overridden: false,
      })
    ).rejects.toThrow();
  });
});
