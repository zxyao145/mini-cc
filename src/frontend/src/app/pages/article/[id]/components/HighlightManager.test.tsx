import { act, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, it, vi } from 'vitest';
import { highlightApi } from '@/lib/api';
import { Highlight } from '@/types';
import HighlightManager from './HighlightManager';
import ReadableContent from './ReadableContent';
vi.mock('@/lib/api', () => ({ articleApi: {}, highlightApi: { anchor: vi.fn() } }));
it('renders only acknowledged anchors; missing text is orphaned and a failed acknowledgement remains pending', async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true }); vi.useFakeTimers();
  const base = { articleId: 'article', createdAt: '', color: '#ffeb3b', note: 'keep note', startOffset: 7, endOffset: 11, anchorStatus: 'pending' as const };
  const original: Highlight[] = [{ ...base, id: 'found', text: 'body' }, { ...base, id: 'absent', text: 'gone' }, { ...base, id: 'failed', text: 'new' }];
  let current = original;
  vi.mocked(highlightApi.anchor).mockImplementation(async (id, anchor) => {
    if (id === 'failed') throw new Error('offline');
    const h = original.find(h => h.id === id)!;
    return 'orphaned' in anchor ? { ...h, anchorStatus: 'orphaned' } : { ...h, ...anchor, anchorStatus: 'ok' };
  });
  function Reader() {
    const [highlights, setHighlights] = useState(original); current = highlights;
    const ref = useRef<HTMLDivElement>(null);
    return <><HighlightManager articleId="article" highlights={highlights} onHighlightsChange={setHighlights}/><ReadableContent contentRef={ref} html="<p>new body</p>"/></>;
  }
  const host = document.createElement('div'); document.body.append(host); const root = createRoot(host);
  try {
    await act(async () => { root.render(<Reader/>); });
    await act(async () => { await vi.advanceTimersByTimeAsync(110); });
    expect(current.map(h => h.anchorStatus)).toEqual(['ok', 'orphaned', 'pending']);
    expect(current.map(h => h.note)).toEqual(['keep note', 'keep note', 'keep note']);
    expect(host.querySelectorAll('[data-highlight-id]')).toHaveLength(1);
    expect(host.querySelector('[data-highlight-id="found"]')?.textContent).toBe('body');
    expect(host.querySelector('[data-highlight-id="found"]')?.hasAttribute('tabindex')).toBe(false);
    expect(host.querySelector('.readable-content')?.textContent).toBe('new body');
  } finally { await act(async () => root.unmount()); host.remove(); vi.useRealTimers(); }
});

it('closes the highlight menu on Escape and leaves article text unchanged', async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const highlight: Highlight = { id: 'existing', articleId: 'article', text: 'Selected text', note: '', color: '#ffeb3b', startOffset: 0, endOffset: 13, createdAt: '' };
  const host = document.createElement('div'); document.body.append(host); const root = createRoot(host);
  function Reader() {
    const [highlights, setHighlights] = useState([highlight]);
    return <><div className="readable-content"><p><span data-highlight-id="existing">Selected text</span></p></div><HighlightManager articleId="article" highlights={highlights} onHighlightsChange={setHighlights}/></>;
  }
  const dismissTooltip = (event: KeyboardEvent) => { if (event.key === 'Escape') event.preventDefault(); };
  document.addEventListener('keydown', dismissTooltip, true);
  try {
    await act(async () => root.render(<Reader/>));
    const trigger = host.querySelector<HTMLElement>('[data-highlight-id="existing"]')!;
    const content = host.querySelector('.readable-content')!;
    const articleText = content.textContent;
    await act(async () => trigger.dispatchEvent(new MouseEvent('click', { bubbles: true })));
    const menu = () => host.querySelector('[aria-label="高亮选项"]');
    expect(menu()).not.toBeNull();
    const dialog = document.createElement('div');
    dialog.setAttribute('role', 'dialog');
    host.append(dialog);
    const pressEscape = () => act(async () => { trigger.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true })); });
    await pressEscape();
    expect(menu()).not.toBeNull();
    dialog.remove();
    await pressEscape();
    expect(menu()).toBeNull();
    expect(content.textContent).toBe(articleText);
  } finally { document.removeEventListener('keydown', dismissTooltip, true); await act(async () => root.unmount()); host.remove(); }
});
