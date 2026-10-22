const { test, expect, request } = require('@playwright/test');
const fs = require('fs');
const crypto = require('crypto');
const runtime = process.env.UC17_RUNTIME;
const { ids, password } = JSON.parse(fs.readFileSync(runtime + '/fixtures.json'));
const API = 'https://localhost:17401', WEB = 'https://localhost:17402', BASE = '/quan-ly/hop-dong-thanh-toan';
let api, anonymous, webState, roleContexts = [];
const pay = (key, amount, extra = {}) => ({ contractId: ids[key], requestId: crypto.randomUUID(), amount,
  paymentType: 'Đặt cọc', paymentMethod: 'Tiền mặt', ...extra });
async function post(data, expected = 200, ctx = api) {
 const r = await ctx.post('/api/manager/payments', { data }); expect.soft(r.status()).toBe(expected); return r;
}
async function detail(key) { const r = await api.get('/api/manager/contracts/' + ids[key]); expect(r.status()).toBe(200); return r.json(); }
async function update(key, data) { return api.put('/api/manager/contracts/'+ids[key], {data:{totalValue:1000,contractContent:'Synthetic contract',paymentTerms:'Synthetic terms',...data}}); }
async function open(page,key) {
 await page.goto(WEB+BASE+'?contractId='+ids[key]);
 await expect(page.locator('#drawerCode')).toHaveText('HD-'+key);
 await page.locator('[data-drawer-tab="payments"]').click();
}
async function collect(page,amount,type='Đặt cọc') {
 await page.getByRole('button',{name:'Ghi nhận thanh toán',exact:true}).click();
 await page.locator('#recordPaymentType').selectOption(type);
 await page.locator('#recordPaymentAmount').fill(String(amount));
 await page.locator('#recordPaymentMethod').fill('Tiền mặt');
 const response=page.waitForResponse(r=>r.url().endsWith(BASE+'/payments')&&r.request().method()==='POST');
 await page.locator('#savePayment').click(); expect((await response).status()).toBe(200);
 await expect(page.locator('#recordPaymentModal')).not.toHaveClass(/open/);
}
test.beforeAll(async ({browser})=>{
 anonymous=await request.newContext({baseURL:API,ignoreHTTPSErrors:true});
 const cache=runtime+'/auth-runtime.json';
 if(fs.existsSync(cache)) {
  const saved=JSON.parse(fs.readFileSync(cache)); webState=saved.webState;
  for(const t of saved.tokens) { const c=await request.newContext({baseURL:API,ignoreHTTPSErrors:true,extraHTTPHeaders:{Authorization:'Bearer '+t}}); if(!api) api=c;else roleContexts.push(c); }
  return;
 }
 const tokens=[];
 // Customer login is intentionally rejected by AuthService; report that role coverage BLOCKED, do not fake JWTs.
 for(const i of [2,1,3,4]) {
  const r=await anonymous.post('/api/auth/login',{data:{identifier:`role${i}@uc17.invalid`,password}});
  expect(r.status(),'isolated login').toBe(200); const body=await r.json(); tokens.push(body.accessToken);
  const ctx=await request.newContext({baseURL:API,ignoreHTTPSErrors:true,extraHTTPHeaders:{Authorization:'Bearer '+body.accessToken}});
  if(i===2) api=ctx; else roleContexts.push(ctx);
 }
 const ctx=await browser.newContext({ignoreHTTPSErrors:true}); const page=await ctx.newPage();
 await page.goto(WEB+'/login');
 await page.locator('input[name="Identifier"]').fill('role2@uc17.invalid');
 await page.locator('input[name="Password"]').fill(password);
 await Promise.all([page.waitForURL(u=>u.pathname!='/login'),page.getByRole('button',{name:'Đăng Nhập',exact:true}).click()]);
 webState=await ctx.storageState(); await ctx.close();
 fs.writeFileSync(cache,JSON.stringify({webState,tokens}));
});
test.afterAll(async()=>{for(const c of [api,anonymous,...roleContexts]) await c?.dispose();});
test.beforeEach(async({context})=>{await context.addCookies(webState?.cookies||[]);});

