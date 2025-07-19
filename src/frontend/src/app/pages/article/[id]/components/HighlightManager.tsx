'use client';

import { useState, useEffect, useCallback, useRef } from 'react';
import { useFloating, autoUpdate, offset, flip, shift, arrow } from '@floating-ui/react';
import { articleApi } from '@/lib/api';
import { Highlight } from '@/types';
import styles from './HighlightManager.module.scss';

interface HighlightManagerProps {
  articleId: string;
  highlights: Highlight[];
  onHighlightsChange: (highlights: Highlight[]) => void;
}

export default function HighlightManager({ 
  articleId, 
  highlights, 
  onHighlightsChange 
}: HighlightManagerProps) {
  const [tooltipVisible, setTooltipVisible] = useState(false);
  const [selectedText, setSelectedText] = useState('');
  const [clickedHighlight, setClickedHighlight] = useState<string | null>(null);
  const [mergeNotification, setMergeNotification] = useState<string | null>(null);
  
  const arrowRef = useRef(null);
  const highlightMenuArrowRef = useRef(null);
  
  // Floating UI for selection tooltip
  const { refs: tooltipRefs, floatingStyles: tooltipStyles } = useFloating({
    open: tooltipVisible,
    onOpenChange: setTooltipVisible,
    placement: 'top',
    middleware: [
      offset(10),
      flip(),
      shift({ padding: 8 }),
      arrow({ element: arrowRef })
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
      shift({ padding: 8 }),
      arrow({ element: highlightMenuArrowRef })
    ],
    whileElementsMounted: autoUpdate,
  });

  const hideTooltip = useCallback(() => {
    setTooltipVisible(false);
    setSelectedText('');
  }, []);

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

    document.addEventListener('mouseup', handleMouseUp);
    document.addEventListener('click', handleClick);
    
    return () => {
      document.removeEventListener('mouseup', handleMouseUp);
      document.removeEventListener('click', handleClick);
    };
  }, [hideTooltip, clickedHighlight, menuRefs, tooltipRefs]);

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

      // Apply new highlights
      if (highlights.length === 0) return;

      const walker = document.createTreeWalker(
        contentDiv,
        NodeFilter.SHOW_TEXT
      );

      const textNodes: { node: Text; text: string; offset: number }[] = [];
      let totalOffset = 0;
      let node: Node | null;

      while ((node = walker.nextNode())) {
        const textNode = node as Text;
        const text = textNode.textContent || '';
        textNodes.push({ node: textNode, text, offset: totalOffset });
        totalOffset += text.length;
      }

      // Sort highlights by start offset (reverse order for proper DOM manipulation)
      const sortedHighlights = [...highlights].sort((a, b) => b.startOffset - a.startOffset);

      sortedHighlights.forEach((highlight) => {
        const startNode = textNodes.find(tn => 
          tn.offset <= highlight.startOffset && tn.offset + tn.text.length > highlight.startOffset
        );
        const endNode = textNodes.find(tn => 
          tn.offset < highlight.endOffset && tn.offset + tn.text.length >= highlight.endOffset
        );

        if (startNode && endNode) {
          const range = document.createRange();
          const startOffsetInNode = highlight.startOffset - startNode.offset;
          const endOffsetInNode = highlight.endOffset - endNode.offset;

          range.setStart(startNode.node, startOffsetInNode);
          range.setEnd(endNode.node, endOffsetInNode);

          if (!range.collapsed) {
            const span = document.createElement('span');
            span.className = styles.highlight;
            span.style.backgroundColor = highlight.color || '#ffeb3b';
            span.style.padding = '2px 4px';
            span.style.borderRadius = '3px';
            span.style.cursor = 'pointer';
            span.style.position = 'relative';
            span.setAttribute('data-highlight-id', highlight.id);
            span.title = highlight.note ? `Note: ${highlight.note}` : 'Click to delete highlight';

            // Add note icon if note exists
            if (highlight.note && highlight.note.trim()) {
              const noteIcon = document.createElement('span');
              noteIcon.className = styles.noteIcon;
              noteIcon.innerHTML = '📝';
              noteIcon.style.position = 'absolute';
              noteIcon.style.top = '-8px';
              noteIcon.style.right = '-8px';
              noteIcon.style.fontSize = '12px';
              noteIcon.style.background = 'white';
              noteIcon.style.borderRadius = '50%';
              noteIcon.style.width = '16px';
              noteIcon.style.height = '16px';
              noteIcon.style.display = 'flex';
              noteIcon.style.alignItems = 'center';
              noteIcon.style.justifyContent = 'center';
              noteIcon.style.boxShadow = '0 1px 3px rgba(0,0,0,0.2)';
              noteIcon.style.zIndex = '10';
              noteIcon.style.pointerEvents = 'none';
              
              span.appendChild(noteIcon);
            }

            try {
              range.surroundContents(span);
            } catch (e) {
              console.warn('Could not apply highlight:', e);
            }
          }
        }
      });
    };

    // Apply highlights after a short delay to ensure DOM is ready
    const timer = setTimeout(applyHighlights, 100);
    return () => clearTimeout(timer);
  }, [highlights]);

  const findAdjacentHighlights = (startOffset: number, endOffset: number): Highlight[] => {
    const tolerance = 5; // 允许5个字符的间隔也被视为相邻
    
    return highlights.filter(h => {
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

  const calculateTextOffset = (contentDiv: HTMLElement, targetNode: Node, offset: number): number => {
    const walker = document.createTreeWalker(
      contentDiv,
      NodeFilter.SHOW_TEXT
    );

    let textOffset = 0;
    let node: Node | null;
    
    while ((node = walker.nextNode())) {
      if (node === targetNode) {
        return textOffset + offset;
      }
      textOffset += (node.textContent || '').length;
    }
    
    return -1;
  };

  const getTextContent = (contentDiv: HTMLElement): string => {
    // 获取纯文本内容，忽略HTML标签
    const walker = document.createTreeWalker(
      contentDiv,
      NodeFilter.SHOW_TEXT
    );

    let textContent = '';
    let node: Node | null;
    while ((node = walker.nextNode())) {
      textContent += node.textContent || '';
    }
    return textContent;
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
          
          // 显示合并通知
          setMergeNotification(`Merging with ${adjacentHighlights.length} existing highlight(s)`);
          setTimeout(() => setMergeNotification(null), 3000);
          
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
          const mergedColor = adjacentHighlights[0]?.color || '#ffeb3b';
          
          console.log(`Merging highlights:`, {
            originalRanges: adjacentHighlights.map(h => `${h.startOffset}-${h.endOffset}`),
            newRange: `${startOffset}-${endOffset}`,
            mergedRange: `${mergedStartOffset}-${mergedEndOffset}`,
            mergedText: mergedText.substring(0, 50) + (mergedText.length > 50 ? '...' : '')
          });
          
          // 删除所有相邻的高亮
          for (const highlight of adjacentHighlights) {
            await articleApi.deleteHighlight(highlight.id);
          }
          
          // 创建合并后的新高亮
          const mergedHighlight = await articleApi.addHighlight(articleId, {
            text: mergedText,
            note: mergedNotes,
            color: mergedColor,
            startOffset: mergedStartOffset,
            endOffset: mergedEndOffset
          });
          
          // 更新高亮列表
          const remainingHighlights = highlights.filter(h => 
            !adjacentHighlights.find(ah => ah.id === h.id)
          );
          onHighlightsChange([...remainingHighlights, mergedHighlight]);
          
        } else {
          // 没有相邻高亮，直接创建新高亮
          const newHighlight = await articleApi.addHighlight(articleId, {
            text: selectedText,
            note: '',
            color: '#ffeb3b',
            startOffset,
            endOffset
          });
          
          onHighlightsChange([...highlights, newHighlight]);
        }
      }
      
      hideTooltip();
      selection.removeAllRanges();
    } catch (error) {
      console.error('Failed to create highlight:', error);
    }
  };

  const handleCancel = () => {
    hideTooltip();
    const selection = window.getSelection();
    if (selection) {
      selection.removeAllRanges();
    }
  };

  return (
    <>
      {tooltipVisible && (
        <div 
          ref={tooltipRefs.setFloating}
          className={styles.selectionTooltip}
          style={tooltipStyles}
        >
          <button onClick={handleCreateHighlight} className={styles.highlightButton}>
            💡 Highlight
          </button>
          <button onClick={handleCancel} className={styles.cancelTooltipButton}>
            ✕
          </button>
          <div ref={arrowRef} className={styles.tooltipArrow} />
        </div>
      )}
      
      {mergeNotification && (
        <div 
          className={styles.mergeNotification}
          style={{
            position: 'fixed',
            top: '20px',
            right: '20px',
            background: '#4caf50',
            color: 'white',
            padding: '12px 16px',
            borderRadius: '6px',
            zIndex: 1002,
            boxShadow: '0 4px 12px rgba(0, 0, 0, 0.15)'
          }}
        >
          ✅ {mergeNotification}
        </div>
      )}
      
      {clickedHighlight && (
        <div 
          ref={menuRefs.setFloating}
          className={styles.highlightOptions}
          style={menuStyles}
        >
          <div className={styles.highlightMenu}>
            <div className={styles.highlightMenuHeader}>
              Highlight Options
            </div>
            <div className={styles.highlightMenuActions}>
              <button
                onClick={() => handleDeleteHighlight(clickedHighlight)}
                className={styles.deleteButton}
              >
                🗑️ Delete Highlight
              </button>
              <button
                onClick={() => setClickedHighlight(null)}
                className={styles.cancelButton}
              >
                Cancel
              </button>
            </div>
            <div ref={highlightMenuArrowRef} className={styles.menuArrow} />
          </div>
        </div>
      )}
    </>
  );
}