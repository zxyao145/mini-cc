"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import styles from "./Sidebar.module.scss";
import { House, Lightbulb, LogOut, Settings, Tags } from "lucide-react";

interface SidebarProps {
  isOpen?: boolean;
  onToggle?: () => void;
}

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

  const navItems = [
    { href: "/", label: "Home", icon: <House size={20}/> },
    // { href: "/pages/highlights", label: "HighLights", icon: "📝" },
    { href: "/pages/highlights", label: "HighLights", icon: <Lightbulb size={20}/> },
    { href: "/pages/tags", label: "Tags", icon: <Tags size={20}/> }
  ];

  return (
    <aside
      className={`${styles.sidebar} ${isOpen ? styles.open : styles.closed}`}
    >
      <div className={styles.header}>
        {isOpen ? <h2 className={styles.title}>MiniCc</h2> : ""}

        {onToggle && (
          <button className={styles.toggle} onClick={onToggle}>
            {isOpen ? "←" : "→"}
          </button>
        )}
      </div>

      <nav className={styles.nav}>
        <ul className={styles.navList}>
          {navItems.map((item) => (
            <li key={item.href} className={styles.navItem}>
              <Link
                href={item.href}
                className={`${styles.navLink} ${
                  pathname === item.href ? styles.active : ""
                }`}
              >
                <span className={styles.icon}>{item.icon}</span>
                {isOpen && <span className={styles.label}>{item.label}</span>}
              </Link>
            </li>
          ))}
        </ul>
      </nav>

      {user && (
        <div className={styles.userSection}>
          {isOpen && (
            <div className={styles.userInfo}>
              <Link href="/pages/account" className={styles.accountLink}>
                <span className={styles.userName}>{user.userName}</span>
                <span className={styles.settingsIcon}>
                  <Settings size={20} />
                </span>
              </Link>
            </div>
          )}
          <button
            onClick={handleLogout}
            className={styles.logoutButton}
            title={isOpen ? "退出登录" : "退出"}
          >
            <span className={styles.icon}>
              <LogOut size={20} />
            </span>
            {isOpen && <span className={styles.label}>退出</span>}
          </button>
        </div>
      )}
    </aside>
  );
}