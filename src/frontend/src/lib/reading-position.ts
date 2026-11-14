import { textNodes } from './text-offsets';

export interface ReadingPosition { textOffset: number | null; blockIndex: number; blockRatio: number; percent: number; }
export interface ReaderBlock { node: Element | Range; index: number; startOffset: number; textLength: number; }
const selector = 'p,h1,h2,h3,h4,h5,h6,li,pre,blockquote,figure,table,dt,dd';
const atomic = 'pre,table,figure';
const inline = /^(A|SPAN|EM|STRONG|B|I|U|S|CODE|SMALL|MARK|SUB|SUP|ABBR|TIME|Q)$/;

export function enumerateReaderBlocks(root: HTMLElement): ReaderBlock[] {
  const selected = new Set(Array.from(root.querySelectorAll(selector)).filter(el => {
    if (el.parentElement?.closest(atomic) && root.contains(el.parentElement.closest(atomic))) return false;
    return el.matches(atomic) || !el.querySelector(selector);
  }));
  const groups: { owner: Element; nodes: ReturnType<typeof textNodes> }[] = [];
  for (const entry of textNodes(root)) {
    let owner = entry.node.parentElement!;
    while (owner !== root && !selected.has(owner)) owner = owner.parentElement!;
    if (owner === root) {
      owner = entry.node.parentElement!;
      while (owner !== root && inline.test(owner.tagName)) owner = owner.parentElement!;
    }
    const last = groups.at(-1);
    if (last?.owner === owner) last.nodes.push(entry);
    else groups.push({ owner, nodes: [entry] });
  }
  // Image-only/empty blocks cannot define a distinct text anchor and are ignored.
  return groups.filter(g => g.nodes.some(n => n.text.trim())).map((group, index) => {
    let node: Element | Range = group.owner;
    if (!selected.has(group.owner)) {
      const range = document.createRange();
      range.setStart(group.nodes[0].node, 0);
      const end = group.nodes.at(-1)!;
      range.setEnd(end.node, end.text.length);
      node = range;
    }
    return { node, index, startOffset: group.nodes[0].offset, textLength: group.nodes.reduce((n, t) => n + t.text.length, 0) };
  });
}

export function readPosition(blocks: ReaderBlock[], totalTextLength: number, viewportHeight = window.innerHeight): ReadingPosition | null {
  if (!blocks.length || !totalTextLength) return null;
  const line = viewportHeight * .2;
  const block = blocks.find(b => b.node.getBoundingClientRect().bottom > line) || blocks.at(-1)!;
  const rect = block.node.getBoundingClientRect();
  const blockRatio = Math.max(0, Math.min(1, (line - rect.top) / (rect.height || 1)));
  const end = blocks.at(-1)!.node.getBoundingClientRect();
  const percent = end.bottom <= viewportHeight ? 100 : Math.min(100, (block.startOffset + blockRatio * block.textLength) / totalTextLength * 100);
  return { textOffset: block.startOffset, blockIndex: block.index, blockRatio, percent };
}

export function restorationScrollTop(blocks: ReaderBlock[], position: ReadingPosition, scrollY = window.scrollY, viewportHeight = window.innerHeight): number {
  if (!blocks.length) return 0;
  let block: ReaderBlock | undefined;
  if (position.textOffset != null) {
    let low = 0, high = blocks.length - 1;
    while (low <= high) {
      const mid = (low + high) >>> 1;
      if (blocks[mid].startOffset <= position.textOffset) { block = blocks[mid]; low = mid + 1; }
      else high = mid - 1;
    }
  } else block = blocks[position.blockIndex];
  block ||= blocks.at(-1)!;
  const rect = block.node.getBoundingClientRect();
  return Math.max(0, scrollY + rect.top + position.blockRatio * rect.height - viewportHeight * .2);
}
