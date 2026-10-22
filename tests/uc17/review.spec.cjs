const { test, expect, request } = require('@playwright/test');
const fs = require('fs');
const root = process.env.UC17_RUNTIME;
const { ids, password } = JSON.parse(fs.readFileSync(root + '/fixtures.json'));
const WEB = 'https://localhost:17402', API = 'https://localhost:17401';
const BASE = '/quan-ly/hop-dong-thanh-toan';
let auth, api;
async function login(page) {
  await page.goto(WEB + '/login');
  await page.locator('[name=Identifier]').fill('role2@uc17.invalid');
  await page.locator('[name=Password]').fill(password);
  await Promise.all([page.waitForURL(u => u.pathname !== '/login'),
    page.getByRole('button', { name: 'Đăng Nhập', exact: true }).click()]);
}
test.beforeAll(async ({ browser }) => {
  const anonymous = await request.newContext({ baseURL: API, ignoreHTTPSErrors: true });
  const r = await anonymous.post('/api/auth/login', { data: { identifier: 'role2@uc17.invalid', password } });
  expect(r.status()).toBe(200);
  api = await request.newContext({ baseURL: API, ignoreHTTPSErrors: true,
    extraHTTPHeaders: { Authorization: 'Bearer ' + (await r.json()).accessToken } });
  await anonymous.dispose();
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  await login(await context.newPage()); auth = await context.storageState(); await context.close();
});
test.afterAll(async () => { await api?.dispose(); });
test.beforeEach(async ({ context }) => { await context.addCookies(auth.cookies); });
async function open(page, key) {
  await page.goto(WEB + BASE + '?contractId=' + ids[key]);
  await expect(page.locator('#drawerCode')).toHaveText('HD-' + key);
  await page.locator('[data-drawer-tab=payments]').click();
}
async function fillPayment(page) {
  await page.locator('#openRecordPayment').click();
  await page.locator('#recordPaymentAmount').fill('100');
  await page.locator('#recordPaymentMethod').fill('Tiền mặt');
}
const intent = (page, id) => page.evaluate(id => sessionStorage.getItem('uc17-payment-intent-' + id), id);
async function detail(key) { return (await api.get('/api/manager/contracts/' + ids[key])).json(); }

test('CR01 uncertain commit, actual expired session redirect, re-login and same RequestId replay', async ({ page, context }) => {
  await open(page, 'S12'); await fillPayment(page);
  const keys = [];
  page.on('request', r => { if (r.url().endsWith(BASE + '/payments') && r.method() === 'POST') keys.push(r.postDataJSON().requestId); });
  await page.route('**' + BASE + '/payments', async route => { await route.fetch(); await route.abort('failed'); });
  await page.locator('#savePayment').click();
  await expect(page.locator('#recordPaymentError')).not.toBeEmpty();
  const original = await intent(page, ids.S12); expect(original).toBeTruthy();
  expect((await detail('S12')).payments).toHaveLength(1);
  await page.unroute('**' + BASE + '/payments');
  await context.clearCookies({ name: 'rp_api_token' });
  const redirect = page.waitForResponse(r => r.url().includes('/login') && r.request().redirectedFrom());
  await page.locator('#savePayment').click(); await redirect;
  await expect(page.locator('#recordPaymentError')).toContainText('Chưa nhận được xác nhận');
  expect(await intent(page, ids.S12)).toBe(original);
  await expect(page.locator('#recordPaymentModal')).toHaveClass(/open/);
  await login(page); await open(page, 'S12'); await fillPayment(page);
  await page.locator('#savePayment').click();
  await expect(page.locator('#recordPaymentModal')).not.toHaveClass(/open/);
  expect(await intent(page, ids.S12)).toBeNull();
  expect(new Set(keys).size).toBe(1); expect(keys).toHaveLength(3);
  const d = await detail('S12'); expect(d.payments).toHaveLength(1); expect(d.paidAmount).toBe(100);
});

