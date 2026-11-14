'use client';

import { useState, useEffect } from "react";
import { PencilLine, Trash2 } from "lucide-react";
import { highlightApi, articleApi } from "@/lib/api";
import { Highlight } from "@/types";
import { updateCachedArticle } from "@/lib/library-cache";
import { Button } from "@/components/arc/button/button";
import { Textarea } from "@/components/arc/textarea/textarea";
import { Tooltip } from "@/components/arc/tooltip/tooltip";
import styles from "./highlights.module.css";

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
        setArticleTitle('未知文章');
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
    <article className={styles.highlightCard}>
      <blockquote className={styles.highlightText} style={{ '--hl': highlight.color || '#ffeb3b' } as React.CSSProperties}>
        {highlight.text}
        {highlight.anchorStatus === 'orphaned' && <span className={styles.orphaned}>原文位置已失效</span>}
      </blockquote>

      <div className={styles.noteSection}>
        {isEditing ? (
          <div className={styles.noteEdit}>
            <Textarea
              label="笔记内容"
              value={editNote}
              onChange={event => setEditNote(event.target.value)}
              placeholder="写下你的笔记…"
              className={styles.noteTextarea}
              rows={3}
            />
            <div className={styles.noteActions}>
              <Button size="sm" onClick={handleSaveNote}>保存</Button>
              <Button size="sm" variant="secondary" onClick={handleCancelEdit}>取消</Button>
            </div>
          </div>
        ) : (
          <div className={styles.noteDisplay}>
            {highlight.note ? (
              <p className={styles.note}><strong>笔记</strong>{highlight.note}</p>
            ) : (
              <p className={styles.noNote}>还没有笔记</p>
            )}
            <Button size="sm" variant="ghost" onClick={() => setIsEditing(true)}>
              <PencilLine size={15} aria-hidden="true" />
              {highlight.note ? '编辑笔记' : '添加笔记'}
            </Button>
          </div>
        )}
      </div>

      <footer className={styles.metadata}>
        <a href={`/pages/article/${highlight.articleId}`} className={styles.articleLink}>{articleTitle}</a>
        <time className={styles.date} dateTime={highlight.createdAt}>{new Date(highlight.createdAt).toLocaleDateString('zh-CN')}</time>
        <Tooltip content="删除高亮">
          <Button
            type="button"
            size="sm"
            iconOnly
            variant="danger"
            onClick={() => onDelete(highlight.id)}
            className={styles.deleteButton}
            aria-label="删除高亮"
          >
            <Trash2 size={16} aria-hidden="true" />
          </Button>
        </Tooltip>
      </footer>
    </article>
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
      setError('加载高亮失败');
    } finally {
      setLoading(false);
    }
  };

  const handleDeleteHighlight = async (highlightId: string) => {
    try {
      await highlightApi.deleteHighlight(highlightId);
      setHighlights(current => {
        const deleted = current.find(h => h.id === highlightId);
        const remaining = current.filter(h => h.id !== highlightId);
        if (deleted) updateCachedArticle(deleted.articleId, { highlightCount: remaining.filter(h => h.articleId === deleted.articleId).length });
        return remaining;
      });
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
    return <div className={styles.container}><h1 className="sr-only">高亮笔记</h1><p className={styles.state}>正在加载高亮…</p></div>;
  }

  if (error) {
    return (
      <div className={styles.container}>
        <h1 className="sr-only">高亮笔记</h1>
        <p className={styles.error} role="alert">{error}</p>
        <Button onClick={() => void loadHighlights()}>重试</Button>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <header className={styles.header}>
        <h1>高亮笔记</h1>
        <span className={styles.count}>{highlights.length} 条高亮</span>
      </header>

      <div className={styles.main}>
        {highlights.length === 0 ? (
          <section className={styles.empty}>
            <h2>还没有高亮</h2>
            <p>阅读文章并标记重要内容后，这些片段会显示在这里。</p>
          </section>
        ) : (
          <div className={styles.highlightsList}>
            {highlights.map(highlight => (
              <HighlightCard
                key={highlight.id}
                highlight={highlight}
                onDelete={handleDeleteHighlight}
                onUpdateNote={handleUpdateNote}
              />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
