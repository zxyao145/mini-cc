import { test, expect } from '@playwright/test';

// Run against an isolated deployment containing the documented 65-article fixture.
// Mutations are limited to creating/soft-deleting each test's own temporary article
// and removing/restoring one existing fixture tag in a finally block.
test.beforeEach(async ({ page }) => {
  const login = await page.request.post('/api/account/login', {
    form: { userName: process.env.E2E_USERNAME || 'mini_cc', password: process.env.E2E_PASSWORD || 'mini_cc_password', rememberMe: 'false' },
  });
  expect(login.ok()).toBe(true);
});
const articleLinks = (page: import('@playwright/test').Page) => page.locator('h3 a[href^="/pages/article/"]');

test('loads all 65 articles exactly once and fits the viewport', async ({ page }, testInfo) => {
  await page.goto('/?view=all');
  await expect(articleLinks(page)).toHaveCount(20);
  for (const expected of [40, 60, 65]) {
    await page.getByRole('button', { name: '加载更多', exact: true }).click();
    await expect(articleLinks(page)).toHaveCount(expected);
  }
  await expect(page.getByRole('status')).toHaveText('共 65 篇');
  await expect(page.getByRole('button', { name: '加载更多', exact: true })).toHaveCount(0);
  const ids = await articleLinks(page).evaluateAll(links => links.map(link => link.getAttribute('href')));
  expect(new Set(ids).size).toBe(65);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({ path: testInfo.outputPath('library.png') });
});

test('combines unread, both tags, Chinese search and extension source', async ({ page }) => {
  const response = await page.request.get('/api/tags');
  const tags = await response.json() as { id: string; name: string }[];
  const selected = ['.NET', '阅读'].map(name => tags.find(tag => tag.name === name)!.id);
  const query = new URLSearchParams({ status: 'unread', tags: selected.join(','), q: '缓存', source: 'extension' });
  const expected = await (await page.request.get(`/api/articles?${query}`)).json() as { total: number; items: { id: string }[] };
  expect(expected.total).toBeGreaterThan(0);
  await page.goto(`/?${query}`);
  await expect(articleLinks(page)).toHaveCount(expected.total);
  await expect(page.getByRole('status')).toHaveText(`共 ${expected.total} 篇`);
  expect(await articleLinks(page).evaluateAll(links => links.map(link => link.getAttribute('href')!.split('/').at(-1)))).toEqual(expected.items.map(item => item.id));
});

test('shows removable empty-result chips and clears filters within the current view', async ({ page }) => {
  await page.goto('/?view=favorites');
  await expect(page.getByRole('heading', { name: '收藏', exact: true })).toBeVisible();
  await page.getByLabel('搜索文章', { exact: true }).fill('missing-library-result-268175');
  await expect(page).toHaveURL(/q=missing-library-result-268175/);
  await expect(page.getByText('没有符合筛选条件的文章')).toBeVisible();
  await expect(page.getByRole('button', { name: '移除搜索：missing-library-result-268175', exact: true })).toBeVisible();
  await page.getByRole('button', { name: '清除筛选', exact: true }).click();
  await expect(page).toHaveURL(/\?view=favorites$/);
  await expect(articleLinks(page).first()).toBeVisible();
});

