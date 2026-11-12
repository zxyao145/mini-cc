'use client';
import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { feedApi } from '@/lib/api';
import { Feed } from '@/types';
import { useAuth } from './AuthContext';
import { notifyLibraryMutation } from '@/lib/library-mutations';

const FeedsContext = createContext({ feeds: [] as Feed[], error: '', loading: false, refresh: async () => {}, changed: async () => {} });
export function FeedsProvider({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  const [feeds, setFeeds] = useState<Feed[]>([]), [error, setError] = useState(''), [loading, setLoading] = useState(false);
  const revision = useRef(0);
  const refresh = useCallback(async () => {
    if (!user) return;
    const request = ++revision.current; setLoading(true);
    try { const result = await feedApi.list(); if (request === revision.current) { setFeeds(result); setError(''); } }
    catch { if (request === revision.current) setError('订阅加载失败，请重试。'); }
    finally { if (request === revision.current) setLoading(false); }
  }, [user]);
  useEffect(() => {
    if (!user) { revision.current++; setFeeds([]); setLoading(false); setError(''); return; }
    const requestRevision = revision;
    void refresh(); const timer = setInterval(() => void refresh(), 5 * 60 * 1000);
    const mutation = () => void refresh(); window.addEventListener('minicc:library-mutation', mutation);
    return () => { requestRevision.current++; clearInterval(timer); window.removeEventListener('minicc:library-mutation', mutation); };
  }, [refresh, user]);
  const changed = useCallback(async () => { notifyLibraryMutation(); }, []);
  return <FeedsContext.Provider value={{ feeds, error, loading, refresh, changed }}>{children}</FeedsContext.Provider>;
}
export const useFeeds = () => useContext(FeedsContext);
