import 'dotenv/config';

const base = {
  client: 'pg',
  migrations: { directory: './src/db/migrations' },
};

export default {
  development: {
    ...base,
    connection: process.env.DATABASE_URL
      || 'postgres://understudy_kingdom:devpassword@localhost:5432/understudy_kingdom_dev',
  },
  test: {
    ...base,
    connection: process.env.TEST_DATABASE_URL
      || 'postgres://understudy_kingdom:devpassword@localhost:5432/understudy_kingdom_test',
  },
};
