"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import type { ReactElement } from "react";
import { useFeeds } from "@/contexts/FeedsContext";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { Sun, Moon, Monitor, AlertTriangle, ChevronDown, ChevronRight, Rss, Archive, BookOpen, Heart, Inbox, Trash2, Highlighter, Library, LogOut, PanelLeftClose, PanelLeftOpen, Settings, Tags } from "lucide-react";
import { parseLibraryQuery, serializeLibraryQuery, LibraryView } from "@/lib/library-query";
import { useAuth } from "@/contexts/AuthContext";
import { Avatar } from "@/components/arc/avatar/avatar";
import { Button } from "@/components/arc/button/button";
import { Dialog, DialogContent } from "@/components/arc/dialog/dialog";
import { Tooltip } from "@/components/arc/tooltip/tooltip";
import { useTheme } from "@/contexts/ThemeContext";
import styles from "./Sidebar.module.css";

interface SidebarProps {
  isOpen?: boolean;
  onToggle?: () => void;
}

const leadingViews = [
  { view: 'inbox', label: '收件箱', icon: Inbox },
] as const;
const libraryViews = [
  { view: 'all', label: '全部文章', icon: Library },
  { view: 'favorites', label: '收藏', icon: Heart },
] as const;
const closingViews = [
  { view: 'archive', label: '归档', icon: Archive },
  { view: 'trash', label: '回收站', icon: Trash2 },
] as const;
const navItems = [
  { href: '/pages/highlights', label: '高亮笔记', icon: Highlighter },
  { href: '/pages/tags', label: '标签', icon: Tags },
  { href: '/pages/feeds', label: '管理订阅', icon: Rss },
];
const themes = [
  { value: 'light', label: '浅色', icon: Sun },
  { value: 'dark', label: '深色', icon: Moon },
  { value: 'system', label: '跟随系统', icon: Monitor },
] as const;

