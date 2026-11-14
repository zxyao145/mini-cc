"use client";
import { useToast } from "@/contexts/ToastContext";
import { trashArticleConfirmation, useConfirm } from "@/contexts/ConfirmContext";
import { useTheme } from "@/contexts/ThemeContext";

import { ExternalLink, X } from "lucide-react";
import { Button } from "@/components/arc/button/button";
import { Tooltip } from "@/components/arc/tooltip/tooltip";
import { articleApi } from "@/lib/api";
import { articleHostname } from "@/lib/article-source";
import { updateCachedArticle } from "@/lib/library-cache";
import { Article, ArticleStatePatch, Tag, Highlight } from "@/types";
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import TagManager from "@/app/pages/article/[id]/components/TagManager";
import HighlightManager from "@/app/pages/article/[id]/components/HighlightManager";
import HighlightList from "@/app/pages/article/[id]/components/HighlightList";

import { useRouter } from "next/navigation";
import { useReadingProgress } from "@/hooks/useReadingProgress";
import { currentLibraryKey } from "@/lib/library-cache";
import ReaderToolbar from "./ReaderToolbar";
import ReadableContent from "./ReadableContent";
import ReaderSettings from "./ReaderSettings";
import { ReaderTocButton, ReaderTocSidebar, useReaderToc } from "./ReaderToc";
import { useReaderPreferences } from "@/contexts/ReaderPreferencesContext";
import { readerPreferenceStyle } from "@/lib/reader-preferences";

import DOMPurify from "dompurify";
import styles from "./Client.module.css";
//import Prism from 'prismjs';

////导入tomorrow代码主题样式（按需引入）
//import 'prismjs/themes/prism-tomorrow.css';

////导入行号效果css、js文件（按需引入）
//import "prismjs/plugins/line-numbers/prism-line-numbers.min.css";
//import "prismjs/plugins/line-numbers/prism-line-numbers.min.js";


