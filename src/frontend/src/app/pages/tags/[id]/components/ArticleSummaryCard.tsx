"use client";

import Link from "next/link";
import { Archive, Heart } from "lucide-react";
import { ArticleSummary } from "@/types";
import styles from "./ArticleSummaryCard.module.scss";

interface ArticleSummaryCardProps {
  article: ArticleSummary;
}

export default function ArticleSummaryCard({ article }: ArticleSummaryCardProps) {
  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString();
  };

  const truncateText = (text: string, maxLength: number) => {
    if (text.length <= maxLength) return text;
    return text.slice(0, maxLength) + "...";
  };

  return (
    <div className={`${styles.card} ${article.isArchived ? styles.archived : ""}`}>
      {article.imageUrl && (
        <div className={styles.imageContainer}>
          <img
            src={article.imageUrl}
            alt={article.title}
            className={styles.image}
          />
        </div>
      )}

      <div className={styles.content}>
        <div className={styles.header}>
          <h3 className={styles.title}>
            <Link href={`/pages/article/${article.id}`} rel="noopener noreferrer">
              {article.title}
            </Link>
          </h3>
          <div className={styles.actions}>
            {article.isFavorite && (
              <span className={styles.favoriteIcon} title="Favorite" aria-label="Favorite">
                <Heart size={16} fill="currentColor" aria-hidden="true" />
              </span>
            )}
            {article.isArchived && (
              <span className={styles.archiveIcon} title="Archived" aria-label="Archived">
                <Archive size={16} aria-hidden="true" />
              </span>
            )}
          </div>
        </div>

        {article.author && <p className={styles.author}>by {article.author}</p>}

        {article.summary && (
          <p className={styles.summary}>{truncateText(article.summary, 120)}</p>
        )}

        <div className={styles.footer}>
          <span className={styles.date}>
            Saved {formatDate(article.createdAt)}
          </span>
        </div>
      </div>
    </div>
  );
}