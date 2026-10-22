const { defineConfig } = require('@playwright/test');
module.exports = defineConfig({ testDir: '.', testMatch: 'uc17.spec.cjs', workers: 1, retries: 0,
  timeout: 60000, reporter: [['list'], ['json', { outputFile: process.env.UC17_RUNTIME + '/results.json' }]],
  outputDir: process.env.UC17_RUNTIME + '/artifacts',
  use: { ignoreHTTPSErrors: true, trace: 'off', screenshot: 'off', video: 'off' } });
