"use client";

import { useState, useEffect, useCallback } from "react";
import { useRouter } from "next/navigation";
import { ArrowLeft, Trash2 } from "lucide-react";
import { TagWithArticleCount } from "@/types";
import { tagApi } from "@/lib/api";
import ArticleList from "@/app/components/ArticleList";
import { useArticleList } from "@/hooks/useArticleList";
import { parseLibraryQuery } from "@/lib/library-query";
import { articleApi } from "@/lib/api";
import { useToast } from "@/contexts/ToastContext";
import styles from "./TagDetail.module.scss";

interface TagDetailProps {
  tagId: string;
}

export default function TagDetail({ tagId }: TagDetailProps) {
  const [tag, setTag] = useState<TagWithArticleCount | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const router = useRouter();
  const toast = useToast();
  const list = useArticleList(parseLibraryQuery(new URLSearchParams({ view: "all", tags: tagId })));

  const loadTag = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const tagData = await tagApi.getTags();
      setTag(tagData.find(tag => tag.id === tagId) || null);
    } catch (err) {
      console.error("Error loading tag:", err);
      setError("Failed to load tag details");
    } finally {
      setLoading(false);
    }
  }, [tagId]);

  useEffect(() => {
    loadTag();
  }, [tagId, loadTag]);

  const handleDeleteTag = async () => {
    if (!tag) return;
    
    if (confirm(`Are you sure you want to delete the tag "${tag.name}"? This will remove it from all articles.`)) {
      try {
        await tagApi.deleteTag(tag.id);
        router.push("/pages/tags");
      } catch (err) {
        console.error("Error deleting tag:", err);
        alert("Failed to delete tag");
      }
    }
  };

  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>Loading tag details...</div>
      </div>
    );
  }

  if (error || !tag) {
    return (
      <div className={styles.container}>
        <div className={styles.error}>
          <h2>Error</h2>
          <p>{error || "Tag not found"}</p>
          <button onClick={() => router.back()} className={styles.backButton}>
            Go Back
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <header className={styles.header}>
        <div className={styles.headerContent}>
          <div className={styles.tagInfo}>
            <div className={styles.tagIcon}>
              <div 
                className={styles.tagColor} 
                style={{ backgroundColor: tag.color }}
              />
              <h1 className={styles.tagName}>{tag.name}</h1>
            </div>
            <p className={styles.tagStats}>
              {list.total} article{list.total !== 1 ? 's' : ''}
            </p>
          </div>
          
          <div className={styles.actions}>
            <button
              onClick={() => router.back()}
              className={styles.backButton}
            >
              <ArrowLeft size={14} aria-hidden="true" /> Back to Tags
            </button>
            <button
              onClick={handleDeleteTag}
              className={styles.deleteButton}
              title="Delete tag"
            >
              <Trash2 size={14} aria-hidden="true" /> Delete Tag
            </button>
          </div>
        </div>
      </header>

      <main className={styles.main}>
        <p role="status">{list.hasMore ? `共 ${list.total} 篇，已加载 ${list.items.length} 篇` : `共 ${list.total} 篇`}</p>
        <ArticleList articles={list.items} loading={list.loading} onDelete={async id => { await articleApi.deleteArticle(id); list.removeItems([id]); toast.deleted([id]); }} onUpdate={list.updateItem} articleStyle="list" />
        {list.error && <p role="alert">{list.error}</p>}
        {(list.hasMore || list.error) && <button type="button" className={styles.backButton} disabled={list.loadingMore} onClick={list.loadMore}>{list.loadingMore ? '正在加载…' : list.error ? '加载失败，重试' : '加载更多'}</button>}
      </main>
    </div>
  );
}