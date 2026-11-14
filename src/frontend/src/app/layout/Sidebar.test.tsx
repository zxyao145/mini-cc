import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, it, vi } from 'vitest';
import Sidebar from './Sidebar';

vi.mock('next/navigation', () => ({ usePathname: () => '/pages/feeds', useRouter: () => ({ push: vi.fn() }), useSearchParams: () => new URLSearchParams() }));
vi.mock('@/contexts/AuthContext', () => ({ useAuth: () => ({ user: null, logout: vi.fn() }) }));
vi.mock('@/contexts/FeedsContext', () => ({ useFeeds: () => ({ feeds: [{ id: 'feed', title: 'A very long feed name', unreadCount: 20, health: 'ok' }] }) }));

it('opens mobile sources in a portal, restores focus, and closes on desktop resize', async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const original = window.matchMedia;
  const mobile = Object.assign(new EventTarget(), { matches: true });
  vi.spyOn(window, 'matchMedia').mockImplementation(query => query === '(max-width: 767px)' ? mobile as MediaQueryList : original(query));
  const host = document.createElement('div');
  document.body.append(host);
  const root = createRoot(host);
  const dialog = () => document.body.querySelector<HTMLElement>('[role="dialog"][data-state="open"]');
  try {
    await act(async () => root.render(<Sidebar />));
    const toggle = host.querySelector<HTMLButtonElement>('button[aria-label="订阅"]')!;
    toggle.focus();
    expect(toggle.getAttribute('aria-haspopup')).toBe('dialog');
    await act(async () => toggle.click());
    expect(toggle.getAttribute('aria-expanded')).toBe('true');
    expect(dialog()?.querySelector('a[title="A very long feed name"]')?.textContent).toContain('20');
    await act(async () => dialog()!.querySelector<HTMLButtonElement>('button[aria-label="关闭订阅来源"]')!.click());
    expect(dialog()).toBeNull();
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    await vi.waitFor(() => expect(document.activeElement).toBe(toggle));
    await act(async () => toggle.click());
    mobile.matches = false;
    await act(async () => mobile.dispatchEvent(new Event('change')));
    expect(dialog()).toBeNull();
    expect(toggle.hasAttribute('aria-haspopup')).toBe(false);
    expect(toggle.getAttribute('aria-expanded')).toBe('true');
  } finally {
    await act(async () => root.unmount());
    host.remove();
    vi.restoreAllMocks();
  }
});
