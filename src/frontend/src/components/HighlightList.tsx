'use client';

import { useState } from 'react';
import { Highlight } from '@/types';
import { articleApi, highlightApi } from '@/lib/api';
import styles from './HighlightList.module.scss';

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
    if (element) {
      element.scrollIntoView({ behavior: 'smooth', block: 'center' });
      // Briefly highlight the element
      element.classList.add(styles.highlighted);
      setTimeout(() => {
        element.classList.remove(styles.highlighted);
      }, 2000);
    }
  };

  if (highlights.length === 0) {
    return null;
  }

  return (
    <div className={styles.highlightList}>
      <div className={styles.header} onClick={() => setIsExpanded(!isExpanded)}>
        <h3>Highlights ({highlights.length})</h3>
        <button className={styles.toggleButton}>
          {isExpanded ? '▼' : '▶'}
        </button>
      </div>
      
      {isExpanded && (
        <div className={styles.highlightItems}>
          {highlights.map((highlight) => (
            <div key={highlight.id} className={styles.highlightItem}>
              <div 
                className={styles.highlightText}
                style={{ backgroundColor: highlight.color || '#ffeb3b' }}
                onClick={() => scrollToHighlight(highlight.id)}
                title="Click to scroll to highlight"
              >
                &quot;{highlight.text}&quot;
              </div>
              
              <div className={styles.highlightMeta}>
                <span className={styles.date}>
                  {new Date(highlight.createdAt).toLocaleDateString()}
                </span>
              </div>
              
              {editingNote === highlight.id ? (
                <div className={styles.noteEdit}>
                  <textarea
                    value={noteText}
                    onChange={(e) => setNoteText(e.target.value)}
                    placeholder="Add a note..."
                    className={styles.noteTextarea}
                  />
                  <div className={styles.noteActions}>
                    <button 
                      onClick={() => handleSaveNote(highlight.id)}
                      className={styles.saveButton}
                    >
                      Save
                    </button>
                    <button 
                      onClick={handleCancelEdit}
                      className={styles.cancelButton}
                    >
                      Cancel
                    </button>
                  </div>
                </div>
              ) : (
                <div className={styles.noteDisplay}>
                  {highlight.note ? (
                    <div className={styles.note}>
                      <strong>Note:</strong> {highlight.note}
                    </div>
                  ) : (
                    <div className={styles.noNote}>No note</div>
                  )}
                </div>
              )}
              
              <div className={styles.actions}>
                <button
                  onClick={() => handleEditNote(highlight)}
                  className={styles.editButton}
                >
                  📝 {highlight.note ? 'Edit Note' : 'Add Note'}
                </button>
                <button
                  onClick={() => handleDeleteHighlight(highlight.id)}
                  className={styles.deleteButton}
                >
                  🗑️ Delete
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}