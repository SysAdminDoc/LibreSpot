import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    environment: "happy-dom",
    globals: true,
    clearMocks: true,
    restoreMocks: true,
    mockReset: true,
    include: ["tests/**/*.test.ts"],
    pool: "forks",
    fileParallelism: false,
    maxWorkers: 1,
  },
});
