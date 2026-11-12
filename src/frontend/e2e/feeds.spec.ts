import { test, expect, Page, APIRequestContext, APIResponse } from '@playwright/test';

let feedIds: string[] = [], articleIds: string[] = [], baseline: string[] = [];
let run = '';
const mock = process.env.E2E_FEED_URL || 'http://127.0.0.1:5018';
async function activeIds(request: APIRequestContext) {
  const ids: string[] = []; let cursor: string | null = null;
  do {
    const response: APIResponse = await request.get('/api/articles', { params: { view: 'all', pageSize: 50, ...(cursor ? { cursor } : {}) } });
    expect(response.ok()).toBe(true); const result: { items: { id: string }[]; nextCursor: string | null } = await response.json(); ids.push(...result.items.map(a => a.id)); cursor = result.nextCursor;
  } while (cursor);
  return ids.sort();
}
async function remember(request: APIRequestContext, id: string) {
  if (!feedIds.includes(id)) feedIds.push(id);
  const result = await (await request.get(`/api/articles?view=feeds&feedId=${id}&pageSize=50`)).json();
  articleIds = Array.from(new Set([...articleIds, ...result.items.map((a: { id: string }) => a.id)])); return result.items;
}
async function add(page: Page, kind: 'rss' | 'atom', title: string, discover = false) {
  await page.getByRole('button', { name: '添加订阅', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: '添加订阅', exact: true });
  await dialog.getByLabel('订阅地址').fill(`${mock}/${discover ? 'discover' : `feed-${kind}`}?run=${run}`);
  await dialog.getByRole('button', { name: '预览订阅', exact: true }).click();
  const confirm = page.getByRole('dialog', { name: '确认订阅', exact: true });
  await expect(confirm.getByText(`Preview ${kind} entry 01`, { exact: true })).toBeVisible();
  await confirm.getByLabel('订阅名称').fill(title);
  const created = page.waitForResponse(r => r.url().endsWith('/api/feeds') && r.request().method() === 'POST');
  await confirm.getByRole('button', { name: '确认添加', exact: true }).click();
  const response = await created; expect(response.status()).toBe(201); const result = await response.json();
  expect(result.imported).toBe(20); await remember(page.request, result.feed.id);
  await expect(page.getByRole('status').filter({ hasText: '已导入 20 篇' })).toBeVisible();
  return result.feed.id as string;
}
async function checkFeedNavigation(page: Page, title: string, mobile: boolean) {
  await page.evaluate(() => window.scrollTo({ top: 0, behavior: 'instant' }));
  const sidebar = page.getByRole('complementary', { name: 'Sidebar' });
  const sidebarBox = await sidebar.boundingBox(); expect(sidebarBox).not.toBeNull();
  if (mobile) {
    expect(sidebarBox!.height).toBeLessThanOrEqual(150);
    const toggle = sidebar.getByRole('button', { name: '订阅', exact: true });
    await toggle.scrollIntoViewIfNeeded(); await toggle.click();
    const menu = page.getByRole('dialog', { name: '订阅来源', exact: true });
    await expect(menu).toBeVisible();
    expect((await sidebar.boundingBox())!.height).toBeLessThanOrEqual(sidebarBox!.height + 1);
    const link = menu.getByTitle(title, { exact: true }); await link.scrollIntoViewIfNeeded();
    const bounds = await link.boundingBox(); expect(bounds!.x).toBeGreaterThanOrEqual(0); expect(bounds!.x + bounds!.width).toBeLessThanOrEqual(375);
    expect(bounds!.height).toBeGreaterThanOrEqual(44);
    expect(await link.evaluate(element => { const r = element.getBoundingClientRect(); const hit = document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2); return hit === element || element.contains(hit); })).toBe(true);
    await menu.getByRole('button', { name: '关闭订阅来源', exact: true }).click();
    await expect(menu).not.toBeVisible();
  } else {
    const links = await sidebar.locator('nav a').evaluateAll(nodes => nodes.map(node => { const r = node.getBoundingClientRect(); return { left: r.left, right: r.right }; }));
    expect(links.every(r => r.left >= sidebarBox!.x && r.right <= sidebarBox!.x + sidebarBox!.width + 1)).toBe(true);
    const link = sidebar.locator('nav').getByTitle(title, { exact: true }); await link.scrollIntoViewIfNeeded();
    const bounds = await link.boundingBox(); expect(bounds!.height).toBeGreaterThanOrEqual(44);
    expect(await link.evaluate(element => { const r = element.getBoundingClientRect(); const hit = document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2); return hit === element || element.contains(hit); })).toBe(true);
  }
  await page.evaluate(() => window.scrollTo({ top: 0, behavior: 'instant' }));
}
test.beforeEach(async ({ page }) => {
  feedIds = []; articleIds = []; run = crypto.randomUUID();
  expect((await page.request.post('/api/account/login', { form: { userName: process.env.E2E_USERNAME || 'mini_cc', password: process.env.E2E_PASSWORD || 'mini_cc_password', rememberMe: 'false' } })).ok()).toBe(true);
  baseline = await activeIds(page.request);
});
test.afterEach(async ({ page }) => {
  try {
    for (const id of feedIds) { await remember(page.request, id); expect((await page.request.delete(`/api/feeds/${id}`)).ok()).toBe(true); }
    if (articleIds.length) {
      expect((await page.request.post('/api/articles/batch', { data: { ids: articleIds, action: 'trash' } })).ok()).toBe(true);
      expect((await page.request.post('/api/articles/batch', { data: { ids: articleIds, action: 'purge' } })).ok()).toBe(true);
    }
  } finally { expect(await activeIds(page.request)).toEqual(baseline); }
});

