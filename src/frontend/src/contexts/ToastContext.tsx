 'use client';
import { createContext, ReactNode, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { articleApi } from '@/lib/api';
import { notifyLibraryMutation } from '@/lib/library-mutations';
import styles from './ToastContext.module.scss';
import { useAuth } from './AuthContext';
interface Toast { id: number; message: string; actionLabel?: string; action?: () => Promise<void> | void; pending?: boolean }
interface ToastContextValue {
  show: (message: string, actionLabel?: string, action?: () => Promise<void> | void, seconds?: number) => void;
  deleted: (ids: string[]) => void; trashRetentionDays: number;
}
const Context = createContext<ToastContextValue>({ show: () => {}, deleted: () => {}, trashRetentionDays: 30 });
export const useToast = () => useContext(Context);
export function ToastProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  const username = user?.userName;
  const [toasts, setToasts] = useState<Toast[]>([]);
  const [settings, setSettings] = useState({ undoWindowSeconds: 10, trashRetentionDays: 30 });
  const nextId = useRef(0); const timers = useRef(new Map<number, ReturnType<typeof setTimeout>>());
  useEffect(() => {
    let active = true;
    if (!username) return;
    articleApi.getLibrarySettings().then(value => { if (active) setSettings(value); }).catch(() => {});
    return () => { active = false; };
  }, [username]);
  useEffect(() => { const timeouts = timers.current; return () => timeouts.forEach(clearTimeout); }, []);
  const dismiss = useCallback((id: number) => {
    clearTimeout(timers.current.get(id)); timers.current.delete(id);
    setToasts(current => current.filter(toast => toast.id !== id));
  }, []);
  const show = useCallback((message: string, actionLabel?: string, action?: () => Promise<void> | void, seconds = 10) => {
    const id = ++nextId.current;
    setToasts(current => [...current, { id, message, actionLabel, action }].slice(-3));
    timers.current.set(id, setTimeout(() => dismiss(id), seconds * 1000));
  }, [dismiss]);
  const deleted = useCallback((ids: string[]) => {
    if (!ids.length) return;
    const restore = async (pendingIds: string[]): Promise<void> => {
      const succeeded: string[] = [], failed: string[] = [];
      for (let start = 0; start < pendingIds.length; start += 200) {
        const chunk = pendingIds.slice(start, start + 200);
        try {
          const result = await articleApi.batch(chunk, 'restore');
          succeeded.push(...result.succeeded); failed.push(...result.failed.map(item => item.id));
        } catch { failed.push(...chunk); }
      }
      notifyLibraryMutation();
      show(`已恢复 ${succeeded.length} 篇文章${failed.length ? `，${failed.length} 篇失败` : ''}`,
        failed.length ? '重试' : undefined, failed.length ? () => restore(failed) : undefined);
    };
    show(`已删除 ${ids.length} 篇文章`, '撤销', () => restore(ids), settings.undoWindowSeconds);
  }, [settings.undoWindowSeconds, show]);
  const run = async (toast: Toast) => {
    clearTimeout(timers.current.get(toast.id));
    setToasts(current => current.map(item => item.id === toast.id ? { ...item, pending: true } : item));
    try { await toast.action?.(); dismiss(toast.id); }
    catch {
      setToasts(current => current.map(item => item.id === toast.id ? { ...item, pending: false, message: '操作失败，请重试' } : item));
    }
  };
  return <Context.Provider value={{ show, deleted, trashRetentionDays: settings.trashRetentionDays }}>{children}
    <div className={styles.region} aria-live="polite" aria-relevant="additions text">{toasts.map(toast => <div className={styles.toast} key={toast.id}>
      <span>{toast.pending ? '处理中…' : toast.message}</span>
      {toast.action && <button disabled={toast.pending} onClick={() => void run(toast)}>{toast.actionLabel}</button>}
      <button aria-label="关闭提示" disabled={toast.pending} onClick={() => dismiss(toast.id)}>×</button>
    </div>)}</div>
  </Context.Provider>;
}
