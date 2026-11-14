'use client';
import { ReactNode, useCallback, useRef } from 'react';
import { Archive, ArchiveRestore, ArrowLeft, BookOpen, CheckCheck, Heart, RotateCcw, Trash2 } from 'lucide-react';
import { Article, ArticleStatePatch } from '@/types';
import { useAppHeaderOffset } from '@/hooks/useAppHeaderOffset';
import { SyncStatus } from '@/lib/progress-writer';

export default function ReaderToolbar({article,pending,syncStatus,onBack,onState,onReset,onDelete,extra}: {
  article:Article;pending:boolean;syncStatus:SyncStatus;onBack:()=>void;onState:(patch:ArticleStatePatch)=>void;onReset?:()=>void;onDelete:()=>void;extra?:ReactNode;
}) {
  const toolbarRef=useRef<HTMLElement>(null);
  const measure = useCallback((toolbar: HTMLElement) => {
    const reader = toolbar.closest<HTMLElement>('.article-detail');
    reader?.style.setProperty('--reader-header-offset', toolbar.style.getPropertyValue('--app-header-offset'));
    reader?.style.setProperty('--reader-toolbar-height', `${toolbar.getBoundingClientRect().height}px`);
  }, []);
  useAppHeaderOffset(toolbarRef, measure);
  const readLabel=article.readingStatus==='read'?'标记未读':'标记已读';
  const archiveLabel=article.isArchived?'移回收件箱':'归档文章';
  const favoriteLabel=article.isFavorite?'取消收藏':'加入收藏';
  return <nav ref={toolbarRef} className="reader-toolbar" aria-label="阅读操作">
    <button onClick={onBack} aria-label="返回文库" title="返回文库"><ArrowLeft size={18}/><span>返回</span></button>
    <button disabled={pending} onClick={()=>onState({readingStatus:article.readingStatus==='read'?'unread':'read'})} aria-label={readLabel} title={readLabel}>{article.readingStatus==='read'?<BookOpen size={18}/>:<CheckCheck size={18}/>}<span>{readLabel}</span></button>
    {onReset && <button disabled={pending} onClick={onReset} aria-label="从头阅读" title="从头阅读"><RotateCcw size={18}/><span>从头阅读</span></button>}
    <button disabled={pending} onClick={()=>onState({isFavorite:!article.isFavorite})} aria-label={favoriteLabel} aria-pressed={article.isFavorite} title={favoriteLabel}><Heart size={18} fill={article.isFavorite?'currentColor':'none'}/></button>
    <button disabled={pending} onClick={()=>onState({isArchived:!article.isArchived})} aria-label={archiveLabel} title={archiveLabel}>{article.isArchived?<ArchiveRestore size={18}/>:<Archive size={18}/>}</button>
    <button disabled={pending} onClick={onDelete} aria-label="删除文章" title="删除文章"><Trash2 size={18}/></button>
    {extra}
    {syncStatus!=='synced'&&<span className="reader-sync" role="status">{syncStatus==='saving'?'保存中':'进度暂未同步'}</span>}
  </nav>;
}
