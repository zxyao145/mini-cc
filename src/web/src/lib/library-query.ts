import { ArticleListItem } from '@/types';

export type LibraryView = 'inbox' | 'all' | 'favorites' | 'archive' | 'trash' | 'feeds';
export type LibrarySort = 'saved_desc' | 'saved_asc' | 'read_desc' | 'published_desc';
export type ReadingStatus = 'unread' | 'reading' | 'read';
export interface LibraryQuery {
  view: LibraryView; q: string; status: ReadingStatus[]; tags: string[];
  source: '' | 'extension' | 'feed'; feedId: string;
  savedFrom: string; savedTo: string; range: '' | 'week' | 'month' | 'custom'; sort: LibrarySort;
}
const statuses: ReadingStatus[] = ['unread', 'reading', 'read'];
const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const date = (value: string | null) => value && Number.isFinite(Date.parse(value)) ? value : '';
export function parseLibraryQuery(params: URLSearchParams): LibraryQuery {
  const view = params.get('view'); const sort = params.get('sort'); const source = params.get('source'); const range = params.get('range');
  const feedId = params.get('feedId') || '';
  return {
    view: ['all', 'favorites', 'archive', 'trash', 'feeds'].includes(view || '') ? view as LibraryView : 'inbox',
    q: (params.get('q') || '').trim().slice(0, 200),
    status: statuses.filter(s => params.get('status')?.split(',').includes(s)),
    tags: Array.from(new Set((params.get('tags') || '').split(',').filter(id => guid.test(id)).map(id => id.toLowerCase()))).sort().slice(0, 10),
    source: source === 'extension' || source === 'feed' ? source : guid.test(feedId) ? 'feed' : '',
    feedId: guid.test(feedId) ? feedId : '', savedFrom: date(params.get('savedFrom')), savedTo: date(params.get('savedTo')),
    range: ['week', 'month', 'custom'].includes(range || '') ? range as LibraryQuery['range'] : '',
    sort: ['saved_asc', 'read_desc', 'published_desc'].includes(sort || '') ? sort as LibrarySort : 'saved_desc',
  };
}
export function serializeLibraryQuery(query: LibraryQuery): string {
  const params = new URLSearchParams();
  if (query.view !== 'inbox') params.set('view', query.view);
  if (query.q.trim()) params.set('q', query.q.trim().slice(0, 200));
  if (query.status.length) params.set('status', statuses.filter(s => query.status.includes(s)).join(','));
  if (query.tags.length) params.set('tags', Array.from(new Set(query.tags)).sort().join(','));
  if (query.source) params.set('source', query.source);
  if (query.feedId) params.set('feedId', query.feedId);
  if (query.savedFrom) params.set('savedFrom', query.savedFrom);
  if (query.savedTo) params.set('savedTo', query.savedTo);
  if (query.range) params.set('range', query.range);
  if (query.sort !== 'saved_desc') params.set('sort', query.sort);
  return params.toString();
}
export function toApiParams(query: LibraryQuery): URLSearchParams {
  const params = new URLSearchParams(serializeLibraryQuery(query)); params.delete('range'); return params;
}
export function isFiltered(query: LibraryQuery): boolean {
  return !!(query.q || query.status.length || query.tags.length || query.source || query.feedId || query.savedFrom || query.savedTo);
}
export function matchesView(item: ArticleListItem, query: LibraryQuery): boolean {
  if (query.view === 'trash' ? !item.deletedAt : !!item.deletedAt) return false;
  if (query.view === 'inbox' && item.isArchived || query.view === 'favorites' && !item.isFavorite || query.view === 'archive' && !item.isArchived) return false;
  if (query.status.length && !query.status.includes(item.readingStatus)) return false;
  if (query.tags.some(id => !item.tags.some(t => t.id === id))) return false;
  if (query.source === 'extension' && !item.savedFromExtension) return false;
  if ((query.source === 'feed' || query.view === 'feeds') && !item.feeds?.length) return false;
  if (query.feedId && !item.feeds?.some(feed => feed.id === query.feedId)) return false;
  if (query.savedFrom && Date.parse(item.createdAt) < Date.parse(query.savedFrom) || query.savedTo && Date.parse(item.createdAt) >= Date.parse(query.savedTo)) return false;
  return true; // Full-text membership remains authoritative on the server.
}
