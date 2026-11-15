import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, it, vi } from 'vitest';
import { articleApi } from '@/lib/api';
import { ToastProvider, useToast } from './ToastContext';
const auth = vi.hoisted(() => ({ user: { userName: 'test' } as { userName: string } | null }));
vi.mock('@/contexts/AuthContext', () => ({ useAuth: () => auth }));
vi.mock('@/lib/api', () => ({ articleApi: { getLibrarySettings: vi.fn().mockResolvedValue({ undoWindowSeconds: 10, trashRetentionDays: 30 }), batch: vi.fn().mockResolvedValue({ succeeded: ['ok'], failed: [] }) } }));
it('undo restores only acknowledged successful IDs and announces through a global polite region', async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  let toast!: ReturnType<typeof useToast>;
  function Harness() { toast = useToast(); return null; }
  const node = document.createElement('div'); document.body.append(node); const root = createRoot(node);
  await act(async () => root.render(<ToastProvider><Harness /></ToastProvider>));
  await act(async () => toast.deleted(['ok']));
  expect(node.querySelector('[aria-live="polite"]')?.textContent).toContain('已删除 1 篇文章');
  await act(async () => (Array.from(node.querySelectorAll('button')).find(b => b.textContent === '撤销')!).click());
  expect(articleApi.batch).toHaveBeenCalledWith(['ok'], 'restore');
  await act(async () => root.unmount()); node.remove();
});

it('loads configured undo duration after login rather than retaining unauthenticated defaults', async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true }); auth.user = null;
  vi.mocked(articleApi.getLibrarySettings).mockClear();
  const node = document.createElement('div'); const root = createRoot(node);
  await act(async () => root.render(<ToastProvider><span /></ToastProvider>));
  const before = vi.mocked(articleApi.getLibrarySettings).mock.calls.length;
  auth.user = { userName: 'after-login' };
  await act(async () => root.render(<ToastProvider><span /></ToastProvider>));
  expect(vi.mocked(articleApi.getLibrarySettings).mock.calls.length).toBe(before + 1);
  await act(async () => root.unmount()); auth.user = { userName: 'test' };
});

it('uses configured undo expiry and keeps at most three visible announcements', async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true }); vi.useFakeTimers();
  vi.mocked(articleApi.getLibrarySettings).mockResolvedValueOnce({ undoWindowSeconds: 3, trashRetentionDays: 30 });
  let toast!: ReturnType<typeof useToast>;
  function Harness() { toast = useToast(); return null; }
  const node = document.createElement('div'); const root = createRoot(node);
  try {
    await act(async () => root.render(<ToastProvider><Harness /></ToastProvider>));
    await act(async () => toast.deleted(['acknowledged']));
    await act(async () => vi.advanceTimersByTime(2999)); expect(node.textContent).toContain('撤销');
    await act(async () => vi.advanceTimersByTime(1)); expect(node.textContent).not.toContain('撤销');
    await act(async () => { for (let i = 0; i < 4; i++) toast.show(`notice${i}`); });
    expect(node.querySelector('[aria-live]')?.children).toHaveLength(3); expect(node.textContent).not.toContain('notice0');
  } finally { await act(async () => root.unmount()); vi.useRealTimers(); }
});

it.each([false, true])('chunks undo above 200 and retries only aggregate failed or unacknowledged IDs (lost chunk: %s)', async loseLastChunk => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const ids = Array.from({ length: 205 }, (_, index) => `article-${index}`);
  vi.mocked(articleApi.batch).mockReset().mockImplementation(async chunk => {
    if (chunk.length > 200) throw new Error('API limit exceeded');
    if (loseLastChunk && chunk[0] === 'article-200') throw new Error('Unacknowledged response');
    return { succeeded: chunk.filter(id => id !== 'article-3'), failed: chunk.includes('article-3') ? [{ id: 'article-3', reason: 'error' }] : [] };
  });
  const failed = ['article-3', ...(loseLastChunk ? ids.slice(200) : [])];
  let toast!: ReturnType<typeof useToast>;
  function Harness() { toast = useToast(); return null; }
  const node = document.createElement('div'); const root = createRoot(node);
  try {
    await act(async () => root.render(<ToastProvider><Harness /></ToastProvider>));
    await act(async () => toast.deleted(ids));
    await act(async () => Array.from(node.querySelectorAll('button')).find(button => button.textContent === '撤销')!.click());
    expect(vi.mocked(articleApi.batch).mock.calls.map(([chunk, action]) => [chunk.length, action])).toEqual([[200, 'restore'], [5, 'restore']]);
    expect(node.textContent).toContain(`已恢复 ${ids.length - failed.length} 篇文章，${failed.length} 篇失败`);
    vi.mocked(articleApi.batch).mockResolvedValueOnce({ succeeded: failed, failed: [] });
    await act(async () => Array.from(node.querySelectorAll('button')).find(button => button.textContent === '重试')!.click());
    expect(vi.mocked(articleApi.batch).mock.calls[2]).toEqual([failed, 'restore']);
    expect(node.textContent).toContain(`已恢复 ${failed.length} 篇文章`);
  } finally { await act(async () => root.unmount()); }
});
