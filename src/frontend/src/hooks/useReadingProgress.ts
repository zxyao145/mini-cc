'use client';
import { RefObject, useCallback, useEffect, useRef, useState } from 'react';
import { Article } from '@/types';
import { articleApi } from '@/lib/api';
import { updateCachedArticle } from '@/lib/library-cache';
import { PendingProgress, ProgressSnapshot, ProgressWriter, SyncStatus } from '@/lib/progress-writer';
import { ReadingPosition, readPosition, restorationScrollTop } from '@/lib/reading-position';
import { useReaderBlocks } from './useReaderBlocks';

function isNewerProgressVersion(value: string, known: string | null): boolean {
  if (!known) return true;
  const milliseconds = Date.parse(value), previous = Date.parse(known);
  if (milliseconds !== previous) return milliseconds > previous;
  // Date.parse truncates the PostgreSQL microseconds used by the server's version clock.
  const fraction = (version: string) => (version.match(/\.(\d+)/)?.[1] || '').padEnd(7, '0');
  return fraction(value) > fraction(known);
}
function positionOf(article: Article): ReadingPosition | null {
  return article.progressBlockIndex == null ? null : {textOffset:article.progressTextOffset,blockIndex:article.progressBlockIndex,blockRatio:article.progressBlockRatio || 0,percent:article.progressPercent || 0};
}
function snapshotOf(article: Article): ProgressSnapshot {
  return {...(positionOf(article) || {}),progressUpdatedAt:article.progressUpdatedAt};
}
function storedProgress(id: string, contentKind: Article['contentKind']): PendingProgress | null {
  try {
    const item = JSON.parse(sessionStorage.getItem(`progress:${id}`) || 'null');
    if (item?.contentKind === 'summaryOnly' && contentKind === 'full') {
      sessionStorage.removeItem(`progress:${id}`);
      return null; // The saved coordinates refer to the replaced summary, not the full body.
    }
    return item?.position && 'baseUpdatedAt' in item ? item : null;
  }
  catch { return null; }
}

