"use client";

import { useState, useEffect, useCallback } from "react";
import { useRouter } from "next/navigation";
import { TagWithArticles } from "@/types";
import { tagApi } from "@/lib/api";
import ArticleSummaryCard from "@/components/ArticleSummaryCard";
import styles from "./TagDetail.module.scss";

interface TagDetailProps {
  tagId: string;
}

export default function TagDetail({ tagId }: TagDetailProps) {
  const [tag, setTag] = useState<TagWithArticles | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const router = useRouter();

  const loadTag = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const tagData = await tagApi.getTagById(tagId);
      setTag(tagData);
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
              {tag.articles.length} article{tag.articles.length !== 1 ? 's' : ''}
            </p>
          </div>
          
          <div className={styles.actions}>
            <button
              onClick={() => router.back()}
              className={styles.backButton}
            >
              ← Back to Tags
            </button>
            <button
              onClick={handleDeleteTag}
              className={styles.deleteButton}
              title="Delete tag"
            >
              Delete Tag
            </button>
          </div>
        </div>
      </header>

      <main className={styles.main}>
        {tag.articles.length === 0 ? (
          <div className={styles.empty}>
            <h2>No articles found</h2>
            <p>This tag doesn&apos;t have any articles associated with it yet.</p>
          </div>
        ) : (
          <div className={styles.articlesList}>
            <h2 className={styles.sectionTitle}>Articles ({tag.articles.length})</h2>
            <div className={styles.articles}>
              {tag.articles.map((article) => (
                <ArticleSummaryCard key={article.id} article={article} />
              ))}
            </div>
          </div>
        )}
      </main>
    </div>
  );
}