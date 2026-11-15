import { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { articleApi } from '@/lib/api';
import { ArticleListItem, ArticleListPage } from '@/types';
import { parseLibraryQuery } from '@/lib/library-query';
import { clearLibraryCache, readLibraryCache, writeLibraryCache } from '@/lib/library-cache';
import { useArticleList } from './useArticleList';
vi.mock('@/lib/api', () => ({ articleApi: { getArticles: vi.fn() } }));
const item = (id: string) => ({ id, isArchived: false, deletedAt: null, readingStatus: 'unread', tags: [] } as unknown as ArticleListItem);
let root: Root, container: HTMLDivElement;
let hook: ReturnType<typeof useArticleList>;
const query = parseLibraryQuery(new URLSearchParams());
function Harness({ view = 'inbox', sort = 'saved_desc' }: { view?: string; sort?: typeof query.sort }) { hook = useArticleList({ ...query, view: view as typeof query.view, sort }); return null; }
beforeEach(() => { Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true }); clearLibraryCache(); vi.clearAllMocks(); container = document.createElement('div'); document.body.append(container); root = createRoot(container); vi.spyOn(window, 'scrollTo').mockImplementation(() => {}); });
afterEach(async () => { await act(async () => root.unmount()); container.remove(); vi.restoreAllMocks(); });
it('appends pages without duplicates and removes newly archived items from inbox and total', async () => {
  vi.mocked(articleApi.getArticles).mockResolvedValueOnce({ items: [item('a')], total: 3, nextCursor: 'cursor' }).mockResolvedValueOnce({ items: [item('a'), item('b')], total: 3, nextCursor: null });
  await act(async () => root.render(<Harness />));
  await act(async () => hook.loadMore());
  expect(hook.items.map(a => a.id)).toEqual(['a', 'b']);
  expect(hook.hasMore).toBe(false);
  vi.mocked(articleApi.getArticles).mockResolvedValueOnce({ items: [item('b')], total: 2, nextCursor: null });
  await act(async () => hook.updateItem('a', { isArchived: true }));
  expect(hook.items.map(a => a.id)).toEqual(['b']);
  expect(hook.total).toBe(2);
});
it('aborts and ignores obsolete responses after query change', async () => {
  let resolve!: (page: ArticleListPage) => void;
  vi.mocked(articleApi.getArticles).mockImplementationOnce(() => new Promise(done => { resolve = done; })).mockResolvedValueOnce({ items: [item('new')], total: 1, nextCursor: null });
  await act(async () => root.render(<Harness />));
  const signal = vi.mocked(articleApi.getArticles).mock.calls[0][1];
  await act(async () => root.render(<Harness view="all" />));
  expect(signal?.aborted).toBe(true);
  await act(async () => resolve({ items: [item('old')], total: 1, nextCursor: null }));
  expect(hook.items[0].id).toBe('new');
});
it('restores loaded tail and merges a refreshed first page without resetting its cursor', async () => {
  writeLibraryCache('', { items: [item('a'), item('b')], total: 3, nextCursor: 'tail', scrollY: 640, savedAt: Date.now(), firstPageIds: ['a'] });
  vi.mocked(articleApi.getArticles).mockResolvedValue({ items: [{ ...item('a'), title: 'Updated' }], total: 3, nextCursor: 'first' });
  await act(async () => root.render(<Harness />));
  expect(hook.items.map(a => a.id)).toEqual(['a', 'b']);
  expect(hook.items[0].title).toBe('Updated');
  vi.mocked(articleApi.getArticles).mockResolvedValueOnce({ items: [item('c')], total: 3, nextCursor: null });
  await act(async () => hook.loadMore());
  expect(vi.mocked(articleApi.getArticles).mock.calls.at(-1)?.[0]?.get('cursor')).toBe('tail');
});
it('expires cache after ten minutes and caps it at five queries', () => {
  for (let i = 0; i < 6; i++) writeLibraryCache(String(i), { items: [], total: 0, nextCursor: null, scrollY: 0, savedAt: Date.now(), firstPageIds: [] });
  expect(readLibraryCache('0')).toBeNull();
  expect(readLibraryCache('5')).not.toBeNull();
  writeLibraryCache('expired', { items: [], total: 0, nextCursor: null, scrollY: 0, savedAt: Date.now() - 600001, firstPageIds: [] });
  expect(readLibraryCache('expired')).toBeNull();
});

