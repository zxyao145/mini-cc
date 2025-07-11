"use client";

import { articleApi } from "@/lib/api";
import { Article, Tag } from "@/types";
import { useEffect, useState } from "react";
import TagManager from "@/components/TagManager";

import DOMPurify from "dompurify";

import "./client.scss";

export default function Client(params: { id: string }) {
  const [loading, setLoading] = useState(false);
  const [article, setArticle] = useState<Article>();

  const getArticle = async (id: string) => {
    setLoading(true);
    try {
      const data = await articleApi.getArticleById(id);
      const cleanHtml = DOMPurify.sanitize(data.readableContent);
      data.readableContent = cleanHtml;
      setArticle(data);
    } catch (error) {
      console.error("Failed to load articles:", error);
    } finally {
      setLoading(false);
    }
  };

  const handleTagsChange = (newTags: Tag[]) => {
    if (article) {
      setArticle({ ...article, tags: newTags });
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

  if (!article) {
    return (
      <div>
        <p>Article not found.</p>
      </div>
    );
  }

  return (
    <main className="article-detail">
      <div className="article-header">
        <h1>{article.title}</h1>
        {article.author && <p className="author">by {article.author}</p>}
        
        <TagManager 
          articleId={article.id}
          tags={article.tags || []}
          onTagsChange={handleTagsChange}
        />
      </div>
      
      <div className="readable-content" dangerouslySetInnerHTML={{ __html: article.readableContent }}></div>
    </main>
  );
}
