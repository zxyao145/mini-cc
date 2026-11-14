import { test, expect, Page, APIRequestContext, APIResponse, Locator } from '@playwright/test';

let ids: string[] = [], tagIds: string[] = [], baseline: string[] = [], token = '';
async function activeIds(request: APIRequestContext) {
  const found: string[] = []; let cursor: string | null = null;
  do {
    const response: APIResponse = await request.get('/api/articles', { params: { view: 'all', pageSize: 50, ...(cursor ? { cursor } : {}) } });
    expect(response.ok()).toBe(true); const page: { items: { id: string }[]; nextCursor: string | null } = await response.json(); found.push(...page.items.map((a: { id: string }) => a.id)); cursor = page.nextCursor;
  } while (cursor);
  return found.sort();
}
async function seed(request: APIRequestContext, count: number) {
  for (let i = 0; i < count; i++) {
    const body = '<p>P4body retained notes and reading anchors. ' + '正文内容。'.repeat(150) + '</p>';
    const response = await request.post('/api/articles/content', { data: { url: `https://batch-p4.example/${token}/${i}`, title: `${token} article ${i}`, originalContent: body, readableContent: body } });
    expect(response.status()).toBe(201); ids.push((await response.json()).id);
  }
}
async function open(page: Page, view = 'all') { await page.goto(`/?view=${view}&q=${token}`); await expect(page.locator('h3')).toHaveCount(ids.length); }
async function selectAll(page: Page) { await page.getByRole('button', { name: '选择', exact: true }).click(); await page.getByRole('button', { name: '全选已加载', exact: true }).click(); }
async function hit(page: Page, locator: Locator) {
  await locator.scrollIntoViewIfNeeded();
  const geometry = await locator.evaluate(element => {
    const rect = element.getBoundingClientRect(), target = document.elementFromPoint(rect.x + rect.width / 2, rect.y + rect.height / 2);
    return { height: rect.height, reachable: !!target && (element === target || element.contains(target)),
      rect: { x: rect.x, y: rect.y, width: rect.width, height: rect.height },
      occluder: target ? { tag: target.tagName, className: String(target.className), text: target.textContent?.slice(0, 80) } : null };
  });
  expect(geometry.height).toBeGreaterThanOrEqual(44); expect(geometry.reachable, JSON.stringify(geometry)).toBe(true);
}
async function acknowledgeBatch(page: Page, trigger: () => Promise<void>) {
  const pending = page.waitForResponse(response => response.url().endsWith('/api/articles/batch') && response.request().method() === 'POST');
  await trigger(); const response = await pending; expect(response.ok()).toBe(true); await response.finished();
  await expect(page.locator('[aria-label="批量操作"][aria-busy="true"]')).toHaveCount(0);
}
async function batch(page: Page, action: string) {
  const button = page.getByRole('region', { name: '批量操作' }).getByRole('button', { name: action, exact: true }); await hit(page, button);
  if (action === '添加标签' || action === '移除标签') await button.click();
  else await acknowledgeBatch(page, () => button.click());
}
async function state(request: APIRequestContext, id: string) {
  const response = await request.get(`/api/articles/${id}`); expect(response.ok()).toBe(true); const article = await response.json();
  return { isFavorite: article.isFavorite, isArchived: article.isArchived, readingStatus: article.readingStatus, progress: article.progress, tags: article.tags, highlights: article.highlights, originalContent: article.originalContent, readableContent: article.readableContent };
}
test.beforeEach(async ({ page }) => {
  ids = []; tagIds = []; token = `P4${crypto.randomUUID().replace(/-/g, '')}`;
  expect((await page.request.post('/api/account/login', { form: { userName: process.env.E2E_USERNAME || 'mini_cc', password: process.env.E2E_PASSWORD || 'mini_cc_password', rememberMe: 'false' } })).ok()).toBe(true);
  baseline = await activeIds(page.request);
});
test.afterEach(async ({ page }) => {
  try {
    if (ids.length) {
      expect((await page.request.post('/api/articles/batch', { data: { ids, action: 'trash' } })).ok()).toBe(true);
      expect((await page.request.post('/api/articles/batch', { data: { ids, action: 'purge' } })).ok()).toBe(true);
    }
    for (const id of tagIds) expect((await page.request.delete(`/api/tags/${id}`)).ok()).toBe(true);
  } finally { expect(await activeIds(page.request)).toEqual(baseline); }
});

