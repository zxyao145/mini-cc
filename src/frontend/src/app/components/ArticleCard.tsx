"use client";

import { useEffect, useState } from "react";
import { Archive, ArchiveRestore, BookOpen, CheckCheck, Chrome, FileText, Rss, RotateCcw, Heart, Highlighter, LoaderCircle, Trash2 } from "lucide-react";
import { Article, ArticleListItem, Tag } from "@/types";
import { useToast } from "@/contexts/ToastContext";
import { notifyLibraryMutation } from "@/lib/library-mutations";
import { articleApi } from "@/lib/api";
import TagManager from "../pages/article/[id]/components/TagManager";
import styles from "./ArticleCard.module.scss";
import Link from "next/link";

interface ArticleCardProps {
  article: ArticleListItem | Article;
  onDelete: (id: string) => Promise<void>;
  onUpdate?: (id: string, changes: Partial<ArticleListItem>) => void;
  layout?: "grid" | "list";
  selectionMode?: boolean; selected?: boolean; selectionDisabled?: boolean; onSelect?: (id: string) => void;
}

export default function ArticleCard({ article, onDelete, onUpdate, layout = "grid", selectionMode, selected, selectionDisabled, onSelect }: ArticleCardProps) {
  const toast = useToast();
  const [currentArticle, setCurrentArticle] = useState(article);
  const [pendingAction, setPendingAction] = useState<"favorite" | "archive" | "read" | "restore" | "delete" | null>(null);
  const [error, setError] = useState("");

  useEffect(() => { setCurrentArticle(article); }, [article]);

  const handleDelete = async () => {
    if (!confirm("将这篇文章移入回收站？")) return;
    setPendingAction("delete");
    setError("");
    try {
      await onDelete(article.id);
    } catch {
      setError("Could not delete this article. Please try again.");
    } finally {
      setPendingAction(null);
    }
  };

  const handleToggle = async (action: "favorite" | "archive") => {
    setPendingAction(action);
    setError("");
    try {
      const updatedArticle = action === "favorite"
        ? await articleApi.updateState(article.id, { isFavorite: !currentArticle.isFavorite })
        : await articleApi.updateState(article.id, { isArchived: !currentArticle.isArchived });
      const changes = action === "favorite"
        ? { isFavorite: updatedArticle.isFavorite }
        : { isArchived: updatedArticle.isArchived };
      setCurrentArticle(current => ({ ...current, ...changes }));
      onUpdate?.(article.id, changes);
    } catch {
      setError(`Could not update ${action === "favorite" ? "favorites" : "archive"}. Please try again.`);
    } finally {
      setPendingAction(null);
    }
  };

  const handleReading = async () => {
    setPendingAction("read"); setError("");
    try {
      const updated = await articleApi.updateState(article.id, { readingStatus: currentArticle.readingStatus === "read" ? "unread" : "read" });
      setCurrentArticle(current => ({ ...current, ...updated })); onUpdate?.(article.id, updated);
    } catch { setError("阅读状态更新失败，请重试。"); } finally { setPendingAction(null); }
  };
  const handlePurge = async () => {
    if (!confirm("将永久删除这篇文章，无法恢复。")) return;
    setPendingAction("delete"); setError("");
    try {
      const result = await articleApi.batch([article.id], 'purge');
      if (result.failed.length) throw new Error('purge failed');
      notifyLibraryMutation({ ids: result.succeeded, remove: true });
      toast.show('已永久删除 1 篇文章');
    } catch { setError("永久删除失败，请重试。"); } finally { setPendingAction(null); }
  };
  const handleRestore = async () => {
    setPendingAction("restore"); setError("");
    try { const updated = await articleApi.restoreArticle(article.id); onUpdate?.(article.id, updated); }
    catch { setError("恢复失败，请重试。"); } finally { setPendingAction(null); }
  };
  const highlightCount = "highlightCount" in currentArticle ? currentArticle.highlightCount : currentArticle.highlights.length;
  const fromExtension = currentArticle.savedFromExtension;
  const feeds = currentArticle.feeds || [];
  const handleTagsChange = (newTags: Tag[]) => {
    setCurrentArticle(current => ({ ...current, tags: newTags }));
    onUpdate?.(article.id, { tags: newTags });
  };

  let source = "Saved article";
  try { source = new URL(currentArticle.url).hostname.replace(/^www\./, ""); } catch { /* Older saved articles may have no URL. */ }

  const favoriteLabel = currentArticle.isFavorite ? "取消收藏" : "加入收藏";
  const archiveLabel = currentArticle.isArchived ? "移回收件箱" : "归档文章";

  return (
    <div data-article-id={article.id} className={`${styles.card} ${layout === "list" ? styles.list : ""}`}>
      {onSelect && <label className={`${styles.selection} ${selectionMode ? styles.selectionVisible : ''}`}>
        <input type="checkbox" aria-label={`选择 ${currentArticle.title}`} checked={!!selected} disabled={selectionDisabled} onChange={() => onSelect(article.id)} />
      </label>}
      {currentArticle.imageUrl && (
        <div className={styles.imageContainer}>
          <img src={currentArticle.imageUrl} alt="" className={styles.image} loading="lazy" />
        </div>
      )}
      <div className={styles.content}>
        <div className={styles.source}>
          <span className={styles.sourceIcon}>{fromExtension ? <Chrome size={16} aria-label="浏览器收藏" /> : feeds.length ? <Rss size={16} aria-label="RSS" /> : <FileText size={16} aria-hidden="true" />}</span>
          <span className={styles.sourceName}>{source}</span>
          {currentArticle.isArchived && <span className={styles.archiveLabel}>已归档</span>}
        </div>
        <h3 className={styles.title}>
          {currentArticle.deletedAt ? currentArticle.title : <Link href={`/pages/article/${currentArticle.id}`} scroll={false}>{currentArticle.title}</Link>}
        </h3>
        <div className={styles.readingState}>
          {currentArticle.readingStatus === "unread" ? <span><span className={styles.unreadDot} aria-hidden="true" />未读</span> : currentArticle.readingStatus === "reading" ? <span>已读 {Math.round(currentArticle.progressPercent || 0)}%</span> : <span>已读{currentArticle.readAt ? ` · ${new Date(currentArticle.readAt).toLocaleDateString("zh-CN", { month: "long", day: "numeric" })}` : ""}</span>}
          {currentArticle.contentKind === "summaryOnly" && <span className={styles.archiveLabel}>仅摘要</span>}
        </div>
        {currentArticle.deletedAt && <p className={styles.trashInfo}>删除于 {new Date(currentArticle.deletedAt).toLocaleDateString("zh-CN")} · {Math.max(0, toast.trashRetentionDays - Math.floor((Date.now() - Date.parse(currentArticle.deletedAt)) / 86400000))} 天后永久删除</p>}
        {currentArticle.author && <p className={styles.author}>by {currentArticle.author}</p>}
        {currentArticle.summary && <p className={styles.summary}>{currentArticle.summary}</p>}
        {!currentArticle.deletedAt && <TagManager articleId={currentArticle.id} tags={currentArticle.tags || []} onTagsChange={handleTagsChange} compact={true} />}
        <div className={styles.footer}>
          <div className={styles.metadata}>
            <time dateTime={currentArticle.createdAt} title={`保存时间：${new Date(currentArticle.createdAt).toLocaleDateString()}`}>
              {new Date(currentArticle.createdAt).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })}
            </time>
            {!!highlightCount && <span className={styles.highlights}><Highlighter size={12} aria-hidden="true" />{highlightCount} 处高亮</span>}
          </div>
          <div hidden={selectionMode} className={styles.actions} role="group" aria-label={`Actions for ${currentArticle.title}`}>
            {currentArticle.deletedAt ? <><button onClick={handleRestore} disabled={pendingAction !== null} className={styles.actionBtn} aria-label="恢复文章" title="恢复文章">{pendingAction === "restore" ? <LoaderCircle size={17} className={styles.spinning} aria-hidden="true" /> : <RotateCcw size={17} aria-hidden="true" />}</button><button onClick={handlePurge} disabled={pendingAction !== null} className={styles.actionBtn} aria-label="永久删除文章" title="永久删除文章"><Trash2 size={17} aria-hidden="true" /></button></> : <>
            <button onClick={handleReading} disabled={pendingAction !== null} className={styles.actionBtn} aria-label={currentArticle.readingStatus === "read" ? "标记未读" : "标记已读"} title={currentArticle.readingStatus === "read" ? "标记未读" : "标记已读"}>{pendingAction === "read" ? <LoaderCircle size={17} className={styles.spinning} aria-hidden="true" /> : currentArticle.readingStatus === "read" ? <BookOpen size={17} aria-hidden="true" /> : <CheckCheck size={17} aria-hidden="true" />}</button>
            <button onClick={() => handleToggle("favorite")} disabled={pendingAction !== null} className={`${styles.actionBtn} ${currentArticle.isFavorite ? styles.favorited : ""}`} aria-label={favoriteLabel} aria-pressed={currentArticle.isFavorite} title={favoriteLabel}>
              {pendingAction === "favorite" ? <LoaderCircle size={17} className={styles.spinning} aria-hidden="true" /> : <Heart size={17} strokeWidth={1.7} fill={currentArticle.isFavorite ? "currentColor" : "none"} aria-hidden="true" />}
            </button>
            <button onClick={() => handleToggle("archive")} disabled={pendingAction !== null} className={`${styles.actionBtn} ${currentArticle.isArchived ? styles.archived : ""}`} aria-label={archiveLabel} aria-pressed={currentArticle.isArchived} title={archiveLabel}>
              {pendingAction === "archive" ? <LoaderCircle size={17} className={styles.spinning} aria-hidden="true" /> : currentArticle.isArchived ? <ArchiveRestore size={17} strokeWidth={1.7} aria-hidden="true" /> : <Archive size={17} strokeWidth={1.7} aria-hidden="true" />}
            </button>
            <button onClick={handleDelete} disabled={pendingAction !== null} className={`${styles.actionBtn} ${styles.deleteBtn}`} aria-label="删除文章" title="删除文章">
              {pendingAction === "delete" ? <LoaderCircle size={17} className={styles.spinning} aria-hidden="true" /> : <Trash2 size={17} strokeWidth={1.7} aria-hidden="true" />}
            </button>
            </>}
          </div>
        </div>
        {error && <p className={styles.error} role="alert">{error}</p>}
      </div>
    </div>
  );
}