test('S01 commitment per contract and draft validation',async({page})=>{
 for(const value of [0,-1,1001,0.001]) expect.soft((await update('S01',{requiredDepositAmount:value})).status()).toBe(400);
 expect((await update('S01',{requiredDepositAmount:250})).status()).toBe(200);
 expect((await detail('S01')).requiredDepositAmount).toBe(250);
 expect((await detail('S02')).requiredDepositAmount).toBe(300);
 await open(page,'S01'); await page.getByRole('button',{name:'Sửa hợp đồng',exact:true}).click();
 await expect(page.locator('#editDeposit')).toHaveValue('250');
 await page.locator('#editDeposit').fill('275'); await page.locator('#saveEdit').click();
 await expect.poll(async()=> (await detail('S01')).requiredDepositAmount).toBe(275);
});
test('S02 three payment types through WEB and balances',async({page})=>{
 await open(page,'S02');
 for(const [amount,type,status,paid] of [[100,'Đặt cọc','DRAFT',100],[200,'Đặt cọc','ACTIVE',300],[400,'Thanh toán đợt','ACTIVE',700],[300,'Quyết toán','ACTIVE',1000]]) {
  await collect(page,amount,type); const d=await detail('S02'); expect(d.statusCode).toBe(status); expect(d.paidAmount).toBe(paid); expect(d.remainingBalance).toBe(1000-paid);
 }
 expect((await detail('S02')).payments).toHaveLength(4);
});
test('S03 partial deposits allowed; prerequisites separate',async()=>{
 expect((await api.post('/api/manager/contracts/'+ids.S03+'/activate')).status()).toBe(409);
 await post(pay('S03',50)); await post(pay('S03',50)); expect((await detail('S03')).statusCode).toBe('DRAFT');
 await post(pay('S03bad',100)); expect((await detail('S03bad')).paidAmount).toBe(100);
 await post(pay('S03bad',200),409); const d=await detail('S03bad'); expect(d.paidAmount).toBe(100); expect(d.statusCode).toBe('DRAFT');
});
test('S04 one-time legacy verification API and WEB',async({page})=>{
 for(const key of ['S04D','S04A']) await post(pay(key,10),409);
 const verify=(key,n)=>api.post('/api/manager/contracts/'+ids[key]+'/deposit-commitment',{data:{requiredDepositAmount:n}});
 expect((await verify('S04D',300)).status()).toBe(200); expect((await verify('S04D',400)).status()).toBe(409);
 await open(page,'S04A'); await page.getByRole('button',{name:'Xác minh mức cọc',exact:true}).click(); await page.locator('#verifiedDeposit').fill('300'); await page.locator('#saveDepositConfirmation').click();
 await expect.poll(async()=> (await detail('S04A')).requiredDepositAmount).toBe(300); expect((await detail('S04A')).statusCode).toBe('ACTIVE');
 const race=await Promise.all([verify('S04race',200),verify('S04race',300)]); expect(race.map(r=>r.status()).sort()).toEqual([200,409]);
});
test('S05 only SUCCESS and exact decimal',async()=>{
 const d=await detail('S05'); expect(d.paidAmount).toBe(100.25); expect(d.depositPaidAmount).toBe(100.25); expect(d.remainingBalance).toBe(899.75); expect(d.payments).toHaveLength(4);
 await post(pay('S05','0.01')); const after=await detail('S05'); expect(after.paidAmount).toBe(100.26); expect(after.remainingBalance).toBe(899.74);
});
test('S06 invalid input and ineligible contracts',async()=>{
 for(const change of [{amount:0},{amount:-1},{amount:0.001},{amount:'invalid'},{amount:'10000000000000000'},{paymentMethod:' '},{paymentMethod:'x'.repeat(51)},{transactionCode:'x'.repeat(101)},{requestId:'00000000-0000-0000-0000-000000000000'}]) await post(pay('S06',1,change),400);
 await post(pay('S06',1,{paymentType:'OTHER'}),409);
 for(const t of ['Thanh toán đợt','Quyết toán']) await post(pay('S06',1,{paymentType:t}),409);
 for(const key of ['S06closed','S06cancel']) await post(pay(key,1),409);
 await post(pay('S06',1,{contractId:2147483647}),404); expect((await detail('S06')).payments).toHaveLength(0);
});
test('S07 overpayment and exact remaining',async()=>{
 await post(pay('S07',400)); await post(pay('S07',600.01),409); await post(pay('S07',600)); await post(pay('S07',0.01),409);
 expect((await detail('S07')).remainingBalance).toBe(0); await post(pay('S07bad',1),409);
});
test('S08 durable idempotency normalized payload and conflicts',async()=>{
 const p=pay('S08',10,{paymentMethod:' Tiền mặt ',transactionCode:' REF '}); const r=await post(p); const first=await r.json();
 const replay=await post({...p,paymentMethod:'Tiền mặt',transactionCode:'REF'}); const b=await replay.json(); expect(b.paymentId).toBe(first.paymentId); expect(b.replayed).toBe(true);
 for(const change of [{amount:11},{paymentType:'Thanh toán đợt'},{paymentMethod:'Khác'},{transactionCode:'OTHER'},{contractId:ids.S08other}]) await post({...p,...change},409);
 expect((await detail('S08')).payments).toHaveLength(1);
});
test('S09 concurrent requests serialize balance and replay',async()=>{
 const p=pay('S09same',10); const same=await Promise.all([post(p),post(p)]); expect(same.map(r=>r.status())).toEqual([200,200]); expect((await detail('S09same')).payments).toHaveLength(1);
 const over=await Promise.all([api.post('/api/manager/payments',{data:pay('S09over',700)}),api.post('/api/manager/payments',{data:pay('S09over',700)})]);
 expect(over.map(r=>r.status()).sort()).toEqual([200,409]); expect((await detail('S09over')).paidAmount).toBe(700);
});
test('S10 Manager only API and anonymous WEB',async()=>{
 for(const ctx of [anonymous,...roleContexts]) {
  const expected=ctx===anonymous?401:403;
  for(const path of ['/api/manager/payments','/api/manager/contracts/'+ids.S10]) expect.soft((await ctx.get(path)).status()).toBe(expected);
  await post(pay('S10',1),expected,ctx);
  expect.soft((await ctx.post('/api/manager/contracts/'+ids.S10+'/deposit-commitment',{data:{requiredDepositAmount:300}})).status()).toBe(expected);
 }
 const ctx=await request.newContext({baseURL:WEB,ignoreHTTPSErrors:true});
 expect((await ctx.get(BASE,{maxRedirects:0})).status()).toBe(302); await ctx.dispose();
});
test('S11 WEB CSRF valid and invalid tokens',async({page})=>{
 await open(page,'S11'); const csrf=await page.locator('#cpAntiforgery input').inputValue();
 for(const path of ['/payments','/contracts/'+ids.S11+'/deposit-commitment']) {
  expect.soft((await page.request.post(WEB+BASE+path,{data:pay('S11',1)})).status()).toBe(400);
  expect.soft((await page.request.post(WEB+BASE+path,{data:pay('S11',1),headers:{RequestVerificationToken:'invalid'}})).status()).toBe(400);
 }
 expect((await page.request.post(WEB+BASE+'/payments',{data:pay('S11',1),headers:{RequestVerificationToken:csrf}})).status()).toBe(200);
 await page.context().clearCookies({name:'rp_api_token'});
 const r=await page.request.post(WEB+BASE+'/payments',{data:pay('S11',1),headers:{RequestVerificationToken:csrf},maxRedirects:0});
 expect([302,401]).toContain(r.status()); expect((await detail('S11')).payments).toHaveLength(1);
});
test('S12 lost response retry keeps intent across reload',async({page})=>{
 await open(page,'S12');
 await page.route('**'+BASE+'/payments',async route=>{if(route.request().method()==='POST'){await route.fetch();await route.abort('failed');}else await route.continue();});
 await page.getByRole('button',{name:'Ghi nhận thanh toán',exact:true}).click(); await page.locator('#recordPaymentAmount').fill('100'); await page.locator('#recordPaymentMethod').fill('Tiền mặt'); await page.locator('#savePayment').click();
 await expect(page.locator('#recordPaymentError')).not.toBeEmpty(); expect((await detail('S12')).payments).toHaveLength(1);
 const intent=await page.evaluate(id=>sessionStorage.getItem('uc17-payment-intent-'+id),ids.S12); expect(intent).toBeTruthy();
 await page.unroute('**'+BASE+'/payments'); await open(page,'S12'); await collect(page,100);
 expect((await detail('S12')).payments).toHaveLength(1);
 expect(await page.evaluate(id=>sessionStorage.getItem('uc17-payment-intent-'+id),ids.S12)).toBeNull();
});
test('S13 money frozen after partial payment; text editable',async({page})=>{
 await post(pay('S13',100)); expect((await update('S13',{totalValue:1200})).status()).toBe(409); expect((await update('S13',{requiredDepositAmount:400})).status()).toBe(409);
 expect((await update('S13',{contractContent:'Corrected text'})).status()).toBe(200); expect((await detail('S13')).requiredDepositAmount).toBe(300);
 await open(page,'S13'); await page.getByRole('button',{name:'Sửa hợp đồng',exact:true}).click();
 expect(await page.locator('#editTotalValue').evaluate(e=>e.readOnly)).toBe(true); expect(await page.locator('#editDeposit').evaluate(e=>e.readOnly)).toBe(true);
});
test('S14 legacy history and filters survive UI refresh',async({page})=>{
 await open(page,'S14'); expect((await detail('S14')).payments).toHaveLength(4);
 await page.reload(); await expect(page.locator('#drawerCode')).toHaveText('HD-S14');
 const response=await api.get('/api/manager/payments',{params:{keyword:'HD-S14',type:'Đặt cọc',status:'SUCCESS'}});
 const b=await response.json(); expect(b.totalItems).toBe(1); expect(b.items[0].amount).toBe(100.25);
 await page.locator('#closeDrawer').click(); await page.locator('[data-main-tab="payments"]').click();
 await page.locator('#paymentKeyword').fill('HD-S14'); await page.locator('#paymentStatus').selectOption('SUCCESS');
 await expect(page.locator('#paymentRows')).toContainText('HD-S14');
});
