"use client";

import ArticleCard from "./ArticleCard";
import { Article } from "@/types";
import styles from "./ArticleList.module.scss";

interface ArticleListProps {
  articles: Article[];
  loading: boolean;
  onDelete: (id: string) => Promise<void>;
  onUpdate?: (article: Article) => void;
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
        <div className={styles.loading}>
          <div className={styles.spinner}></div>
          <p>Loading articles...</p>
        </div>
      </div>
    );
  }

  if (articles.length === 0) {
    return (
      <div className={styles.container}>
        <div className={styles.empty}>
          <h3>No articles found</h3>
          <p>Start by saving your first article using the form above!</p>
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
            onDelete={onDelete}
            onUpdate={onUpdate}
          />
        ))}
      </div>
    </div>
  );
}
