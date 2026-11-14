"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { useFeeds } from "@/contexts/FeedsContext";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { X, AlertTriangle, ChevronDown, ChevronRight, Rss, Archive, BookOpen, Heart, Inbox, Trash2, Highlighter, Library, LogOut, PanelLeftClose, PanelLeftOpen, Settings, Tags } from "lucide-react";
import { parseLibraryQuery, serializeLibraryQuery, LibraryView } from "@/lib/library-query";
import { useAuth } from "@/contexts/AuthContext";
import styles from "./Sidebar.module.scss";

interface SidebarProps {
  isOpen?: boolean;
  onToggle?: () => void;
}

const libraryViews = [
  { view: 'inbox', label: '收件箱', icon: Inbox },
  { view: 'all', label: '全部文章', icon: Library },
  { view: 'favorites', label: '收藏', icon: Heart },
  { view: 'archive', label: '归档', icon: Archive },
  { view: 'trash', label: '回收站', icon: Trash2 },
] as const;
const navItems = [
  { href: '/pages/feeds', label: '管理订阅', icon: Rss },
  { href: '/pages/highlights', label: 'Highlights', icon: Highlighter },
  { href: '/pages/tags', label: 'Tags', icon: Tags },
];

export default function Sidebar({ isOpen = true, onToggle }: SidebarProps) {
  const pathname = usePathname();
  const params = useSearchParams();
  const query = parseLibraryQuery(new URLSearchParams(params.toString()));
  const viewLink = (view: LibraryView) => { const search = serializeLibraryQuery({ ...query, view }); return search ? `/?${search}` : "/"; };
  const router = useRouter();
  const { user, logout } = useAuth();
  const { feeds } = useFeeds();
  const [feedsOpen, setFeedsOpen] = useState(true);
  const [mobile, setMobile] = useState(false);
  const [mobileFeedsOpen, setMobileFeedsOpen] = useState(false);
  const feedDialog = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const media = window.matchMedia('(max-width: 767px)');
    const change = () => { setMobile(media.matches); if (!media.matches) { feedDialog.current?.close(); setMobileFeedsOpen(false); } };
    change(); media.addEventListener('change', change);
    return () => media.removeEventListener('change', change);
  }, []);
  const closeFeeds = () => { feedDialog.current?.close(); setMobileFeedsOpen(false); };
  const toggleFeeds = () => {
    if (mobile) { feedDialog.current?.showModal(); setMobileFeedsOpen(true); }
    else setFeedsOpen(open => !open);
  };
  const feedLinks = (close?: () => void) => <><li><Link href="/?view=feeds" className={styles.navLink} onClick={close}>全部订阅文章</Link></li>{feeds.map(feed => <li key={feed.id}><Link href={`/?view=feeds&feedId=${feed.id}`} scroll={false} onClick={close} className={`${styles.navLink} ${query.feedId === feed.id && pathname === '/' ? styles.active : ''}`} title={feed.title}><span className={styles.feedTitle}>{feed.title}</span>{feed.health === 'failing' && <AlertTriangle size={14} aria-label="更新失败"/>}<span className={styles.feedCount}>{feed.unreadCount}</span></Link></li>)}</>;

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
          {libraryViews.map(({ view, label, icon: Icon }) => {
            const active = pathname === '/' && query.view === view;
            return <li key={view}><Link href={viewLink(view)} scroll={false} className={`${styles.navLink} ${active ? styles.active : ''}`} aria-label={label} title={label} aria-current={active ? 'page' : undefined}><Icon size={19} strokeWidth={1.8} aria-hidden="true" /><span className={styles.label}>{label}</span></Link></li>;
          })}
          <li>
            <button className={`${styles.navLink} ${styles.feedToggle}`} aria-expanded={mobile ? mobileFeedsOpen : feedsOpen && isOpen} aria-haspopup={mobile ? 'dialog' : undefined} aria-controls={mobile ? 'sidebar-feed-dialog' : 'sidebar-feed-list'} onClick={toggleFeeds} title="订阅"><Rss size={19}/><span className={styles.label}>订阅</span>{feedsOpen ? <ChevronDown size={16}/> : <ChevronRight size={16}/>}</button>
            {feedsOpen && <ul id="sidebar-feed-list" className={styles.feedList}>{feedLinks()}</ul>}
          </li>
          {navItems.map(({ href, label, icon: Icon }) => {
            const active = href === "/" ? pathname === "/" || pathname.startsWith("/pages/article/") : pathname.startsWith(href);
            return (
              <li key={href}>
                <Link href={href} scroll={false} className={`${styles.navLink} ${active ? styles.active : ""}`} aria-label={label} aria-current={active ? "page" : undefined} title={label}>
                  <Icon size={19} strokeWidth={1.8} aria-hidden="true" />
                  <span className={styles.label}>{label}</span>
                </Link>
              </li>
            );
          })}
        </ul>
      </nav>

      <dialog ref={feedDialog} id="sidebar-feed-dialog" className={styles.feedDialog} aria-label="订阅来源" onClose={() => setMobileFeedsOpen(false)}>
        <div className={styles.feedDialogHeader}><strong>订阅来源</strong><button aria-label="关闭订阅来源" onClick={closeFeeds}><X size={20}/></button></div>
        <ul className={styles.feedMenuList}>{feedLinks(closeFeeds)}</ul>
      </dialog>

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
