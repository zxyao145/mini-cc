'use client';

import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { Button } from '@/components/arc/button/button';
import { Dialog, DialogContent } from '@/components/arc/dialog/dialog';

type ConfirmOptions = { title?: string; confirmLabel?: string };
type Confirm = (message: string, options?: ConfirmOptions) => Promise<boolean>;
const ConfirmContext = createContext<Confirm>(async () => false);

export const trashArticleConfirmation = ['将这篇文章移入回收站？', { title: '删除文章', confirmLabel: '移入回收站' }] as const;

export const useConfirm = () => useContext(ConfirmContext);

export function ConfirmProvider({ children }: { children: ReactNode }) {
  const [request, setRequest] = useState<(ConfirmOptions & { message: string; open: boolean }) | null>(null);
  const resolve = useRef<(answer: boolean) => void>(null);
  const cancelButton = useRef<HTMLButtonElement>(null);

  const confirm = useCallback<Confirm>((message, options) => {
    if (resolve.current) return Promise.resolve(false);
    return new Promise<boolean>(answer => {
      resolve.current = answer;
      setRequest({ ...options, message, open: true });
    });
  }, []);

  const finish = useCallback((answer: boolean) => {
    const pending = resolve.current;
    resolve.current = null;
    setRequest(current => current && { ...current, open: false });
    pending?.(answer);
  }, []);

  useEffect(() => {
    const onHistory = () => { if (resolve.current) finish(false); };
    window.addEventListener('popstate', onHistory);
    return () => {
      window.removeEventListener('popstate', onHistory);
      resolve.current?.(false);
      resolve.current = null;
    };
  }, [finish]);

  return <ConfirmContext.Provider value={confirm}>
    {children}
    <Dialog open={request?.open ?? false} onOpenChange={open => { if (!open) finish(false); }}>
      <DialogContent
        title={request?.title ?? '确认操作'}
        description={request?.message}
        onInteractOutside={event => event.preventDefault()}
        onOpenAutoFocus={event => { event.preventDefault(); cancelButton.current?.focus(); }}
        footer={<>
          <Button ref={cancelButton} variant="secondary" onClick={() => finish(false)}>取消</Button>
          <Button variant="danger" onClick={() => finish(true)}>{request?.confirmLabel ?? '确认'}</Button>
        </>}
      />
    </Dialog>
  </ConfirmContext.Provider>;
}
