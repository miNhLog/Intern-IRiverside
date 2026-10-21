// Remaining subcases of the same initial pass, not a rerun of S01–S14.
const {test,expect,request}=require('@playwright/test');
const fs=require('fs'); const root=process.env.UC17_RUNTIME;
const {webState,tokens}=JSON.parse(fs.readFileSync(root+'/auth-runtime.json'));
const {ids,password}=JSON.parse(fs.readFileSync(root+'/fixtures.json'));
const WEB='https://localhost:17402', BASE='/quan-ly/hop-dong-thanh-toan';
test('S01 remaining create with commitment through WEB',async({page,context})=>{
 await context.addCookies(webState.cookies);await page.goto(WEB+BASE);await page.locator('#openCreateContract').click();
 await page.locator('#eligibleKeyword').fill('DT-CREATE');await page.locator('#eligibleBookings').getByRole('button').first().click();
 await page.locator('#createTotalValue').fill('1000');await page.locator('#createDeposit').fill('125');await page.locator('#saveDraft').click();
 await expect(page.locator('#createModal')).not.toHaveClass(/open/);await expect(page.locator('#drawerGeneral')).toContainText('125');
});
test('S10 remaining WEB non-Manager role access',async()=>{
 for(const i of [1,3,4]) {
  const c=await request.newContext({baseURL:WEB,ignoreHTTPSErrors:true});
  const html=await (await c.get('/login')).text();const token=html.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/)[1];
  await c.post('/login',{form:{Identifier:`role${i}@uc17.invalid`,Password:password,__RequestVerificationToken:token},maxRedirects:0});
  const r=await c.get(BASE,{maxRedirects:0});expect(r.status()).toBe(302);expect(r.headers().location).toContain('khong-co-quyen');
  const p=await c.post(BASE+'/payments',{data:{},maxRedirects:0});expect(p.status()).toBe(302);expect(p.headers().location).toContain('khong-co-quyen');await c.dispose();
 }
});
test('S12 remaining changed intent cancel and double submit',async({page,context})=>{
 await context.addCookies(webState.cookies);await page.goto(WEB+BASE+'?contractId='+ids.MCP);await page.locator('[data-drawer-tab=payments]').click();
 await page.evaluate(id=>sessionStorage.setItem('uc17-payment-intent-'+id,JSON.stringify({requestId:crypto.randomUUID(),digest:'old'})),ids.MCP);
 await page.getByRole('button',{name:'Ghi nhận thanh toán',exact:true}).click();await page.locator('#recordPaymentAmount').fill('100');await page.locator('#recordPaymentMethod').fill('Tiền mặt');
 let requests=0;page.on('request',r=>{if(r.url().endsWith(BASE+'/payments')&&r.method()==='POST') requests++;});page.once('dialog',d=>d.dismiss());
 await page.locator('#savePayment').click();await expect(page.locator('#savePayment')).toBeEnabled();expect(requests).toBe(0);
 await page.evaluate(id=>sessionStorage.removeItem('uc17-payment-intent-'+id),ids.MCP);
 await page.locator('#savePayment').dblclick();await expect(page.locator('#recordPaymentModal')).not.toHaveClass(/open/);expect(requests).toBe(1);
});
