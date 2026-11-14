'use client';

import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { systemThemeQuery, themeStorageKey } from './theme-preference';

type Theme = 'light' | 'dark';
export type ThemePreference = Theme | 'system';

const ThemeContext = createContext<{
  preference: ThemePreference;
  theme: Theme;
  setPreference: (preference: ThemePreference) => void;
}>({ preference: 'system', theme: 'light', setPreference: () => {} });

export const useTheme = () => useContext(ThemeContext);

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, updatePreference] = useState<ThemePreference>('system');
  const [systemTheme, setSystemTheme] = useState<Theme>('light');
  const [ready, setReady] = useState(false);
  const theme = preference === 'system' ? systemTheme : preference;

  useEffect(() => {
    try {
      const stored = localStorage.getItem(themeStorageKey);
      if (stored === 'dark' || stored === 'light') updatePreference(stored);
    } catch { /* Storage may be unavailable. */ }
    const media = window.matchMedia(systemThemeQuery);
    const readSystemTheme = () => setSystemTheme(media.matches ? 'dark' : 'light');
    readSystemTheme();
    media.addEventListener('change', readSystemTheme);
    setReady(true);
    return () => media.removeEventListener('change', readSystemTheme);
  }, []);

  useEffect(() => {
    if (ready) document.documentElement.dataset.theme = theme;
  }, [ready, theme]);

  const setPreference = (next: ThemePreference) => {
    updatePreference(next);
    try { localStorage.setItem(themeStorageKey, next); } catch { /* Apply for this session. */ }
  };

  return <ThemeContext.Provider value={{ preference, theme, setPreference }}>{children}</ThemeContext.Provider>;
}
