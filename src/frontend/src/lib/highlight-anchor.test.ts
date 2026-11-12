import { describe, expect, it } from 'vitest';
import { nearestAnchor } from './highlight-anchor';

describe('pending highlight anchors', () => {
  it('uses rendered UTF-16 text and nearest occurrence, including overlapping matches', () => {
    expect(nearestAnchor('😀aba aba aba', 'aba', 8)).toEqual({ startOffset: 6, endOffset: 9 });
    expect(nearestAnchor('aaaa', 'aa', 2)).toEqual({ startOffset: 2, endOffset: 4 });
  });
  it('marks only absent text orphaned', () => {
    expect(nearestAnchor('new body', 'old', 0)).toEqual({ orphaned: true });
    expect(nearestAnchor('new body', '', 0)).toEqual({ orphaned: true });
  });
});
