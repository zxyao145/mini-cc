import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, it, vi } from 'vitest';
import { ThemeProvider, useTheme } from './ThemeContext';

it('persists a manual selection and reacts to system changes only in system mode', async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const media = Object.assign(new EventTarget(), { matches: true });
  const matchMedia = vi.spyOn(window, 'matchMedia').mockReturnValue(media as MediaQueryList);
  localStorage.setItem('app-theme', 'light');
  const container = document.createElement('div');
  const root = createRoot(container);
  function Controls() {
    const { preference, theme, setPreference } = useTheme();
    return <button onClick={() => setPreference('system')}>{preference}/{theme}</button>;
  }
  try {
    await act(async () => root.render(<ThemeProvider><Controls /></ThemeProvider>));
    expect(container.textContent).toBe('light/light');
    expect(document.documentElement.dataset.theme).toBe('light');
    await act(async () => container.querySelector('button')!.click());
    expect(container.textContent).toBe('system/dark');
    expect(localStorage.getItem('app-theme')).toBe('system');
    media.matches = false;
    await act(async () => media.dispatchEvent(new Event('change')));
    expect(document.documentElement.dataset.theme).toBe('light');
  } finally {
    await act(async () => root.unmount());
    localStorage.removeItem('app-theme');
    delete document.documentElement.dataset.theme;
    matchMedia.mockRestore();
  }
});
