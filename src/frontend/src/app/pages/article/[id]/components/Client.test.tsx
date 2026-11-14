import { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { articleApi } from '@/lib/api';
import { Article, ArticleListItem, Highlight, Tag } from '@/types';
import { clearLibraryCache, readLibraryCache, writeLibraryCache } from '@/lib/library-cache';
import { parseLibraryQuery } from '@/lib/library-query';
import { useArticleList } from '@/hooks/useArticleList';
import Client from './Client';
import { defaultReaderPreferences } from '@/lib/reader-preferences';
vi.mock('@/contexts/ReaderPreferencesContext',()=>({useReaderPreferences:()=>({preferences:defaultReaderPreferences,beforeChange:()=>()=>{},update:vi.fn(),retry:vi.fn(),unsynced:false})}));
vi.mock('next/navigation',()=>({useRouter:()=>({back:vi.fn(),push:vi.fn(),replace:vi.fn()})}));
vi.mock('@/lib/api', () => ({ articleApi: { getArticleById: vi.fn(), getArticles: vi.fn(), updateState:vi.fn(), resetProgress:vi.fn(), saveProgress:vi.fn() } }));
vi.mock('./TagManager', () => ({ default: ({ onTagsChange }: { onTagsChange: (tags: Tag[]) => void }) => <button onClick={() => onTagsChange([])}>Remove reader tag</button> }));
vi.mock('./HighlightManager', () => ({ default: () => null }));
vi.mock('./HighlightList', () => ({ default: ({ highlights, onHighlightsChange }: { highlights: Highlight[]; onHighlightsChange: (items: Highlight[]) => void }) => <><span>{highlights.map(h => h.note).join(',')}</span><button onClick={() => onHighlightsChange(highlights.slice(1))}>Remove reader highlight</button></> }));
const tag = { id: '00000000-0000-0000-0000-000000000001', name: 'Selected', color: '#112233', createdAt: '2026-11-08T00:00:00Z' };
const filteredKey = `view=all&tags=${tag.id}`;
let root: Root, container: HTMLDivElement;
beforeEach(() => { Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true }); clearLibraryCache(); vi.resetAllMocks(); vi.spyOn(window, 'scrollTo').mockImplementation(() => {}); container = document.createElement('div'); document.body.append(container); root = createRoot(container); });
afterEach(async () => { await act(async () => root.unmount()); container.remove(); vi.restoreAllMocks(); });
it('patches reader tags/highlight counts, preserves detail content, and reconciles a filtered cached tail on return', async () => {
  const rows = Array.from({ length: 65 }, (_, index) => ({ id: `a${index + 1}`, title: 'Title', isArchived: false, deletedAt: null, readingStatus: 'unread', tags: [tag], highlightCount: index === 24 ? 2 : 0 } as ArticleListItem));
  const entry = { items: rows.slice(0, 40), total: 65, nextCursor: 'after:a40', scrollY: 640, savedAt: Date.now(), firstPageIds: rows.slice(0, 20).map(a => a.id) };
  writeLibraryCache(filteredKey, entry); writeLibraryCache('view=all', entry);
  const highlights = [{ id: 'h1', note: 'first note' }, { id: 'h2', note: 'retained note' }] as Highlight[];
  vi.mocked(articleApi.getArticleById).mockResolvedValue({ ...rows[24], progress:null, originalContent: 'Retained original body', textContentLength: 20, progressTextOffset: null, progressBlockIndex: null, progressBlockRatio: null, progressUpdatedAt: null, progressSessionId: null, normalizedUrl: 'https://example.com/a25', savedFromExtensionAt: null, readableContent: '<p>Retained reader body</p>', highlights } as Article);
  await act(async () => root.render(<Client id="a25" />));
  await act(async () => (Array.from(container.querySelectorAll('button')).find(b => b.textContent === 'Remove reader tag')!).click());
  expect(readLibraryCache(filteredKey)?.items.some(a => a.id === 'a25')).toBe(false);
  expect(readLibraryCache(filteredKey)?.total).toBe(64);
  expect(container.textContent).toContain('Retained reader body');
  expect(container.textContent).toContain('first note,retained note');
  await act(async () => (Array.from(container.querySelectorAll('button')).find(b => b.textContent === 'Remove reader highlight')!).click());
  expect(readLibraryCache('view=all')?.items.find(a => a.id === 'a25')?.highlightCount).toBe(1);
  expect(container.textContent).toContain('Retained reader body');
  expect(container.textContent).toContain('retained note');
  const remaining = rows.filter(a => a.id !== 'a25');
  vi.mocked(articleApi.getArticles).mockImplementation(async params => {
    const after = params?.get('cursor')?.replace('after:', '');
    const start = after ? remaining.findIndex(a => a.id === after) + 1 : 0;
    return { items: remaining.slice(start, start + 20), total: 64, nextCursor: `after:${remaining[start + 19].id}` };
  });
  let list!: ReturnType<typeof useArticleList>;
  function ReturnedList() { list = useArticleList(parseLibraryQuery(new URLSearchParams(filteredKey))); return null; }
  await act(async () => root.render(<ReturnedList />));
  expect(list.items).toHaveLength(40);
  expect(list.items.some(a => a.id === 'a25')).toBe(false);
  expect(list.items.at(-1)?.id).toBe('a41');
  expect(list.total).toBe(64);
});
it('exposes reader read/favorite/archive/reset actions and merges acknowledged rows without dropping body or notes', async () => {
  const details = {progress:null,savedFromExtension:false,feeds:[],author:'',originalContent:'',textContentLength:14,summary:'',createdAt:'',lastReadAt:null,progressTextOffset:null,progressBlockRatio:null,progressPercent:null,progressSessionId:null,deletedAt:null,publishedAt:null,normalizedUrl:'https://example.com',contentKind:'full',savedFromExtensionAt:null,imageUrl:'',id:'reader',title:'Reader actions',url:'https://example.com',readableContent:'<p>Preserved body</p>',highlights:[{id:'h',articleId:'reader',text:'body',color:'#ffff00',startOffset:0,endOffset:4,createdAt:'',note:'Preserved note'}],tags:[],readingStatus:'unread',isArchived:false,isFavorite:false,progressBlockIndex:null,progressUpdatedAt:null} as Article;
  vi.mocked(articleApi.getArticleById).mockResolvedValue(details);
  vi.mocked(articleApi.updateState).mockResolvedValue({...details,readingStatus:'read',highlightCount:1,savedFromExtension:false,feeds:[]} as ArticleListItem);
  vi.mocked(articleApi.resetProgress).mockResolvedValue({progressUpdatedAt:'v1',readingStatus:'read',lastReadAt:null});
  await act(async()=>root.render(<Client id="reader"/>));
  await act(async()=>container.querySelector<HTMLButtonElement>('button[aria-label="标记已读"]')!.click());
  expect(articleApi.updateState).toHaveBeenCalledWith('reader',{readingStatus:'read'});
  expect(container.textContent).toContain('Preserved body');expect(container.textContent).toContain('Preserved note');
  expect(container.querySelector('button[aria-label="标记未读"]')).not.toBeNull();
  expect(container.querySelector('button[aria-label="归档文章"]')).not.toBeNull();
  await act(async()=>container.querySelector<HTMLButtonElement>('button[aria-label="从头阅读"]')!.click());
  expect(articleApi.resetProgress).toHaveBeenCalledWith('reader',expect.any(String));
  expect(container.querySelector('button[aria-label="标记未读"]')).not.toBeNull();
});

