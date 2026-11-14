import { act, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, it, vi } from 'vitest';
import { articleApi, highlightApi } from '@/lib/api';
import { Highlight } from '@/types';
import HighlightManager from './HighlightManager';
import ReadableContent from './ReadableContent';
vi.mock('@/lib/api', () => ({ articleApi: { addHighlight: vi.fn(), deleteHighlight: vi.fn() }, highlightApi: { anchor: vi.fn() } }));
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
    return <><HighlightManager articleId="article" bodyStale={false} highlights={highlights} onHighlightsChange={setHighlights}/><ReadableContent contentRef={ref} html="<p>new body</p>"/></>;
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
    return <><div className="readable-content"><p><span data-highlight-id="existing">Selected text</span></p></div><HighlightManager articleId="article" highlights={highlights} bodyStale={false} onHighlightsChange={setHighlights}/></>;
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

it('does not re-anchor pending highlights while the body is stale', async () => {
  vi.mocked(highlightApi.anchor).mockReset();
  const pending: Highlight = { articleId: 'article', id: 'pending', createdAt: '', color: '#ffeb3b', note: 'note', text: 'body', startOffset: 7, endOffset: 11, anchorStatus: 'pending' };
  const onHighlightsChange = vi.fn();
  const onContentChanged = vi.fn();
  function Reader({ bodyStale }: { bodyStale: boolean }) {
    const ref = useRef<HTMLDivElement>(null);
    return <><HighlightManager articleId="article" highlights={[pending]} onHighlightsChange={onHighlightsChange} bodyStale={bodyStale} contentRevision={0} onContentChanged={onContentChanged}/><ReadableContent contentRef={ref} html="<p>new body</p>"/></>;
  }
  const host = document.createElement('div'); document.body.append(host); const root = createRoot(host);
  vi.mocked(highlightApi.anchor).mockRejectedValue({ isAxiosError: true, response: { status: 409, data: { current: { contentRevision: 1 } } } });
  try {
    await act(async () => root.render(<Reader bodyStale />));
    expect(highlightApi.anchor).not.toHaveBeenCalled();
    await act(async () => root.render(<Reader bodyStale={false} />));
    expect(highlightApi.anchor).toHaveBeenCalledExactlyOnceWith('pending', { startOffset: 4, endOffset: 8, contentRevision: 0 });
    expect(onContentChanged).toHaveBeenCalledWith({ contentRevision: 1 });
    expect(onHighlightsChange).not.toHaveBeenCalled();
  } finally { await act(async () => root.unmount()); host.remove(); }
});

it('hides highlight creation for a selection when the displayed body becomes stale', async () => {
  vi.useFakeTimers();
  const host = document.createElement('div'); document.body.append(host); const root = createRoot(host);
  const onHighlightsChange = vi.fn();
  function Reader({ bodyStale }: { bodyStale: boolean }) {
    const ref = useRef<HTMLDivElement>(null);
    return <><HighlightManager articleId="article" highlights={[]} onHighlightsChange={onHighlightsChange} bodyStale={bodyStale}/><ReadableContent contentRef={ref} html="<p>new body</p>"/></>;
  }
  try {
    await act(async () => root.render(<Reader bodyStale={false} />));
    const selection = window.getSelection()!;
    const range = document.createRange();
    range.selectNodeContents(host.querySelector('.readable-content p')!);
    selection.addRange(range);
    await act(async () => {
      document.dispatchEvent(new MouseEvent('mouseup'));
      await vi.advanceTimersByTimeAsync(60);
    });
    const createButton = () => Array.from(host.querySelectorAll('button')).find(button => button.textContent === '高亮');
    expect(createButton()).toBeDefined();
    await act(async () => root.render(<Reader bodyStale />));
    expect(createButton()).toBeUndefined();
    selection.removeAllRanges();
  } finally { await act(async () => root.unmount()); host.remove(); vi.useRealTimers(); }
});


