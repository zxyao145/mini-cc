"use client";

import ArticleCard from "./ArticleCard";
import { Article } from "@/types";
import styles from "./ArticleList.module.scss";

interface ArticleListProps {
  articles: Article[];
  loading: boolean;
  onDelete: (id: number) => Promise<void>;
  onRefresh: () => Promise<void>;
}

export default function ArticleList({ articles, loading, onDelete, onRefresh }: ArticleListProps) {
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

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <h2>My Articles ({articles.length})</h2>
        <button onClick={onRefresh} className="btn btn-secondary">
          Refresh
        </button>
      </div>
      
      <div className={styles.grid}>
        {articles.map((article) => (
          <ArticleCard
            key={article.id}
            article={article}
            onDelete={onDelete}
          />
        ))}
      </div>
    </div>
  );
}