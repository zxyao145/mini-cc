import assert from 'node:assert/strict';
import { chromium, expect } from '@playwright/test';

const origin = process.env.E2E_BASE_URL ?? 'http://127.0.0.1:5000';
const browser = await chromium.launch();

try {
  const page = await browser.newPage();
  await expect(async () => {
    const response = await page.goto(origin);
    assert.equal(response?.status(), 200, 'The library page must load through nginx');
    await expect(page.getByRole('heading', { name: '登录', exact: true })).toBeVisible();
  }).toPass({ timeout: 30_000 });
  await page.getByRole('textbox', { name: '用户名' }).fill('mini_cc');
  await page.getByLabel('密码', { exact: true }).fill('mini_cc_password');

  const [login, articles] = await Promise.all([
    page.waitForResponse(response => response.url() === `${origin}/api/account/login` && response.request().method() === 'POST'),
    page.waitForResponse(response => {
      const url = new URL(response.url());
      return url.origin === origin && url.pathname === '/api/articles' && response.request().method() === 'GET';
    }),
    page.getByRole('button', { name: '登录', exact: true }).click(),
  ]);

  assert.equal(login.status(), 200, 'Sign-in must succeed through nginx');
  assert.equal(articles.status(), 200, 'The authenticated library must load through nginx');
  assert.deepEqual(await articles.json(), { items: [], total: 0, nextCursor: null });
  await expect(page.getByRole('link', { name: '收件箱', exact: true })).toBeVisible();
} finally {
  await browser.close();
}
