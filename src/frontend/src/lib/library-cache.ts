import { ArticleListItem } from '@/types';
import { LibraryQuery, matchesView, parseLibraryQuery } from './library-query';

export interface LibraryCacheEntry {
  items: ArticleListItem[]; total: number; nextCursor: string | null;
  scrollY: number; savedAt: number; firstPageIds: string[]; needsReconcile?: boolean;
}
const cache = new Map<string, LibraryCacheEntry>();
const storageKey = 'minicc.library.cache.v1';
export const currentLibraryKey = 'minicc.library.currentQuery';
function persist() {
  try { sessionStorage.setItem(storageKey, JSON.stringify(Array.from(cache))); } catch { /* Storage may be unavailable or full. */ }
}
function hydrate() {
  if (!cache.size) {
    try {
      const entries = JSON.parse(sessionStorage.getItem(storageKey) || '[]') as [string, LibraryCacheEntry][];
      if (Array.isArray(entries)) for (const [k, value] of entries.slice(-5)) {
        if (value && Array.isArray(value.items) && Array.isArray(value.firstPageIds) && Number.isFinite(value.savedAt)) cache.set(k, value);
      }
    } catch { /* A corrupted session cache is discarded. */ }
  }
}
export function readLibraryCache(key: string): LibraryCacheEntry | null {
  hydrate();
  const value = cache.get(key);
  if (!value || Date.now() - value.savedAt > 600_000) { cache.delete(key); return null; }
  return value;
}
export function writeLibraryCache(key: string, entry: LibraryCacheEntry) {
  cache.delete(key); cache.set(key, entry);
  while (cache.size > 5) cache.delete(cache.keys().next().value!);
  try { sessionStorage.setItem(currentLibraryKey, key); } catch { /* Optional restoration. */ }
  persist();
}
export function patchLibraryEntry(entry: LibraryCacheEntry, query: LibraryQuery, id: string, changes: Partial<ArticleListItem>): LibraryCacheEntry {
  const sortKey = query.view === 'trash' ? 'deletedAt' : query.sort === 'read_desc' ? 'lastReadAt' : query.sort === 'published_desc' ? 'publishedAt' : 'createdAt';
  let changedSort = false;
  const items = entry.items.flatMap(item => {
    if (item.id !== id) return [item];
    changedSort = Object.prototype.hasOwnProperty.call(changes, sortKey) && item[sortKey] !== changes[sortKey];
    const updated = { ...item, ...changes };
    return matchesView(updated, query) ? [updated] : [];
  });
  return { ...entry, items, total: Math.max(0, entry.total - (entry.items.length - items.length)),
    needsReconcile: entry.needsReconcile || changedSort || items.length !== entry.items.length };
}
// Reader/P2 can patch existing cached list rows without replacing detail content.
export function updateCachedArticle(id: string, changes: Partial<ArticleListItem>) {
  hydrate();
  for (const [key, entry] of Array.from(cache)) {
    cache.set(key, patchLibraryEntry(entry, parseLibraryQuery(new URLSearchParams(key)), id, changes));
  }
  persist();
}
export function invalidateLibraryCaches() {
  hydrate();
  for (const [key, entry] of Array.from(cache)) cache.set(key, { ...entry, needsReconcile: true });
  persist();
}
export function clearLibraryCache() {
  cache.clear();
  try { sessionStorage.removeItem(storageKey); sessionStorage.removeItem(currentLibraryKey); } catch { /* Optional cache. */ }
}