test('RSS discovery and Atom import twenty each; refresh, edit, pause, unsubscribe and deletion preserve library rules', async ({ page }, testInfo) => {
  await page.goto('/pages/feeds');
  const rssTitle = `RSS ${run} ${"长订阅名称与来源信息".repeat(8)}`, atomTitle = `Atom ${run}`;
  const rss = await add(page, 'rss', rssTitle, true); const atom = await add(page, 'atom', atomTitle);
  await checkFeedNavigation(page, rssTitle, testInfo.project.name === 'mobile');
  await page.screenshot({ path: testInfo.outputPath('feed-navigation-long-title.png'), fullPage: true });
  let row = page.getByRole('region', { name: rssTitle, exact: true });
  await row.getByRole('button', { name: '立即刷新', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: '已导入 0 篇' })).toBeVisible();
  expect((await remember(page.request, rss)).length).toBe(20); expect((await remember(page.request, atom)).length).toBe(20);
  await row.getByRole('button', { name: '编辑', exact: true }).click();
  const edit = page.getByRole('dialog', { name: '编辑订阅', exact: true });
  await edit.getByLabel('订阅名称').fill(`${rssTitle} renamed`); await edit.getByRole('button', { name: '保存', exact: true }).click();
  row = page.getByRole('region', { name: `${rssTitle} renamed`, exact: true }); await expect(row).toBeVisible();
  await row.getByRole('button', { name: '暂停', exact: true }).click(); await expect(row.getByText('已暂停', { exact: true })).toBeVisible();
  await expect(row.getByRole('button', { name: '立即刷新', exact: true })).toBeDisabled();
  await row.getByRole('button', { name: '恢复', exact: true }).click(); await expect(row.getByText('正常', { exact: true })).toBeVisible();
  const entries = await remember(page.request, rss); const deleted = entries[0].id;
  expect((await page.request.delete(`/api/articles/${deleted}`)).ok()).toBe(true);
  expect((await page.request.post(`/api/feeds/${rss}/refresh`)).ok()).toBe(true);
  expect((await page.request.get(`/api/articles/${deleted}`)).status()).toBe(404);
  expect((await page.request.post('/api/articles/batch', { data: { ids: [deleted], action: 'purge' } })).ok()).toBe(true);
  expect((await page.request.post(`/api/feeds/${rss}/refresh`)).ok()).toBe(true); expect((await remember(page.request, rss)).length).toBe(19);
  page.once('dialog', async dialog => { expect(dialog.message()).toContain('已导入的文章会保留'); await dialog.accept(); });
  await row.getByRole('button', { name: '取消订阅', exact: true }).click(); await expect(row).toHaveCount(0);
  expect((await page.request.get(`/api/articles/${entries[1].id}`)).ok()).toBe(true);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.evaluate(() => window.scrollTo({ top: 0, behavior: 'instant' }));
  await page.screenshot({ path: testInfo.outputPath('feed-management.png'), fullPage: true });
});

