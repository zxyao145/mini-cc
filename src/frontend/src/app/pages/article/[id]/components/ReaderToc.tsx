'use client';
import { RefObject, useCallback, useEffect, useRef, useState } from 'react';
import { List } from 'lucide-react';
import { collectReaderHeadings, ReaderHeading } from '@/lib/reader-toc';
import ReaderPanel from './ReaderPanel';
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
function Entries({toc,onJump}:{toc:Toc;onJump?:()=>void}){return <nav aria-label="文章目录"><ol>{toc.headings.map(h=><li key={h.id} className={h.level>Math.min(...toc.headings.map(h=>h.level))?'reader-toc-nested':''}><a href={`#${h.id}`} aria-current={toc.active===h.id?'location':undefined} onClick={event=>{event.preventDefault();toc.jump(h);onJump?.();}}>{h.text}</a></li>)}</ol></nav>;}
export function ReaderTocSidebar({toc}:{toc:Toc}){return toc.headings.length?<aside className="reader-toc-sidebar"><strong>目录</strong><Entries toc={toc}/></aside>:null;}
export function ReaderTocButton({toc}:{toc:Toc}){
  const [open,setOpen]=useState(false),trigger=useRef<HTMLButtonElement>(null),close=useCallback(()=>setOpen(false),[]);
  useEffect(()=>{
    const media=window.matchMedia('(min-width: 1200px)');
    const change=()=>{if(media.matches)close();};
    media.addEventListener('change',change);return ()=>media.removeEventListener('change',change);
  },[close]);
  if(!toc.headings.length)return null;
  return <div className="reader-toc-control"><button ref={trigger} aria-label="目录" title="目录" aria-expanded={open} onClick={()=>setOpen(!open)}><List size={18}/></button>{open&&<ReaderPanel label="目录" drawerBreakpoint={1200} trigger={trigger} onClose={close} className="reader-toc-panel"><Entries toc={toc} onJump={close}/></ReaderPanel>}</div>;
}
