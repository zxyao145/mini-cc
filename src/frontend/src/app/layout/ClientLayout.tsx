"use client";

import { Suspense, useState } from "react";
import { usePathname } from "next/navigation";
import Sidebar from "@/app/layout/Sidebar";
import { ReaderPreferencesProvider } from "@/contexts/ReaderPreferencesContext";
import { FeedsProvider } from "@/contexts/FeedsContext";
import { ToastProvider } from "@/contexts/ToastContext";
import { AuthProvider } from "@/contexts/AuthContext";
import ProtectedRoute from "@/app/layout/ProtectedRoute";
import styles from "./ClientLayout.module.scss";

export default function ClientLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const pathname = usePathname();
  const isLoginPage = pathname === "/pages/login";

  return (
    <AuthProvider>
      <ReaderPreferencesProvider>
      <ToastProvider>
      <FeedsProvider>
      {isLoginPage ? (
        children
      ) : (
        <ProtectedRoute>
          <div className={styles.layout}>
            <a className="skip-link" href="#main-content">Skip to content</a>
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
      </ToastProvider>
      </ReaderPreferencesProvider>
    </AuthProvider>
  );
}