export default function Client(params: { id: string }) {
  //useEffect(() => {
  //  Prism.highlightAll();
  //}, []);

  const [loading, setLoading] = useState(false);
  const [article, setArticle] = useState<Article>();

  const router = useRouter();
  const toast = useToast();
  const confirm = useConfirm();
  const { theme } = useTheme();
  const contentRef = useRef<HTMLDivElement>(null);
  const [pending, setPending] = useState(false);
  const [actionError, setActionError] = useState("");
  const [endDismissed, setEndDismissed] = useState(false);
  const isLink = article?.contentKind === 'link';
  const patchArticle = useCallback((changes: Partial<Article>) => setArticle(current => current ? {
    ...current, ...changes,
    // List/progress acknowledgements cannot change the version or kind of the body currently rendered.
    contentKind: changes.readableContent === undefined ? current.contentKind : changes.contentKind ?? current.contentKind,
    contentRevision: changes.readableContent === undefined ? current.contentRevision : changes.contentRevision ?? current.contentRevision,
  } : current), []);
  const progress = useReadingProgress(isLink ? undefined : article, contentRef, patchArticle);
  const { preferences, beforeChange } = useReaderPreferences();
  const toc = useReaderToc(contentRef, isLink ? undefined : article?.readableContent);
  useLayoutEffect(() => beforeChange(progress.preserveLayout), [beforeChange, progress.preserveLayout]);
  const back = () => {
    let fromLibrary = false;
    try { fromLibrary = history.length > 1 && sessionStorage.getItem(currentLibraryKey) !== null; } catch {}
    if (document.referrer && new URL(document.referrer).origin === location.origin) fromLibrary = true;
    if (fromLibrary) router.back(); else router.push("/");
  };
  const changeState = async (patch: ArticleStatePatch) => {
    if (!article) return;
    setPending(true); setActionError("");
    try {
      await progress.withStateChange(async () => {
        const updated = await articleApi.updateState(article.id, patch);
        progress.observeContent({ contentKind: updated.contentKind });
        patchArticle(updated); updateCachedArticle(article.id, updated);
      }, patch.readingStatus === "unread");
    } catch { setActionError("文章状态更新失败，请重试。"); }
    finally { setPending(false); }
  };
  const reset = async () => {
    setPending(true); setActionError("");
    try { await progress.reset(); } catch { setActionError("重置进度失败，请重试。"); }
    finally { setPending(false); }
  };
  const remove = async () => {
    if (!article || !(await confirm(...trashArticleConfirmation))) return;
    setPending(true); setActionError("");
    try {
      await articleApi.deleteArticle(article.id);
      updateCachedArticle(article.id, { deletedAt: new Date().toISOString() });
      toast.deleted([article.id]);
      router.push("/");
    } catch { setActionError("删除失败，请重试。"); setPending(false); }
  };

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
      updateCachedArticle(article.id, { tags: newTags });
    }
  };

  const handleHighlightsChange = (newHighlights: Highlight[]) => {
    if (article) {
      setArticle(current => current ? { ...current, highlights: newHighlights } : current);
      updateCachedArticle(article.id, { highlightCount: newHighlights.length });
    }
  };

  useEffect(() => {
    getArticle(params.id);
  }, [params.id]);

  if (loading) {
    return (
      <div className={styles.reader}>
        <p>正在加载文章…</p>
      </div>
    );
  }

  if (!article) {
    return (
      <div className={styles.reader}>
        <p>找不到这篇文章。</p>
      </div>
    );
  }

  const linkHost = isLink ? articleHostname(article.url) : undefined;

  return (
    <main className={`${styles.reader} reader article-detail${toc.headings.length ? ` ${styles.hasToc}` : ""}`} style={readerPreferenceStyle(preferences)} data-reader-theme={preferences.theme === "system" ? theme : preferences.theme}>
      <ReaderToolbar article={article} pending={pending} syncStatus={progress.syncStatus} onBack={back} onState={changeState} onReset={isLink ? undefined : reset} onDelete={remove} extra={isLink ? undefined : <><ReaderSettings /><ReaderTocButton toc={toc} /></>} />
      {actionError && <p className={styles.actionError} role="alert">{actionError}</p>}
      {progress.contentChanged && <div className={styles.conflict} role="alert"><span>正文已更新，请刷新后继续阅读。旧正文的进度已暂停同步。</span><Button variant="secondary" size="sm" onClick={() => { sessionStorage.removeItem(`progress:${article.id}`); window.location.reload(); }}>刷新正文</Button></div>}
      {!progress.contentChanged && progress.conflict && <div className={styles.conflict} role="alert">
        <span>已在其他设备读到 {Math.round(progress.conflict.percent || 0)}%</span>
        <Button variant="secondary" size="sm" onClick={() => progress.chooseConflict(true)}>跳转</Button>
        <Button variant="ghost" size="sm" onClick={() => progress.chooseConflict(false)}>留在这里</Button>
      </div>}
      {!isLink && <div className={styles.progress} role="progressbar" aria-label="阅读进度" aria-valuemin={0} aria-valuemax={100} aria-valuenow={Math.round(progress.percent)}><span style={{ width: `${progress.percent}%` }} /></div>}
      <div className={styles.articleHeader}>
        <div className={`${styles.titleSection} article-title-section`}>
          <h1>{article.title}</h1>
          {isLink ? <p className={`${styles.source} reader-source`}>{linkHost && `${linkHost} · `}保存于 {new Date(article.createdAt).toLocaleDateString("zh-CN")}</p> : <>
            {article.author && <p className={styles.author}>作者：{article.author}</p>}
            {!!article.feeds?.length && <p className={`${styles.source} reader-source`}>来自 {article.feeds.map(f => f.title).join('，')}{article.publishedAt && ` · 发布于 ${new Date(article.publishedAt).toLocaleDateString("zh-CN")}`}</p>}
          </>}
        </div>
        
        <div className={styles.articleActions}>
          <a 
            href={article.url} 
            target="_blank" 
            rel="noopener noreferrer"
            className={`${styles.visitOriginal}${isLink ? ` ${styles.primary} primary` : ""} visit-original-btn`}
          >
            <ExternalLink size={16} aria-hidden="true" />
            <span className={styles.text}>访问原文</span>
          </a>
        </div>
        
        <TagManager 
          articleId={article.id}
          tags={article.tags || []}
          onTagsChange={handleTagsChange}
        />
        {isLink && <div className={styles.linkDetail}>
          {article.imageUrl && <>
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img className={styles.linkCover} src={article.imageUrl} alt="" />
          </>}
          {article.summary && <p className={styles.linkSummary}>{article.summary}</p>}
        </div>}
      </div>
      
      {article.contentKind === 'summaryOnly' && <div className={`${styles.summaryBanner} reader-summary-banner`}><strong>仅摘要</strong> · 订阅源只提供了摘要。<a href={article.url} target="_blank" rel="noopener noreferrer">访问原文</a><p>用浏览器插件保存原文即可补全全文，标签和笔记会保留。</p></div>}
      {!isLink && <div className={`${styles.bodyRow} reader-body-row`}>
      <div className={styles.articleContent}>
        <HighlightManager
          articleId={article.id}
          bodyStale={progress.contentChanged}
          contentRevision={article.contentRevision}
          onContentChanged={progress.observeContent}
          highlights={article.highlights || []}
          onHighlightsChange={handleHighlightsChange}
        />
        <ReadableContent html={article.readableContent} contentRef={contentRef} />
        
        <HighlightList
          highlights={article.highlights || []}
          onHighlightsChange={handleHighlightsChange}
        />
      </div>
      <ReaderTocSidebar toc={toc} />
      </div>}
      {progress.atEnd && article.readingStatus !== "read" && !endDismissed && <div className={styles.endPrompt} role="region" aria-label="读完提示">
        <span>读完了？</span><Button variant="secondary" size="sm" disabled={pending} onClick={() => changeState({ readingStatus: "read" })}>标记已读</Button>
        <Tooltip content="关闭读完提示"><Button variant="ghost" size="sm" iconOnly aria-label="关闭读完提示" onClick={() => setEndDismissed(true)}><X size={16} aria-hidden="true" /></Button></Tooltip>
      </div>}
    </main>
  );
}