function ThemeControls({ compact }: { compact: boolean }) {
  const { preference, setPreference } = useTheme();
  if (compact) {
    const index = themes.findIndex(theme => theme.value === preference);
    const { label, icon: Icon } = themes[index];
    const next = themes[(index + 1) % themes.length];
    const description = `主题：${label}，切换到${next.label}`;
    return <Tooltip content={description} side="bottom">
      <Button variant="ghost" size="sm" iconOnly aria-label={description} onClick={() => setPreference(next.value)}>
        <Icon size={16} aria-hidden="true" />
      </Button>
    </Tooltip>;
  }
  return <div className={styles.themeSwitch} role="group" aria-label="界面主题">
    {themes.map(({ value, label, icon: Icon }) => (
      <Tooltip key={value} content={label}>
        <Button variant="ghost" size="sm" iconOnly aria-label={label} aria-pressed={preference === value} onClick={() => setPreference(value)}>
          <Icon size={16} aria-hidden="true" />
        </Button>
      </Tooltip>
    ))}
  </div>;
}

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
  const feedsButton = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    const media = window.matchMedia('(max-width: 767px)');
    const change = () => { setMobile(media.matches); if (!media.matches) setMobileFeedsOpen(false); };
    change(); media.addEventListener('change', change);
    return () => media.removeEventListener('change', change);
  }, []);

  const toggleFeeds = () => {
    if (mobile) setMobileFeedsOpen(true);
    else setFeedsOpen(open => !open);
  };
  const compact = mobile || !isOpen;
  const feedLinks = (close?: () => void) => <>
    <li>
      <Link href="/?view=feeds" className={styles.navLink} onClick={close} aria-current={pathname === '/' && query.view === 'feeds' && !query.feedId ? 'page' : undefined}>
        全部订阅文章
      </Link>
    </li>
    {feeds.map(feed => (
      <li key={feed.id}>
        <Link href={`/?view=feeds&feedId=${feed.id}`} scroll={false} onClick={close} className={styles.navLink}
          aria-current={query.feedId === feed.id && pathname === '/' ? 'page' : undefined} title={feed.title}>
          <span className={styles.feedTitle}>{feed.title}</span>
          {feed.health === 'failing' && <AlertTriangle className={styles.feedIssue} size={14} aria-label="更新失败" />}
          <span className={styles.feedCount}>{feed.unreadCount}</span>
        </Link>
      </li>
    ))}
  </>;

  const handleLogout = async () => {
    try {
      await logout();
      router.push("/pages/login");
    } catch (error) {
      console.error("Logout failed:", error);
    }
  };
  const toggleLabel = isOpen ? '收起侧边栏' : '展开侧边栏';
  const feedsExpanded = mobile ? mobileFeedsOpen : feedsOpen && isOpen;
  const accountLabel = '个人账户';
  const tooltipSide = mobile ? 'bottom' : 'right';
  // Tooltips only stand in for hidden labels: navigation labels hide in the collapsed desktop sidebar, footer labels whenever compact.
  const navLabelsHidden = !isOpen && !mobile;
  const hint = (show: boolean, label: string, node: ReactElement) => show ? <Tooltip content={label} side={tooltipSide}>{node}</Tooltip> : node;
  const viewItems = (views: readonly { view: LibraryView; label: string; icon: typeof Inbox }[]) => views.map(({ view, label, icon: Icon }) => (
    <li key={view}>
      {hint(navLabelsHidden, label,
        <Link href={viewLink(view)} scroll={false} className={styles.navLink} aria-label={label} aria-current={pathname === '/' && query.view === view ? 'page' : undefined}>
          <Icon size={18} strokeWidth={1.8} aria-hidden="true" /><span className={styles.label}>{label}</span>
        </Link>)}
    </li>
  ));

  return <aside className={`${styles.sidebar} ${isOpen ? "" : styles.closed}`} data-app-sidebar aria-label="侧边栏">
    <div className={styles.header}>
      <Link href="/" className={styles.brand} aria-label="MiniCc 文库" title="MiniCc 文库">
        <span className={styles.brandMark}><BookOpen size={18} strokeWidth={1.8} aria-hidden="true" /></span>
        <span className={styles.brandName}>MiniCc</span>
      </Link>
      {onToggle && <Tooltip content={toggleLabel} side="right">
        <Button variant="ghost" size="sm" iconOnly className={styles.toggle} onClick={onToggle} aria-label={toggleLabel} aria-expanded={isOpen}>
          {isOpen ? <PanelLeftClose size={17} aria-hidden="true" /> : <PanelLeftOpen size={17} aria-hidden="true" />}
        </Button>
      </Tooltip>}
    </div>

    <nav className={styles.nav} aria-label="主导航">
      <p className={styles.sectionLabel}>工作区</p>
      <ul className={styles.navList}>
        {viewItems(leadingViews)}
        <li>
          {hint(navLabelsHidden, '订阅',
            <button type="button" ref={feedsButton} className={`${styles.navLink} ${styles.feedToggle}`} aria-label="订阅" aria-expanded={feedsExpanded}
              aria-haspopup={mobile ? 'dialog' : undefined} aria-controls={feedsExpanded ? (mobile ? 'sidebar-feed-dialog' : 'sidebar-feed-list') : undefined} onClick={toggleFeeds}>
              <Rss size={18} strokeWidth={1.8} aria-hidden="true" /><span className={styles.label}>订阅</span>
              <span className={styles.chevron} aria-hidden="true">{feedsOpen ? <ChevronDown size={14} /> : <ChevronRight size={14} />}</span>
            </button>)}
          {feedsOpen && <ul id="sidebar-feed-list" className={styles.feedList}>{feedLinks()}</ul>}
        </li>
        {viewItems(libraryViews)}
        {navItems.map(({ href, label, icon: Icon }) => (
          <li key={href}>
            {hint(navLabelsHidden, label,
              <Link href={href} scroll={false} className={styles.navLink} aria-label={label} aria-current={pathname.startsWith(href) ? "page" : undefined}>
                <Icon size={18} strokeWidth={1.8} aria-hidden="true" /><span className={styles.label}>{label}</span>
              </Link>)}
          </li>
        ))}
        {viewItems(closingViews)}
      </ul>
    </nav>

    <Dialog open={mobileFeedsOpen} onOpenChange={setMobileFeedsOpen}>
      <DialogContent id="sidebar-feed-dialog" title="订阅来源" closeLabel="关闭订阅来源" className={styles.feedDialog}
        onInteractOutside={event => event.preventDefault()}
        onCloseAutoFocus={event => { event.preventDefault(); feedsButton.current?.focus(); }}>
        <ul className={styles.feedMenuList}>{feedLinks(() => setMobileFeedsOpen(false))}</ul>
      </DialogContent>
    </Dialog>

    <div className={styles.footer}>
      {user && <div className={styles.userSection}>
        {hint(compact, accountLabel,
          <Link href="/pages/account" className={styles.accountLink} aria-label={accountLabel} aria-current={pathname === "/pages/account" ? "page" : undefined}>
            <Avatar name={user.userName} size="sm" aria-hidden="true" />
            <span className={styles.userInfo}><span className={styles.userName}>{user.userName}</span><span className={styles.userCaption}>{accountLabel}</span></span>
            <Settings size={16} className={styles.settingsIcon} aria-hidden="true" />
          </Link>)}
        {hint(compact, '退出登录',
          <Button variant="ghost" size="sm" iconOnly={compact} onClick={handleLogout} className={styles.logoutButton} aria-label="退出登录">
            <LogOut size={16} aria-hidden="true" /><span className={styles.label}>退出登录</span>
          </Button>)}
      </div>}
      <ThemeControls compact={compact} />
    </div>
  </aside>;
}
