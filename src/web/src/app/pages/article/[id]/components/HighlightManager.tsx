'use client';

import axios from 'axios';
import { useState, useEffect, useLayoutEffect, useCallback, useRef } from 'react';
import { useFloating, autoUpdate, offset, flip, shift } from '@floating-ui/react';
import { CircleCheck, Highlighter, Trash2, X } from 'lucide-react';
import { Button } from '@/components/arc/button/button';
import { Tooltip } from '@/components/arc/tooltip/tooltip';
import { nearestAnchor } from '@/lib/highlight-anchor';
import { articleApi, highlightApi } from '@/lib/api';
import { Highlight } from '@/types';
import { calculateTextOffset, getTextContent, textNodes } from '@/lib/text-offsets';
import styles from './HighlightManager.module.css';

const DEFAULT_HIGHLIGHT_COLOR = '#ffeb3b';

interface HighlightManagerProps {
  articleId: string;
  highlights: Highlight[];
  bodyStale: boolean;
  contentRevision?: number;
  onContentChanged?: (next: { contentRevision: number }) => void;
  onHighlightsChange: (highlights: Highlight[]) => void;
}

export default function HighlightManager({ 
  articleId,
  bodyStale,
  contentRevision,
  onContentChanged,
  highlights, 
  onHighlightsChange 
}: HighlightManagerProps) {
  const [tooltipVisible, setTooltipVisible] = useState(false);
  const [selectedText, setSelectedText] = useState('');
  const [clickedHighlight, setClickedHighlight] = useState<string | null>(null);
  const [mergeNotification, setMergeNotification] = useState<string | null>(null);
  
  const bodySession = useRef<object | null>(null);
  useLayoutEffect(() => {
    bodySession.current = {};
    return () => { bodySession.current = null; };
  }, [articleId, contentRevision]);

  // Floating UI for selection tooltip
  const { refs: tooltipRefs, floatingStyles: tooltipStyles } = useFloating({
    open: tooltipVisible,
    onOpenChange: setTooltipVisible,
    placement: 'top',
    middleware: [
      offset(10),
      flip(),
      shift({ padding: 8 })
    ],
    whileElementsMounted: autoUpdate,
  });
  
  // Floating UI for highlight options menu
  const { refs: menuRefs, floatingStyles: menuStyles } = useFloating({
    open: !!clickedHighlight,
    onOpenChange: (open) => !open && setClickedHighlight(null),
    placement: 'bottom',
    middleware: [
      offset(5),
      flip(),
      shift({ padding: 8 })
    ],
    whileElementsMounted: autoUpdate,
  });

  const hideTooltip = useCallback(() => {
    setTooltipVisible(false);
    setSelectedText('');
  }, []);

  const handleCancel = useCallback(() => {
    hideTooltip();
    window.getSelection()?.removeAllRanges();
  }, [hideTooltip]);

  useEffect(() => {
    const handleMouseUp = () => {
      // Small delay to ensure selection is finalized
      setTimeout(() => {
        const selection = window.getSelection();
        
        if (selection && selection.toString().trim() && selection.rangeCount > 0) {
          const range = selection.getRangeAt(0);
          const contentDiv = document.querySelector('.readable-content');
          
          if (contentDiv && contentDiv.contains(range.commonAncestorContainer)) {
            const rect = range.getBoundingClientRect();
            
            // Create virtual element for floating-ui
            const virtualEl = {
              getBoundingClientRect() {
                return new DOMRect(rect.x, rect.y, rect.width, rect.height);
              },
            };
            
            tooltipRefs.setReference(virtualEl as { getBoundingClientRect: () => DOMRect });
            setSelectedText(selection.toString().trim());
            setTooltipVisible(true);
          }
        }
      }, 50);
    };

    const handleClick = (e: MouseEvent) => {
      const target = e.target as Element;
      
      // Check if clicking on a highlight
      const highlightElement = target.closest('[data-highlight-id]');
      if (highlightElement) {
        e.preventDefault();
        e.stopPropagation();
        const highlightId = highlightElement.getAttribute('data-highlight-id');
        
        // Set the clicked highlight and reference element for floating menu
        if (clickedHighlight === highlightId) {
          setClickedHighlight(null);
        } else {
          setClickedHighlight(highlightId);
          menuRefs.setReference(highlightElement);
        }
        return;
      }
      
      // Only hide tooltip if clicking outside of it and not on selected text
      if (!target.closest(`.${styles.selectionTooltip}`) && !target.closest(`.${styles.highlightOptions}`)) {
        setTimeout(() => {
          const selection = window.getSelection();
          if (!selection || !selection.toString().trim()) {
            hideTooltip();
          }
        }, 10);
        setClickedHighlight(null);
      }
    };

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape' || document.querySelector('[role="dialog"]')) return;
      if (clickedHighlight) {
        event.preventDefault();
        setClickedHighlight(null);
      } else if (tooltipVisible) {
        event.preventDefault();
        handleCancel();
      }
    };

    document.addEventListener('mouseup', handleMouseUp);
    document.addEventListener('click', handleClick);
    document.addEventListener('keydown', handleKeyDown);
    
    return () => {
      document.removeEventListener('mouseup', handleMouseUp);
      document.removeEventListener('click', handleClick);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [hideTooltip, handleCancel, clickedHighlight, menuRefs, tooltipVisible, tooltipRefs]);

  const observeContentConflict = useCallback((error: unknown) => {
    if (!axios.isAxiosError(error) || error.response?.status !== 409) return false;
    const revision = error.response.data?.current?.contentRevision;
    if (typeof revision !== 'number') return false;
    onContentChanged?.({ contentRevision: revision });
    return true;
  }, [onContentChanged]);

  const latestHighlights = useRef({ highlights, onHighlightsChange });
  latestHighlights.current = { highlights, onHighlightsChange };
  useEffect(() => {
    if (bodyStale) return;
    const content = document.querySelector('.readable-content') as HTMLElement | null;
    if (!content) return;
    let mounted = true;
    const pending = highlights.filter(h => h.anchorStatus === 'pending');
    const text = getTextContent(content);
    if (pending.length) void Promise.allSettled(pending.map(async highlight => {
      try {
        const anchor = nearestAnchor(text, highlight.text, highlight.startOffset);
        return await highlightApi.anchor(highlight.id, { ...anchor, contentRevision });
      } catch (error) {
        if (mounted) observeContentConflict(error);
        throw error;
      }
    })).then(results => {
      if (!mounted) return;
      const resolved = new Map(results.flatMap(result => result.status === 'fulfilled' ? [[result.value.id, result.value] as const] : []));
      if (resolved.size) latestHighlights.current.onHighlightsChange(latestHighlights.current.highlights.map(h => resolved.has(h.id) ? { ...h, startOffset: resolved.get(h.id)!.startOffset, endOffset: resolved.get(h.id)!.endOffset, anchorStatus: resolved.get(h.id)!.anchorStatus } : h));
    });
    return () => { mounted = false; };
  }, [articleId, bodyStale, contentRevision, highlights, observeContentConflict]);

  // Apply highlights to content
  useEffect(() => {
    const applyHighlights = () => {
      const contentDiv = document.querySelector('.readable-content') as HTMLElement;
      if (!contentDiv) return;

      // Remove existing highlight spans
      const existingHighlights = contentDiv.querySelectorAll('[data-highlight-id]');
      existingHighlights.forEach(element => {
        const parent = element.parentNode;
        if (parent) {
          parent.replaceChild(document.createTextNode(element.textContent || ''), element);
          parent.normalize();
        }
      });

      if (highlights.length === 0) return;

      const nodes = textNodes(contentDiv);
      const lastNode = nodes.at(-1);
      if (!lastNode) return;
      const contentLength = lastNode.offset + lastNode.text.length;

      // Work from the end so splitting a text node preserves its earlier offsets.
      const sortedHighlights = highlights.filter(h => !h.anchorStatus || h.anchorStatus === 'ok').sort((a, b) => b.startOffset - a.startOffset);

      for (const highlight of sortedHighlights) {
        if (highlight.startOffset < 0 || highlight.endOffset > contentLength) continue;

        let lastFragment: HTMLSpanElement | undefined;
        for (const { node, offset } of nodes) {
          if (offset >= highlight.endOffset) break;
          const start = Math.max(0, highlight.startOffset - offset);
          const end = Math.min(node.length, highlight.endOffset - offset);
          // Paragraph separators count toward offsets but do not need a wrapper.
          if (start >= end || !node.data.slice(start, end).trim()) continue;

          const span = document.createElement('span');
          span.className = styles.highlight;
          span.style.setProperty('--hl', highlight.color || DEFAULT_HIGHLIGHT_COLOR);
          span.setAttribute('data-highlight-id', highlight.id);
          span.title = highlight.note ? `笔记：${highlight.note}` : '点击管理高亮';

          const range = document.createRange();
          range.setStart(node, start);
          range.setEnd(node, end);
          range.surroundContents(span);
          lastFragment = span;
        }

        if (lastFragment && highlight.note?.trim()) {
          const noteIcon = document.createElement('span');
          noteIcon.className = styles.noteIcon;
          noteIcon.setAttribute('aria-hidden', 'true');
          lastFragment.appendChild(noteIcon);
        }
      }
    };

    // Apply highlights after a short delay to ensure DOM is ready
    const timer = setTimeout(() => {
      applyHighlights();
      document.querySelector('.readable-content')?.dispatchEvent(new Event('reader-highlights-rendered'));
    }, 100);
    return () => clearTimeout(timer);
  }, [highlights]);

  const findAdjacentHighlights = (startOffset: number, endOffset: number): Highlight[] => {
    const tolerance = 5; // 允许5个字符的间隔也被视为相邻
    
    return highlights.filter(h => {
      if (h.anchorStatus === 'pending' || h.anchorStatus === 'orphaned') return false;
      // 检查是否相邻或重叠
      return (
        // 新选择与现有高亮直接相邻
        h.endOffset === startOffset || h.startOffset === endOffset ||
        // 新选择与现有高亮在容忍范围内相邻
        Math.abs(h.endOffset - startOffset) <= tolerance || 
        Math.abs(h.startOffset - endOffset) <= tolerance ||
        // 新选择与现有高亮重叠
        (startOffset >= h.startOffset && startOffset <= h.endOffset) ||
        (endOffset >= h.startOffset && endOffset <= h.endOffset) ||
        // 新选择包含现有高亮
        (startOffset <= h.startOffset && endOffset >= h.endOffset) ||
        // 现有高亮包含新选择
        (h.startOffset <= startOffset && h.endOffset >= endOffset)
      );
    });
  };

  const handleDeleteHighlight = async (highlightId: string) => {
    try {
      await articleApi.deleteHighlight(highlightId);
      onHighlightsChange(highlights.filter(h => h.id !== highlightId));
      setClickedHighlight(null);
    } catch (error) {
      console.error('Failed to delete highlight:', error);
    }
  };

  const handleCreateHighlight = async () => {
    if (bodyStale) return;
    const session = bodySession.current;
    const selection = window.getSelection();
    if (!selectedText || !selection || selection.rangeCount === 0) return;

    try {
      const range = selection.getRangeAt(0);
      const contentDiv = document.querySelector('.readable-content') as HTMLElement;
      if (!contentDiv) return;

      // Calculate text offsets using the new function
      const startOffset = calculateTextOffset(contentDiv, range.startContainer, range.startOffset);
      const endOffset = calculateTextOffset(contentDiv, range.endContainer, range.endOffset);

      if (startOffset >= 0 && endOffset >= 0) {
        // 查找相邻或重叠的高亮
        const adjacentHighlights = findAdjacentHighlights(startOffset, endOffset);
        
        if (adjacentHighlights.length > 0) {
          console.log(`Found ${adjacentHighlights.length} adjacent highlights, merging...`);
          
          // 需要合并高亮
          const allOffsets = [
            startOffset,
            endOffset,
            ...adjacentHighlights.flatMap(h => [h.startOffset, h.endOffset])
          ];
          
          const mergedStartOffset = Math.min(...allOffsets);
          const mergedEndOffset = Math.max(...allOffsets);
          
          // 获取合并后的文本内容 (包括中间的任何间隔文本)
          const fullTextContent = getTextContent(contentDiv);
          const mergedText = fullTextContent.slice(mergedStartOffset, mergedEndOffset);
          
          // 合并笔记 (保留所有非空笔记)
          const allNotes = [
            ...adjacentHighlights.map(h => h.note).filter(note => note && note.trim()),
            // 如果用户当前选择包含了新的文本区域，可以考虑添加提示
          ];
          const mergedNotes = allNotes.length > 0 ? allNotes.join('; ') : '';
          
          // 选择颜色（优先使用第一个相邻高亮的颜色）
          const mergedColor = adjacentHighlights[0]?.color || DEFAULT_HIGHLIGHT_COLOR;
          
          console.log(`Merging highlights:`, {
            originalRanges: adjacentHighlights.map(h => `${h.startOffset}-${h.endOffset}`),
            newRange: `${startOffset}-${endOffset}`,
            mergedRange: `${mergedStartOffset}-${mergedEndOffset}`,
            mergedText: mergedText.substring(0, 50) + (mergedText.length > 50 ? '...' : '')
          });
          
          // Save the merged note before removing its originals.
          const mergedHighlight = await articleApi.addHighlight(articleId, {
            text: mergedText,
            note: mergedNotes,
            color: mergedColor,
            startOffset: mergedStartOffset,
            endOffset: mergedEndOffset,
            contentRevision
          });

          if (bodySession.current === session) {
            setMergeNotification(`已与 ${adjacentHighlights.length} 个相邻高亮合并`);
            setTimeout(() => {
              if (bodySession.current === session) setMergeNotification(null);
            }, 3000);
          }
          const deletedIds = new Set<string>();
          try {
            for (const highlight of adjacentHighlights) {
              await articleApi.deleteHighlight(highlight.id);
              deletedIds.add(highlight.id);
            }
          } finally {
            const remainingHighlights = latestHighlights.current.highlights.filter(h => !deletedIds.has(h.id));
            if (bodySession.current === session) onHighlightsChange([...remainingHighlights, mergedHighlight]);
          }
          
        } else {
          // 没有相邻高亮，直接创建新高亮
          const newHighlight = await articleApi.addHighlight(articleId, {
            text: selectedText,
            note: '',
            color: DEFAULT_HIGHLIGHT_COLOR,
            startOffset,
            endOffset,
            contentRevision
          });
          
          if (bodySession.current === session) onHighlightsChange([...latestHighlights.current.highlights, newHighlight]);
        }
      }
      
      if (bodySession.current === session) {
        hideTooltip();
        selection.removeAllRanges();
      }
    } catch (error) {
      if (bodySession.current !== session) return;
      if (observeContentConflict(error)) {
        hideTooltip();
        selection.removeAllRanges();
      } else {
        console.error('Failed to create highlight:', error);
      }
    }
  };

  return (
    <>
      {!bodyStale && tooltipVisible && (
        <div 
          ref={tooltipRefs.setFloating}
          className={styles.selectionTooltip}
          style={tooltipStyles}
        >
          <Button onClick={handleCreateHighlight} variant="ghost" size="sm" className={styles.highlightButton}>
            <Highlighter size={15} aria-hidden="true" />高亮
          </Button>
          <Tooltip content="取消选择"><Button onClick={handleCancel} variant="ghost" size="sm" iconOnly className={styles.cancelTooltipButton} aria-label="取消选择">
            <X size={15} aria-hidden="true" />
          </Button></Tooltip>
        </div>
      )}
      
      {mergeNotification && (
        <div className={styles.mergeNotification} role="status">
          <CircleCheck size={16} aria-hidden="true" /> {mergeNotification}
        </div>
      )}
      
      {clickedHighlight && (
        <div 
          ref={menuRefs.setFloating}
          className={styles.highlightOptions}
          style={menuStyles}
        >
          <div className={styles.highlightMenu} role="group" aria-label="高亮选项">
            <div className={styles.highlightMenuHeader}>{highlights.find(h => h.id === clickedHighlight)?.text}</div>
            <div className={styles.highlightMenuActions}>
              <Button onClick={() => setClickedHighlight(null)} variant="ghost" size="sm" className={styles.menuButton}>
                <X size={15} aria-hidden="true" />关闭<kbd aria-hidden="true">Esc</kbd>
              </Button>
              <Button onClick={() => void handleDeleteHighlight(clickedHighlight)} variant="ghost" size="sm" className={`${styles.menuButton} ${styles.menuDanger}`}>
                <Trash2 size={15} aria-hidden="true" />删除高亮
              </Button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
