export type HighlightAnchor = { startOffset: number; endOffset: number } | { orphaned: true };
export function nearestAnchor(text: string, selected: string, previousOffset: number): HighlightAnchor {
  if (!selected) return { orphaned: true };
  let closest = -1;
  for (let at = text.indexOf(selected); at !== -1; at = text.indexOf(selected, at + 1)) {
    if (closest === -1 || Math.abs(at - previousOffset) < Math.abs(closest - previousOffset)) closest = at;
  }
  return closest < 0 ? { orphaned: true } : { startOffset: closest, endOffset: closest + selected.length };
}
