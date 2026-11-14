'use client';
import Link from 'next/link';
import { useState } from 'react';
import { AlertTriangle, Pause, PencilLine, Play, Plus, RefreshCw, Rss, Trash2, X } from 'lucide-react';
import { useFeeds } from '@/contexts/FeedsContext';
import { feedApi } from '@/lib/api';
import { Feed } from '@/types';
import FeedDialog, { feedError } from './FeedDialog';
import styles from './feeds.module.scss';

export default function FeedsPage() {
  const { feeds, error, loading, refresh, changed } = useFeeds();
  const [editing, setEditing] = useState<Feed | 'new' | null>(null), [pending, setPending] = useState<string | null>(null);
  const [message, setMessage] = useState(''), [actionError, setActionError] = useState('');
  async function action(feed: Feed, operation: 'refresh' | 'pause' | 'unsubscribe' | 'trash') {
    if (operation === 'unsubscribe' && !confirm(`取消订阅“${feed.title}”？已导入的文章会保留。`)) return;
    if (operation === 'trash' && !confirm('将这个订阅源的文章移入回收站？同时来自其他订阅源或经浏览器插件保存的文章会跳过。')) return;
    setPending(feed.id); setMessage(''); setActionError('');
    try {
      if (operation === 'refresh') { const result = await feedApi.refresh(feed.id); if (result.status === 'failing') setActionError(result.lastError || '更新失败'); else setMessage(`已导入 ${result.imported} 篇，跳过 ${result.skipped} 条`); }
      if (operation === 'pause') { await feedApi.edit(feed.id, { paused: feed.status !== 'paused' }); setMessage(feed.status === 'paused' ? '已恢复订阅' : '已暂停订阅'); }
      if (operation === 'unsubscribe') { await feedApi.unsubscribe(feed.id); setMessage('已取消订阅，文章已保留'); }
      if (operation === 'trash') { const result = await feedApi.trash(feed.id); setMessage(`已移入回收站 ${result.trashed} 篇，跳过 ${result.skipped} 篇`); }
      await changed();
    } catch (e) { setActionError(feedError(e)); }
    finally { setPending(null); }
  }
  return <div className={styles.page}>
    <header className={styles.heading}><div><span className={styles.eyebrow}>你的阅读来源</span><h1><Rss size={28}/>订阅</h1><p className={styles.muted}>让喜欢的网站，定期来到你的文库。</p></div><button className={styles.primary} onClick={() => setEditing('new')}><Plus size={18}/>添加订阅</button></header>
    {message && <p className={styles.notice} role="status">{message}</p>}{actionError && <p className={styles.error} role="alert">{actionError}</p>}
    {error && <p role="alert" className={styles.error}>{error}<button onClick={() => void refresh()}>重试</button></p>}
    {!feeds.length ? <section className={styles.empty}><Rss size={36}/><h2>{loading ? '正在加载订阅…' : '从一个订阅开始'}</h2><p>添加 RSS 或 Atom 地址，首次最多导入最近 20 篇。</p></section> : <div className={styles.list}>{feeds.map(feed => <section className={styles.feed} key={feed.id} aria-label={feed.title}>
      <div className={styles.feedInfo}><Link href={`/?view=feeds&feedId=${feed.id}`}><h2>{feed.title}</h2></Link><p className={styles.muted}>{feed.unreadCount} 篇未读 · {feed.articleCount} 篇文章</p><p className={styles.updated}>最近成功更新：{feed.lastSuccessAt ? new Date(feed.lastSuccessAt).toLocaleString() : '尚未成功更新'}</p><span className={feed.health === 'failing' ? styles.failure : styles.status}>{feed.status === 'paused' ? <><Pause size={14}/>已暂停</> : feed.health === 'failing' ? <><AlertTriangle size={14}/>更新失败</> : '正常'}</span>{feed.lastError && <p className={styles.error}>{feed.lastError}</p>}<div className={styles.tags}>{feed.tags.map(t => <span key={t.id}>{t.name}</span>)}</div></div>
      <div className={styles.actions}><button disabled={pending !== null || feed.status === 'paused'} onClick={() => void action(feed, 'refresh')}><RefreshCw size={16}/>{pending === feed.id ? '处理中…' : '立即刷新'}</button><button disabled={pending !== null} onClick={() => setEditing(feed)}><PencilLine size={16}/>编辑</button><button disabled={pending !== null} onClick={() => void action(feed, 'pause')}>{feed.status === 'paused' ? <Play size={16}/> : <Pause size={16}/>} {feed.status === 'paused' ? '恢复' : '暂停'}</button><button disabled={pending !== null} onClick={() => void action(feed, 'unsubscribe')}><X size={16}/>取消订阅</button><button className={styles.danger} disabled={pending !== null} onClick={() => void action(feed, 'trash')}><Trash2 size={16}/>删除已导入文章</button></div>
    </section>)}</div>}
    {editing && <FeedDialog feed={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} onDone={text => { setEditing(null); setMessage(text); setActionError(''); void changed(); }}/ >}
  </div>;
}
