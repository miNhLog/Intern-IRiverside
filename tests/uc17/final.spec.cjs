const { test, expect, request } = require('@playwright/test');
const fs = require('fs');
const root = process.env.UC17_RUNTIME;
const { ids, password } = JSON.parse(fs.readFileSync(root + '/fixtures.json'));
const WEB = 'https://localhost:17402', API = 'https://localhost:17401';
const BASE = '/quan-ly/hop-dong-thanh-toan';
let api, auth;
test.beforeAll(async ({ browser }) => {
  const anonymous = await request.newContext({ baseURL: API, ignoreHTTPSErrors: true });
  const login = await anonymous.post('/api/auth/login', { data: { identifier: 'role2@uc17.invalid', password } });
  expect(login.status()).toBe(200);
  api = await request.newContext({ baseURL: API, ignoreHTTPSErrors: true,
    extraHTTPHeaders: { Authorization: 'Bearer ' + (await login.json()).accessToken } });
  await anonymous.dispose();
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await context.newPage();
  await page.goto(WEB + '/login');
  await page.locator('[name=Identifier]').fill('role2@uc17.invalid');
  await page.locator('[name=Password]').fill(password);
  await Promise.all([page.waitForURL(u => u.pathname !== '/login', { waitUntil: 'domcontentloaded' }),
    page.getByRole('button', { name: 'Đăng Nhập', exact: true }).click()]);
  auth = await context.storageState();
  await context.close();
});
test.afterAll(async () => { await api?.dispose(); });
test.beforeEach(async ({ context }) => { await context.addCookies(auth.cookies); });
async function open(page, key) {
  await page.goto(WEB + BASE + '?contractId=' + ids[key]);
  await expect(page.locator('#drawerCode')).toHaveText('HD-' + key);
  await page.locator('[data-drawer-tab=payments]').click();
}
async function detail(key) { return (await api.get('/api/manager/contracts/' + ids[key])).json(); }

test('S14 payment dialog initial focus, keyboard confinement and close restoration', async ({ page }) => {
  await open(page, 'S02');
  const trigger = page.getByRole('button', { name: 'Ghi nhận thanh toán', exact: true });
  await trigger.click();
  await expect(page.locator('#recordPaymentAmount')).toBeFocused();
  const first = page.locator('#recordPaymentModal [data-close-modal]');
  const last = page.locator('#savePayment');
  await last.focus(); await page.keyboard.press('Tab'); await expect(first).toBeFocused();
  await page.keyboard.press('Shift+Tab'); await expect(last).toBeFocused();
  await page.locator('#closeDrawer').focus();
  await expect(page.locator('#recordPaymentAmount')).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(page.locator('#recordPaymentModal')).not.toHaveClass(/open/);
  await expect(page.locator('#contractDrawer')).toHaveClass(/open/);
  await expect(trigger).toBeFocused();
  await page.keyboard.press('Enter');
  await first.click(); await expect(trigger).toBeFocused();
});

test('S02/S12 affected submit, duplicate click and focus after drawer refresh', async ({ page }) => {
  await open(page, 'S12');
  await page.getByRole('button', { name: 'Ghi nhận thanh toán', exact: true }).click();
  await page.locator('#recordPaymentAmount').fill('100');
  await page.locator('#recordPaymentMethod').fill('Tiền mặt');
  let posts = 0;
  page.on('request', r => { if (r.url().endsWith(BASE + '/payments') && r.method() === 'POST') posts++; });
  await page.locator('#savePayment').dblclick();
  await expect.poll(async () => (await detail('S12')).paidAmount).toBe(100);
  await expect(page.locator('#openRecordPayment')).toBeFocused();
  expect(posts).toBe(1); expect((await detail('S12')).payments).toHaveLength(1);
});

test('S12 affected failure and retry keeps dialog focus and one payment', async ({ page }) => {
  await open(page, 'MCP');
  await page.route('**' + BASE + '/payments', async route => {
    await route.fetch(); await route.abort('failed');
  });
  await page.getByRole('button', { name: 'Ghi nhận thanh toán', exact: true }).click();
  await page.locator('#recordPaymentAmount').fill('100'); await page.locator('#recordPaymentMethod').fill('Tiền mặt');
  await page.locator('#savePayment').click();
  await expect(page.locator('#recordPaymentError')).not.toBeEmpty();
  await page.locator('#savePayment').focus(); await page.keyboard.press('Tab');
  await expect(page.locator('#recordPaymentModal [data-close-modal]')).toBeFocused();
  await page.unroute('**' + BASE + '/payments');
  await page.locator('#savePayment').click();
  await expect(page.locator('#openRecordPayment')).toBeFocused();
  expect((await detail('MCP')).payments).toHaveLength(1);
});

