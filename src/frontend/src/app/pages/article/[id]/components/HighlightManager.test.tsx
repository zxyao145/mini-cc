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
    expect(host.querySelector('.readable-content')?.textContent).toBe('new body');
  } finally { await act(async () => root.unmount()); host.remove(); vi.useRealTimers(); }
});
