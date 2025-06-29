"use client";

import { useState } from "react";
import Sidebar from "@/components/Sidebar";
import styles from "./layout.module.scss";

export default function ClientLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const [sidebarOpen, setSidebarOpen] = useState(true);

  return (
    <div className={styles.layout}>
      <Sidebar 
        isOpen={sidebarOpen} 
        onToggle={() => setSidebarOpen(!sidebarOpen)} 
      />
      <main className={`${styles.content} ${sidebarOpen ? styles.sidebarOpen : styles.sidebarClosed}`}>
        {children}
      </main>
    </div>
  );
}