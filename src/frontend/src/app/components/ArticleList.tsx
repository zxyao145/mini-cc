"use client";

import { BookOpen } from "lucide-react";
import ArticleCard from "./ArticleCard";
import { Article } from "@/types";
import styles from "./ArticleList.module.scss";

interface ArticleListProps {
  articles: Article[];
  loading: boolean;
  onDelete: (id: string) => Promise<void>;
  onUpdate?: (id: string, changes: Partial<Article>) => void;
  articleStyle: string;
}

export default function ArticleList({
  articles,
  loading,
  onDelete,
  onUpdate,
  articleStyle,
}: ArticleListProps) {
  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading} role="status">
          <div className={styles.spinner} aria-hidden="true"></div>
          <p>Loading articles...</p>
        </div>
      </div>
    );
  }

  if (articles.length === 0) {
    return (
      <div className={styles.container}>
        <div className={styles.empty}>
          <BookOpen size={32} strokeWidth={1.5} className={styles.emptyIcon} aria-hidden="true" />
          <h3>No articles found</h3>
          <p>Save articles using the Chrome extension, then read and manage them here.</p>
        </div>
      </div>
    );
  }

  const className = articleStyle == "grid"
   ? styles.articleGrid 
   : styles.articleList;


  return (
    <div>
      <div className={className}>
        {articles.map((article) => (
          <ArticleCard
            key={article.id}
            article={article}
            layout={articleStyle === "list" ? "list" : "grid"}
            onDelete={onDelete}
            onUpdate={onUpdate}
          />
        ))}
      </div>
    </div>
  );
}
