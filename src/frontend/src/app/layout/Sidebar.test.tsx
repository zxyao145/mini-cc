import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, it, vi } from 'vitest';
import Sidebar from './Sidebar';
vi.mock('next/navigation', () => ({ usePathname: () => '/pages/feeds', useRouter: () => ({ push: vi.fn() }), useSearchParams: () => new URLSearchParams() }));
vi.mock('@/contexts/AuthContext', () => ({ useAuth: () => ({ user: null, logout: vi.fn() }) }));
vi.mock('@/contexts/FeedsContext', () => ({ useFeeds: () => ({ feeds: [{ id: 'feed', title: 'A very long feed name', unreadCount: 20, health: 'ok' }] }) }));
it('opens mobile sources in a dialog and closes it without changing desktop expansion', async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  vi.spyOn(window, 'matchMedia').mockImplementation(() => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() }) as unknown as MediaQueryList);
  const host = document.createElement('div'); document.body.append(host); const root = createRoot(host);
  try {
    await act(async () => root.render(<Sidebar/>));
    const toggle = host.querySelector<HTMLButtonElement>('button[title="订阅"]')!;
    expect(toggle.getAttribute('aria-haspopup')).toBe('dialog'); expect(toggle.getAttribute('aria-expanded')).toBe('false');
    await act(async () => toggle.click());
    const dialog = host.querySelector('dialog')!; expect(dialog.open).toBe(true); expect(toggle.getAttribute('aria-expanded')).toBe('true');
    expect(dialog.querySelector('a[title="A very long feed name"]')?.textContent).toContain('20');
    await act(async () => dialog.querySelector<HTMLButtonElement>('button[aria-label="关闭订阅来源"]')!.click());
    expect(dialog.open).toBe(false); expect(toggle.getAttribute('aria-expanded')).toBe('false');
  } finally { await act(async () => root.unmount()); host.remove(); vi.restoreAllMocks(); }
});
