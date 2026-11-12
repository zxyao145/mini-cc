'use client';
import { useCallback, useEffect, useRef, useState } from 'react';
import { LayoutGrid, List, Search, X } from 'lucide-react';
import { tagApi } from '@/lib/api';
import { useFeeds } from '@/contexts/FeedsContext';
import { LibraryQuery, parseLibraryQuery, ReadingStatus, serializeLibraryQuery } from '@/lib/library-query';
import { TagWithArticleCount } from '@/types';
import styles from './LibraryToolbar.module.scss';

export const statusLabels = { unread: '未读', reading: '阅读中', read: '已读' };
interface Props {
  query: LibraryQuery; onChange: (query: LibraryQuery, replace: boolean) => void;
  articleStyle: string; onStyleChange: (style: 'grid' | 'list') => void; onSelect?: () => void;
}
export default function LibraryToolbar({ query: incomingQuery, onChange, articleStyle, onStyleChange, onSelect }: Props) {
  const { feeds } = useFeeds();
  const [query, setQuery] = useState(incomingQuery);
  const [search, setSearch] = useState(incomingQuery.q);
  const [tagSearch, setTagSearch] = useState('');
  const [tags, setTags] = useState<TagWithArticleCount[]>([]);
  const [tagError, setTagError] = useState(false);
  const toolbar = useRef<HTMLDivElement>(null);
  const callback = useRef(onChange); callback.current = onChange;
  const current = useRef(incomingQuery);
  const sent = useRef(incomingQuery);
  const incoming = useRef(incomingQuery); incoming.current = incomingQuery;
  const searchValue = useRef(incomingQuery.q);
  const pending = useRef<string[]>([]);
  const incomingKey = serializeLibraryQuery(incomingQuery);
  const adoptUrl = useCallback((value: LibraryQuery, keepSearch = false) => {
    const next = keepSearch ? { ...value, q: searchValue.current } : value;
    current.current = next; sent.current = value; pending.current = [];
    searchValue.current = next.q; setQuery(next); setSearch(next.q);
  }, []);
  useEffect(() => {
    // Older router commits must not erase edits already sent in a newer navigation.
    if (pending.current.includes(incomingKey) && incomingKey !== serializeLibraryQuery(sent.current)) return;
    const ownAcknowledgement = pending.current.includes(incomingKey);
    adoptUrl(incoming.current, ownAcknowledgement && searchValue.current !== sent.current.q);
  }, [incomingKey, adoptUrl]);
  useEffect(() => {
    const onHistory = () => adoptUrl(parseLibraryQuery(new URLSearchParams(window.location.search)));
    window.addEventListener('popstate', onHistory);
    return () => window.removeEventListener('popstate', onHistory);
  }, [adoptUrl]);
  const publish = useCallback((patch: Partial<LibraryQuery>, replace: boolean) => {
    const next = { ...current.current, ...patch };
    current.current = next; searchValue.current = next.q; setQuery(next); setSearch(next.q);
    const key = serializeLibraryQuery(next);
    if (key === serializeLibraryQuery(sent.current)) return;
    sent.current = next; pending.current.push(key);
    callback.current(next, replace);
  }, []);
  useEffect(() => {
    if (search === sent.current.q) return;
    const timer = setTimeout(() => publish({ q: searchValue.current }, true), 300);
    return () => clearTimeout(timer);
  }, [search, publish]);
  useEffect(() => { let cancelled = false; tagApi.getTags().then(data => { if (!cancelled) setTags(data); }).catch(() => { if (!cancelled) setTagError(true); }); return () => { cancelled = true; }; }, []);
  useEffect(() => {
    const close = (event: PointerEvent) => {
      toolbar.current?.querySelectorAll('details[open]').forEach(detail => { if (!detail.contains(event.target as Node)) detail.removeAttribute('open'); });
    };
    document.addEventListener('pointerdown', close);
    return () => document.removeEventListener('pointerdown', close);
  }, []);
  const change = (patch: Partial<LibraryQuery>) => publish(patch, false);
  const toggleStatus = (status: ReadingStatus) => change({ status: current.current.status.includes(status) ? current.current.status.filter(s => s !== status) : [...current.current.status, status] });
  const toggleTag = (id: string) => change({ tags: current.current.tags.includes(id) ? current.current.tags.filter(t => t !== id) : [...current.current.tags, id] });
  const dateRange = (range: LibraryQuery['range']) => {
    if (range === 'custom' || !range) return change({ range, savedFrom: '', savedTo: '' });
    const end = new Date(); end.setHours(0, 0, 0, 0); end.setDate(end.getDate() + 1);
    const start = new Date(end);
    if (range === 'week') start.setDate(start.getDate() - 7);
    else {
      const day = start.getDate(); start.setDate(1); start.setMonth(start.getMonth() - 1);
      const lastDay = new Date(start.getFullYear(), start.getMonth() + 1, 0).getDate();
      start.setDate(Math.min(day, lastDay));
    }
    change({ range, savedFrom: start.toISOString(), savedTo: end.toISOString() });
  };
  const localDate = (value: string, end = false) => {
    if (!value) return '';
    const date = new Date(value); if (end) date.setDate(date.getDate() - 1);
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
  };
  const setDate = (field: 'savedFrom' | 'savedTo', value: string) => {
    const date = new Date(value + 'T00:00:00');
    if (field === 'savedTo') date.setDate(date.getDate() + 1);
    change({ [field]: value ? date.toISOString() : '', range: 'custom' });
  };
  const chips = [
    ...query.status.map(status => ({ label: statusLabels[status], remove: () => toggleStatus(status) })),
    ...query.tags.map(id => ({ label: `标签：${tags.find(tag => tag.id === id)?.name || id}`, remove: () => toggleTag(id) })),
    ...(query.q ? [{ label: `搜索：${query.q}`, remove: () => { setSearch(''); change({ q: '' }); } }] : []),
    ...(query.source || query.feedId ? [{ label: query.source === 'extension' ? '浏览器收藏' : 'RSS', remove: () => change({ source: '', feedId: '' }) }] : []),
    ...(query.savedFrom || query.savedTo ? [{ label: query.range === 'week' ? '最近一周' : query.range === 'month' ? '最近一个月' : `保存时间：${localDate(query.savedFrom) || '不限'} 至 ${localDate(query.savedTo, true) || '不限'}`, remove: () => change({ savedFrom: '', savedTo: '', range: '' }) }] : []),
  ];
  return <div ref={toolbar} className={styles.toolbar} onKeyDown={event => {
    if (event.key === 'Escape') { const detail = (event.target as HTMLElement).closest('details'); detail?.removeAttribute('open'); detail?.querySelector('summary')?.focus(); }
  }}>
    <div className={styles.top}>
      <form className={styles.search} role="search" onSubmit={event => { event.preventDefault(); publish({ q: searchValue.current }, true); }}>
        <label className="sr-only" htmlFor="library-search">搜索文章</label><Search size={18} aria-hidden="true" />
        <input id="library-search" type="search" maxLength={200} value={search} onChange={event => { const value = event.target.value; searchValue.current = value; current.current = { ...current.current, q: value }; setSearch(value); }} placeholder="搜索文章…" />
        {search && <button type="button" aria-label="清除搜索" onClick={() => { publish({ q: '' }, true); }}><X size={17} aria-hidden="true" /></button>}
      </form>
      {onSelect && <button type="button" onClick={onSelect} style={{ minHeight: 44, padding: "8px 12px" }}>选择</button>}
      <div className={styles.viewSwitch} role="group" aria-label="文章布局">
        <button type="button" onClick={() => onStyleChange('grid')} aria-label="卡片视图" title="卡片视图" aria-pressed={articleStyle === 'grid'}><LayoutGrid size={18} aria-hidden="true" /></button>
        <button type="button" onClick={() => onStyleChange('list')} aria-label="列表视图" title="列表视图" aria-pressed={articleStyle === 'list'}><List size={18} aria-hidden="true" /></button>
      </div>
    </div>
    <div className={styles.filters}>
      <details className={styles.dropdown}><summary>阅读状态{query.status.length ? ` · ${query.status.length}` : ''}</summary><fieldset className={styles.panel}><legend className="sr-only">阅读状态</legend>
        {(Object.keys(statusLabels) as ReadingStatus[]).map(status => <label key={status}><input type="checkbox" checked={query.status.includes(status)} onChange={() => toggleStatus(status)} />{statusLabels[status]}</label>)}
      </fieldset></details>
      <details className={styles.dropdown}><summary>标签{query.tags.length ? ` · ${query.tags.length}` : ''}</summary><div className={styles.panel}>
        <label htmlFor="tag-search">搜索标签</label><input id="tag-search" value={tagSearch} onChange={event => setTagSearch(event.target.value)} placeholder="标签名称" />
        {tagError && <p role="alert">标签加载失败</p>}
        <div className={styles.tagOptions}>{tags.filter(tag => tag.name.toLowerCase().includes(tagSearch.toLowerCase())).map(tag => <label key={tag.id}><input type="checkbox" checked={query.tags.includes(tag.id)} disabled={query.tags.length >= 10 && !query.tags.includes(tag.id)} onChange={() => toggleTag(tag.id)} />{tag.name}</label>)}</div>
        {!tagError && !tags.length && <p>暂无标签</p>}
      </div></details>
      <label className={styles.select}>来源<select value={query.source} onChange={event => change({ source: event.target.value as LibraryQuery['source'], feedId: '' })}><option value="">全部</option><option value="extension">浏览器收藏</option><option value="feed">RSS</option></select></label>
      {(query.source === 'feed' || query.view === 'feeds' || query.feedId) && <label className={styles.select}>订阅源<select aria-label="订阅源" value={query.feedId} onChange={event => change({ feedId: event.target.value })}><option value="">全部订阅</option>{query.feedId && !feeds.some(feed => feed.id === query.feedId) && <option value={query.feedId}>当前订阅</option>}{feeds.map(feed => <option key={feed.id} value={feed.id}>{feed.title}</option>)}</select></label>}
      <label className={styles.select}>保存时间<select value={query.range || (query.savedFrom || query.savedTo ? 'custom' : '')} onChange={event => dateRange(event.target.value as LibraryQuery['range'])}><option value="">不限</option><option value="week">最近一周</option><option value="month">最近一个月</option><option value="custom">自定义</option></select></label>
      <label className={styles.select}>排序<select value={query.sort} disabled={query.view === 'trash'} onChange={event => change({ sort: event.target.value as LibraryQuery['sort'] })}>{query.view === 'trash' ? <option value={query.sort}>最近删除</option> : <><option value="saved_desc">最近保存</option><option value="saved_asc">最早保存</option><option value="read_desc">最近阅读</option>{(query.view === 'feeds' || query.sort === 'published_desc') && <option value="published_desc">最新发布</option>}</>}</select></label>
    </div>
    {query.range === 'custom' && <div className={styles.dates}>
      <label>起始日期<input type="date" value={localDate(query.savedFrom)} max={localDate(query.savedTo, true) || undefined} onChange={event => setDate('savedFrom', event.target.value)} /></label>
      <span aria-hidden="true">—</span><label>截止日期<input type="date" value={localDate(query.savedTo, true)} min={localDate(query.savedFrom) || undefined} onChange={event => setDate('savedTo', event.target.value)} /></label>
    </div>}
    {!!chips.length && <div className={styles.chips} aria-label="当前筛选条件">{chips.map(chip => <button type="button" key={chip.label} onClick={chip.remove} aria-label={`移除${chip.label}`}><span>{chip.label}</span><X size={14} aria-hidden="true" /></button>)}<button type="button" className={styles.clear} onClick={() => { publish({ ...parseLibraryQuery(new URLSearchParams()), view: current.current.view, sort: current.current.sort }, false); }}>清除筛选</button></div>}
  </div>;
}
