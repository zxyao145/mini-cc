'use client';
import { createContext, ReactNode, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { readerPreferencesApi } from '@/lib/api';
import { defaultReaderPreferences, normalizeReaderPreferences, ReaderPreferences } from '@/lib/reader-preferences';
import { useAuth } from './AuthContext';

function cachedPreferences() {
  try { return normalizeReaderPreferences(JSON.parse(localStorage.getItem('reader-preferences')||'null')); }
  catch { return defaultReaderPreferences; }
}
interface PreferencesContext {
  preferences:ReaderPreferences; unsynced:boolean; update:(value:ReaderPreferences)=>void; retry:()=>Promise<void>;
  beforeChange:(capture:()=>void)=>(()=>void);
}
const Context=createContext<PreferencesContext|undefined>(undefined);
export function ReaderPreferencesProvider({children}:{children:ReactNode}) {
  const {user}=useAuth();
  const [preferences,setPreferences]=useState(cachedPreferences);
  const [unsynced,setUnsynced]=useState(false);
  const current=useRef(preferences), edits=useRef(0), saved=useRef(0), busy=useRef(false), due=useRef(false), epoch=useRef(0);
  const timer=useRef<ReturnType<typeof setTimeout>|undefined>(undefined);
  const captures=useRef(new Set<()=>void>());
  const beforeChange=useCallback((capture:()=>void)=>{captures.current.add(capture);return ()=>{captures.current.delete(capture);};},[]);
  const apply=useCallback((value:ReaderPreferences)=>{
    const next=normalizeReaderPreferences(value);
    if(JSON.stringify(next)!==JSON.stringify(current.current))captures.current.forEach(capture=>capture());
    current.current=next;setPreferences(next);
    try {localStorage.setItem('reader-preferences',JSON.stringify(next));}catch{/* Reading remains available without storage. */}
  },[]);
  const load=useCallback(async()=>{
    const revision=edits.current, session=epoch.current;
    try {
      const value=await readerPreferencesApi.get();
      if(session===epoch.current && revision===edits.current){apply(value);saved.current=revision;setUnsynced(false);}
    }catch {if(session===epoch.current && revision===edits.current)setUnsynced(true);}
  },[apply]);
  const flush=useCallback(async function flushLatest():Promise<void> {
    due.current=true;
    if(busy.current || edits.current===saved.current)return;
    busy.current=true;due.current=false;
    const revision=edits.current, session=epoch.current, value=current.current;
    try {
      await readerPreferencesApi.put(value);
      if(session===epoch.current){saved.current=revision;setUnsynced(false);}
    }catch {if(session===epoch.current)setUnsynced(true);}
    finally {
      busy.current=false;
      // The single in-flight request is shared across auth epochs. Its completion
      // releases that barrier, but only current-epoch work may be acknowledged.
      if(due.current && edits.current!==saved.current)await flushLatest();
    }
  },[]);
  useEffect(()=>{
    const session=epoch.current;
    if(user?.userName)void load();
    return ()=>{epoch.current=session+1;due.current=false;clearTimeout(timer.current);};
  },[user?.userName,load]);
  const update=useCallback((value:ReaderPreferences)=>{
    edits.current++;apply(value);clearTimeout(timer.current);due.current=false;
    timer.current=setTimeout(()=>{void flush();},500);
  },[apply,flush]);
  const retry=useCallback(async()=>{
    clearTimeout(timer.current);
    if(edits.current===saved.current)await load();else await flush();
  },[load,flush]);
  return <Context.Provider value={{preferences,unsynced,update,retry,beforeChange}}>{children}</Context.Provider>;
}
export function useReaderPreferences(){const value=useContext(Context);if(!value)throw new Error('ReaderPreferencesProvider is required');return value;}