it('keeps loaded rows on load-more error and retries the same cursor', async () => {
  vi.mocked(articleApi.getArticles).mockResolvedValueOnce({ items: [item('a')], total: 2, nextCursor: 'next' }).mockRejectedValueOnce(new Error('offline')).mockResolvedValueOnce({ items: [item('b')], total: 2, nextCursor: null });
  await act(async () => root.render(<Harness />));
  await act(async () => hook.loadMore());
  expect(hook.items.map(a => a.id)).toEqual(['a']);
  expect(hook.error).toBe('加载失败，重试');
  expect(hook.hasMore).toBe(true);
  await act(async () => hook.loadMore());
  expect(hook.items.map(a => a.id)).toEqual(['a', 'b']);
  expect(hook.error).toBeNull();
  expect(vi.mocked(articleApi.getArticles).mock.calls.slice(1).map(([params]) => params?.get('cursor'))).toEqual(['next', 'next']);
});
it('restores scroll after rendering cached rows and persists loaded pages on departure', async () => {
  writeLibraryCache('', { items: [item('a'), item('b')], total: 2, nextCursor: null, scrollY: 640, savedAt: Date.now(), firstPageIds: ['a'] });
  vi.mocked(articleApi.getArticles).mockResolvedValue({ items: [item('a')], total: 2, nextCursor: 'first' });
  await act(async () => root.render(<Harness />));
  await act(async () => new Promise(resolve => setTimeout(resolve, 30)));
  expect(window.scrollTo).toHaveBeenCalledWith(0, 640);
  await act(async () => root.unmount());
  expect(readLibraryCache('')?.items.map(a => a.id)).toEqual(['a', 'b']);
  root = createRoot(container);
});

it('saves the last library scroll when a short reader document clamps window scroll before cleanup', async () => {
  vi.mocked(articleApi.getArticles).mockResolvedValue({ items: [item('a')], total: 1, nextCursor: null });
  await act(async () => root.render(<Harness />));
  const scroll = vi.spyOn(window, 'scrollY', 'get').mockReturnValue(640);
  window.dispatchEvent(new Event('scroll'));
  scroll.mockReturnValue(0); // The destination document is shorter than the library.
  await act(async () => root.unmount());
  expect(readLibraryCache('')?.scrollY).toBe(640);
  root = createRoot(container);
});

