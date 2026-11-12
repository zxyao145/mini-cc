'use client';
import { ReactNode, RefObject, useEffect, useRef, useState } from 'react';
import { X } from 'lucide-react';
export default function ReaderPanel({label,trigger,onClose,children,className='',drawerBreakpoint=768}:{label:string;trigger:RefObject<HTMLButtonElement|null>;onClose:()=>void;children:ReactNode;className?:string;drawerBreakpoint?:number}) {
  const panel=useRef<HTMLDivElement>(null);
  const [drawer,setDrawer]=useState(()=>window.matchMedia(`(max-width: ${drawerBreakpoint-1}px)`).matches);
  useEffect(()=>{
    const media=window.matchMedia(`(max-width: ${drawerBreakpoint-1}px)`);
    const change=()=>setDrawer(media.matches);
    change();media.addEventListener('change',change);
    return ()=>media.removeEventListener('change',change);
  },[drawerBreakpoint]);
  useEffect(()=>{
    const node=panel.current;
    node?.querySelector<HTMLElement>('fieldset button:not(:disabled),input,a,button')?.focus({preventScroll:true});
    const outside=(event:PointerEvent)=>{if(!node?.contains(event.target as Node)&&!trigger.current?.contains(event.target as Node))onClose();};
    const key=(event:KeyboardEvent)=>{
      if(event.key==='Escape'){event.preventDefault();onClose();}
      if(event.key==='Tab'){
        const controls=Array.from(node?.querySelectorAll<HTMLElement>('button:not(:disabled),input,a[href]')||[]);
        const first=controls[0],last=controls.at(-1);
        if(event.shiftKey&&document.activeElement===first){event.preventDefault();last?.focus({preventScroll:true});}
        else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first?.focus({preventScroll:true});}
      }
    };
    document.addEventListener('pointerdown',outside);document.addEventListener('keydown',key);
    const button=trigger.current;
    return ()=>{document.removeEventListener('pointerdown',outside);document.removeEventListener('keydown',key);button?.focus({preventScroll:true});};
  },[onClose,trigger]);
  return <><div className={`reader-backdrop${drawer?' reader-backdrop-visible':''}`} aria-hidden="true"/><div ref={panel} className={`reader-panel ${className}${drawer?' reader-drawer':''}`} role="dialog" aria-label={label} aria-modal={drawer || undefined}>
    <div className="reader-panel-heading"><strong>{label}</strong><button onClick={onClose} aria-label={`关闭${label}`}><X size={18}/></button></div>{children}
  </div></>;
}
