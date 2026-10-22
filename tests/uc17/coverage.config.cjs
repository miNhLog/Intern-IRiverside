const base=require('./playwright.config.cjs');
module.exports={...base,testMatch:'coverage.spec.cjs',reporter:[['list'],['json',{outputFile:process.env.UC17_RUNTIME+'/coverage-results.json'}]]};
