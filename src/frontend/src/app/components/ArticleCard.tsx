"use client";

import { useEffect, useState } from "react";
import { Archive, ArchiveRestore, FileText, Heart, Highlighter, LoaderCircle, Trash2 } from "lucide-react";
import { Article, Tag } from "@/types";
import { articleApi } from "@/lib/api";
import TagManager from "../pages/article/[id]/components/TagManager";
import styles from "./ArticleCard.module.scss";
import Link from "next/link";

interface ArticleCardProps {
  article: Article;
  onDelete: (id: string) => Promise<void>;
  onUpdate?: (id: string, changes: Partial<Article>) => void;
  layout?: "grid" | "list";
}

export default function ArticleCard({ article, onDelete, onUpdate, layout = "grid" }: ArticleCardProps) {
  const [currentArticle, setCurrentArticle] = useState(article);
  const [pendingAction, setPendingAction] = useState<"favorite" | "archive" | "delete" | null>(null);
  const [error, setError] = useState("");

  useEffect(() => { setCurrentArticle(article); }, [article]);

  const handleDelete = async () => {
    if (!confirm("Are you sure you want to delete this article?")) return;
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
        ? await articleApi.toggleFavorite(article.id)
        : await articleApi.toggleArchive(article.id);
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

  const handleTagsChange = (newTags: Tag[]) => {
    setCurrentArticle(current => ({ ...current, tags: newTags }));
    onUpdate?.(article.id, { tags: newTags });
  };

  let source = "Saved article";
  try { source = new URL(currentArticle.url).hostname.replace(/^www\./, ""); } catch { /* Older saved articles may have no URL. */ }

  const favoriteLabel = currentArticle.isFavorite ? "Remove from favorites" : "Add to favorites";
  const archiveLabel = currentArticle.isArchived ? "Unarchive article" : "Archive article";

  return (
    <div className={`${styles.card} ${layout === "list" ? styles.list : ""}`}>
      {currentArticle.imageUrl && (
        <div className={styles.imageContainer}>
          <img src={currentArticle.imageUrl} alt="" className={styles.image} loading="lazy" />
        </div>
      )}
      <div className={styles.content}>
        <div className={styles.source}>
          <span className={styles.sourceIcon}><FileText size={16} strokeWidth={1.7} aria-hidden="true" /></span>
          <span className={styles.sourceName}>{source}</span>
          {currentArticle.isArchived && <span className={styles.archiveLabel}>Archived</span>}
        </div>
        <h3 className={styles.title}>
          <Link href={`/pages/article/${currentArticle.id}`}>{currentArticle.title}</Link>
        </h3>
        {currentArticle.author && <p className={styles.author}>by {currentArticle.author}</p>}
        {currentArticle.summary && <p className={styles.summary}>{currentArticle.summary}</p>}
        <TagManager articleId={currentArticle.id} tags={currentArticle.tags || []} onTagsChange={handleTagsChange} compact={true} />
        <div className={styles.footer}>
          <div className={styles.metadata}>
            <time dateTime={currentArticle.createdAt} title={`Saved ${new Date(currentArticle.createdAt).toLocaleDateString()}`}>
              {new Date(currentArticle.createdAt).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })}
            </time>
            {!!currentArticle.highlights?.length && <span className={styles.highlights}><Highlighter size={12} aria-hidden="true" />{currentArticle.highlights.length} highlight{currentArticle.highlights.length === 1 ? "" : "s"}</span>}
          </div>
          <div className={styles.actions} role="group" aria-label={`Actions for ${currentArticle.title}`}>
            <button onClick={() => handleToggle("favorite")} disabled={pendingAction !== null} className={`${styles.actionBtn} ${currentArticle.isFavorite ? styles.favorited : ""}`} aria-label={favoriteLabel} aria-pressed={currentArticle.isFavorite} title={favoriteLabel}>
              {pendingAction === "favorite" ? <LoaderCircle size={17} className={styles.spinning} aria-hidden="true" /> : <Heart size={17} strokeWidth={1.7} fill={currentArticle.isFavorite ? "currentColor" : "none"} aria-hidden="true" />}
            </button>
            <button onClick={() => handleToggle("archive")} disabled={pendingAction !== null} className={`${styles.actionBtn} ${currentArticle.isArchived ? styles.archived : ""}`} aria-label={archiveLabel} aria-pressed={currentArticle.isArchived} title={archiveLabel}>
              {pendingAction === "archive" ? <LoaderCircle size={17} className={styles.spinning} aria-hidden="true" /> : currentArticle.isArchived ? <ArchiveRestore size={17} strokeWidth={1.7} aria-hidden="true" /> : <Archive size={17} strokeWidth={1.7} aria-hidden="true" />}
            </button>
            <button onClick={handleDelete} disabled={pendingAction !== null} className={`${styles.actionBtn} ${styles.deleteBtn}`} aria-label="Delete article" title="Delete article">
              {pendingAction === "delete" ? <LoaderCircle size={17} className={styles.spinning} aria-hidden="true" /> : <Trash2 size={17} strokeWidth={1.7} aria-hidden="true" />}
            </button>
          </div>
        </div>
        {error && <p className={styles.error} role="alert">{error}</p>}
      </div>
    </div>
  );
}
