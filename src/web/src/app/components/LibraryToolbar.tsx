'use client';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ChevronDown, LayoutGrid, List, ListChecks, Search, X } from 'lucide-react';
import { Button } from '@/components/arc/button/button';
import { Checkbox } from '@/components/arc/checkbox/checkbox';
import { Input } from '@/components/arc/input/input';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/arc/popover/popover';
import { Select } from '@/components/arc/select/select';
import { Tooltip } from '@/components/arc/tooltip/tooltip';
import { tagApi } from '@/lib/api';
import { useFeeds } from '@/contexts/FeedsContext';
import { LibraryQuery, parseLibraryQuery, ReadingStatus, serializeLibraryQuery } from '@/lib/library-query';
import { TagWithArticleCount } from '@/types';
import styles from './LibraryToolbar.module.css';

export const statusLabels = { unread: '未读', reading: '阅读中', read: '已读' };
interface Props {
  query: LibraryQuery;
  onChange: (query: LibraryQuery, replace: boolean) => void;
  articleStyle: string;
  onStyleChange: (style: 'grid' | 'list') => void;
  onSelect?: () => void;
}

export default function LibraryToolbar({ query: incomingQuery, onChange, articleStyle, onStyleChange, onSelect }: Props) {
  const { feeds } = useFeeds();
  const [query, setQuery] = useState(incomingQuery);
  const [search, setSearch] = useState(incomingQuery.q);
  const [tagSearch, setTagSearch] = useState('');
  const [tags, setTags] = useState<TagWithArticleCount[]>([]);
  const [tagError, setTagError] = useState(false);
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

  useEffect(() => {
    let cancelled = false;
    tagApi.getTags().then(data => { if (!cancelled) setTags(data); }).catch(() => { if (!cancelled) setTagError(true); });
    return () => { cancelled = true; };
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
    ...(query.contentKind ? [{ label: '内容类型：链接', remove: () => change({ contentKind: '' }) }] : []),
    ...(query.savedFrom || query.savedTo ? [{ label: query.range === 'week' ? '最近一周' : query.range === 'month' ? '最近一个月' : `保存时间：${localDate(query.savedFrom) || '不限'} 至 ${localDate(query.savedTo, true) || '不限'}`, remove: () => change({ savedFrom: '', savedTo: '', range: '' }) }] : []),
  ];
  const statusSummary = query.status.length ? query.status.map(status => statusLabels[status]).join('、') : '全部';
  const tagSummary = query.tags.length ? `${query.tags.length} 个标签` : '全部';
  const rangeValue = query.range || (query.savedFrom || query.savedTo ? 'custom' : '');

  return <div className={styles.toolbar}>
    <div className={styles.top}>
      <form className={styles.search} role="search" onSubmit={event => { event.preventDefault(); publish({ q: searchValue.current }, true); }}>
        <Input id="library-search" label="搜索文章" hideLabel type="search" maxLength={200} value={search} onChange={event => { const value = event.target.value; searchValue.current = value; current.current = { ...current.current, q: value }; setSearch(value); }} placeholder="搜索文章…" leading={<Search size={18} />} wrapperClassName={styles.searchField} className={styles.searchInput}
          trailing={search ? <Tooltip content="清除搜索"><Button variant="ghost" size="sm" iconOnly aria-label="清除搜索" onClick={() => publish({ q: '' }, true)}><X size={17} aria-hidden="true" /></Button></Tooltip> : undefined} />
      </form>
      <div className={styles.topActions}>
        {onSelect && <Button variant="secondary" size="sm" className={styles.selectMode} onClick={onSelect}><ListChecks size={16} aria-hidden="true" />选择</Button>}
        <div className={styles.viewSwitch} role="group" aria-label="文章布局">
          <Tooltip content="卡片视图"><Button variant="ghost" size="sm" iconOnly aria-label="卡片视图" onClick={() => onStyleChange('grid')} aria-pressed={articleStyle === 'grid'} className={styles.viewButton}><LayoutGrid size={17} aria-hidden="true" /></Button></Tooltip>
          <Tooltip content="列表视图"><Button variant="ghost" size="sm" iconOnly aria-label="列表视图" onClick={() => onStyleChange('list')} aria-pressed={articleStyle === 'list'} className={styles.viewButton}><List size={17} aria-hidden="true" /></Button></Tooltip>
        </div>
      </div>
    </div>

    <div className={styles.filters}>
      <Popover>
        <PopoverTrigger asChild><Button variant="secondary" size="sm" className={styles.filterTrigger} aria-label="阅读状态" data-active={!!query.status.length || undefined}><span>阅读状态</span><span className={styles.filterValue}>{statusSummary}</span><ChevronDown size={14} aria-hidden="true" /></Button></PopoverTrigger>
        <PopoverContent className={styles.filterPanel} aria-label="阅读状态">
          {(Object.keys(statusLabels) as ReadingStatus[]).map(status => <Checkbox key={status} compact label={statusLabels[status]} checked={query.status.includes(status)} onCheckedChange={() => toggleStatus(status)} />)}
        </PopoverContent>
      </Popover>
      <Popover>
        <PopoverTrigger asChild><Button variant="secondary" size="sm" className={styles.filterTrigger} aria-label="标签" data-active={!!query.tags.length || undefined}><span>标签</span><span className={styles.filterValue}>{tagSummary}</span><ChevronDown size={14} aria-hidden="true" /></Button></PopoverTrigger>
        <PopoverContent className={`${styles.filterPanel} ${styles.tagPanel}`} aria-label="标签">
          <Input label="搜索标签" hideLabel value={tagSearch} onChange={event => setTagSearch(event.target.value)} placeholder="搜索标签" wrapperClassName={styles.tagSearch} leading={<Search size={15} />} />
          {tagError && <p role="alert" className={styles.filterMessage}>标签加载失败</p>}
          <div className={styles.tagOptions}>
            {tags.filter(tag => tag.name.toLowerCase().includes(tagSearch.toLowerCase())).map(tag => <Checkbox key={tag.id} compact label={tag.name} checked={query.tags.includes(tag.id)} disabled={query.tags.length >= 10 && !query.tags.includes(tag.id)} onCheckedChange={() => toggleTag(tag.id)} />)}
            {!tagError && !tags.length && <p className={styles.filterMessage}>暂无标签</p>}
          </div>
        </PopoverContent>
      </Popover>
      <Select label="来源" hideLabel variant="chip" value={query.source} options={[
        { value: '', label: '全部' }, { value: 'extension', label: '浏览器收藏' }, { value: 'feed', label: 'RSS' },
      ]} onValueChange={source => change({ source: source as LibraryQuery['source'], feedId: '' })} />
      <Select label="内容类型" hideLabel variant="chip" value={query.contentKind} options={[
        { value: '', label: '全部' }, { value: 'link', label: '链接' },
      ]} onValueChange={contentKind => change({ contentKind: contentKind as LibraryQuery['contentKind'] })} />
      {(query.source === 'feed' || query.view === 'feeds' || query.feedId) && <Select label="订阅源" hideLabel variant="chip" value={query.feedId} options={[
        { value: '', label: '全部订阅' }, ...(query.feedId && !feeds.some(feed => feed.id === query.feedId) ? [{ value: query.feedId, label: '当前订阅' }] : []), ...feeds.map(feed => ({ value: feed.id, label: feed.title })),
      ]} onValueChange={feedId => change({ feedId })} />}
      <Select label="保存时间" hideLabel variant="chip" value={rangeValue} options={[
        { value: '', label: '不限' }, { value: 'week', label: '最近一周' }, { value: 'month', label: '最近一个月' }, { value: 'custom', label: '自定义' },
      ]} onValueChange={range => dateRange(range as LibraryQuery['range'])} />
      <Select label="排序" hideLabel variant="chip" value={query.sort} disabled={query.view === 'trash'} options={query.view === 'trash'
        ? [{ value: query.sort, label: '最近删除' }]
        : [{ value: 'saved_desc', label: '最近保存' }, { value: 'saved_asc', label: '最早保存' }, { value: 'read_desc', label: '最近阅读' }, ...((query.view === 'feeds' || query.sort === 'published_desc') ? [{ value: 'published_desc', label: '最新发布' }] : [])]}
        onValueChange={sort => change({ sort: sort as LibraryQuery['sort'] })} />
    </div>
    {query.range === 'custom' && <div className={styles.dates}>
      <Input label="起始日期" type="date" value={localDate(query.savedFrom)} max={localDate(query.savedTo, true) || undefined} onChange={event => setDate('savedFrom', event.target.value)} wrapperClassName={styles.dateInput} />
      <span aria-hidden="true">—</span>
      <Input label="截止日期" type="date" value={localDate(query.savedTo, true)} min={localDate(query.savedFrom) || undefined} onChange={event => setDate('savedTo', event.target.value)} wrapperClassName={styles.dateInput} />
    </div>}
    {!!chips.length && <div className={styles.chips} aria-label="当前筛选条件">
      {chips.map(chip => <Button variant="ghost" size="sm" className={styles.chip} key={chip.label} onClick={chip.remove} aria-label={`移除${chip.label}`}><span>{chip.label}</span><X size={14} aria-hidden="true" /></Button>)}
      <Button variant="ghost" size="sm" className={styles.clear} onClick={() => { publish({ ...parseLibraryQuery(new URLSearchParams('status=')), view: current.current.view, sort: current.current.sort }, false); }}>清除筛选<X size={14} aria-hidden="true" /></Button>
    </div>}
  </div>;
}
