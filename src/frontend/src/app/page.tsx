"use client";

import { useState, useEffect, useCallback } from "react";
import ArticleList from "@/app/components/ArticleList";
import SearchBar from "@/app/components/HomeHeader";
import { Article } from "@/types";
import { articleApi } from "@/lib/api";
import styles from "./page.module.scss";

export default function Home() {
  const [articles, setArticles] = useState<Article[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchTerm, setSearchTerm] = useState("");
  const [articleStyle, setArticleStyle] = useState("grid");

  const loadArticles = useCallback(async () => {
    setLoading(true);
    try {
      const data = await articleApi.getArticles(1, 20, searchTerm);
      setArticles(data);
    } catch (error) {
      console.error("Failed to load articles:", error);
    } finally {
      setLoading(false);
    }
  }, [searchTerm]);
 
  useEffect(() => {
    loadArticles();
  }, [searchTerm, loadArticles]);


  const handleDeleteArticle = async (id: string) => {
    await articleApi.deleteArticle(id);
    setArticles(current => current.filter(article => article.id !== id));
  };

  const handleUpdateArticle = (id: string, changes: Partial<Article>) => {
    setArticles(current => current.map(article =>
      article.id === id ? { ...article, ...changes } : article
    ));
  };

  return (
    <div className={styles.container}>
      <header className={styles.header}>
        <SearchBar 
            value={searchTerm} 
            onChange={setSearchTerm} 
            onStyleChange={setArticleStyle}
            placeholder="Search articles..." 
          />
      </header>

      <div className={styles.main}>
        <div className={styles.sectionHeading}>
          <h1>{searchTerm ? "Search results" : "All articles"}</h1>
          <span className={styles.count} role="status">{loading ? "Loading…" : `${articles.length} article${articles.length === 1 ? "" : "s"}`}</span>
        </div>
        <ArticleList 
          articles={articles} 
          loading={loading}
          onDelete={handleDeleteArticle}
          onUpdate={handleUpdateArticle}
          articleStyle={articleStyle}
        />
      </div>
    </div>
  );
}