test('plugin completes an open summary, preserves notes and state, then resolves only matching highlights', async ({ page }, testInfo) => {
  const created = await page.request.post('/api/feeds', { data: { feedUrl: `${mock}/feed-rss?run=${run}`, title: `Upgrade ${run}`, tagIds: [] } });
  expect(created.status()).toBe(201); const id = (await created.json()).feed.id; const entries = await remember(page.request, id);
  const summary = entries.find((a: { contentKind: string }) => a.contentKind === 'summaryOnly'); expect(summary).toBeTruthy();
  for (const [text, startOffset, endOffset] of [['Temporary', 0, 9], ['summary', 15, 22]] as const) {
    expect((await page.request.post(`/api/articles/${summary.id}/highlights`, { data: { text, startOffset, endOffset, note: `keep ${text}`, color: '#ffeb3b' } })).ok()).toBe(true);
  }
  expect((await page.request.patch(`/api/articles/${summary.id}/state`, { data: { isFavorite: true, isArchived: true, readingStatus: 'read' } })).ok()).toBe(true);
  await page.goto(`/pages/article/${summary.id}`); await expect(page.locator('.reader-summary-banner')).toBeVisible(); await expect(page.locator('.reader-source')).toContainText(`Upgrade ${run}`);
  const body = '<p>New Temporary full body. ' + 'Long body text retained after plugin save. '.repeat(30) + '</p>';
  const upgraded = await page.request.post('/api/articles/content', { data: { url: summary.url, title: 'Full body', originalContent: body, readableContent: body } });
  expect(upgraded.status()).toBe(201); const detail = await upgraded.json(); expect(detail.highlights.every((h: { anchorStatus: string }) => h.anchorStatus === 'pending')).toBe(true);
  await page.evaluate(() => document.dispatchEvent(new Event('visibilitychange')));
  await expect(page.getByText('正文已补全，请刷新后继续阅读。旧正文的进度已暂停同步。')).toBeVisible();
  await page.getByRole('button', { name: '刷新正文', exact: true }).click(); await expect(page.locator('.readable-content')).toContainText('New Temporary full body.');
  await expect(page.locator('.reader-summary-banner')).toHaveCount(0);
  await expect(page.locator('[data-highlight-id]')).toHaveCount(1);
  await expect.poll(async () => {
    const fresh = await (await page.request.get(`/api/articles/${summary.id}`)).json();
    return fresh.highlights.map((h: { anchorStatus: string }) => h.anchorStatus).sort();
  }).toEqual(['ok', 'orphaned']);
  const fresh = await (await page.request.get(`/api/articles/${summary.id}`)).json();
  expect(fresh.isFavorite).toBe(true); expect(fresh.isArchived).toBe(true); expect(fresh.readingStatus).toBe('read');
  await page.getByRole('button', { name: 'Expand highlights', exact: true }).click();
  await expect(page.getByText(/原文位置已失效/)).toBeVisible(); await expect(page.getByText('keep summary', { exact: false })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: testInfo.outputPath('feed-completed-article.png'), fullPage: true });
});

