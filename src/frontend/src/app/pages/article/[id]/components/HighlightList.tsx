'use client';

import { useState } from 'react';
import { ChevronDown, ChevronRight, PencilLine, Trash2 } from 'lucide-react';
import { Button } from '@/components/arc/button/button';
import { Textarea } from '@/components/arc/textarea/textarea';
import { Highlight } from '@/types';
import { articleApi, highlightApi } from '@/lib/api';
import styles from './HighlightList.module.css';

interface HighlightListProps {
  highlights: Highlight[];
  onHighlightsChange: (highlights: Highlight[]) => void;
}

export default function HighlightList({ highlights, onHighlightsChange }: HighlightListProps) {
  const [editingNote, setEditingNote] = useState<string | null>(null);
  const [noteText, setNoteText] = useState('');
  const [isExpanded, setIsExpanded] = useState(false);

  const handleDeleteHighlight = async (highlightId: string) => {
    try {
      await articleApi.deleteHighlight(highlightId);
      onHighlightsChange(highlights.filter(h => h.id !== highlightId));
    } catch (error) {
      console.error('Failed to delete highlight:', error);
    }
  };

  const handleEditNote = (highlight: Highlight) => {
    setEditingNote(highlight.id);
    setNoteText(highlight.note || '');
  };

  const handleSaveNote = async (highlightId: string) => {
    try {
      const updated = await highlightApi.updateHighlight(highlightId, noteText);
      onHighlightsChange(highlights.map(h => h.id === highlightId ? updated : h));
      setEditingNote(null);
      setNoteText('');
    } catch (error) {
      console.error('Failed to update highlight note:', error);
    }
  };

  const handleCancelEdit = () => {
    setEditingNote(null);
    setNoteText('');
  };

  const scrollToHighlight = (highlightId: string) => {
    const element = document.querySelector(`[data-highlight-id="${highlightId}"]`);
    if (!element) return;
    element.scrollIntoView({ behavior: 'smooth', block: 'center' });
    element.classList.add(styles.highlighted);
    setTimeout(() => element.classList.remove(styles.highlighted), 2000);
  };

  if (highlights.length === 0) return null;

  return (
    <section className={styles.highlightList} aria-label="高亮笔记">
      <Button variant="ghost" className={styles.header} aria-expanded={isExpanded} aria-label={isExpanded ? '收起高亮' : '展开高亮'} onClick={() => setIsExpanded(!isExpanded)}>
        <strong>高亮笔记（{highlights.length}）</strong>
        {isExpanded ? <ChevronDown size={18} aria-hidden="true" /> : <ChevronRight size={18} aria-hidden="true" />}
      </Button>

      {isExpanded && <div className={styles.highlightItems}>
        {highlights.map((highlight) => (
          <article key={highlight.id} className={styles.highlightItem}>
            <button type="button" className={styles.highlightText} style={{ '--hl': highlight.color || '#ffeb3b' } as React.CSSProperties} onClick={() => scrollToHighlight(highlight.id)} title="跳转到正文高亮">
              {highlight.text}
              {highlight.anchorStatus === 'orphaned' && <small> · 原文位置已失效</small>}
            </button>

            <div className={styles.highlightMeta}>
              <time className={styles.date} dateTime={highlight.createdAt}>{new Date(highlight.createdAt).toLocaleDateString('zh-CN')}</time>
            </div>

            {editingNote === highlight.id ? (
              <div className={styles.noteEdit}>
                <Textarea label="笔记" value={noteText} onChange={(event) => setNoteText(event.target.value)} placeholder="添加笔记…" className={styles.noteTextarea} />
                <div className={styles.noteActions}>
                  <Button onClick={() => void handleSaveNote(highlight.id)} variant="primary" size="sm" className={styles.saveButton}>保存</Button>
                  <Button onClick={handleCancelEdit} variant="secondary" size="sm" className={styles.cancelButton}>取消</Button>
                </div>
              </div>
            ) : (
              <div className={styles.noteDisplay}>
                {highlight.note ? <p className={styles.note}><strong>笔记：</strong>{highlight.note}</p> : <p className={styles.noNote}>暂无笔记</p>}
              </div>
            )}

            <div className={styles.actions}>
              <Button onClick={() => handleEditNote(highlight)} variant="ghost" size="sm" className={styles.editButton}>
                <PencilLine size={14} aria-hidden="true" />{highlight.note ? '编辑笔记' : '添加笔记'}
              </Button>
              <Button onClick={() => void handleDeleteHighlight(highlight.id)} variant="danger" size="sm">
                <Trash2 size={14} aria-hidden="true" />删除
              </Button>
            </div>
          </article>
        ))}
      </div>}
    </section>
  );
}