export function useReadingProgress(article: Article | undefined, contentRef: RefObject<HTMLElement | null>, onArticleChange: (changes: Partial<Article>) => void) {
  const articleId = article?.id;
  const layout = useReaderBlocks(contentRef, article?.readableContent);
  const writer = useRef<ProgressWriter | null>(null);
  const latest = useRef({article,onArticleChange}); latest.current = {article,onArticleChange};
  const [contentChanged,setContentChanged] = useState(false);
  const staleContent = useRef(false);
  const loadedKind = useRef<Article["contentKind"] | undefined>(article?.contentKind);
  const [syncStatus,setSyncStatus] = useState<SyncStatus>('synced');
  const [conflict,setConflict] = useState<ProgressSnapshot | null>(null);
  const [percent,setPercent] = useState(0);
  const [atEnd,setAtEnd] = useState(false);
  const markingUnread = useRef(false);
  const userScrolled = useRef(false);
  const intent = useRef(0);
  const correctingUntil = useRef(0);
  const restoration = useRef<ReadingPosition | null>(null);
  const initialAnchor = useRef<ReadingPosition | null>(null);
  const restoredId = useRef<string | null>(null);
  const dwell = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const observeContentKind = useCallback((kind: Article['contentKind'] | undefined, current?: ProgressSnapshot) => {
    const known = latest.current.article;
    if (!known || loadedKind.current !== 'summaryOnly' || kind !== 'full') return false;
    staleContent.current = true; setContentChanged(true); clearTimeout(dwell.current);
    writer.current?.showConflict(current ?? snapshotOf(known));
    return true;
  }, []);
  const programmatic = useRef(false);
  const layoutChange = useRef(0);
  const layoutAnchor = useRef<ReadingPosition | null>(null);
  const preserveLayout = useCallback(() => {
    if (!articleId) return;
    const anchor = layoutAnchor.current || readPosition(layout.blocks,layout.totalTextLength);
    if (!anchor) return;
    layoutAnchor.current=anchor;
    const revision = ++layoutChange.current;
    correctingUntil.current=0;programmatic.current=true;clearTimeout(dwell.current);
    requestAnimationFrame(()=>requestAnimationFrame(()=>{
      if(revision!==layoutChange.current)return;
      window.scrollTo({top:restorationScrollTop(layout.blocks,anchor),behavior:'instant'});
      setPercent(anchor.percent);
      requestAnimationFrame(()=>{if(revision===layoutChange.current){programmatic.current=false;layoutAnchor.current=null;}});
    }));
  },[articleId,layout]);
  const restore = useCallback((position: ReadingPosition | null) => {
    restoration.current = position;
    correctingUntil.current = Date.now()+2000;
    programmatic.current = true;
    window.scrollTo({top:position ? restorationScrollTop(layout.blocks,position) : 0,behavior:'instant'});
    requestAnimationFrame(()=>{programmatic.current=false;});
    setPercent(position?.percent || 0);
  }, [layout.blocks]);

  useEffect(() => {
    if (!article?.id) return;
    let mounted = true;
    const id = article.id;
    userScrolled.current=false;intent.current=0;clearTimeout(dwell.current);
    staleContent.current=false;setContentChanged(false);loadedKind.current=article.contentKind;
    setSyncStatus('synced');setConflict(null);setAtEnd(false);
    const pending = storedProgress(id, article.contentKind);
    initialAnchor.current = pending?.position || positionOf(article);
    const instance = new ProgressWriter(crypto.randomUUID(),article.progressUpdatedAt,{
      send:(request,keepalive)=>articleApi.saveProgress(id,request,keepalive),
      reset:async sessionId=>{
        const result = await articleApi.resetProgress(id,sessionId);
        // This kind comes from the same locked transaction as the reset acknowledgement.
        // An upgrade after an acknowledged summary reset still invalidates its version on the server.
        if (article.contentKind === 'summaryOnly' && result.contentKind !== 'summaryOnly') {
          if (mounted) observeContentKind('full', { progressUpdatedAt: result.progressUpdatedAt });
          throw new Error('正文已更新，请刷新后继续');
        }
        return result;
      },
      persist:value=>{try {if(value)sessionStorage.setItem(`progress:${id}`,JSON.stringify({...value,contentKind:article.contentKind}));else sessionStorage.removeItem(`progress:${id}`);}catch{/* Optional storage may be full/unavailable. */}},
      onChange:(status,current)=>{if(mounted){setSyncStatus(status);setConflict(current);}},
      onSaved:(result,position)=>{
        const changes: Partial<Article> = {...result,progressSessionId:instance.sessionId,
          progress:position ? {...position,updatedAt:result.progressUpdatedAt} : null,
          progressTextOffset:position?.textOffset ?? null,progressBlockIndex:position?.blockIndex ?? null,
          progressBlockRatio:position?.blockRatio ?? null,progressPercent:position?.percent ?? null};
        updateCachedArticle(id,{readingStatus:result.readingStatus,lastReadAt:result.lastReadAt,progressPercent:position?.percent ?? null});
        if(mounted)latest.current.onArticleChange(changes);
      },
    },pending);
    writer.current=instance;
    instance.setVisible(document.visibilityState !== 'hidden');
    if(pending && document.visibilityState !== 'hidden')void instance.flush();
    const refresh = async () => {
      try {
        const fresh = await articleApi.getArticleById(id);
        if (!mounted) return;
        if (observeContentKind(fresh.contentKind, snapshotOf(fresh))) return;
        if(fresh.progressUpdatedAt && isNewerProgressVersion(fresh.progressUpdatedAt, instance.baseUpdatedAt) && fresh.progressSessionId!==instance.sessionId)
          instance.showConflict(snapshotOf(fresh));
      } catch {/* A failed refresh must not rebase pending data. Retried PUT still checks its original base. */}
      finally { if(mounted)instance.setVisible(document.visibilityState !== 'hidden'); }
    };
    const visibility = () => {
      clearTimeout(dwell.current);
      if(document.visibilityState==='hidden'){instance.setVisible(false);void instance.flush(true);}
      else void refresh();
    };
    const pagehide=()=>{instance.setVisible(false);void instance.flush(true);};
    document.addEventListener('visibilitychange',visibility);window.addEventListener('pagehide',pagehide);
    return ()=>{
      mounted=false;layoutChange.current+=1;layoutAnchor.current=null;programmatic.current=false;
      clearTimeout(dwell.current);instance.setVisible(false);void instance.flush(true);
      document.removeEventListener('visibilitychange',visibility);window.removeEventListener('pagehide',pagehide);
      if(writer.current===instance)writer.current=null;
    };
    // A page session is created once per article, not per acknowledged progress render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [article?.id]);

  useEffect(()=>{
    if(!article?.id || !layout.blocks.length || restoredId.current===article.id)return;
    restoredId.current=article.id;
    restore(initialAnchor.current);
  },[article,layout.blocks,restore]);

  useEffect(()=>{
    if(!articleId || !layout.blocks.length)return;
    const measure=()=>{
      const current=readPosition(layout.blocks,layout.totalTextLength);
      if(!current)return;
      setPercent(current.percent);
      setAtEnd(layout.blocks.at(-1)!.node.getBoundingClientRect().bottom<=window.innerHeight);
      return current;
    };
    const active=()=>{layoutAnchor.current=null;layoutChange.current++;intent.current=Date.now();correctingUntil.current=0;programmatic.current=false;};
    const key=(event:KeyboardEvent)=>{
      if(/^(INPUT|TEXTAREA|SELECT)$/.test((event.target as Element)?.tagName))return;
      if(['ArrowDown','ArrowUp','PageDown','PageUp','Home','End',' '].includes(event.key))active();
    };
    const pointer=(event:PointerEvent)=>{if(event.clientX>=document.documentElement.clientWidth-20)active();};
    const scroll=()=>{
      const current=measure();
      clearTimeout(dwell.current);
      if(staleContent.current || programmatic.current || markingUnread.current || !current || document.visibilityState==='hidden')return;
      if(Date.now()-intent.current<2000 && intent.current>0)userScrolled.current=true;
      if(!userScrolled.current || (current.percent<2 && current.blockIndex<2))return;
      dwell.current=setTimeout(()=>{
        if(document.visibilityState==='hidden')return;
        const settled=measure();
        if(!settled || settled.blockIndex!==current.blockIndex || Math.abs(settled.blockRatio-current.blockRatio)>.1)return;
        writer.current?.queue(settled);
      },3000);
    };
    const image=()=>{if(Date.now()<correctingUntil.current && restoration.current){const until=correctingUntil.current;restore(restoration.current);correctingUntil.current=until;}};
    measure();
    // Highlight wrappers replace text nodes; renewed block refs must keep an active dwell alive.
    if(userScrolled.current)scroll();
    window.addEventListener('scroll',scroll,{passive:true});window.addEventListener('resize',measure);
    window.addEventListener('wheel',active,{passive:true});window.addEventListener('touchmove',active,{passive:true});
    window.addEventListener('keydown',key);window.addEventListener('pointerdown',pointer);
    const content=contentRef.current;content?.addEventListener('load',image,true);
    return()=>{clearTimeout(dwell.current);window.removeEventListener('scroll',scroll);window.removeEventListener('resize',measure);window.removeEventListener('wheel',active);window.removeEventListener('touchmove',active);window.removeEventListener('keydown',key);window.removeEventListener('pointerdown',pointer);content?.removeEventListener('load',image,true);};
  },[articleId,layout,contentRef,restore]);

  const chooseConflict=async(jump:boolean)=>{
    if(!conflict || staleContent.current)return;
    if (loadedKind.current === 'summaryOnly' && latest.current.article) {
      try {
        const fresh = await articleApi.getArticleById(latest.current.article.id);
        if (observeContentKind(fresh.contentKind, snapshotOf(fresh))) return;
      } catch { return; }
    }
    writer.current?.resolveConflict(conflict,jump);
    if(jump){userScrolled.current=false;clearTimeout(dwell.current);restore(conflict.blockIndex==null?null:{textOffset:conflict.textOffset??null,blockIndex:conflict.blockIndex,blockRatio:conflict.blockRatio||0,percent:conflict.percent||0});}
  };
  const reset=async()=>{
    if(staleContent.current)throw new Error("正文已更新，请刷新后继续");
    clearTimeout(dwell.current);userScrolled.current=false;intent.current=0;
    await writer.current?.reset();restore(null);
  };
  const withStateChange=async(action:()=>Promise<void>, unread=false)=>{
    if(unread){markingUnread.current=true;clearTimeout(dwell.current);userScrolled.current=false;intent.current=0;}
    try { if(writer.current)await writer.current.withStateChange(action,unread);else await action(); }
    finally {if(unread){markingUnread.current=false;clearTimeout(dwell.current);userScrolled.current=false;intent.current=0;}}
  };
  const progress = articleId
    ? {contentChanged,syncStatus,conflict,percent,atEnd}
    : {contentChanged:false,syncStatus:'synced' as const,conflict:null,percent:0,atEnd:false};
  return {observeContentKind,...progress,chooseConflict,reset,withStateChange,preserveLayout};
}
