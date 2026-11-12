'use client';
import { useCallback, useLayoutEffect, useRef, useState } from 'react';
import { ArticleListItem, ArticleListPage } from '@/types';
import { articleApi } from '@/lib/api';
import { LibraryQuery, parseLibraryQuery, serializeLibraryQuery, toApiParams } from '@/lib/library-query';
import { LibraryCacheEntry, patchLibraryEntry, readLibraryCache, writeLibraryCache, updateCachedArticle } from '@/lib/library-cache';

const unique = (items: ArticleListItem[]) => Array.from(new Map(items.map(item => [item.id, item])).values());
interface ListSession {
  key: string; controller: AbortController; requests: Set<AbortController>; revision: number;
  more: boolean; refresh: Promise<void> | null; needsRefresh: boolean;
}
async function fetchPage(session: ListSession, params: URLSearchParams): Promise<ArticleListPage> {
  const controller = new AbortController();
  const abort = () => controller.abort();
  session.controller.signal.addEventListener('abort', abort, { once: true });
  session.requests.add(controller);
  try { return await articleApi.getArticles(params, controller.signal); }
  finally { session.requests.delete(controller); session.controller.signal.removeEventListener('abort', abort); }
}
export function useArticleList(query: LibraryQuery) {
  const key = serializeLibraryQuery(query);
  const [state, setState] = useState<LibraryCacheEntry>({ items: [], total: 0, nextCursor: null, scrollY: 0, savedAt: 0, firstPageIds: [] });
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const active = useRef<ListSession | null>(null);
  const snapshot = useRef(state);
  const commit = useCallback((next: LibraryCacheEntry) => { snapshot.current = next; setState(next); }, []);
  const isActive = useCallback((session: ListSession, revision = session.revision) =>
    active.current === session && !session.controller.signal.aborted && revision === session.revision, []);

  const refresh = useCallback((session: ListSession, cached: LibraryCacheEntry | null) => {
    const revision = session.revision;
    session.needsRefresh = true;
    const task = (async () => {
      try {
        const params = toApiParams(parseLibraryQuery(new URLSearchParams(session.key)));
        const first = await fetchPage(session, params);
        if (!isActive(session, revision)) return;
        const ids = first.items.map(item => item.id);
        const stable = cached && first.total === cached.total && ids.length === cached.firstPageIds.length && ids.every((id, index) => id === cached.firstPageIds[index]);
        const reconcile = cached && (cached.needsReconcile || !stable);
        let items = first.items;
        let nextCursor = first.nextCursor;
        // A missing first-page ID may have moved to page 2. Rebuild the loaded
        // interval when membership changed, then retain its matching tail cursor.
        if (reconcile) {
          while (items.length < cached.items.length && nextCursor) {
            params.set('cursor', nextCursor);
            const page = await fetchPage(session, params);
            if (!isActive(session, revision)) return;
            items = unique([...items, ...page.items]); nextCursor = page.nextCursor;
          }
        } else if (cached) {
          const tail = cached.items.filter(item => !cached.firstPageIds.includes(item.id));
          items = unique([...first.items, ...tail]);
          if (tail.length) nextCursor = cached.nextCursor;
        }
        if (!isActive(session, revision)) return;
        commit({ ...snapshot.current, items, total: first.total, nextCursor, firstPageIds: ids, needsReconcile: false, savedAt: Date.now() });
        session.needsRefresh = false; setError(null);
      } catch { if (isActive(session, revision)) setError('加载失败，重试'); }
      finally { if (isActive(session, revision)) setLoading(false); }
    })();
    session.refresh = task;
    void task.then(() => { if (session.refresh === task) session.refresh = null; });
    return task;
  }, [commit, isActive]);

  // Save before list DOM removal; flush restored rows before the restoration frame.
  useLayoutEffect(() => {
    const session: ListSession = { key, controller: new AbortController(), requests: new Set(), revision: 0, more: false, refresh: null, needsRefresh: false };
    active.current = session;
    const cached = readLibraryCache(key);
    commit(cached || { items: [], total: 0, nextCursor: null, scrollY: 0, savedAt: 0, firstPageIds: [] });
    setLoading(!cached); setLoadingMore(false); setError(null);
    const frame = cached ? requestAnimationFrame(() => window.scrollTo(0, cached.scrollY)) : null;
    void refresh(session, cached);
    let scrollY = cached?.scrollY ?? window.scrollY;
    const rememberScroll = () => { scrollY = window.scrollY; };
    window.addEventListener('scroll', rememberScroll, { passive: true });
    const save = () => {
      if (snapshot.current.savedAt) writeLibraryCache(key, { ...snapshot.current, scrollY, savedAt: Date.now() });
    };
    const mutation = (event: Event) => {
      const detail = (event as CustomEvent<import('@/lib/library-mutations').LibraryMutation>).detail || {};
      let next = snapshot.current;
      for (const id of detail.ids || []) {
        if (detail.remove) {
          const items = next.items.filter(item => item.id !== id);
          next = { ...next, items, total: Math.max(0, next.total - (next.items.length - items.length)) };
        } else if (detail.changes) next = patchLibraryEntry(next, parseLibraryQuery(new URLSearchParams(session.key)), id, detail.changes);
      }
      next = { ...next, needsReconcile: true };
      commit(next); session.revision++; session.requests.forEach(controller => controller.abort());
      void refresh(session, next);
    };
    window.addEventListener('minicc:library-mutation', mutation);
    window.addEventListener('pagehide', save);
    return () => { window.removeEventListener('minicc:library-mutation', mutation); save(); session.controller.abort(); if (frame !== null) cancelAnimationFrame(frame); window.removeEventListener('pagehide', save); window.removeEventListener('scroll', rememberScroll); };
  }, [key, commit, refresh]);

  const loadMore = useCallback(async () => {
    const session = active.current;
    if (!session || session.more || !isActive(session)) return;
    session.more = true; setLoadingMore(true); setError(null);
    const wasNextPage = !!snapshot.current.nextCursor;
    try {
      if (session.needsRefresh && !session.refresh) { await refresh(session, snapshot.current); return; }
      while (isActive(session)) {
        if (session.refresh) { await session.refresh; if (session.needsRefresh) return; continue; }
        if (session.needsRefresh || wasNextPage && !snapshot.current.nextCursor) return;
        const revision = session.revision;
        const cursor = snapshot.current.nextCursor;
        const params = toApiParams(parseLibraryQuery(new URLSearchParams(session.key)));
        if (cursor) params.set('cursor', cursor);
        try {
          const page = await fetchPage(session, params);
          if (!isActive(session)) return;
          if (!isActive(session, revision)) continue;
          commit({ ...snapshot.current, items: unique(cursor ? [...snapshot.current.items, ...page.items] : page.items), total: page.total,
            nextCursor: page.nextCursor, firstPageIds: cursor ? snapshot.current.firstPageIds : page.items.map(item => item.id), savedAt: Date.now() });
          return;
        } catch { if (!isActive(session)) return; if (!isActive(session, revision)) continue; setError('加载失败，重试'); return; }
      }
    } finally { session.more = false; if (isActive(session)) setLoadingMore(false); }
  }, [commit, isActive, refresh]);

  const reconcileMutation = useCallback((next: LibraryCacheEntry) => {
    commit(next);
    const session = active.current;
    if (!session || !isActive(session)) return;
    session.revision++;
    session.requests.forEach(controller => controller.abort());
    void refresh(session, next); // Issued after acknowledgement, so its count/state includes this mutation.
  }, [commit, isActive, refresh]);
  const updateItem = useCallback((id: string, changes: Partial<ArticleListItem>) => {
    updateCachedArticle(id, changes);
    if (!snapshot.current.items.some(item => item.id === id)) return;
    reconcileMutation(patchLibraryEntry(snapshot.current, parseLibraryQuery(new URLSearchParams(active.current?.key)), id, changes));
  }, [reconcileMutation]);
  const removeItems = useCallback((ids: string[]) => {
    ids.forEach(id => updateCachedArticle(id, { deletedAt: new Date().toISOString() }));
    const current = snapshot.current;
    const items = current.items.filter(item => !ids.includes(item.id));
    if (items.length !== current.items.length) reconcileMutation({ ...current, items, total: Math.max(0, current.total - (current.items.length - items.length)), needsReconcile: true });
  }, [reconcileMutation]);
  return { items: state.items, total: state.total, loading, loadingMore, error, hasMore: !!state.nextCursor, loadMore, updateItem, removeItems };
}
