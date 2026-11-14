import { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import Home from './page';
const router = vi.hoisted(() => ({ push: vi.fn(), replace: vi.fn() }));
vi.mock('next/navigation', () => ({ useSearchParams: () => new URLSearchParams(), useRouter: () => router }));
vi.mock('@/lib/api', () => ({ articleApi: { getArticles: vi.fn().mockResolvedValue({ items: [], total: 0, nextCursor: null }) }, tagApi: { getTags: vi.fn().mockResolvedValue([]) } }));
let container: HTMLDivElement, root: Root;
beforeEach(() => { Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true }); vi.clearAllMocks(); container = document.createElement('div'); document.body.append(container); root = createRoot(container); });
afterEach(async () => { await act(async () => root.unmount()); container.remove(); });
it('sends the final off intent when an on navigation has not committed its URL props yet', async () => {
  await act(async () => root.render(<Home />));
  const checkbox = container.querySelector('input[type=checkbox]') as HTMLInputElement;
  await act(async () => { checkbox.click(); checkbox.click(); });
  expect(router.push.mock.calls.map(([url]) => url)).toEqual(['/?status=unread', '/']);
  expect(router.push.mock.calls.every(([, options]) => options.scroll === false)).toBe(true);
});