test('CR01 rejects non-JSON, malformed JSON and unexpected success contracts without losing intent', async ({ page }) => {
  await open(page, 'S02'); await fillPayment(page);
  const keys = [];
  const invalid = [
    ['text/html', '<html>login</html>'], ['text/plain', 'ok'], ['application/json', '{'],
    ['application/json', 'null'], ['application/json', '{}'],
    ['application/json', JSON.stringify({ success: true, data: { paymentId: 0, replayed: false, message: 'bad' } })],
    ['application/json', JSON.stringify({ success: true, data: { paymentId: '1', replayed: false, message: 'bad' } })],
    ['application/json', JSON.stringify({ success: true, data: { paymentId: 1 } })]
  ];
  let original;
  for (const [contentType, body] of invalid) {
    await page.route('**' + BASE + '/payments', async route => {
      keys.push(route.request().postDataJSON().requestId); await route.fulfill({ status: 200, contentType, body });
    });
    await page.locator('#recordPaymentError').evaluate(e => e.textContent = '');
    await page.locator('#savePayment').click();
    await expect(page.locator('#recordPaymentError')).not.toBeEmpty();
    await expect(page.locator('#savePayment')).toBeEnabled();
    original ??= await intent(page, ids.S02);
    expect(await intent(page, ids.S02)).toBe(original);
    await expect(page.locator('#recordPaymentModal')).toHaveClass(/open/);
    await page.unroute('**' + BASE + '/payments');
  }
  expect(new Set(keys).size).toBe(1); expect((await detail('S02')).payments).toHaveLength(0);
  await page.locator('#savePayment').dblclick();
  await expect(page.locator('#openRecordPayment')).toBeFocused();
  expect(await intent(page, ids.S02)).toBeNull(); expect((await detail('S02')).payments).toHaveLength(1);
});

test('CR03 commitment keyboard lifecycle, drawer preservation and focus after confirmation', async ({ page }) => {
  await open(page, 'S04A'); const trigger = page.locator('#openConfirmDeposit');
  await trigger.click(); await expect(page.locator('#verifiedDeposit')).toBeFocused();
  const first = page.locator('#confirmDepositModal [data-close-modal]'), last = page.locator('#saveDepositConfirmation');
  await last.focus(); await page.keyboard.press('Tab'); await expect(first).toBeFocused();
  await page.keyboard.press('Shift+Tab'); await expect(last).toBeFocused();
  await page.locator('#closeDrawer').focus(); await expect(page.locator('#verifiedDeposit')).toBeFocused();
  await page.keyboard.press('Escape'); await expect(trigger).toBeFocused();
  await expect(page.locator('#contractDrawer')).toHaveClass(/open/);
  await page.keyboard.press('Enter'); await first.click(); await expect(trigger).toBeFocused();
  await trigger.click(); await page.locator('#verifiedDeposit').fill('300'); await last.click();
  await expect(page.locator('#openRecordPayment')).toBeFocused();
  expect((await detail('S04A')).statusCode).toBe('ACTIVE');
  await page.locator('#openRecordPayment').click(); await expect(page.locator('#recordPaymentAmount')).toBeFocused();
  await page.keyboard.press('Escape'); await expect(page.locator('#openRecordPayment')).toBeFocused();
  await expect(page.locator('#contractDrawer')).toHaveClass(/open/);
});

test('CR02 isolated SQL hostile SET options, new filtered index and repeat execution', async () => {
  const result = JSON.parse(fs.readFileSync(root + '/review-sql.json'));
  expect(result).toEqual({ runs: 2, uniqueFilteredIndex: true, legacyCommitmentNull: true, legacyRequestNull: true, legacyAmount: 5 });
  const bootstrap = fs.readFileSync('Backend/HeThongDatTiecCuoi_API/Sql/RiversidePalaceDB_FINAL.sql', 'utf8');
  const beforeIndex = bootstrap.slice(bootstrap.indexOf('-- Required session options for the UC17 filtered index.'), bootstrap.indexOf('CREATE UNIQUE INDEX UX_ThanhToan_RequestId'));
  for (const option of ['ANSI_NULLS', 'ANSI_PADDING', 'ANSI_WARNINGS', 'ARITHABORT', 'CONCAT_NULL_YIELDS_NULL', 'QUOTED_IDENTIFIER'])
    expect(beforeIndex).toContain('SET ' + option + ' ON;');
  expect(beforeIndex).toContain('SET NUMERIC_ROUNDABORT OFF;');
});
