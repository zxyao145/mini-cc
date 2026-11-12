import { describe, it, expect } from 'vitest';
import { enumerateReaderBlocks, readPosition, restorationScrollTop } from './reading-position';
import { calculateTextOffset } from './text-offsets';

function content(html: string) { const el = document.createElement('div'); el.innerHTML = html; return el; }
describe('text based reader anchors', () => {
  it('uses innermost and atomic blocks plus consecutive inline anonymous runs without overlap', () => {
    const el = content('intro <em>inline</em> end<p>A <span>mark</span></p>tail<b> text</b><blockquote><p>B</p><p>C</p></blockquote><pre><code>D</code></pre><table><tbody><tr><td><p>E</p></td></tr></tbody></table><figure><p>F</p></figure><figure><img src="x"></figure>');
    const blocks = enumerateReaderBlocks(el);
    expect(blocks.map(b => b.node instanceof Range ? b.node.toString() : b.node.textContent)).toEqual(['intro inline end', 'A mark', 'tail text', 'B', 'C', 'D', 'E', 'F']);
    expect(blocks.map(b => b.startOffset)).toEqual([0, 16, 22, 31, 32, 33, 34, 35]);
    expect(blocks.map(b => b.textLength).reduce((a,b) => a+b,0)).toBe(el.textContent!.length);
    expect(calculateTextOffset(el, el.querySelector('code')!.firstChild!, 1)).toBe(34);
  });
  it('computes text percent and restores by offset across geometry changes with index fallback', () => {
    const el = content('<p>aaaa</p><p>bbbbbb</p>');
    const blocks = enumerateReaderBlocks(el);
    blocks.forEach((b,i) => { b.node.getBoundingClientRect = () => ({ top: i * 200, bottom: i * 200 + 200, height: 200 }) as DOMRect; });
    expect(readPosition(blocks, 10, 500)).toMatchObject({textOffset: 0, blockIndex: 0, blockRatio: .5, percent: 100});
    const anchor = {textOffset: 4,blockIndex: 0,blockRatio: .5,percent:70};
    expect(restorationScrollTop(blocks, anchor, 100, 500)).toBe(300);
    expect(restorationScrollTop(blocks, {...anchor,textOffset:null,blockIndex:1}, 100,500)).toBe(300);
  });
});
