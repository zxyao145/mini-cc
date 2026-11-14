"use client";

import { BookOpen } from "lucide-react";
import ArticleCard from "./ArticleCard";
import { ArticleListItem } from "@/types";
import styles from "./ArticleList.module.scss";

interface ArticleListProps {
  articles: ArticleListItem[];
  loading: boolean;
  onDelete: (id: string) => Promise<void>;
  onUpdate?: (id: string, changes: Partial<ArticleListItem>) => void;
  articleStyle: string;
  selectionMode?: boolean; selected?: string[]; selectionDisabled?: boolean; onSelect?: (id: string) => void;
}

export default function ArticleList({
  articles,
  loading,
  onDelete,
  onUpdate,
  articleStyle, selectionMode, selected, selectionDisabled, onSelect,
}: ArticleListProps) {
  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading} role="status">
          <div className={styles.spinner} aria-hidden="true"></div>
          <p>正在加载文章…</p>
        </div>
      </div>
    );
  }

  if (articles.length === 0) {
    return (
      <div className={styles.container}>
        <div className={styles.empty}>
          <BookOpen size={32} strokeWidth={1.5} className={styles.emptyIcon} aria-hidden="true" />
          <h3>没有找到文章</h3>
          <p>使用浏览器插件保存文章，在这里阅读和管理。</p>
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
            selectionMode={selectionMode} selected={selected?.includes(article.id)} selectionDisabled={selectionDisabled} onSelect={onSelect}
            layout={articleStyle === "list" ? "list" : "grid"}
            onDelete={onDelete}
            onUpdate={onUpdate}
          />
        ))}
      </div>
    </div>
  );
}
