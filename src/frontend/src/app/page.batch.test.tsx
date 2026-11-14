import { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { articleApi } from '@/lib/api';
import { ArticleListItem } from '@/types';
import Home from './page';
import { ToastProvider } from '@/contexts/ToastContext';
import { clearLibraryCache } from '@/lib/library-cache';
const state = vi.hoisted(() => ({ search: 'view=all' }));
vi.mock('next/navigation', () => ({ useSearchParams: () => new URLSearchParams(state.search), useRouter: () => ({ push: vi.fn(), replace: vi.fn() }) }));
vi.mock('@/contexts/AuthContext', () => ({ useAuth: () => ({ user: { userName: 'test' } }) }));
vi.mock('@/lib/api', () => ({ articleApi: { getArticles: vi.fn(), batch: vi.fn(), getLibrarySettings: vi.fn().mockResolvedValue({ undoWindowSeconds: 10, trashRetentionDays: 30 }) }, tagApi: { getTags: vi.fn().mockResolvedValue([]) } }));
vi.mock('./pages/article/[id]/components/TagManager', () => ({ default: () => null }));
let root: Root, container: HTMLDivElement;
const item = (id: string) => ({ id, title: id, url: 'https://example.com', createdAt: '2026-11-08T00:00:00Z', deletedAt: null, readingStatus: 'unread', tags: [], highlightCount: 0 } as unknown as ArticleListItem);
beforeEach(() => { Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true }); clearLibraryCache(); vi.clearAllMocks(); state.search = 'view=all'; container = document.createElement('div'); document.body.append(container); root = createRoot(container); });
afterEach(async () => { await act(async () => root.unmount()); container.remove(); });
async function click(text: string) { await act(async () => (Array.from(container.querySelectorAll('button')).find(button => button.textContent === text)!).click()); }
const render = async () => { await act(async () => root.render(<ToastProvider><Home /></ToastProvider>)); };
it('keeps only failures selected, patches only successes, and retries the exact failures', async () => {
  let rows = Array.from({ length: 20 }, (_, i) => item(`Article ${i}`));
  vi.mocked(articleApi.getArticles).mockImplementation(async () => ({ items: rows, total: rows.length, nextCursor: null }));
  vi.mocked(articleApi.batch).mockImplementation(async ids => {
    if (ids.length === 20) { rows = rows.slice(18); return { succeeded: ids.slice(0, 18), failed: ids.slice(18).map(id => ({ id, reason: 'inTrash' })) }; }
    rows = []; return { succeeded: ids, failed: [] };
  });
  await render(); await click('选择'); await click('全选已加载');
  const bar = container.querySelector('[aria-label="批量操作"]')!;
  expect(bar.compareDocumentPosition(container.querySelector('header')!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  await click('归档');
  expect(container.textContent).toContain('18 篇已归档，2 篇失败');
  expect(container.querySelectorAll('input[aria-label^="选择 "]:checked')).toHaveLength(2);
  await click('重试'); expect(vi.mocked(articleApi.batch).mock.calls[1][0]).toEqual(['Article 18', 'Article 19']);
});
it('does not carry an in-flight batch failure selection into a different query', async () => {
  let resolve!: (result: { succeeded: string[]; failed: { id: string; reason: string }[] }) => void;
  vi.mocked(articleApi.getArticles).mockResolvedValue({ items: [item('a')], total: 1, nextCursor: null });
  vi.mocked(articleApi.batch).mockReturnValue(new Promise(done => { resolve = done; }));
  await render(); await click('选择'); await click('全选已加载'); await click('归档');
  state.search = 'view=favorites'; await render();
  await act(async () => resolve({ succeeded: [], failed: [{ id: 'a', reason: 'inTrash' }] }));
  expect(container.querySelector('[aria-label="批量操作"]')).toBeNull();
  expect(container.querySelectorAll('input[aria-label^="选择 "]:checked')).toHaveLength(0);
});

it('does not revive old selection after navigating A to B and back to A before a batch failure', async () => {
  let resolve!: (result: { succeeded: string[]; failed: { id: string; reason: string }[] }) => void;
  vi.mocked(articleApi.getArticles).mockResolvedValue({ items: [item('a')], total: 1, nextCursor: null });
  vi.mocked(articleApi.batch).mockReturnValue(new Promise(done => { resolve = done; }));
  await render(); await click('选择'); await click('全选已加载'); await click('归档');
  state.search = 'view=favorites'; await render();
  state.search = 'view=all'; await render();
  await act(async () => resolve({ succeeded: [], failed: [{ id: 'a', reason: 'inTrash' }] }));
  expect(container.querySelector('[aria-label="批量操作"]')).toBeNull();
  expect(container.querySelectorAll('input[aria-label^="选择 "]:checked')).toHaveLength(0);
  // Its global retry remains valid, but cannot reselect rows in a new query session either.
  vi.mocked(articleApi.batch).mockResolvedValueOnce({ succeeded: [], failed: [{ id: 'a', reason: 'inTrash' }] });
  await click('重试');
  expect(container.querySelector('[aria-label="批量操作"]')).toBeNull();
});
