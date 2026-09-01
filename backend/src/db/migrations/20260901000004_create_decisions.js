export function up(knex) {
  return knex.schema.createTable('decisions', (table) => {
    table.uuid('id').primary().defaultTo(knex.raw('gen_random_uuid()'));
    table.uuid('kingdom_id').notNullable().references('id').inTable('kingdoms');
    table.integer('cycle_number').notNullable();
    table.jsonb('player_recommendation').notNullable();
    table.jsonb('ruler_outcome').notNullable();
    table.boolean('overridden').notNullable();
    table.timestamp('created_at').defaultTo(knex.fn.now());
    table.unique(['kingdom_id', 'cycle_number']);
  });
}

export function down(knex) {
  return knex.schema.dropTable('decisions');
}
