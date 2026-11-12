import { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { beforeEach, afterEach, it, expect, vi } from 'vitest';
import { ReaderPreferencesProvider, useReaderPreferences } from './ReaderPreferencesContext';
import { defaultReaderPreferences } from '@/lib/reader-preferences';
import { readerPreferencesApi } from '@/lib/api';
vi.mock('@/lib/api',()=>({readerPreferencesApi:{get:vi.fn(),put:vi.fn()}}));
const auth=vi.hoisted(()=>({user:{userName:'test'} as {userName:string}|null}));
vi.mock('./AuthContext',()=>({useAuth:()=>({user:auth.user})}));
let state:ReturnType<typeof useReaderPreferences>,root:Root,host:HTMLDivElement;
function Consumer(){state=useReaderPreferences();return <span>{state.preferences.fontSize}</span>;}
const deferred=<T,>()=>{let resolve!:(value:T)=>void;let reject!:(reason:unknown)=>void;const promise=new Promise<T>((a,b)=>{resolve=a;reject=b;});return {promise,resolve,reject};};
beforeEach(()=>{Object.assign(globalThis,{IS_REACT_ACT_ENVIRONMENT:true});vi.useFakeTimers();vi.resetAllMocks();auth.user={userName:"test"};localStorage.clear();host=document.createElement('div');document.body.append(host);root=createRoot(host);vi.mocked(readerPreferencesApi.put).mockResolvedValue(defaultReaderPreferences);});
afterEach(async()=>{await act(async()=>root.unmount());host.remove();vi.useRealTimers();});
const mount=()=>act(async()=>root.render(<ReaderPreferencesProvider><Consumer/></ReaderPreferencesProvider>));
it('uses cache on first render, then adopts server defaults without a PUT',async()=>{
 const get=deferred<typeof defaultReaderPreferences>();vi.mocked(readerPreferencesApi.get).mockReturnValue(get.promise);localStorage.setItem('reader-preferences',JSON.stringify({...defaultReaderPreferences,fontSize:24}));
 await mount();expect(state.preferences.fontSize).toBe(24);
 await act(async()=>get.resolve({...defaultReaderPreferences,fontSize:20}));expect(state.preferences.fontSize).toBe(20);expect(JSON.parse(localStorage.getItem('reader-preferences')!).fontSize).toBe(20);expect(readerPreferencesApi.put).not.toHaveBeenCalled();
});
it('applies edits immediately, debounces 500ms and ignores an old initial GET',async()=>{
 const get=deferred<typeof defaultReaderPreferences>();vi.mocked(readerPreferencesApi.get).mockReturnValue(get.promise);await mount();
 await act(async()=>state.update({...state.preferences,fontSize:23}));expect(state.preferences.fontSize).toBe(23);expect(JSON.parse(localStorage.getItem('reader-preferences')!).fontSize).toBe(23);
 await act(async()=>{get.resolve(defaultReaderPreferences);await vi.advanceTimersByTimeAsync(499);});expect(state.preferences.fontSize).toBe(23);expect(readerPreferencesApi.put).not.toHaveBeenCalled();
 await act(async()=>vi.advanceTimersByTimeAsync(1));expect(readerPreferencesApi.put).toHaveBeenCalledWith({...defaultReaderPreferences,fontSize:23});
});
it('serializes pending writes, coalesces latest intent and does not apply old responses',async()=>{
 vi.mocked(readerPreferencesApi.get).mockResolvedValue(defaultReaderPreferences);const put=deferred<typeof defaultReaderPreferences>();vi.mocked(readerPreferencesApi.put).mockReturnValueOnce(put.promise);await mount();
 await act(async()=>{state.update({...state.preferences,fontSize:20});await vi.advanceTimersByTimeAsync(500);});
 await act(async()=>{state.update({...state.preferences,fontSize:22});state.update({...state.preferences,fontSize:24});await vi.advanceTimersByTimeAsync(500);});expect(readerPreferencesApi.put).toHaveBeenCalledTimes(1);
 await act(async()=>put.resolve({...defaultReaderPreferences,fontSize:20}));expect(state.preferences.fontSize).toBe(24);expect(readerPreferencesApi.put).toHaveBeenCalledTimes(2);expect(vi.mocked(readerPreferencesApi.put).mock.calls[1][0].fontSize).toBe(24);
});
it('keeps failed local preferences, displays unsynced state and retries latest intent',async()=>{
 vi.mocked(readerPreferencesApi.get).mockResolvedValue(defaultReaderPreferences);vi.mocked(readerPreferencesApi.put).mockRejectedValueOnce(new Error('offline'));await mount();
 await act(async()=>{state.update({...state.preferences,theme:'sepia'});await vi.advanceTimersByTimeAsync(500);});expect(state.unsynced).toBe(true);expect(state.preferences.theme).toBe('sepia');
 await act(async()=>state.retry());expect(state.unsynced).toBe(false);expect(readerPreferencesApi.put).toHaveBeenCalledTimes(2);
});
it('captures old reader geometry before both server adoption and explicit edits',async()=>{
 const get=deferred<typeof defaultReaderPreferences>();vi.mocked(readerPreferencesApi.get).mockReturnValue(get.promise);await mount();
 const captured:number[]=[];const unsubscribe=state.beforeChange(()=>captured.push(state.preferences.fontSize));
 await act(async()=>get.resolve({...defaultReaderPreferences,fontSize:22}));
 await act(async()=>state.update({...state.preferences,fontSize:24}));expect(captured).toEqual([18,22]);unsubscribe();
});

it('drains current-login intent after an old-login PUT finishes without releasing a newer request',async()=>{
 vi.mocked(readerPreferencesApi.get).mockResolvedValue(defaultReaderPreferences);
 const old=deferred<typeof defaultReaderPreferences>(),current=deferred<typeof defaultReaderPreferences>();
 vi.mocked(readerPreferencesApi.put).mockReturnValueOnce(old.promise).mockReturnValueOnce(current.promise);await mount();
 await act(async()=>{state.update({...state.preferences,fontSize:20});await vi.advanceTimersByTimeAsync(500);});
 auth.user=null;await mount();auth.user={userName:'test'};await mount();
 await act(async()=>{state.update({...state.preferences,fontSize:24});await vi.advanceTimersByTimeAsync(500);});expect(readerPreferencesApi.put).toHaveBeenCalledTimes(1);
 await act(async()=>old.resolve({...defaultReaderPreferences,fontSize:20}));
 expect(vi.mocked(readerPreferencesApi.put).mock.calls.map(([p])=>p.fontSize)).toEqual([20,24]);expect(state.preferences.fontSize).toBe(24);
 await act(async()=>{state.update({...state.preferences,fontSize:26});await vi.advanceTimersByTimeAsync(500);});expect(readerPreferencesApi.put).toHaveBeenCalledTimes(2);
 await act(async()=>current.resolve({...defaultReaderPreferences,fontSize:24}));
 expect(vi.mocked(readerPreferencesApi.put).mock.calls.map(([p])=>p.fontSize)).toEqual([20,24,26]);expect(state.preferences.fontSize).toBe(26);expect(state.unsynced).toBe(false);
});
it('ignores old-login failures and retains a failed current-login edit for retry',async()=>{
 vi.mocked(readerPreferencesApi.get).mockResolvedValue(defaultReaderPreferences);const old=deferred<typeof defaultReaderPreferences>();
 vi.mocked(readerPreferencesApi.put).mockReturnValueOnce(old.promise).mockRejectedValueOnce(new Error('current offline'));await mount();
 await act(async()=>{state.update({...state.preferences,fontSize:20});await vi.advanceTimersByTimeAsync(500);});
 auth.user=null;await mount();auth.user={userName:'test'};await mount();
 await act(async()=>{state.update({...state.preferences,fontSize:24});await vi.advanceTimersByTimeAsync(500);});
 await act(async()=>old.reject(new Error('old offline')));
 expect(vi.mocked(readerPreferencesApi.put).mock.calls.map(([p])=>p.fontSize)).toEqual([20,24]);expect(state.unsynced).toBe(true);expect(state.preferences.fontSize).toBe(24);
 await act(async()=>state.retry());expect(state.unsynced).toBe(false);expect(vi.mocked(readerPreferencesApi.put).mock.lastCall?.[0].fontSize).toBe(24);
});