test('returns to filtered loaded pages and scroll position, including a reload', async ({ page }) => {
  await page.goto('/?status=unread&source=extension');
  await expect(articleLinks(page)).toHaveCount(20);
  await page.getByRole('button', { name: '加载更多', exact: true }).click();
  await expect(articleLinks(page)).toHaveCount(40);
  const link = articleLinks(page).nth(25);
  await link.scrollIntoViewIfNeeded();
  const scrollY = await page.evaluate(() => window.scrollY);
  expect(scrollY).toBeGreaterThan(0);
  await link.click();
  await expect(page).toHaveURL(/\/pages\/article\//);
  await expect(page.locator('.article-title-section h1')).toBeVisible();
  await page.goBack();
  await expect(page).toHaveURL(/status=unread&source=extension/);
  await expect(articleLinks(page)).toHaveCount(40);
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBeGreaterThan(scrollY - 100);
  expect(await page.evaluate(() => window.scrollY)).toBeLessThan(scrollY + 100);
  await page.reload();
  await expect(articleLinks(page)).toHaveCount(40);
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBeGreaterThan(scrollY - 100);
});

test('reconciles a leading save while away and traverses all 66 server-cursor rows', async ({ page }) => {
  let createdId: string | undefined;
  try {
    await page.goto('/?view=all');
    await expect(articleLinks(page)).toHaveCount(20);
    const boundary = await articleLinks(page).nth(19).getAttribute('href');
    await page.getByRole('button', { name: '加载更多', exact: true }).click();
    await expect(articleLinks(page)).toHaveCount(40);
    await articleLinks(page).nth(25).click();
    await expect(page.locator('.article-title-section h1')).toBeVisible();
    const unique = crypto.randomUUID();
    const saved = await page.request.post('/api/articles/content', { data: {
      url: `https://example.com/mini-cc-p1-insertion/${unique}`, title: `P1 temporary insertion ${unique}`,
      originalContent: '<p>Temporary pagination fixture</p>', readableContent: '<p>Temporary pagination fixture</p>',
    } });
    expect(saved.status()).toBe(201);
    createdId = (await saved.json() as { id: string }).id;
    await page.goBack();
    await expect(page.getByRole('status')).toHaveText('共 66 篇，已加载 40 篇');
    for (const count of [60, 66]) {
      await page.getByRole('button', { name: '加载更多', exact: true }).click();
      await expect(articleLinks(page)).toHaveCount(count);
    }
    await expect(page.getByRole('status')).toHaveText('共 66 篇');
    const hrefs = await articleLinks(page).evaluateAll(links => links.map(link => link.getAttribute('href')));
    expect(new Set(hrefs).size).toBe(66);
    expect(hrefs).toContain(`/pages/article/${createdId}`);
    expect(hrefs).toContain(boundary);
  } finally {
    if (createdId) expect((await page.request.delete(`/api/articles/${createdId}`)).status()).toBe(204);
  }
});

test('composes rapid toolbar edits and follows browser back/forward', async ({ page }) => {
  await page.goto('/?status=unread');
  await expect(articleLinks(page)).toHaveCount(20);
  await page.locator('summary').filter({ hasText: /^标签/ }).click();
  await expect(page.getByLabel('搜索标签', { exact: true })).toBeVisible();
  await page.evaluate(() => {
    const details = document.querySelector('#tag-search')!.closest('details')!;
    for (const name of ['阅读', '.NET']) {
      const label = Array.from(details.querySelectorAll('label')).find(label => label.textContent === name)!;
      (label.querySelector('input') as HTMLInputElement).click();
    }
    const source = document.querySelector('select')!;
    source.value = 'extension'; source.dispatchEvent(new Event('change', { bubbles: true }));
    const search = document.querySelector('#library-search') as HTMLInputElement;
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(search, '缓存');
    search.dispatchEvent(new Event('input', { bubbles: true }));
  });
  await expect.poll(() => new URL(page.url()).searchParams.get('tags')?.split(',').length).toBe(2);
  await expect.poll(() => new URL(page.url()).searchParams.get('q')).toBe('缓存');
  await expect(page.getByRole('status')).toHaveText('共 1 篇');
  const finalUrl = page.url();
  await page.goBack();
  const previous = new URL(page.url()).searchParams;
  await expect(page.getByLabel('搜索文章', { exact: true })).toHaveValue(previous.get('q') || '');
  await expect(page.getByRole('combobox', { name: '来源', exact: true })).toHaveValue(previous.get('source') || '');
  await page.goForward();
  await expect(page).toHaveURL(finalUrl);
  await expect(page.getByLabel('搜索文章', { exact: true })).toHaveValue('缓存');
  await expect(page.getByRole('combobox', { name: '来源', exact: true })).toHaveValue('extension');
});

test('removes a selected tag from a reader tail row and restores the original fixture tag', async ({ page }) => {
  const tags = await (await page.request.get('/api/tags')).json() as { id: string; name: string; color: string }[];
  const selected = tags.find(tag => tag.name === '.NET')!;
  const query = new URLSearchParams({ view: 'all', tags: selected.id });
  const before = await (await page.request.get(`/api/articles?${query}`)).json() as { total: number };
  expect(before.total).toBeGreaterThan(20);
  let articleId: string | undefined;
  try {
    await page.goto(`/?${query}`);
    await expect(articleLinks(page)).toHaveCount(20);
    await page.getByRole('button', { name: '加载更多', exact: true }).click();
    await expect(articleLinks(page)).toHaveCount(before.total);
    const link = articleLinks(page).nth(20);
    articleId = (await link.getAttribute('href'))!.split('/').at(-1)!;
    await link.click();
    await expect(page).toHaveURL(new RegExp(`/pages/article/${articleId}$`));
    await expect(page.locator('.article-title-section h1')).toBeVisible();
    await page.getByRole('button', { name: 'Remove .NET tag', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Remove .NET tag', exact: true })).toHaveCount(0);
    await page.goBack();
    await expect(articleLinks(page)).toHaveCount(before.total - 1);
    await expect(page.getByRole('status')).toHaveText(`共 ${before.total - 1} 篇`);
    await expect(page.locator(`h3 a[href="/pages/article/${articleId}"]`)).toHaveCount(0);
  } finally {
    if (articleId) expect((await page.request.post(`/api/articles/${articleId}/tags`, { data: { name: selected.name, color: selected.color } })).ok()).toBe(true);
  }
});
