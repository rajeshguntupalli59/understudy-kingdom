export function up(knex) {
  return knex.schema.createTable('users', (table) => {
    table.uuid('id').primary().defaultTo(knex.raw('gen_random_uuid()'));
    table.string('device_id').unique();
    table.string('device_secret_hash');
    table.string('google_sub').unique();
    table.string('apple_sub').unique();
    table.string('email');
    table.timestamp('created_at').defaultTo(knex.fn.now());
    table.string('country_code');
  });
}

export function down(knex) {
  return knex.schema.dropTable('users');
}
