"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import styles from "./Sidebar.module.scss";

interface SidebarProps {
  isOpen?: boolean;
  onToggle?: () => void;
}

export default function Sidebar({ isOpen = true, onToggle }: SidebarProps) {
  const pathname = usePathname();

  const navItems = [
    { href: "/", label: "Home", icon: "🏠" },
    // { href: "/highlights", label: "HighLights", icon: "📝" },
    // { href: "/tags", label: "Tags", icon: "🏷️" }
  ];

  return (
    <aside className={`${styles.sidebar} ${isOpen ? styles.open : styles.closed}`}>
      <div className={styles.header}>
        {
          isOpen ? <h2 className={styles.title}>OmeReader</h2> : ""
        }
        
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
                className={`${styles.navLink} ${pathname === item.href ? styles.active : ""}`}
              >
                <span className={styles.icon}>{item.icon}</span>
                {isOpen && <span className={styles.label}>{item.label}</span>}
              </Link>
            </li>
          ))}
        </ul>
      </nav>
    </aside>
  );
}