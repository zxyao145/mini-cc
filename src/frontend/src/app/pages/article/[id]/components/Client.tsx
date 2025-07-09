"use client";

import { articleApi } from "@/lib/api";
import { Article } from "@/types";
import { useEffect, useState } from "react";

import DOMPurify from "dompurify";

import "./client.scss";

export default function Client(params: { id: string }) {
  const [loading, setLoading] = useState(false);
  const [article, setArticles] = useState<Article>();

  const getArticle = async (id: string) => {
    setLoading(true);
    try {
      const data = await articleApi.getArticleById(id);
      const cleanHtml = DOMPurify.sanitize(data.readableContent);
      data.readableContent = cleanHtml;
      setArticles(data);
    } catch (error) {
      console.error("Failed to load articles:", error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    getArticle(params.id);
  }, [params.id]);

  if (loading) {
    return (
      <div>
        <p>Loading article...</p>
      </div>
    );
  }

  return (
    <main className="article-detail">
      <h1>{article?.title}</h1>
      <div className="readable-content" dangerouslySetInnerHTML={{ __html: article?.readableContent ?? "" }}></div>
    </main>
  );
}