test('batch trash undo and trash restore retain every state, note, tag and position; favorite is explicit', async ({ page }, testInfo) => {
  await seed(page.request, 5);
  for (let i = 0; i < ids.length; i++) {
    expect((await page.request.patch(`/api/articles/${ids[i]}/state`, { data: { isFavorite: i % 2 === 0, isArchived: i % 2 === 1, readingStatus: i % 2 === 0 ? 'read' : 'unread' } })).ok()).toBe(true);
    const tag = await page.request.post(`/api/articles/${ids[i]}/tags`, { data: { name: token, color: '#336699' } }); expect(tag.ok()).toBe(true); if (!tagIds.length) tagIds.push((await tag.json()).id);
    expect((await page.request.post(`/api/articles/${ids[i]}/highlights`, { data: { text: 'P4body', startOffset: 0, endOffset: 6, note: `P4 note ${i}`, color: '#ffeb3b' } })).ok()).toBe(true);
    expect((await page.request.put(`/api/articles/${ids[i]}/progress`, { data: { textOffset: i + 10, blockIndex: 0, blockRatio: .3, percent: i + 10, sessionId: token, baseUpdatedAt: null } })).ok()).toBe(true);
  }
  const before = await Promise.all(ids.map(id => state(page.request, id)));
  await open(page);
  if (testInfo.project.name === 'desktop') {
    const card = page.locator('[data-article-id]').first(); await card.hover(); await card.getByRole('checkbox', { name: /选择 / }).check();
    await expect(page.getByRole('region', { name: '批量操作' })).toBeVisible(); await page.getByRole('button', { name: '全选已加载', exact: true }).click();
  } else await selectAll(page);
  await page.screenshot({ path: testInfo.outputPath('batch-selection.png') });
  await batch(page, '删除');
  await expect(page.getByText('已删除 5 篇文章', { exact: true })).toBeVisible(); const undo = page.getByRole('button', { name: '撤销', exact: true }); await hit(page, undo); await undo.click();
  await expect(page.locator('h3')).toHaveCount(5); expect(await Promise.all(ids.map(id => state(page.request, id)))).toEqual(before);
  await selectAll(page); await batch(page, '删除'); await open(page, 'trash'); await expect(page.getByText(/天后永久删除/)).toHaveCount(5);
  await selectAll(page); await batch(page, '恢复'); await expect(page.locator('h3')).toHaveCount(0);
  expect(await Promise.all(ids.map(id => state(page.request, id)))).toEqual(before);
  await open(page); await selectAll(page); await batch(page, '收藏');
  await expect.poll(async () => (await Promise.all(ids.map(id => state(page.request, id)))).every(a => a.isFavorite)).toBe(true);
  for (const action of ['移除标签', '添加标签']) {
    await selectAll(page); await batch(page, action);
    const group = page.getByRole('group', { name: action, exact: true }); await group.getByRole('checkbox', { name: token, exact: true }).check();
    const apply = group.getByRole('button', { name: '应用', exact: true }); await hit(page, apply); await acknowledgeBatch(page, () => apply.click());
    await expect(page.getByRole('region', { name: '批量操作' })).toHaveCount(0);
    await expect.poll(async () => (await state(page.request, ids[0])).tags.length).toBe(action === '移除标签' ? 0 : 1);
  }
  expect((await (await page.request.get('/api/tags')).json()).some((tag: { id: string }) => tag.id === tagIds[0])).toBe(true);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: testInfo.outputPath('batch-restored.png') });
});

test('partial archive failure keeps two items selected and retry acts only on failures below the mobile header', async ({ page }, testInfo) => {
  await seed(page.request, 20); await open(page); await selectAll(page);
  const selected = await page.locator('input[aria-label^="选择 "]:checked').evaluateAll(inputs => inputs.map(input => input.closest('[data-article-id]')!.getAttribute('data-article-id')));
  const failed = selected.slice(-2) as string[];
  expect((await page.request.post('/api/articles/batch', { data: { ids: failed, action: 'trash' } })).ok()).toBe(true);
  await batch(page, '归档'); await expect(page.getByText('18 篇已归档，2 篇失败', { exact: true })).toBeVisible();
  await expect(page.locator('input[aria-label^="选择 "]:checked')).toHaveCount(2);
  await expect(page.getByRole('region', { name: '批量操作' })).toContainText('已选 2 篇');
  await page.screenshot({ path: testInfo.outputPath('batch-partial.png') });
  const retry = page.getByRole('button', { name: '重试', exact: true }); await hit(page, retry);
  // Restore only the two concurrent deletions so retry can now succeed.
  expect((await page.request.post('/api/articles/batch', { data: { ids: failed, action: 'restore' } })).ok()).toBe(true);
  const requests: string[][] = []; page.on('request', request => { if (request.url().endsWith('/api/articles/batch')) requests.push(request.postDataJSON().ids); });
  await acknowledgeBatch(page, () => retry.click()); await expect(page.getByRole('region', { name: '批量操作' })).toHaveCount(0); expect(requests).toEqual([failed]);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true); await page.screenshot({ path: testInfo.outputPath('batch-partial-retry.png') });
});

test('single reader deletion carries undo to library; purge confirms and empty-trash uses the whole trash count', async ({ page }) => {
  await seed(page.request, 2); await page.goto(`/pages/article/${ids[0]}`); await expect(page.locator('.readable-content')).toBeVisible();
  page.once('dialog', dialog => dialog.accept()); await page.getByRole('button', { name: '删除文章', exact: true }).click();
  await expect(page).toHaveURL(/\/$/); await expect(page.getByText('已删除 1 篇文章', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: '撤销', exact: true }).click(); await expect.poll(async () => (await page.request.get(`/api/articles/${ids[0]}`)).status()).toBe(200);
  expect((await page.request.post('/api/articles/batch', { data: { ids, action: 'trash' } })).ok()).toBe(true);
  await open(page, 'trash'); page.once('dialog', async dialog => { expect(dialog.message()).toContain('无法恢复'); await dialog.accept(); });
  await page.locator(`[data-article-id="${ids[0]}"]`).getByRole('button', { name: '永久删除文章', exact: true }).click(); await expect(page.locator('h3')).toHaveCount(1);
  const trashPage = await (await page.request.get('/api/articles?view=trash&pageSize=1')).json();
  page.once('dialog', async dialog => { expect(dialog.message()).toBe(`将永久删除 ${trashPage.total} 篇文章，无法恢复。`); await dialog.accept(); });
  await page.getByRole('button', { name: '清空回收站', exact: true }).click(); await expect(page.locator('h3')).toHaveCount(0);
});
