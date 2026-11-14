import { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { Article } from '@/types';
import { articleApi } from '@/lib/api';
import { ConfirmProvider } from '@/contexts/ConfirmContext';
import ArticleCard from './ArticleCard';

vi.mock('@/lib/api', () => ({ articleApi: { updateState: vi.fn() } }));
vi.mock('../pages/article/[id]/components/TagManager', () => ({ default: () => null }));

let container: HTMLDivElement;
let root: Root;

beforeEach(() => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  vi.clearAllMocks();
  container = document.createElement('div');
  document.body.append(container);
  root = createRoot(container);
});

afterEach(async () => {
  await act(async () => root.unmount());
  container.remove();
});
const renderCard = async (article: Article | import('@/types').ArticleListItem, onDelete: (id: string) => Promise<void> = async () => {}) => {
  await act(async () => root.render(<ConfirmProvider><ArticleCard article={article} onDelete={onDelete} /></ConfirmProvider>));
};

it.each([
  { field: 'isFavorite' as const, action: '收藏', initial: false },
  { field: 'isFavorite' as const, action: '收藏', initial: true },
  { field: 'isArchived' as const, action: '归档', initial: false },
  { field: 'isArchived' as const, action: '归档', initial: true },
])('two $action clicks invert displayed state from $initial with unchanged props', async ({ field, initial }) => {
  const article: Article = { progress:null,savedFromExtension:false,feeds:[],
    id: 'article-id', url: 'https://example.com/article', title: 'Title', author: '',
    originalContent: '', readableContent: 'content', textContentLength: 7, summary: '',
    createdAt: '2026-11-08T00:00:00Z', imageUrl: '', tags: [], highlights: [],
    isFavorite: initial, isArchived: initial, readingStatus: 'unread', readAt: null,
    lastReadAt: null, progressTextOffset: null, progressBlockIndex: null, progressBlockRatio: null,
    progressPercent: null, progressUpdatedAt: null, progressSessionId: null, deletedAt: null,
    publishedAt: null, normalizedUrl: 'https://example.com/article', contentKind: 'full',
    savedFromExtensionAt: null,
  };
  vi.mocked(articleApi.updateState).mockImplementation(async (_, patch) => ({ ...article, ...patch, savedFromExtension: false, feeds: [], highlightCount: 0 } as import('@/types').ArticleListItem));
  await renderCard(article);
  const button = () => Array.from(container.querySelectorAll<HTMLButtonElement>('button[aria-pressed]'))[field === 'isFavorite' ? 0 : 1];

  await act(async () => button().click());
  expect(button().getAttribute('aria-pressed')).toBe(String(!initial));
  await act(async () => button().click());

  expect(vi.mocked(articleApi.updateState).mock.calls.map(([, patch]) => patch[field])).toEqual([!initial, initial]);
  expect(button().getAttribute('aria-pressed')).toBe(String(initial));
  expect(article[field]).toBe(initial);
});

it('marks an article read using returned list state without replacing its content', async () => {
  const article = { id: 'a', title: 'Title', url: 'https://example.com', createdAt: '2026-11-08T00:00:00Z', readingStatus: 'unread', tags: [], isArchived: false, isFavorite: false, contentKind: 'full', highlightCount: 2, feeds: [], savedFromExtension: true } as unknown as import('@/types').ArticleListItem;
  const updated = { ...article, readingStatus: 'read' as const, readAt: '2026-11-08T00:00:00Z' };
  vi.mocked(articleApi.updateState).mockResolvedValue(updated);
  const onUpdate = vi.fn();
  await act(async () => root.render(<ConfirmProvider><ArticleCard article={article} onDelete={async () => {}} onUpdate={onUpdate} /></ConfirmProvider>));
  const button = Array.from(container.querySelectorAll('button')).find(b => b.getAttribute('aria-label') === '标记已读')!;
  expect(button).toBeDefined();
  await act(async () => button.click());
  expect(articleApi.updateState).toHaveBeenCalledWith('a', { readingStatus: 'read' });
  expect(onUpdate).toHaveBeenCalledWith('a', updated);
  expect(container.textContent).toContain('已读');
});

it.each([
  { contentKind: 'link', showsBadge: true, url: 'https://www.example.com', source: 'example.com' },
  { contentKind: 'full', showsBadge: false, url: '', source: '浏览器收藏' },
] as const)('shows a 链接 badge only for $contentKind items', async ({ contentKind, showsBadge, url, source }) => {
  const article = {
    id: 'article-id', title: 'Title', url, createdAt: '2026-11-08T00:00:00Z',
    readingStatus: 'unread', tags: [], isArchived: false, isFavorite: false, contentKind,
    highlightCount: 0, feeds: [], savedFromExtension: true,
  } as unknown as import('@/types').ArticleListItem;
  await renderCard(article);
  expect(container.textContent?.includes('链接')).toBe(showsBadge);
  expect(container.textContent).toContain(source);
});

it.each([
  { publishedAt: '2026-11-08T12:00:00Z', expected: '发布时间：2026年11月8日' },
  { publishedAt: null, expected: null },
])('shows the publish date below the author when publishedAt is $publishedAt', async ({ publishedAt, expected }) => {
  const article = {
    id: 'a', title: 'Title', url: 'https://example.com', author: '阮一峰', publishedAt, createdAt: '2026-11-14T12:00:00Z',
    readingStatus: 'unread', tags: [], isArchived: false, isFavorite: false, contentKind: 'full',
    highlightCount: 0, feeds: [], savedFromExtension: true,
  } as unknown as import('@/types').ArticleListItem;
  await renderCard(article);
  const lines = Array.from(container.querySelectorAll('p')).map(p => p.textContent);
  const author = lines.indexOf('作者：阮一峰');
  expect(author).toBeGreaterThanOrEqual(0);
  if (expected) expect(lines[author + 1]).toBe(expected);
  else expect(container.textContent).not.toContain('发布时间');
});

it('requires confirmation before moving an article to trash', async () => {
  const article = { id: 'a', title: '待删除文章', url: 'https://example.com', createdAt: '2026-11-08T00:00:00Z', readingStatus: 'unread', tags: [], isArchived: false, isFavorite: false, contentKind: 'full', highlightCount: 0, feeds: [], savedFromExtension: true } as unknown as import('@/types').ArticleListItem;
  const onDelete = vi.fn(async () => {});
  await renderCard(article, onDelete);
  await act(async () => container.querySelector<HTMLButtonElement>('button[aria-label="删除文章"]')!.click());
  const dialog = document.body.querySelector('[role="dialog"]')!;
  expect(dialog.textContent).toContain('将这篇文章移入回收站？');
  expect(dialog.querySelector('h2')?.getAttribute('aria-label')).toBe('删除文章');
  expect(Array.from(dialog.querySelectorAll('button')).map(button => button.getAttribute('aria-label') || button.textContent?.trim())).toEqual(['关闭', '取消', '移入回收站']);
  await act(async () => Array.from(document.body.querySelectorAll('button')).find(button => button.textContent === '取消')!.click());
  expect(onDelete).not.toHaveBeenCalled();
});
