import { describe, expect, it, vi } from 'vitest';
import { isFiltered, parseLibraryQuery, serializeLibraryQuery, toApiParams, matchesView } from './library-query';
import { ArticleListItem } from '@/types';

const tag = '00000000-0000-0000-0000-000000000001';
describe('library URLs', () => {
  it('omits defaults and keeps view separate from filters', () => {
    const query = parseLibraryQuery(new URLSearchParams());
    expect(query.view).toBe('inbox');
    expect(serializeLibraryQuery(query)).toBe('');
    expect(isFiltered({ ...query, view: 'favorites' })).toBe(false);
  });
  it('round trips combined filters and excludes presentation range from API', () => {
    const query = parseLibraryQuery(new URLSearchParams(`view=all&status=read,unread,read&tags=${tag}&q=缓存&source=extension&savedFrom=2026-11-01T00:00:00Z&range=week&sort=read_desc`));
    expect(parseLibraryQuery(new URLSearchParams(serializeLibraryQuery(query)))).toEqual(query);
    expect(query.status).toEqual(['unread', 'read']);
    expect(toApiParams(query).get('range')).toBeNull();
    expect(toApiParams(query).get('q')).toBe('缓存');
    expect(isFiltered(query)).toBe(true);
  });
  it('normalizes invalid input, sorts/deduplicates tags and limits search', () => {
    const query = parseLibraryQuery(new URLSearchParams(`view=unknown&status=invalid&tags=bad,${tag},${tag}&sort=bad&source=bad&q=${'a'.repeat(220)}&savedTo=bad`));
    expect(query.view).toBe('inbox');
    expect(query.status).toEqual([]);
    expect(query.tags).toEqual([tag]);
    expect(query.sort).toBe('saved_desc');
    expect(query.source).toBe('');
    expect(query.savedTo).toBe('');
    expect(query.q).toHaveLength(200);
  });
  it('local membership composes view and filters for state/tag updates', () => {
    const item = { isArchived: false, isFavorite: true, deletedAt: null, readingStatus: 'unread', tags: [{ id: tag }], savedFromExtension: true } as ArticleListItem;
    const query = parseLibraryQuery(new URLSearchParams(`status=unread&tags=${tag}`));
    expect(matchesView(item, query)).toBe(true);
    expect(matchesView({ ...item, isArchived: true }, query)).toBe(false);
    expect(matchesView({ ...item, readingStatus: 'read' }, query)).toBe(false);
    expect(matchesView({ ...item, tags: [] }, query)).toBe(false);
    expect(matchesView({ ...item, deletedAt: '2026-01-01' }, query)).toBe(false);
  });
});

describe('reader cache updates', () => {
  it('patches cached metadata and removes rows that no longer match their query', async () => {
    const { clearLibraryCache, writeLibraryCache, readLibraryCache, updateCachedArticle } = await import('./library-cache');
    clearLibraryCache();
    const item = { id: 'a', title: 'Old', isArchived: false, deletedAt: null, readingStatus: 'unread', tags: [] } as unknown as ArticleListItem;
    const entry = { items: [item], total: 1, nextCursor: null, scrollY: 500, savedAt: Date.now(), firstPageIds: ['a'] };
    writeLibraryCache('', entry); writeLibraryCache('view=all', entry);
    updateCachedArticle('a', { isArchived: true, title: 'Updated' });
    expect(readLibraryCache('')?.items).toEqual([]);
    expect(readLibraryCache('')?.total).toBe(0);
    expect(readLibraryCache('view=all')?.items[0].title).toBe('Updated');
    expect(readLibraryCache('view=all')?.scrollY).toBe(500);
    expect(JSON.parse(sessionStorage.getItem('minicc.library.cache.v1') || '[]')).toHaveLength(2);
    clearLibraryCache();
  });
});

it('applies reader patches after a document reload hydrates the session cache', async () => {
  const original = await import('./library-cache');
  original.clearLibraryCache();
  original.writeLibraryCache('view=all', { items: [{ id: 'a', title: 'Old', deletedAt: null, tags: [], readingStatus: 'unread' } as unknown as ArticleListItem], total: 1, nextCursor: null, scrollY: 300, savedAt: Date.now(), firstPageIds: ['a'] });
  vi.resetModules();
  const fresh = await import('./library-cache');
  fresh.updateCachedArticle('a', { title: 'New' });
  expect(fresh.readLibraryCache('view=all')?.items[0].title).toBe('New');
  fresh.clearLibraryCache();
});

it('keeps explicit extension source with a feed ID as an AND filter', () => {
  const query = parseLibraryQuery(new URLSearchParams(`view=feeds&source=extension&feedId=${tag}`));
  expect(query.source).toBe('extension');
  expect(toApiParams(query).get('source')).toBe('extension');
  const shared = { deletedAt: null, tags: [], readingStatus: 'unread', savedFromExtension: true, feeds: [{ id: tag, title: 'Feed' }] } as unknown as ArticleListItem;
  expect(matchesView(shared, query)).toBe(true);
  expect(matchesView({ ...shared, savedFromExtension: false }, query)).toBe(false);
  expect(matchesView({ ...shared, feeds: [] }, query)).toBe(false);
});
