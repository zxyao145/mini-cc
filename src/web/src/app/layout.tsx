import type { Metadata } from "next";
import { GeistSans } from "geist/font/sans";
import { systemThemeQuery, themeStorageKey } from "@/contexts/theme-preference";
import "@fontsource-variable/inter";
import "./globals.css";

import ClientLayout from "./layout/ClientLayout";

export const metadata: Metadata = {
  title: "MiniCc · 稍后阅读",
  description: "保存文章、订阅更新和整理阅读笔记",
};

const initialTheme = `(() => {
  let preference;
  try { preference = localStorage.getItem('${themeStorageKey}'); } catch {}
  document.documentElement.dataset.theme = preference === 'dark' || preference === 'light'
    ? preference : matchMedia('${systemThemeQuery}').matches ? 'dark' : 'light';
})();`;

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="zh-CN" className={GeistSans.variable} suppressHydrationWarning>
      <head><script dangerouslySetInnerHTML={{ __html: initialTheme }} /></head>
      <body>
        <ClientLayout>{children}</ClientLayout>
      </body>
    </html>
  );
}
