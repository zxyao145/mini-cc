"use client";
import { useToast } from "@/contexts/ToastContext";

import { ExternalLink } from "lucide-react";
import { articleApi } from "@/lib/api";
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
//import Prism from 'prismjs';

////导入tomorrow代码主题样式（按需引入）
//import 'prismjs/themes/prism-tomorrow.css';

////导入行号效果css、js文件（按需引入）
//import "prismjs/plugins/line-numbers/prism-line-numbers.min.css";
//import "prismjs/plugins/line-numbers/prism-line-numbers.min.js";


import "./client.scss";

export default function Client(params: { id: string }) {
  //useEffect(() => {
  //  Prism.highlightAll();
  //}, []);

  const [loading, setLoading] = useState(false);
  const [article, setArticle] = useState<Article>();

  const router = useRouter();
  const toast = useToast();
  const contentRef = useRef<HTMLDivElement>(null);
  const [pending, setPending] = useState(false);
  const [actionError, setActionError] = useState("");
  const [endDismissed, setEndDismissed] = useState(false);
  const patchArticle = useCallback((changes: Partial<Article>) => setArticle(current => current ? {
    ...current, ...changes,
    // List/progress acknowledgements cannot change the kind of the body currently rendered.
    contentKind: changes.readableContent === undefined ? current.contentKind : changes.contentKind ?? current.contentKind,
  } : current), []);
  const progress = useReadingProgress(article, contentRef, patchArticle);
  const { preferences, beforeChange } = useReaderPreferences();
  const toc = useReaderToc(contentRef, article?.readableContent);
  const [systemDark, setSystemDark] = useState(false);
  useLayoutEffect(() => beforeChange(progress.preserveLayout), [beforeChange, progress.preserveLayout]);
  useEffect(() => {
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    const change = () => setSystemDark(media.matches);
    change(); media.addEventListener("change", change);
    return () => media.removeEventListener("change", change);
  }, []);
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
        progress.observeContentKind(updated.contentKind);
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
    if (!article || !confirm("将这篇文章移入回收站？")) return;
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
    <main className={`article-detail reader${toc.headings.length ? " reader-has-toc" : ""}`} style={readerPreferenceStyle(preferences)} data-reader-theme={preferences.theme === "system" ? (systemDark ? "dark" : "light") : preferences.theme}>
      <ReaderToolbar article={article} pending={pending} syncStatus={progress.syncStatus} onBack={back} onState={changeState} onReset={reset} onDelete={remove} extra={<><ReaderSettings /><ReaderTocButton toc={toc} /></>} />
      {actionError && <p role="alert">{actionError}</p>}
      {progress.contentChanged && <div className="reader-conflict" role="alert"><span>正文已补全，请刷新后继续阅读。旧正文的进度已暂停同步。</span><button onClick={() => { sessionStorage.removeItem(`progress:${article.id}`); window.location.reload(); }}>刷新正文</button></div>}
      {!progress.contentChanged && progress.conflict && <div className="reader-conflict" role="alert">
        <span>已在其他设备读到 {Math.round(progress.conflict.percent || 0)}%</span>
        <button onClick={() => progress.chooseConflict(true)}>跳转</button>
        <button onClick={() => progress.chooseConflict(false)}>留在这里</button>
      </div>}
      <div className="reader-progress" role="progressbar" aria-label="阅读进度" aria-valuemin={0} aria-valuemax={100} aria-valuenow={Math.round(progress.percent)}><span style={{ width: `${progress.percent}%` }} /></div>
      <div className="article-header">
        <div className="article-title-section">
          <h1>{article.title}</h1>
          {article.author && <p className="author">by {article.author}</p>}
          {!!article.feeds?.length && <p className="reader-source">来自 {article.feeds.map(f => f.title).join('，')}{article.publishedAt && ` · 发布于 ${new Date(article.publishedAt).toLocaleDateString()}`}</p>}
        </div>
        
        <div className="article-actions">
          <a 
            href={article.url} 
            target="_blank" 
            rel="noopener noreferrer"
            className="visit-original-btn"
            title="访问原始页面"
          >
            <ExternalLink size={16} aria-hidden="true" />
            <span className="text">访问原文</span>
          </a>
        </div>
        
        <TagManager 
          articleId={article.id}
          tags={article.tags || []}
          onTagsChange={handleTagsChange}
        />
      </div>
      
      {article.contentKind === 'summaryOnly' && <div className="reader-summary-banner"><strong>仅摘要</strong> · 订阅源只提供了摘要。<a href={article.url} target="_blank" rel="noopener noreferrer">访问原文</a><p>用浏览器插件保存原文即可补全全文，标签和笔记会保留。</p></div>}
      <div className="reader-body-row">
      <div className="article-content">
        <HighlightManager
          articleId={article.id}
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
      </div>
      {progress.atEnd && article.readingStatus !== "read" && !endDismissed && <div className="reader-end-prompt" role="region" aria-label="读完提示">
        <span>读完了？</span><button disabled={pending} onClick={() => changeState({ readingStatus: "read" })}>标记已读</button>
        <button aria-label="关闭读完提示" onClick={() => setEndDismissed(true)}>×</button>
      </div>}
    </main>
  );
}
