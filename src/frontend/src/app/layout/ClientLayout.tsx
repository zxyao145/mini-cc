"use client";

import { Suspense, useState } from "react";
import { usePathname } from "next/navigation";
import Sidebar from "@/app/layout/Sidebar";
import { ReaderPreferencesProvider } from "@/contexts/ReaderPreferencesContext";
import { FeedsProvider } from "@/contexts/FeedsContext";
import { ToastProvider } from "@/contexts/ToastContext";
import { AuthProvider } from "@/contexts/AuthContext";
import ProtectedRoute from "@/app/layout/ProtectedRoute";
import { ConfirmProvider } from "@/contexts/ConfirmContext";
import { ThemeProvider } from "@/contexts/ThemeContext";
import styles from "./ClientLayout.module.css";

export default function ClientLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const pathname = usePathname();
  const isLoginPage = pathname === "/pages/login";

  return (
    <ThemeProvider>
    <AuthProvider>
      <ReaderPreferencesProvider>
      <ToastProvider>
      <ConfirmProvider>
      <FeedsProvider>
      {isLoginPage ? (
        children
      ) : (
        <ProtectedRoute>
          <div className={styles.layout}>
            <a className="skip-link" href="#main-content">跳到主要内容</a>
            <Suspense fallback={null}><Sidebar
              isOpen={sidebarOpen} 
              onToggle={() => setSidebarOpen(!sidebarOpen)} 
            /></Suspense>
            <main id="main-content" tabIndex={-1} className={`${styles.content} ${sidebarOpen ? styles.sidebarOpen : styles.sidebarClosed}`}>
              {children}
            </main>
          </div>
        </ProtectedRoute>
      )}
      </FeedsProvider>
      </ConfirmProvider>
      </ToastProvider>
      </ReaderPreferencesProvider>
    </AuthProvider>
    </ThemeProvider>
  );
}