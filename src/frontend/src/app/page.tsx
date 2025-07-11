"use client";

import { useState, useEffect, useCallback } from "react";
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


  const handleAddArticle = async (url: string) => {
    try {
      const newArticle = await articleApi.saveArticle(url);
      setArticles([newArticle, ...articles]);
    } catch (error) {
      console.error("Failed to save article:", error);
    }
  };

  const handleDeleteArticle = async (id: string) => {
    try {
      await articleApi.deleteArticle(id);
      setArticles(articles.filter(article => article.id !== id));
    } catch (error) {
      console.error("Failed to delete article:", error);
    }
  };

  const handleUpdateArticle = (updatedArticle: Article) => {
    setArticles(articles.map(article => 
      article.id === updatedArticle.id ? updatedArticle : article
    ));
  };

  return (
    <div className={styles.container}>
      <header className={styles.header}>
        <SearchBar 
            value={searchTerm} 
            onChange={setSearchTerm} 
            placeholder="Search articles..." 
          />
      </header>

      <main className={styles.main}>
        <div className={styles.controls}>
          <AddArticleForm onAdd={handleAddArticle} />
        </div>

        <ArticleList 
          articles={articles} 
          loading={loading}
          onDelete={handleDeleteArticle}
          onRefresh={loadArticles}
          onUpdate={handleUpdateArticle}
        />
      </main>
    </div>
  );
}