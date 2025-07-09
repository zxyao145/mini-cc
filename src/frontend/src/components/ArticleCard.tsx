"use client";

import { useState } from "react";
import { Article } from "@/types";
import { articleApi } from "@/lib/api";
import TagList from "./TagList";
import styles from "./ArticleCard.module.scss";
import Link from "next/link";

interface ArticleCardProps {
  article: Article;
  onDelete: (id: string) => Promise<void>;
}

export default function ArticleCard({ article, onDelete }: ArticleCardProps) {
  const [isDeleting, setIsDeleting] = useState(false);
  const [isFavorite, setIsFavorite] = useState(article.isFavorite);
  const [isArchived, setIsArchived] = useState(article.isArchived);

  const handleDelete = async () => {
    if (confirm("Are you sure you want to delete this article?")) {
      setIsDeleting(true);
      try {
        await onDelete(article.id);
      } catch (error) {
        console.error("Failed to delete article:", error);
        setIsDeleting(false);
      }
    }
  };

  const handleToggleFavorite = async () => {
    try {
      await articleApi.toggleFavorite(article.id);
      setIsFavorite(!isFavorite);
    } catch (error) {
      console.error("Failed to toggle favorite:", error);
    }
  };

  const handleToggleArchive = async () => {
    try {
      await articleApi.toggleArchive(article.id);
      setIsArchived(!isArchived);
    } catch (error) {
      console.error("Failed to toggle archive:", error);
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString();
  };

  const truncateText = (text: string, maxLength: number) => {
    if (text.length <= maxLength) return text;
    return text.slice(0, maxLength) + "...";
  };

  return (
    <div className={`${styles.card} ${isArchived ? styles.archived : ""}`}>
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
            <button
              onClick={handleToggleFavorite}
              className={`${styles.actionBtn} ${
                isFavorite ? styles.favorited : ""
              }`}
              title={isFavorite ? "Remove from favorites" : "Add to favorites"}
            >
              ♥
            </button>
            <button
              onClick={handleToggleArchive}
              className={`${styles.actionBtn} ${
                isArchived ? styles.archived : ""
              }`}
              title={isArchived ? "Unarchive" : "Archive"}
            >
              📁
            </button>
            <button
              onClick={handleDelete}
              disabled={isDeleting}
              className={`${styles.actionBtn} ${styles.deleteBtn}`}
              title="Delete article"
            >
              {isDeleting ? "..." : "🗑"}
            </button>
          </div>
        </div>

        {article.author && <p className={styles.author}>by {article.author}</p>}

        {article.summary && (
          <p className={styles.summary}>{truncateText(article.summary, 150)}</p>
        )}

        {article.tags && article.tags.length > 0 && (
          <TagList tags={article.tags} />
        )}

        <div className={styles.footer}>
          <span className={styles.date}>
            Saved {formatDate(article.createdAt)}
          </span>
          {article.highlights && article.highlights.length > 0 && (
            <span className={styles.highlights}>
              {article.highlights.length} highlight
              {article.highlights.length !== 1 ? "s" : ""}
            </span>
          )}
        </div>
      </div>
    </div>
  );
}