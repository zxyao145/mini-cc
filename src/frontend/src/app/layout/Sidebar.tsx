"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { BookOpen, Highlighter, Library, LogOut, PanelLeftClose, PanelLeftOpen, Settings, Tags } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import styles from "./Sidebar.module.scss";

interface SidebarProps {
  isOpen?: boolean;
  onToggle?: () => void;
}

const navItems = [
  { href: "/", label: "Library", icon: Library },
  { href: "/pages/highlights", label: "Highlights", icon: Highlighter },
  { href: "/pages/tags", label: "Tags", icon: Tags },
];

export default function Sidebar({ isOpen = true, onToggle }: SidebarProps) {
  const pathname = usePathname();
  const router = useRouter();
  const { user, logout } = useAuth();

  const handleLogout = async () => {
    try {
      await logout();
      router.push("/pages/login");
    } catch (error) {
      console.error("Logout failed:", error);
    }
  };

  return (
    <aside className={`${styles.sidebar} ${isOpen ? "" : styles.closed}`} aria-label="Sidebar">
      <div className={styles.header}>
        <Link href="/" className={styles.brand} aria-label="MiniCc library">
          <span className={styles.brandMark}><BookOpen size={20} strokeWidth={1.8} aria-hidden="true" /></span>
          <span className={styles.brandName}>MiniCc<span className={styles.brandDot}>.</span></span>
        </Link>
        {onToggle && (
          <button className={styles.toggle} onClick={onToggle} aria-label={isOpen ? "Collapse sidebar" : "Expand sidebar"} aria-expanded={isOpen} title={isOpen ? "Collapse sidebar" : "Expand sidebar"}>
            {isOpen ? <PanelLeftClose size={18} aria-hidden="true" /> : <PanelLeftOpen size={18} aria-hidden="true" />}
          </button>
        )}
      </div>

      <nav className={styles.nav} aria-label="Main navigation">
        <p className={styles.sectionLabel}>WORKSPACE</p>
        <ul className={styles.navList}>
          {navItems.map(({ href, label, icon: Icon }) => {
            const active = href === "/" ? pathname === "/" || pathname.startsWith("/pages/article/") : pathname.startsWith(href);
            return (
              <li key={href}>
                <Link href={href} className={`${styles.navLink} ${active ? styles.active : ""}`} aria-label={label} aria-current={active ? "page" : undefined} title={label}>
                  <Icon size={19} strokeWidth={1.8} aria-hidden="true" />
                  <span className={styles.label}>{label}</span>
                </Link>
              </li>
            );
          })}
        </ul>
      </nav>

      {user && (
        <div className={styles.userSection}>
          <Link href="/pages/account" className={styles.accountLink} aria-label={`Account settings for ${user.userName}`} title="Account settings" aria-current={pathname === "/pages/account" ? "page" : undefined}>
            <span className={styles.avatar} aria-hidden="true">{user.userName.slice(0, 1).toUpperCase()}</span>
            <span className={styles.userInfo}><span className={styles.userName}>{user.userName}</span><span className={styles.userCaption}>Personal account</span></span>
            <Settings size={17} className={styles.settingsIcon} aria-hidden="true" />
          </Link>
          <button onClick={handleLogout} className={styles.logoutButton} aria-label="Sign out" title="Sign out">
            <LogOut size={18} strokeWidth={1.8} aria-hidden="true" />
            <span className={styles.label}>Sign out</span>
          </button>
        </div>
      )}
    </aside>
  );
}
