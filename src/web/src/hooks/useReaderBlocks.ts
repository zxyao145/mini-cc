'use client';
import { RefObject, useEffect, useState } from 'react';
import { enumerateReaderBlocks, ReaderBlock } from '@/lib/reading-position';
import { decorateReaderContent } from '@/lib/reader-content';

export function useReaderBlocks(contentRef: RefObject<HTMLElement | null>, content: string | undefined) {
  const [layout, setLayout] = useState<{blocks: ReaderBlock[]; totalTextLength: number}>({blocks:[],totalTextLength:0});
  useEffect(() => {
    const root = contentRef.current;
    if (!root || content == null) return;
    const compute = () => {
      decorateReaderContent(root);
      setLayout({blocks:enumerateReaderBlocks(root),totalTextLength:root.textContent?.length || 0});
    };
    compute();
    root.addEventListener('reader-highlights-rendered',compute);
    return () => root.removeEventListener('reader-highlights-rendered',compute);
  }, [contentRef, content]);
  return layout;
}