const deferredPage = () => {
  let resolve!: (page: ArticleListPage) => void;
  const promise = new Promise<ArticleListPage>(done => { resolve = done; });
  return { promise, resolve };
};
const seedCachedRows = (rows: ArticleListItem[], total: number, key = '') => {
  writeLibraryCache(key, { items: rows, total, nextCursor: 'after:a40', scrollY: 640, savedAt: Date.now(), firstPageIds: rows.slice(0, 20).map(a => a.id) });
};
it.each(['leading save', 'changed sort key'])('reconciles a cached loaded prefix after a %s and traverses every row exactly once', async reason => {
  const original = Array.from({ length: 65 }, (_, index) => ({ ...item(`a${index + 1}`), createdAt: new Date(Date.UTC(2026, 10, 8, 10, -index)).toISOString(), lastReadAt: new Date(Date.UTC(2026, 10, 8, 10, -index)).toISOString() }));
  const sort = reason === 'changed sort key' ? 'read_desc' : 'saved_desc';
  seedCachedRows(original.slice(0, 40), 65, sort === 'read_desc' ? 'sort=read_desc' : '');
  const server = reason === 'leading save' ? [item('new'), ...original] : [{ ...original[39], lastReadAt: '2026-11-08T12:00:00Z' }, ...original.filter(a => a.id !== 'a40')];
  vi.mocked(articleApi.getArticles).mockImplementation(async params => {
    const after = params?.get('cursor')?.replace('after:', '');
    const start = after ? server.findIndex(a => a.id === after) + 1 : 0;
    const items = server.slice(start, start + 20);
    return { items, total: server.length, nextCursor: start + items.length < server.length ? `after:${items.at(-1)!.id}` : null };
  });
  await act(async () => root.render(<Harness sort={sort} />));
  expect(hook.items.map(a => a.id)).toEqual(server.slice(0, 40).map(a => a.id));
  while (hook.hasMore) await act(async () => hook.loadMore());
  expect(hook.total).toBe(server.length);
  expect(hook.items.map(a => a.id)).toEqual(server.map(a => a.id));
  expect(new Set(hook.items.map(a => a.id)).size).toBe(server.length);
  expect(vi.mocked(articleApi.getArticles).mock.calls[1][0]?.get('cursor')).toBe('after:a19');
});
it.each(['archive', 'delete', 'favorite'])('does not let a stale silent refresh undo an acknowledged %s', async action => {
  const view = action === 'favorite' ? 'all' : 'inbox';
  writeLibraryCache(view === 'all' ? 'view=all' : '', { items: [item('a'), item('b')], total: 3, nextCursor: 'tail', scrollY: 640, savedAt: Date.now(), firstPageIds: ['a'] });
  const old = deferredPage(); const fresh = deferredPage();
  vi.mocked(articleApi.getArticles).mockReset().mockReturnValueOnce(old.promise).mockReturnValueOnce(fresh.promise);
  await act(async () => root.render(<Harness view={view} />));
  const oldSignal = vi.mocked(articleApi.getArticles).mock.calls[0][1];
  await act(async () => {
    if (action === 'delete') hook.removeItems(['a']);
    else hook.updateItem('a', action === 'archive' ? { isArchived: true } : { isFavorite: true });
  });
  await act(async () => old.resolve({ items: [item('a')], total: 3, nextCursor: 'old-first' }));
  expect(oldSignal?.aborted).toBe(true);
  if (action === 'favorite') expect(hook.items.find(a => a.id === 'a')?.isFavorite).toBe(true);
  else expect(hook.items.map(a => a.id)).toEqual(['b']);
  expect(hook.total).toBe(action === 'favorite' ? 3 : 2);
  await act(async () => fresh.resolve({ items: action === 'favorite' ? [{ ...item('a'), isFavorite: true }] : [item('b')], total: action === 'favorite' ? 3 : 2, nextCursor: 'fresh-first' }));
  expect(hook.total).toBe(action === 'favorite' ? 3 : 2);
});
it('reconciles a deletion while load more is pending without restoring the old count or deleted row', async () => {
  const oldMore = deferredPage();
  vi.mocked(articleApi.getArticles).mockReset().mockResolvedValueOnce({ items: [item('a'), item('b')], total: 3, nextCursor: 'old-tail' }).mockReturnValueOnce(oldMore.promise).mockResolvedValueOnce({ items: [item('b'), item('c')], total: 2, nextCursor: null });
  await act(async () => root.render(<Harness />));
  let pending!: Promise<void>;
  await act(async () => { pending = hook.loadMore(); });
  await act(async () => hook.removeItems(['a']));
  await act(async () => { oldMore.resolve({ items: [item('c')], total: 3, nextCursor: null }); await pending; });
  expect(hook.items.map(a => a.id)).toEqual(['b', 'c']);
  expect(hook.total).toBe(2);
  expect(hook.hasMore).toBe(false);
  expect(hook.loadingMore).toBe(false);
});

