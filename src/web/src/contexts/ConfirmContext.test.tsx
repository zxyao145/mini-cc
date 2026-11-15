import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, it, vi } from 'vitest';
import { ConfirmProvider, useConfirm } from './ConfirmContext';

it('focuses cancellation, resolves the selected answer, and returns focus to the opener', async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const host = document.createElement('div');
  document.body.append(host);
  const root = createRoot(host);
  const answer = vi.fn();
  function Trigger() {
    const confirm = useConfirm();
    return <button onClick={() => { void confirm('永久删除这篇文章？', { confirmLabel: '永久删除' }).then(answer); }}>打开</button>;
  }
  const dialog = () => document.body.querySelector<HTMLElement>('[role="dialog"][data-state="open"]');
  const button = (text: string) => Array.from(dialog()!.querySelectorAll('button')).find(item => item.textContent === text)!;
  try {
    await act(async () => root.render(<ConfirmProvider><Trigger /></ConfirmProvider>));
    const trigger = host.querySelector('button')!;
    trigger.focus();
    await act(async () => trigger.click());
    expect(dialog()?.textContent).toContain('永久删除这篇文章？');
    expect(document.activeElement).toBe(button('取消'));
    await act(async () => button('取消').click());
    expect(answer).toHaveBeenLastCalledWith(false);
    await vi.waitFor(() => expect(document.activeElement).toBe(trigger));
    await act(async () => trigger.click());
    await act(async () => button('永久删除').click());
    expect(answer).toHaveBeenLastCalledWith(true);
  } finally {
    await act(async () => root.unmount());
    host.remove();
  }
});
