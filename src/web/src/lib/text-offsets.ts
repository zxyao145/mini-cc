/** UTF-16 text offsets shared with stored highlights; empty decoration spans add no text. */
export function textNodes(root: Node) {
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
  const nodes: { node: Text; text: string; offset: number }[] = [];
  let offset = 0;
  let node: Node | null;
  while ((node = walker.nextNode())) {
    const text = node.textContent || '';
    nodes.push({ node: node as Text, text, offset });
    offset += text.length;
  }
  return nodes;
}

export function calculateTextOffset(root: Node, target: Node, offset: number): number {
  // Range endpoints may be elements (e.g. selecting an entire paragraph).
  const range = document.createRange();
  range.selectNodeContents(root);
  try { range.setEnd(target, offset); return range.toString().length; }
  catch { return -1; }
}

export function getTextContent(root: Node): string {
  return textNodes(root).map(n => n.text).join('');
}