it('retries a failed mutation reconciliation before resuming an invalidated load-more cursor', async () => {
  const oldMore = deferredPage();
  vi.mocked(articleApi.getArticles).mockReset()
    .mockResolvedValueOnce({ items: [item('a'), item('b')], total: 3, nextCursor: 'old-tail' })
    .mockReturnValueOnce(oldMore.promise)
    .mockRejectedValueOnce(new Error('offline'))
    .mockResolvedValueOnce({ items: [item('new'), item('b'), item('c')], total: 3, nextCursor: null });
  await act(async () => root.render(<Harness />));
  let pending!: Promise<void>;
  await act(async () => { pending = hook.loadMore(); });
  await act(async () => hook.removeItems(['a']));
  await act(async () => { oldMore.resolve({ items: [item('c')], total: 3, nextCursor: null }); await pending; });
  expect(articleApi.getArticles).toHaveBeenCalledTimes(3);
  expect(hook.items.map(a => a.id)).toEqual(['b']);
  expect(hook.error).toBe('加载失败，重试');
  await act(async () => hook.loadMore());
  expect(vi.mocked(articleApi.getArticles).mock.calls[3][0]?.get('cursor')).toBeNull();
  expect(hook.items.map(a => a.id)).toEqual(['new', 'b', 'c']);
  expect(hook.total).toBe(3);
});

it('reconciles a restore against the current query and keeps its loaded interval and matching tail cursor', async () => {
  const server = Array.from({ length: 65 }, (_, i) => item(`a${i}`));
  seedCachedRows(server.slice(0, 40), 65);
  vi.mocked(articleApi.getArticles).mockImplementation(async params => {
    const start = Number(params?.get('cursor') || 0);
    return { items: server.slice(start, start + 20), total: server.length, nextCursor: start + 20 < server.length ? String(start + 20) : null };
  });
  await act(async () => root.render(<Harness />));
  server.push(item('restored-outside-range'));
  await act(async () => window.dispatchEvent(new CustomEvent('minicc:library-mutation')));
  expect(hook.items.map(a => a.id)).toEqual(server.slice(0, 40).map(a => a.id));
  expect(hook.total).toBe(66);
  await act(async () => hook.loadMore());
  expect(hook.items.map(a => a.id)).toEqual(server.slice(0, 60).map(a => a.id));
});

it('uses the latest full-text query for restore and ignores its older in-flight response', async () => {
  const rows = Array.from({ length: 40 }, (_, index) => item(`a${index}`));
  const key = 'view=all&q=body-only&sort=saved_asc';
  seedCachedRows(rows, 65, key);
  const old = deferredPage();
  const server = [item('restored-leading'), ...rows, item('restored-outside')];
  vi.mocked(articleApi.getArticles).mockReset().mockReturnValueOnce(old.promise).mockImplementation(async params => {
    expect(params?.get('q')).toBe('body-only'); expect(params?.get('sort')).toBe('saved_asc');
    const start = Number(params?.get('cursor') || 0);
    return { items: server.slice(start, start + 20), total: server.length, nextCursor: start + 20 < server.length ? String(start + 20) : null };
  });
  function FullTextQuery() { hook = useArticleList(parseLibraryQuery(new URLSearchParams(key))); return null; }
  await act(async () => root.render(<FullTextQuery />));
  const olderSignal = vi.mocked(articleApi.getArticles).mock.calls[0][1];
  await act(async () => window.dispatchEvent(new CustomEvent('minicc:library-mutation')));
  expect(olderSignal?.aborted).toBe(true);
  await act(async () => old.resolve({ items: rows.slice(0, 20), total: 65, nextCursor: 'obsolete' }));
  expect(hook.items.map(a => a.id)).toEqual(server.slice(0, 40).map(a => a.id));
  expect(hook.total).toBe(42);
  await act(async () => hook.loadMore());
  expect(hook.items.map(a => a.id)).toEqual(server.map(a => a.id));
});