it('keeps summary body/kind coherent when a state response reports completion and pauses reset', async () => {
  const summary = { id:'summary-metadata', title:'Summary', url:'https://example.com/summary', readableContent:'<p>Old summary body</p>', contentKind:'summaryOnly', tags:[], feeds:[], highlights:[], readingStatus:'unread', progressBlockIndex:null, progressUpdatedAt:null, isFavorite:false, isArchived:false } as unknown as Article;
  vi.mocked(articleApi.getArticleById).mockResolvedValue(summary);
  vi.mocked(articleApi.updateState).mockResolvedValue({ id:summary.id, contentKind:'full', isFavorite:true, isArchived:false, readingStatus:'unread', tags:[], feeds:[], highlightCount:0 } as unknown as ArticleListItem);
  await act(async () => root.render(<Client id={summary.id}/>));
  await act(async () => container.querySelector<HTMLButtonElement>('button[aria-label="加入收藏"]')!.click());
  expect(container.querySelector('.readable-content')?.textContent).toBe('Old summary body');
  expect(container.querySelector('.reader-summary-banner')).not.toBeNull();
  expect(container.textContent).toContain('正文已补全，请刷新后继续阅读');
  await act(async () => container.querySelector<HTMLButtonElement>('button[aria-label="从头阅读"]')!.click());
  expect(articleApi.resetProgress).not.toHaveBeenCalled();
  expect(container.textContent).not.toContain('留在这里');
});

