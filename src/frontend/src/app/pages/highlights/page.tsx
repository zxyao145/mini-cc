'use client';

import { useState, useEffect } from "react";
import { highlightApi, articleApi } from "@/lib/api";
import { Highlight } from "@/types";
import styles from "./highlights.module.scss";

interface HighlightCardProps {
  highlight: Highlight;
  onDelete: (id: string) => void;
  onUpdateNote: (id: string, note: string) => void;
}

function HighlightCard({ highlight, onDelete, onUpdateNote }: HighlightCardProps) {
  const [isEditing, setIsEditing] = useState(false);
  const [editNote, setEditNote] = useState(highlight.note || '');
  const [articleTitle, setArticleTitle] = useState<string>('');

  useEffect(() => {
    const loadArticleTitle = async () => {
      try {
        const article = await articleApi.getArticleById(highlight.articleId);
        setArticleTitle(article.title);
      } catch (err) {
        console.error('Failed to load article title:', err);
        setArticleTitle('Unknown Article');
      }
    };
    
    if (highlight.articleId) {
      loadArticleTitle();
    }
  }, [highlight.articleId]);

  const handleSaveNote = () => {
    onUpdateNote(highlight.id, editNote);
    setIsEditing(false);
  };

  const handleCancelEdit = () => {
    setEditNote(highlight.note || '');
    setIsEditing(false);
  };

  return (
    <div className={styles.highlightCard}>
      <div className={styles.highlightText} style={{ backgroundColor: highlight.color || '#ffeb3b' }}>
        {highlight.text}
      </div>
      
      <div className={styles.noteSection}>
        {isEditing ? (
          <div className={styles.noteEdit}>
            <textarea
              value={editNote}
              onChange={(e) => setEditNote(e.target.value)}
              placeholder="Add a note..."
              className={styles.noteTextarea}
            />
            <div className={styles.noteActions}>
              <button onClick={handleSaveNote} className={styles.saveButton}>
                Save
              </button>
              <button onClick={handleCancelEdit} className={styles.cancelButton}>
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
            <button 
              onClick={() => setIsEditing(true)}
              className={styles.editButton}
            >
              {highlight.note ? 'Edit Note' : 'Add Note'}
            </button>
          </div>
        )}
      </div>
      
      <div className={styles.metadata}>
        <a 
          href={`/pages/article/${highlight.articleId}`} 
          className={styles.articleLink}
        >
          {articleTitle}
        </a>
        <span className={styles.date}>
          {new Date(highlight.createdAt).toLocaleDateString()}
        </span>
        <button 
          onClick={() => onDelete(highlight.id)}
          className={styles.deleteButton}
          title="Delete highlight"
        >
          ×
        </button>
      </div>
    </div>
  );
}

export default function HighlightsPage() {
  const [highlights, setHighlights] = useState<Highlight[]>([]);
  const [loading, setLoading] = useState(true);

  const [error, setError] = useState<string | null>(null);

  const loadHighlights = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await highlightApi.getHighlights();
      setHighlights(data);
    } catch (err) {
      console.error('Failed to load highlights:', err);
      setError('Failed to load highlights');
    } finally {
      setLoading(false);
    }
  };

  const handleDeleteHighlight = async (highlightId: string) => {
    try {
      await highlightApi.deleteHighlight(highlightId);
      setHighlights(highlights.filter(h => h.id !== highlightId));
    } catch (err) {
      console.error('Failed to delete highlight:', err);
    }
  };

  const handleUpdateNote = async (highlightId: string, note: string) => {
    try {
      const updated = await highlightApi.updateHighlight(highlightId, note);
      setHighlights(highlights.map(h => h.id === highlightId ? updated : h));
    } catch (err) {
      console.error('Failed to update highlight note:', err);
    }
  };

  useEffect(() => {
    loadHighlights();
  }, []);

  if (loading) {
    return (
      <div className={styles.container}>
        <h1>HighLights</h1>
        <p>Loading highlights...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className={styles.container}>
        <h1>HighLights</h1>
        <p className={styles.error}>{error}</p>
        <button onClick={loadHighlights} className={styles.retryButton}>
          Retry
        </button>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <header className={styles.header}>
        <h1>My HighLights</h1>
        <p>All your saved highlights in one place</p>
      </header>

      <main className={styles.main}>
        {highlights.length === 0 ? (
          <div className={styles.empty}>
            <h2>No highlights yet</h2>
            <p>Start reading articles and highlight important passages to see them here.</p>
          </div>
        ) : (
          <div className={styles.highlightsList}>
            {highlights.map((highlight) => (
              <HighlightCard
                key={highlight.id}
                highlight={highlight}
                onDelete={handleDeleteHighlight}
                onUpdateNote={handleUpdateNote}
              />
            ))}
          </div>
        )}
      </main>
    </div>
  );
}