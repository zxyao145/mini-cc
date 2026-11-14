'use client';
import { useCallback, useRef, useState } from 'react';
import { Settings2 } from 'lucide-react';
import { useReaderPreferences } from '@/contexts/ReaderPreferencesContext';
import { defaultReaderPreferences, ReaderPreferences } from '@/lib/reader-preferences';
import ReaderPanel from './ReaderPanel';
export default function ReaderSettings(){
  const {preferences:p,update,unsynced,retry}=useReaderPreferences();
  const [open,setOpen]=useState(false),trigger=useRef<HTMLButtonElement>(null);
  const close=useCallback(()=>setOpen(false),[]);
  const change=(partial:Partial<ReaderPreferences>)=>update({...p,...partial});
  return <div className="reader-settings-control">
    <button ref={trigger} aria-label="阅读设置" title="阅读设置" aria-expanded={open} onClick={()=>setOpen(!open)}><Settings2 size={18}/></button>
    {open&&<ReaderPanel label="阅读设置" trigger={trigger} onClose={close}>
      <fieldset><legend>字号</legend><div className="reader-font-size"><button aria-label="减小字号" disabled={p.fontSize<=16} onClick={()=>change({fontSize:p.fontSize-1})}>A−</button><output aria-label="当前字号">{p.fontSize}</output><button aria-label="增大字号" disabled={p.fontSize>=28} onClick={()=>change({fontSize:p.fontSize+1})}>A+</button></div></fieldset>
      <label className="reader-line-height">行距 <output>{p.lineHeight.toFixed(1)}</output><input aria-label="行距" type="range" min="1.5" max="2.2" step="0.1" value={p.lineHeight} onChange={e=>change({lineHeight:Number(e.target.value)})}/></label>
      <fieldset><legend>宽度</legend><div className="reader-segments">{(['narrow','standard','wide'] as const).map((value,i)=><button key={value} aria-pressed={p.width===value} onClick={()=>change({width:value})}>{['窄','标准','宽'][i]}</button>)}</div></fieldset>
      <fieldset><legend>字体</legend><div className="reader-segments">{(['system','serif'] as const).map((value,i)=><button key={value} aria-pressed={p.fontFamily===value} onClick={()=>change({fontFamily:value})}>{['系统字体','衬线字体'][i]}</button>)}</div></fieldset>
      <fieldset><legend>主题</legend><div className="reader-themes">{(['system','light','dark','sepia'] as const).map((value,i)=><button key={value} className={`reader-theme-${value}`} aria-pressed={p.theme===value} onClick={()=>change({theme:value})}>{['跟随系统','浅色','深色','护眼'][i]}</button>)}</div></fieldset>
      <button className="reader-defaults" onClick={()=>update(defaultReaderPreferences)}>恢复默认</button>
      {unsynced&&<p role="status">偏好未同步，<button onClick={()=>void retry()}>重试</button></p>}
    </ReaderPanel>}
  </div>;
}