it('pauses unseen summary replacement before adopting a full-body reset acknowledgement', async () => {
  const summary = { id:'summary-reset', title:'Summary', url:'https://example.com/summary', readableContent:'<p>Old summary body</p>', contentKind:'summaryOnly', tags:[], feeds:[], highlights:[], readingStatus:'unread', progressBlockIndex:null, progressUpdatedAt:null } as unknown as Article;
  vi.mocked(articleApi.getArticleById).mockResolvedValue(summary);
  vi.mocked(articleApi.resetProgress).mockResolvedValue({ progressUpdatedAt:'2026-11-08T03:00:00Z', readingStatus:'unread', lastReadAt:null, contentKind:'full' });
  await act(async () => root.render(<Client id={summary.id}/>));
  await act(async () => container.querySelector<HTMLButtonElement>('button[aria-label="从头阅读"]')!.click());
  expect(container.textContent).toContain('正文已补全，请刷新后继续阅读');
  expect(container.querySelector('.readable-content')?.textContent).toBe('Old summary body');
  expect(container.querySelector('.reader-summary-banner')).not.toBeNull();
});

it('renders a link without reader chrome or progress traffic when a state update reports full', async () => {
  const link = {
    id: 'link', title: 'Video title', url: 'https://www.youtube.com/watch?v=abc', author: '',
    readableContent: '', summary: 'Video description', imageUrl: 'https://img.example/cover.jpg',
    createdAt: '2026-11-13T00:00:00Z', contentKind: 'link', tags: [], feeds: [], highlights: [],
    readingStatus: 'unread', isArchived: false, isFavorite: false,
  } as unknown as Article;
  const updated = {
    id: 'link', title: 'Video title', url: link.url, readingStatus: 'read', contentKind: 'full',
    isFavorite: false, isArchived: false, tags: [], feeds: [], highlightCount: 0, savedFromExtension: true,
  } as unknown as ArticleListItem;
  vi.mocked(articleApi.getArticleById).mockResolvedValue(link);
  vi.mocked(articleApi.updateState).mockResolvedValue(updated);

  await act(async () => root.render(<Client id="link" />));
  expect(articleApi.updateState).not.toHaveBeenCalled();
  await act(async () => container.querySelector<HTMLButtonElement>('button[aria-label="标记已读"]')!.click());

  expect(articleApi.updateState).toHaveBeenCalledWith('link', { readingStatus: 'read' });
  expect(container.querySelector('button[aria-label="标记未读"]')).not.toBeNull();
  expect(container.querySelector('[role="progressbar"]')).toBeNull();
  expect(container.querySelector('button[aria-label="从头阅读"]')).toBeNull();
  expect(container.querySelector('button[aria-label="阅读设置"]')).toBeNull();
  expect(container.querySelector('button[aria-label="目录"]')).toBeNull();
  expect(container.querySelector('.reader-body-row')).toBeNull();
  expect(container.querySelector('.readable-content')).toBeNull();
  expect(container.textContent).not.toContain('Remove reader highlight');
  const visit = container.querySelector<HTMLAnchorElement>('a.visit-original-btn.primary')!;
  expect(visit.href).toBe(link.url);
  expect(visit.target).toBe('_blank');
  expect(visit.rel).toBe('noopener noreferrer');
  expect(container.textContent).toContain('Video description');
  expect(container.textContent).toContain('youtube.com');
  expect(container.textContent).toContain('保存于');
  expect(container.querySelector('img[src="https://img.example/cover.jpg"]')).not.toBeNull();

  document.dispatchEvent(new Event('visibilitychange'));
  await act(async () => { await Promise.resolve(); });
  expect(articleApi.getArticleById).toHaveBeenCalledTimes(1);
  expect(articleApi.saveProgress).not.toHaveBeenCalled();
  expect(articleApi.resetProgress).not.toHaveBeenCalled();
});
