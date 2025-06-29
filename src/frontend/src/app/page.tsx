"use client";

import { useState, useEffect } from "react";
import ArticleList from "@/components/ArticleList";
import AddArticleForm from "@/components/AddArticleForm";
import SearchBar from "@/components/SearchBar";
import { Article } from "@/types";
import { articleApi } from "@/lib/api";
import styles from "./page.module.scss";

export default function Home() {
  const [articles, setArticles] = useState<Article[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchTerm, setSearchTerm] = useState("");

  useEffect(() => {
    loadArticles();
  }, [searchTerm]);

  const loadArticles = async () => {
    setLoading(true);
    try {
      const data = await articleApi.getArticles(1, 20, searchTerm);
      setArticles(data);
    } catch (error) {
      console.error("Failed to load articles:", error);
    } finally {
      setLoading(false);
    }
  };

  const handleAddArticle = async (url: string) => {
    try {
      const newArticle = await articleApi.saveArticle(url);
      setArticles([newArticle, ...articles]);
    } catch (error) {
      console.error("Failed to save article:", error);
    }
  };

  const handleDeleteArticle = async (id: number) => {
    try {
      await articleApi.deleteArticle(id);
      setArticles(articles.filter(article => article.id !== id));
    } catch (error) {
      console.error("Failed to delete article:", error);
    }
  };

  return (
    <div className={styles.container}>
      <header className={styles.header}>
        <h1>OmeReader</h1>
        <p>Your personal read-later app</p>
      </header>

      <main className={styles.main}>
        <div className={styles.controls}>
          <AddArticleForm onAdd={handleAddArticle} />
          <SearchBar 
            value={searchTerm} 
            onChange={setSearchTerm} 
            placeholder="Search articles..." 
          />
        </div>

        <ArticleList 
          articles={articles} 
          loading={loading}
          onDelete={handleDeleteArticle}
          onRefresh={loadArticles}
        />
      </main>
    </div>
  );
}