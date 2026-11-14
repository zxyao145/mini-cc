'use client';
import { Suspense, useCallback, useEffect, useRef, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { BookOpen, LoaderCircle } from 'lucide-react';
import ArticleList from '@/app/components/ArticleList';
import LibraryToolbar from '@/app/components/LibraryToolbar';
import { ArticleListItem } from '@/types';
import { articleApi } from '@/lib/api';
import { isFiltered, LibraryQuery, parseLibraryQuery, serializeLibraryQuery } from '@/lib/library-query';
import { useArticleList } from '@/hooks/useArticleList';
import styles from './page.module.scss';
import BatchToolbar, { actionLabels } from '@/app/components/BatchToolbar';
import { BatchAction, notifyLibraryMutation } from '@/lib/library-mutations';
import { useToast } from '@/contexts/ToastContext';

const viewLabels = { inbox: '收件箱', all: '全部文章', favorites: '收藏', archive: '归档', trash: '回收站', feeds: '订阅' };
const emptyLabels = { inbox: '没有待处理的文章', all: '还没有保存文章', favorites: '还没有收藏文章', archive: '还没有归档文章', trash: '回收站是空的', feeds: '还没有订阅文章' };
function ContinueReading() {
  const [items, setItems] = useState<ArticleListItem[]>([]);
  useEffect(() => {
    const controller = new AbortController();
    articleApi.getArticles(new URLSearchParams('view=all&status=reading&sort=read_desc&pageSize=5'), controller.signal).then(page => { if (!controller.signal.aborted) setItems(page.items); }).catch(() => {});
    return () => controller.abort();
  }, []);
  if (!items.length) return null;
  return <section className={styles.continue} aria-label="继续阅读"><h2><BookOpen size={16} aria-hidden="true" />继续阅读</h2><div>{items.map(item => <Link key={item.id} href={`/pages/article/${item.id}`} scroll={false}><span>{item.title}</span><span>{Math.round(item.progressPercent || 0)}%</span></Link>)}</div></section>;
}
function Library() {
  const params = useSearchParams();
  const router = useRouter();
  const query = parseLibraryQuery(new URLSearchParams(params.toString()));
  const { items, total, loading, loadingMore, error, hasMore, loadMore, updateItem, removeItems } = useArticleList(query);
  const toast = useToast();
  const [selectionMode, setSelectionMode] = useState(false);
  const [selected, setSelected] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const busyRef = useRef(false);
  const [processed, setProcessed] = useState(0);
  const [failedRows, setFailedRows] = useState<ArticleListItem[]>([]);
  const queryKey = serializeLibraryQuery(query);
  const querySession = useRef({ key: queryKey, generation: 0 });
  if (querySession.current.key !== queryKey) querySession.current = { key: queryKey, generation: querySession.current.generation + 1 };
  useEffect(() => { setSelectionMode(false); setSelected([]); setFailedRows([]); }, [queryKey]);
  const [articleStyle, setArticleStyle] = useState<'grid' | 'list'>('grid');
  const navigate = useCallback((next: LibraryQuery, replace: boolean) => {
    const value = serializeLibraryQuery(next);
    const url = value ? `/?${value}` : '/';
    if (replace) router.replace(url, { scroll: false }); else router.push(url, { scroll: false });
  }, [router]);
  const handleDelete = async (id: string) => { await articleApi.deleteArticle(id); removeItems([id]); toast.deleted([id]); };
  const toggleSelection = (id: string) => { setSelectionMode(true); setSelected(current => current.includes(id) ? current.filter(value => value !== id) : [...current, id]); };
  const perform = async (action: BatchAction, tagIds?: string[], retryIds?: string[], retryGeneration?: number) => {
    const ids = retryIds || selected;
    if (busyRef.current || !ids.length) return;
    const requestGeneration = retryGeneration ?? querySession.current.generation;
    if (action === 'purge' && !confirm(`将永久删除 ${ids.length} 篇文章，无法恢复。`)) return;
    busyRef.current = true; setBusy(true); setProcessed(0);
    try {
      const result: import('@/lib/library-mutations').BatchResult = { succeeded: [], failed: [] };
      for (let start = 0; start < ids.length; start += 200) {
        const chunk = ids.slice(start, start + 200);
        try { const part = await articleApi.batch(chunk, action, tagIds); result.succeeded.push(...part.succeeded); result.failed.push(...part.failed); }
        catch { result.failed.push(...chunk.map(id => ({ id, reason: 'error' }))); }
        setProcessed(Math.min(start + chunk.length, ids.length));
      }
      const failed = result.failed.map(item => item.id);
      const rows = [...items, ...failedRows].filter(item => failed.includes(item.id));
      if (requestGeneration === querySession.current.generation) { setSelected(failed); setFailedRows(Array.from(new Map(rows.map(item => [item.id, item])).values())); setSelectionMode(!!failed.length); }
      const changes = action === 'favorite' ? { isFavorite: true } : action === 'unfavorite' ? { isFavorite: false } : action === 'archive' ? { isArchived: true } : action === 'unarchive' ? { isArchived: false } : action === 'markRead' ? { readingStatus: 'read' as const } : action === 'markUnread' ? { readingStatus: 'unread' as const } : undefined;
      notifyLibraryMutation({ ids: result.succeeded, changes, remove: action === 'trash' || action === 'restore' || action === 'purge' });
      if (action === 'trash') toast.deleted(result.succeeded);
      if (failed.length) toast.show(`${result.succeeded.length} 篇已${actionLabels[action]}，${failed.length} 篇失败`, '重试', () => perform(action, tagIds, failed, requestGeneration));
      else if (action !== 'trash') toast.show(`已${actionLabels[action]} ${result.succeeded.length} 篇`);
    } catch { toast.show('操作失败，请重试'); }
    finally { busyRef.current = false; setBusy(false); }
  };
  const emptyTrash = async () => {
    if (busyRef.current) return;
    busyRef.current = true; setBusy(true);
    try {
      const page = await articleApi.getArticles(new URLSearchParams('view=trash&pageSize=1'));
      if (!confirm(`将永久删除 ${page.total} 篇文章，无法恢复。`)) return;
      const result = await articleApi.emptyTrash(); notifyLibraryMutation(); setSelected([]); setFailedRows([]); setSelectionMode(false); toast.show(`已永久删除 ${result.purged} 篇文章`); }
    catch { toast.show('清空回收站失败，请重试'); } finally { busyRef.current = false; setBusy(false); }
  };
  const displayedItems = [...items, ...failedRows.filter(row => !items.some(item => item.id === row.id))];
  const filtered = isFiltered(query);
  return <div className={styles.container}>
    {selectionMode && <BatchToolbar selected={selected.length} loaded={displayedItems.length} processed={processed} trash={query.view === 'trash'} busy={busy} onSelectAll={() => setSelected(items.map(item => item.id))} onCancel={() => { setSelectionMode(false); setSelected([]); setFailedRows([]); }} onAction={perform} />}
    <header className={styles.header}><LibraryToolbar query={query} onChange={navigate} articleStyle={articleStyle} onStyleChange={setArticleStyle} onSelect={() => setSelectionMode(true)} /></header>
    {query.view === 'inbox' && !filtered && <ContinueReading />}
    <div className={styles.main}>
      <div className={styles.sectionHeading}><h1>{viewLabels[query.view]}</h1>{query.view === 'trash' && <button type="button" disabled={busy || !total} onClick={emptyTrash} style={{ minHeight: 44 }}>清空回收站</button>}<span className={styles.count} role="status" aria-atomic="true">{loading ? '正在加载…' : hasMore ? `共 ${total} 篇，已加载 ${items.length} 篇` : `共 ${total} 篇`}</span></div>
      {!loading && !displayedItems.length && !error ? <div className={styles.empty}><BookOpen size={32} strokeWidth={1.5} aria-hidden="true" /><h2>{filtered ? '没有符合筛选条件的文章' : emptyLabels[query.view]}</h2><p>{filtered ? '调整上方筛选条件，或清除筛选后重试。' : '使用浏览器插件保存文章，在这里阅读和管理。'}</p></div> : <ArticleList articles={displayedItems} selectionMode={selectionMode} selected={selected} selectionDisabled={busy} onSelect={toggleSelection} loading={loading} onDelete={handleDelete} onUpdate={updateItem} articleStyle={articleStyle} />}
      {error && <p className={styles.error} role="alert">{error}</p>}
      {(hasMore || error) && <div className={styles.loadMore}><button type="button" onClick={loadMore} disabled={loading || loadingMore}>{loadingMore ? <><LoaderCircle size={16} className={styles.spinning} aria-hidden="true" />正在加载…</> : error ? '加载失败，重试' : '加载更多'}</button></div>}
    </div>
  </div>;
}
export default function Home() { return <Suspense fallback={<div className={styles.container} role="status">正在加载文库…</div>}><Library /></Suspense>; }
