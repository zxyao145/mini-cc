import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, it, vi } from 'vitest';
import HighlightManager from '@/app/pages/article/[id]/components/HighlightManager';
import { decorateReaderContent } from './reader-content';
import { getTextContent } from './text-offsets';
import { enumerateReaderBlocks } from './reading-position';
it('preserves concatenated UTF-16 text through real highlighting, notes, heading ids, table wrappers and lazy images', async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true }); vi.useFakeTimers();
  const host = document.createElement('div'); document.body.append(host);
  const root = createRoot(host);
  const html = '<h2>标题😀</h2><p>before <em>inline</em> after</p><table><tbody><tr><td>cell</td></tr></tbody></table><figure><img src="x"></figure>';
  const raw = document.createElement('div'); raw.innerHTML = html;
  const before = getTextContent(raw);
  try {
    await act(async () => root.render(<><HighlightManager articleId="test" highlights={[{id:'h',articleId:'test',text:'before',note:'note',color:'#ffeb3b',startOffset:4,endOffset:10,createdAt:''}]} onHighlightsChange={() => {}} /><div className="readable-content" dangerouslySetInnerHTML={{__html:html}} /></>));
    const content = host.querySelector<HTMLElement>('.readable-content')!;
    decorateReaderContent(content);
    await act(async () => vi.advanceTimersByTime(150));
    expect(content.querySelector('[data-highlight-id]')).not.toBeNull();
    expect(content.querySelector('[data-highlight-id] span')?.textContent).toBe('');
    expect(content.querySelector('h2')?.id).toBeTruthy();
    expect(content.querySelector('table')?.parentElement?.className).toBe('reader-table');
    expect(content.querySelector('img')?.loading).toBe('lazy');
    expect(getTextContent(content)).toBe(before);
    expect(enumerateReaderBlocks(content).map(b => b.startOffset)).toEqual([0,4,23]);
  } finally { await act(async () => root.unmount()); host.remove(); vi.useRealTimers(); }
});
