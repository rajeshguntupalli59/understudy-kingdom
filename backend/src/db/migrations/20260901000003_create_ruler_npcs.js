export function up(knex) {
  return knex.schema.createTable('ruler_npcs', (table) => {
    table.uuid('id').primary().defaultTo(knex.raw('gen_random_uuid()'));
    table.uuid('kingdom_id').notNullable().references('id').inTable('kingdoms');
    table.integer('mood').notNullable().defaultTo(50);
    table.integer('loyalty').notNullable().defaultTo(50);
    table.string('agenda').notNullable().defaultTo('Expansionist');
    table.integer('trait_seed');
  });
}

export function down(knex) {
  return knex.schema.dropTable('ruler_npcs');
}