test('unseen completion during reset or state change keeps the loaded summary paused until reload', async ({ page }) => {
  const created = await page.request.post('/api/feeds', { data: { feedUrl: `${mock}/feed-rss?run=${run}`, title: `Body guard ${run}`, tagIds: [] } });
  expect(created.status()).toBe(201); const feedId = (await created.json()).feed.id;
  const summaries = (await remember(page.request, feedId)).filter((a: { contentKind: string }) => a.contentKind === 'summaryOnly').slice(0, 2);
  expect(summaries).toHaveLength(2);
  for (const [index, summary] of summaries.entries()) {
    await page.goto(`/pages/article/${summary.id}`); await expect(page.locator('.reader-summary-banner')).toBeVisible();
    const oldText = await page.locator('.readable-content').textContent();
    const body = `<p>Completed body ${index}. ${'Full article text. '.repeat(60)}</p>`;
    const upgrade = async () => {
      const response = await page.request.post('/api/articles/content', { data: { url: summary.url, title: 'Full', originalContent: body, readableContent: body } });
      expect(response.status()).toBe(201);
    };
    if (index === 0) {
      const resetPath = `**/api/articles/${summary.id}/progress/reset`;
      await page.route(resetPath, async route => { await upgrade(); await route.continue(); });
      const acknowledgement = page.waitForResponse(response => response.url().endsWith(`/articles/${summary.id}/progress/reset`));
      await page.getByRole('button', { name: '从头阅读', exact: true }).click();
      const reset = await acknowledgement; expect(reset.ok()).toBe(true); expect((await reset.json()).contentKind).toBe('full');
      await page.unroute(resetPath);
    } else {
      await upgrade();
      await page.getByRole('button', { name: '加入收藏', exact: true }).click();
      await expect(page.getByRole('button', { name: '取消收藏', exact: true })).toBeVisible();
    }
    await expect(page.getByText('正文已补全，请刷新后继续阅读。旧正文的进度已暂停同步。')).toBeVisible();
    await expect(page.locator('.reader-summary-banner')).toBeVisible();
    await expect(page.locator('.readable-content')).toHaveText(oldText!);
    await expect(page.getByRole('button', { name: '留在这里', exact: true })).toHaveCount(0);
    const current = await (await page.request.get(`/api/articles/${summary.id}`)).json();
    expect(current.progressTextOffset).toBeNull(); expect(current.contentKind).toBe('full');
    await page.getByRole('button', { name: '刷新正文', exact: true }).click();
    await expect(page.locator('.readable-content')).toContainText(`Completed body ${index}.`);
    await expect(page.locator('.reader-summary-banner')).toHaveCount(0);
  }
});

test('RSS feed selector preserves Favorites search tags status dates and sort', async ({ page }) => {
  const ownFeeds: string[] = [], chosenArticles: string[] = [];
  const tags = await (await page.request.get('/api/tags')).json(); const tag = tags.find((t: { name: string }) => t.name === '.NET'); expect(tag).toBeTruthy();
  for (const kind of ['rss', 'atom']) {
    const created = await page.request.post('/api/feeds', { data: { feedUrl: `${mock}/feed-${kind}?run=${run}`, title: `Filter ${kind} ${run}`, tagIds: [] } });
    expect(created.status()).toBe(201); const id = (await created.json()).feed.id; ownFeeds.push(id);
    const entries = await remember(page.request, id); chosenArticles.push(entries[0].id);
    expect((await page.request.patch(`/api/articles/${entries[0].id}/state`, { data: { isFavorite: true, readingStatus: 'unread' } })).ok()).toBe(true);
    expect((await page.request.post(`/api/articles/${entries[0].id}/tags`, { data: { name: tag.name } })).ok()).toBe(true);
  }
  const filters = new URLSearchParams({ view: 'favorites', q: 'Preview', tags: tag.id, status: 'unread', savedFrom: '2020-01-01T00:00:00Z', savedTo: '2099-01-01T00:00:00Z', range: 'custom', sort: 'published_desc' });
  await page.goto(`/?${filters}`); await expect(page.locator('[data-article-id]')).toHaveCount(2);
  await expect(page.getByRole('combobox', { name: '订阅源', exact: true })).toHaveCount(0);
  await page.getByRole('combobox', { name: '来源', exact: true }).selectOption('feed'); filters.set('source', 'feed');
  const selector = page.getByRole('combobox', { name: '订阅源', exact: true }); await expect(selector).toBeVisible();
  await selector.selectOption(ownFeeds[0]);
  await expect(page.locator('[data-article-id]')).toHaveCount(1); await expect(page.locator(`[data-article-id="${chosenArticles[0]}"]`)).toBeVisible();
  await expect.poll(() => new URL(page.url()).searchParams.get('feedId')).toBe(ownFeeds[0]);
  filters.forEach((value, key) => expect(new URL(page.url()).searchParams.get(key)).toBe(value));
  await selector.selectOption(''); await expect(page.locator('[data-article-id]')).toHaveCount(2);
  expect(new URL(page.url()).searchParams.has('feedId')).toBe(false);
  filters.forEach((value, key) => expect(new URL(page.url()).searchParams.get(key)).toBe(value));
});