test('S04 unchanged legacy commitment dialog interaction', async ({ page }) => {
  await open(page, 'S04A');
  await page.getByRole('button', { name: 'Xác minh mức cọc', exact: true }).click();
  await page.locator('#verifiedDeposit').fill('300'); await page.locator('#saveDepositConfirmation').click();
  await expect.poll(async () => (await detail('S04A')).requiredDepositAmount).toBe(300);
  expect((await detail('S04A')).statusCode).toBe('ACTIVE');
  await page.getByRole('button', { name: 'Ghi nhận thanh toán', exact: true }).click();
  await expect(page.locator('#recordPaymentAmount')).toBeFocused();
});

test('S10 legitimate synthetic Customer login and authorization availability', async ({}, info) => {
  const context = await request.newContext({ baseURL: API, ignoreHTTPSErrors: true });
  try {
    const response = await context.post('/api/auth/login', { data: { identifier: 'role5@uc17.invalid', password } });
    fs.writeFileSync(root + '/customer-observation.json', JSON.stringify({ loginStatus: response.status(),
      tokenIssued: response.status() === 200, syntheticRole: 'Customer' }));
    if (response.status() === 401) {
      info.annotations.push({ type: 'BLOCKED', description: 'AuthService rejects Customer for password and Google login; no legitimate session, no forged JWT.' });
      test.skip(true, 'BLOCKED: existing authentication intentionally rejects Customer; cannot verify authenticated 403.');
    }
    expect(response.status()).toBe(200);
    const token = (await response.json()).accessToken;
    const headers = { Authorization: 'Bearer ' + token };
    expect((await context.get('/api/manager/payments', { headers })).status()).toBe(403);
    expect((await context.post('/api/manager/payments', { headers, data: { contractId: ids.S10,
      requestId: require('crypto').randomUUID(), amount: 1, paymentType: 'Đặt cọc', paymentMethod: 'Tiền mặt' } })).status()).toBe(403);
    expect((await context.post('/api/manager/contracts/' + ids.S10 + '/deposit-commitment', {
      headers, data: { requiredDepositAmount: 300 } })).status()).toBe(403);
  } finally { await context.dispose(); }
});

test('S14 date range includes both boundary days, excludes adjacent days API and WEB', async ({ page }) => {
  const query = { keyword: 'HD-S14', fromDate: '2026-01-10', toDate: '2026-01-11' };
  const r = await api.get('/api/manager/payments', { params: query }); expect(r.status()).toBe(200);
  const data = await r.json(); expect(data.totalItems).toBe(2);
  expect(data.items.map(p => p.transactionCode).sort()).toEqual(['DATE-START', 'DATE-END']);
  for (const params of [{ keyword: 'HD-S14', fromDate: '2026-01-12' }, { keyword: 'HD-S14', toDate: '2026-01-09' }]) {
    const list = await (await api.get('/api/manager/payments', { params })).json(); expect(list.totalItems).toBe(1);
  }
  await page.goto(WEB + BASE); await page.locator('[data-main-tab=payments]').click();
  await page.locator('#paymentKeyword').fill('HD-S14');
  await page.locator('#paymentFromDate').fill('2026-01-10'); await page.locator('#paymentFromDate').dispatchEvent('change');
  const response = page.waitForResponse(r => r.url().includes(BASE + '/payments?') &&
    new URL(r.url()).searchParams.get('toDate') === '2026-01-11');
  await page.locator('#paymentToDate').fill('2026-01-11'); await page.locator('#paymentToDate').dispatchEvent('change');
  expect((await response).status()).toBe(200);
  const rows = page.locator('#paymentRows tr'); await expect(rows).toHaveCount(2);
  await expect(page.locator('#paymentRows')).toContainText('DATE-START');
  await expect(page.locator('#paymentRows')).toContainText('DATE-END');
  await expect(page.locator('#paymentRows')).not.toContainText('DATE-BEFORE');
  await expect(page.locator('#paymentRows')).not.toContainText('DATE-AFTER');
});
