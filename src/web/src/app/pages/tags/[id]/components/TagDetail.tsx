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
import { Button } from "@/components/arc/button/button";
import { useConfirm } from "@/contexts/ConfirmContext";
import styles from "./TagDetail.module.css";

interface TagDetailProps {
  tagId: string;
}

export default function TagDetail({ tagId }: TagDetailProps) {
  const [tag, setTag] = useState<TagWithArticleCount | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const router = useRouter();
  const toast = useToast();
  const confirm = useConfirm();
  const list = useArticleList(parseLibraryQuery(new URLSearchParams({ view: "all", tags: tagId })));

  const loadTag = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const tagData = await tagApi.getTags();
      setTag(tagData.find(tag => tag.id === tagId) || null);
    } catch (err) {
      console.error("Error loading tag:", err);
      setError("标签详情加载失败");
    } finally {
      setLoading(false);
    }
  }, [tagId]);

  useEffect(() => {
    loadTag();
  }, [tagId, loadTag]);

  const handleDeleteTag = async () => {
    if (!tag) return;
    
    if (await confirm(`删除“${tag.name}”标签？它会从所有文章中移除。`, { title: "删除标签", confirmLabel: "删除标签" })) {
      try {
        await tagApi.deleteTag(tag.id);
        router.push("/pages/tags");
      } catch (err) {
        console.error("Error deleting tag:", err);
        toast.show("标签删除失败，请重试。");
      }
    }
  };

  if (loading) {
    return <div className={styles.container}><p className={styles.state}>正在加载标签详情…</p></div>;
  }

  if (error || !tag) {
    return (
      <div className={styles.container}>
        <section className={styles.error}>
          <h1>无法打开标签</h1>
          <p>{error || "找不到这个标签。"}</p>
          <Button variant="secondary" onClick={() => router.back()}><ArrowLeft size={16} aria-hidden="true" />返回</Button>
        </section>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <header className={styles.header}>
        <div className={styles.headerContent}>
          <div className={styles.tagInfo}>
            <div className={styles.tagIcon}>
              <span className={styles.tagColor} style={{ backgroundColor: tag.color }} aria-hidden="true" />
              <h1 className={styles.tagName}>{tag.name}</h1>
            </div>
            <p className={styles.tagStats}>{list.total} 篇文章</p>
          </div>

          <div className={styles.actions}>
            <Button variant="secondary" onClick={() => router.back()}>
              <ArrowLeft size={16} aria-hidden="true" />返回标签
            </Button>
            <Button variant="danger" onClick={() => void handleDeleteTag()}>
              <Trash2 size={16} aria-hidden="true" />删除标签
            </Button>
          </div>
        </div>
      </header>

      <div className={styles.main}>
        <p className={styles.listStatus} role="status">
          {list.hasMore ? `共 ${list.total} 篇，已加载 ${list.items.length} 篇` : `共 ${list.total} 篇`}
        </p>
        <ArticleList articles={list.items} loading={list.loading} onDelete={async id => { await articleApi.deleteArticle(id); list.removeItems([id]); toast.deleted([id]); }} onUpdate={list.updateItem} articleStyle="list" />
        {list.error && <p role="alert" className={styles.errorMessage}>{list.error}</p>}
        {(list.hasMore || list.error) && (
          <Button variant="secondary" disabled={list.loadingMore} onClick={list.loadMore}>
            {list.loadingMore ? '正在加载…' : list.error ? '加载失败，重试' : '加载更多'}
          </Button>
        )}
      </div>
    </div>
  );
}
