'use client';
import { RefObject, useCallback, useEffect, useRef, useState } from 'react';
import { List } from 'lucide-react';
import { Button } from '@/components/arc/button/button';
import { Tooltip } from '@/components/arc/tooltip/tooltip';
import { collectReaderHeadings, ReaderHeading } from '@/lib/reader-toc';
import ReaderPanel from './ReaderPanel';
import styles from './ReaderToc.module.css';
export function useReaderToc(contentRef:RefObject<HTMLElement|null>,html:string|undefined){
  const [headings,setHeadings]=useState<ReaderHeading[]>([]),[active,setActive]=useState('');
  useEffect(()=>{
    const root=contentRef.current;if(!root||html==null)return;
    const entries=collectReaderHeadings(root);setHeadings(entries);setActive(entries[0]?.id||'');
    if(!entries.length)return;
    const measure=()=>{
      const positions=entries.map(heading=>({heading,top:heading.node.getBoundingClientRect().top}));
      const current=positions.find(h=>h.top>=window.innerHeight*.2&&h.top<=window.innerHeight*.3)
        || positions.filter(h=>h.top<window.innerHeight*.2).at(-1) || positions[0];
      if(current)setActive(current.heading.id);
    };
    const observer=new IntersectionObserver(measure,{rootMargin:'-20% 0px -70% 0px'});
    entries.forEach(h=>observer.observe(h.node));
    // Large jumps can skip the observer band entirely; geometry still identifies their section.
    window.addEventListener('scroll',measure,{passive:true});
    return ()=>{observer.disconnect();window.removeEventListener('scroll',measure);};
  },[contentRef,html]);
  const jump=useCallback((heading:ReaderHeading)=>{
    heading.node.scrollIntoView({behavior:'instant',block:'start'});history.replaceState(history.state,'',`#${encodeURIComponent(heading.id)}`);setActive(heading.id);
  },[]);
  return {headings,active,jump};
}
type Toc=ReturnType<typeof useReaderToc>;
function Entries({toc,onJump}:{toc:Toc;onJump?:()=>void}){const minLevel=Math.min(...toc.headings.map(h=>h.level));return <nav className={styles.entries} aria-label="文章目录"><ol>{toc.headings.map(h=><li key={h.id} className={h.level>minLevel?`${styles.nested} reader-toc-nested`:undefined}><a className={styles.entry} href={`#${h.id}`} aria-current={toc.active===h.id?'location':undefined} onClick={event=>{event.preventDefault();toc.jump(h);onJump?.();}}><span className={styles.entryLabel}>{h.text}</span></a></li>)}</ol></nav>;}
export function ReaderTocSidebar({toc}:{toc:Toc}){return toc.headings.length?<aside className={`${styles.sidebar} reader-toc-sidebar`}><strong className={styles.sidebarTitle}><List size={14} aria-hidden="true"/>目录</strong><Entries toc={toc}/></aside>:null;}
export function ReaderTocButton({toc}:{toc:Toc}){
  const [open,setOpen]=useState(false),trigger=useRef<HTMLButtonElement>(null),close=useCallback(()=>setOpen(false),[]);
  useEffect(()=>{
    const media=window.matchMedia('(min-width: 1200px)');
    const change=()=>{if(media.matches)close();};
    media.addEventListener('change',change);return ()=>media.removeEventListener('change',change);
  },[close]);
  if(!toc.headings.length)return null;
  return <div className={`${styles.control} reader-toc-control`}><Tooltip content="目录"><Button ref={trigger} variant="ghost" size="sm" iconOnly aria-label="目录" aria-expanded={open} onClick={()=>setOpen(!open)}><List size={18}/></Button></Tooltip>{open&&<ReaderPanel label="目录" drawerBreakpoint={1200} trigger={trigger} onClose={close} className={styles.tocPanel}><Entries toc={toc} onJump={close}/></ReaderPanel>}</div>;
}
