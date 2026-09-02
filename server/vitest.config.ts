import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'node',
    include: ['test/**/*.test.ts'],
    testTimeout: 15000,
    // Integration tests share one live Postgres database and each DB-touching
    // test file truncates the same tables in afterEach. Vitest runs test
    // *files* in parallel by default, which was harmless with a single DB
    // test file (kingdoms.test.ts) but starts racing -- TRUNCATE's ACCESS
    // EXCLUSIVE lock colliding with another file's in-flight INSERT, causing
    // FK violations and deadlocks -- now that decisions.test.ts is a second
    // one. Run files sequentially so DB state stays isolated between them.
    fileParallelism: false,
  },
});
