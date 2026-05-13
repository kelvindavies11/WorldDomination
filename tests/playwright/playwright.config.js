// @ts-check
const { defineConfig } = require("@playwright/test");

module.exports = defineConfig({
  testDir: "./specs",
  timeout: 60_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL: "http://localhost:5057",
    headless: true,
  },
  // Do not spin up a dev server — the user starts it manually with dotnet run
  webServer: undefined,
});
