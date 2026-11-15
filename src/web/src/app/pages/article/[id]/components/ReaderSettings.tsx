'use client';
import { useCallback, useRef, useState } from 'react';
import { Settings2 } from 'lucide-react';
import { Button } from '@/components/arc/button/button';
import { Tooltip } from '@/components/arc/tooltip/tooltip';
import { useReaderPreferences } from '@/contexts/ReaderPreferencesContext';
import { defaultReaderPreferences, ReaderPreferences } from '@/lib/reader-preferences';
import ReaderPanel from './ReaderPanel';
import styles from './ReaderSettings.module.css';
export default function ReaderSettings(){
  const {preferences:p,update,unsynced,retry}=useReaderPreferences();
  const [open,setOpen]=useState(false),trigger=useRef<HTMLButtonElement>(null);
  const close=useCallback(()=>setOpen(false),[]);
  const change=(partial:Partial<ReaderPreferences>)=>update({...p,...partial});
  return <div className={styles.control}>
    <Tooltip content="阅读设置"><Button ref={trigger} variant="ghost" size="sm" iconOnly aria-label="阅读设置" aria-expanded={open} onClick={()=>setOpen(!open)}><Settings2 size={18}/></Button></Tooltip>
    {open&&<ReaderPanel label="阅读设置" trigger={trigger} onClose={close}>
      <div className={styles.settings}>
        <fieldset><legend>字号</legend><div className={styles.fontSize}><Button variant="secondary" size="sm" aria-label="减小字号" disabled={p.fontSize<=16} onClick={()=>change({fontSize:p.fontSize-1})}>A−</Button><output aria-label="当前字号">{p.fontSize}</output><Button variant="secondary" size="sm" aria-label="增大字号" disabled={p.fontSize>=28} onClick={()=>change({fontSize:p.fontSize+1})}>A+</Button></div></fieldset>
        <label className={styles.lineHeight}><span className={styles.lineHeightLabel}>行距</span><output>{p.lineHeight.toFixed(1)}</output><input aria-label="行距" type="range" min="1.5" max="2.2" step="0.1" value={p.lineHeight} onChange={e=>change({lineHeight:Number(e.target.value)})}/></label>
        <fieldset><legend>宽度</legend><div className={styles.segments}>{(['narrow','standard','wide'] as const).map((value,i)=><Button key={value} variant={p.width===value?'secondary':'ghost'} size="sm" aria-pressed={p.width===value} onClick={()=>change({width:value})}>{['窄','标准','宽'][i]}</Button>)}</div></fieldset>
        <fieldset><legend>字体</legend><div className={styles.segments}>{(['system','serif'] as const).map((value,i)=><Button key={value} variant={p.fontFamily===value?'secondary':'ghost'} size="sm" aria-pressed={p.fontFamily===value} onClick={()=>change({fontFamily:value})}>{['系统字体','衬线字体'][i]}</Button>)}</div></fieldset>
        <fieldset><legend>主题</legend><div className={styles.themes}>{(['system','light','dark','sepia'] as const).map((value,i)=><Button key={value} variant="secondary" size="sm" className={value==='system'?styles.themeSystem:value==='light'?styles.themeLight:value==='dark'?styles.themeDark:styles.themeSepia} aria-pressed={p.theme===value} onClick={()=>change({theme:value})}>{['跟随系统','浅色','深色','护眼'][i]}</Button>)}</div></fieldset>
        <Button variant="secondary" className={styles.defaults} onClick={()=>update(defaultReaderPreferences)}>恢复默认</Button>
        {unsynced&&<p className={styles.status} role="status">偏好未同步，<Button variant="ghost" size="sm" onClick={()=>void retry()}>重试</Button></p>}
      </div>
    </ReaderPanel>}
  </div>;
}
