const base = require('./playwright.config.cjs');
module.exports = { ...base, testMatch: 'review.spec.cjs',
  reporter: [['list'], ['json', { outputFile: process.env.UC17_RUNTIME + '/review-results.json' }]] };