it.each(['stale merge', 'stale create', 'successful merge'])('handles a manual highlight %s', async scenario => {
  vi.useFakeTimers();
  const host = document.createElement('div');
  document.body.append(host);
  const root = createRoot(host);
  const existing: Highlight = { articleId: 'article', id: 'original', createdAt: '', color: '#ffeb3b', note: 'preserve this note', text: 'prior', startOffset: 0, endOffset: 5, anchorStatus: 'ok' };
  const highlights = scenario === 'stale create' ? [] : [existing];
  const onContentChanged = vi.fn();
  const onHighlightsChange = vi.fn();
  const add = vi.mocked(articleApi.addHighlight);
  const remove = vi.mocked(articleApi.deleteHighlight);
  add.mockReset();
  remove.mockReset();
  const merged = { ...existing, id: 'merged', text: 'prior words', endOffset: 11 };
  if (scenario === 'successful merge') add.mockResolvedValue(merged);
  else add.mockRejectedValue({ isAxiosError: true, response: { status: 409, data: { current: { contentRevision: 1 } } } });

  function Reader() {
    const ref = useRef<HTMLDivElement>(null);
    return <><HighlightManager articleId="article" highlights={highlights} onHighlightsChange={onHighlightsChange} bodyStale={false} contentRevision={0} onContentChanged={onContentChanged}/><ReadableContent contentRef={ref} html="<p>prior words</p>"/></>;
  }

  const selection = window.getSelection()!;
  try {
    await act(async () => root.render(<Reader />));
    const text = host.querySelector('.readable-content p')!.firstChild!;
    const range = document.createRange();
    range.setStart(text, 6);
    range.setEnd(text, 11);
    selection.addRange(range);
    await act(async () => {
      document.dispatchEvent(new MouseEvent('mouseup'));
      await vi.advanceTimersByTimeAsync(60);
    });
    const createButton = Array.from(host.querySelectorAll('button')).find(button => button.textContent === '高亮')!;
    await act(async () => createButton.click());

    if (scenario === 'successful merge') {
      expect(add).toHaveBeenCalledBefore(remove);
      expect(remove).toHaveBeenCalledExactlyOnceWith('original');
      expect(onHighlightsChange).toHaveBeenCalledExactlyOnceWith([merged]);
      expect(onContentChanged).not.toHaveBeenCalled();
    } else {
      expect(remove).not.toHaveBeenCalled();
      expect(onHighlightsChange).not.toHaveBeenCalled();
      expect(onContentChanged).toHaveBeenCalledExactlyOnceWith({ contentRevision: 1 });
    }
    expect(add).toHaveBeenCalledExactlyOnceWith('article', {
      text: highlights.length ? 'prior words' : 'words',
      note: highlights.length ? 'preserve this note' : '',
      color: '#ffeb3b',
      startOffset: highlights.length ? 0 : 6,
      endOffset: 11,
      contentRevision: 0,
    });
  } finally {
    selection.removeAllRanges();
    await act(async () => root.unmount());
    host.remove();
    vi.useRealTimers();
  }
});

it('ignores a manual highlight conflict after opening another article', async () => {
  vi.useFakeTimers();
  const host = document.createElement('div');
  document.body.append(host);
  const root = createRoot(host);
  const onContentChanged = vi.fn();
  let rejectSave!: (error: unknown) => void;
  const add = vi.mocked(articleApi.addHighlight);
  add.mockReset();
  add.mockImplementation(() => new Promise((_, reject) => { rejectSave = reject; }));

  function Reader({ articleId }: { articleId: string }) {
    const ref = useRef<HTMLDivElement>(null);
    return <><HighlightManager articleId={articleId} highlights={[]} onHighlightsChange={() => {}} bodyStale={false} contentRevision={0} onContentChanged={onContentChanged}/><ReadableContent contentRef={ref} html="<p>prior words</p>"/></>;
  }

  const selection = window.getSelection()!;
  try {
    await act(async () => root.render(<Reader articleId="first" />));
    const range = document.createRange();
    range.selectNodeContents(host.querySelector('.readable-content p')!);
    selection.addRange(range);
    await act(async () => {
      document.dispatchEvent(new MouseEvent('mouseup'));
      await vi.advanceTimersByTimeAsync(60);
    });
    const createButton = Array.from(host.querySelectorAll('button')).find(button => button.textContent === '高亮')!;
    await act(async () => createButton.click());
    expect(add).toHaveBeenCalledTimes(1);
    await act(async () => root.render(<Reader articleId="second" />));
    await act(async () => rejectSave({ isAxiosError: true, response: { status: 409, data: { current: { contentRevision: 1 } } } }));
    expect(onContentChanged).not.toHaveBeenCalled();
  } finally {
    selection.removeAllRanges();
    await act(async () => root.unmount());
    host.remove();
    vi.useRealTimers();
  }
});
