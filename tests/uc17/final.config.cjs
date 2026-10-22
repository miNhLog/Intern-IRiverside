const base = require('./playwright.config.cjs');
module.exports = { ...base, testMatch: 'final.spec.cjs',
  reporter: [['list'], ['json', { outputFile: process.env.UC17_RUNTIME + '/final-results.json' }]] };
