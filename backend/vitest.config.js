import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    // Every test file shares one real Postgres test database (truncate-based
    // cleanup between tests -- see test/helpers/testDb.js). Running test
    // files in parallel (Vitest's default) races those truncates against
    // concurrently-running inserts/reads in other files, causing rare,
    // non-reproducible failures. Discovered by running the full suite
    // repeatedly during Backend Task 4's review and seeing intermittent
    // failures that a single run didn't reproduce.
    fileParallelism: false,
  },
});
