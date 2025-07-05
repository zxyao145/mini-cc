'use client';

import { useState, useEffect } from "react";
import styles from "./highlights.module.scss";

interface Highlight {
  id: number;
  text: string;
  note?: string;
  articleTitle: string;
  articleUrl: string;
  createdAt: string;
}

export default function HighlightsPage() {
  const [highlights, setHighlights] = useState<Highlight[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // TODO: Load highlights from API
    setLoading(false);
    // Mock data for now
    setHighlights([
      {
        id: 1,
        text: "This is an important highlighted text from an article",
        note: "My personal note about this highlight",
        articleTitle: "Sample Article Title",
        articleUrl: "/article/1",
        createdAt: "2024-01-15T10:30:00Z"
      },
      {
        id: 2,
        text: "Another highlighted section that I found interesting",
        articleTitle: "Another Article",
        articleUrl: "/article/2",
        createdAt: "2024-01-14T15:45:00Z"
      }
    ]);
  }, []);

  if (loading) {
    return (
      <div className={styles.container}>
        <h1>HighLights</h1>
        <p>Loading highlights...</p>
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
              <div key={highlight.id} className={styles.highlightCard}>
                <div className={styles.highlightText}>
                  {highlight.text}
                </div>
                
                {highlight.note && (
                  <div className={styles.note}>
                    <strong>Note:</strong> {highlight.note}
                  </div>
                )}
                
                <div className={styles.metadata}>
                  <a href={highlight.articleUrl} className={styles.articleLink}>
                    {highlight.articleTitle}
                  </a>
                  <span className={styles.date}>
                    {new Date(highlight.createdAt).toLocaleDateString()}
                  </span>
                </div>
              </div>
            ))}
          </div>
        )}
      </main>
    </div>
  );
}