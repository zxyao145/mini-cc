import { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { useRef } from 'react';
import ReadableContent from '@/app/pages/article/[id]/components/ReadableContent';
import { useReadingProgress } from './useReadingProgress';
import { articleApi } from '@/lib/api';
import { Article } from '@/types';
vi.mock('@/lib/api', () => ({articleApi:{saveProgress:vi.fn(),resetProgress:vi.fn(),getArticleById:vi.fn()}}));
let root: Root, host: HTMLDivElement;
const article = {id:'a',readableContent:'<p>first</p><p>second</p><p>third</p>',progressUpdatedAt:null,progressTextOffset:null,progressBlockIndex:null,readingStatus:'unread'} as Article;
let readerState: ReturnType<typeof useReadingProgress>;
function Reader({data=article, active=true}:{data?:Article; active?:boolean}) { const ref=useRef<HTMLDivElement>(null); readerState=useReadingProgress(active ? data : undefined,ref,()=>{}); return active ? <ReadableContent contentRef={ref} html={data.readableContent}/> : null; }
beforeEach(() => {
  Object.assign(globalThis,{IS_REACT_ACT_ENVIRONMENT:true}); vi.useFakeTimers(); vi.resetAllMocks(); sessionStorage.clear();
  vi.spyOn(window,'scrollTo').mockImplementation(()=>{});
  vi.spyOn(HTMLElement.prototype,'getBoundingClientRect').mockImplementation(function(this:HTMLElement) { const i=Array.from(this.parentElement!.children).indexOf(this); return {top:i*300-200,bottom:i*300+100,height:300} as DOMRect; });
  vi.mocked(articleApi.saveProgress).mockResolvedValue({progressUpdatedAt:'v1',readingStatus:'reading',lastReadAt:'v1'});
  host=document.createElement('div');document.body.append(host);root=createRoot(host);
});
afterEach(async()=>{await act(async()=>root.unmount());host.remove();vi.useRealTimers();vi.restoreAllMocks();});
it('does not save an opened article or programmatic scroll; active visible scroll must dwell three seconds', async()=>{
  await act(async()=>root.render(<Reader/>));
  await act(async()=>{window.dispatchEvent(new Event('scroll'));await vi.advanceTimersByTimeAsync(6000);});
  expect(articleApi.saveProgress).not.toHaveBeenCalled();
  await act(async()=>{window.dispatchEvent(new WheelEvent('wheel'));window.dispatchEvent(new Event('scroll'));await vi.advanceTimersByTimeAsync(2999);});
  expect(articleApi.saveProgress).not.toHaveBeenCalled();
  await act(async()=>vi.advanceTimersByTimeAsync(2));
  expect(articleApi.saveProgress).toHaveBeenCalledTimes(1);
  expect(vi.mocked(articleApi.saveProgress).mock.calls[0][1].blockIndex).toBeGreaterThanOrEqual(0);
});
it('submits stored failed progress with its original base even when fresh detail is newer', async()=>{
  sessionStorage.setItem('progress:a',JSON.stringify({position:{textOffset:5,blockIndex:1,blockRatio:.5,percent:40},baseUpdatedAt:'old'}));
  await act(async()=>root.render(<Reader/>));
  expect(vi.mocked(articleApi.saveProgress).mock.calls[0][1].baseUpdatedAt).toBe('old');
});
it('restores a saved anchor, corrects image layout only during the two-second window, and stops on user intent',async()=>{
  const data={...article,progressTextOffset:5,progressBlockIndex:1,progressBlockRatio:.5,progressPercent:40};
  await act(async()=>root.render(<Reader data={data}/>));
  const image=document.createElement('img');host.querySelector('.readable-content')!.append(image);
  expect(window.scrollTo).toHaveBeenCalledTimes(1);
  await act(async()=>{image.dispatchEvent(new Event('load'));});
  expect(window.scrollTo).toHaveBeenCalledTimes(2);
  await act(async()=>{await vi.advanceTimersByTimeAsync(2001);image.dispatchEvent(new Event('load'));});
  expect(window.scrollTo).toHaveBeenCalledTimes(2);
  expect(articleApi.saveProgress).not.toHaveBeenCalled();
});
it('flushes an eligible throttled position with keepalive on pagehide instead of acknowledging it early',async()=>{
  await act(async()=>root.render(<Reader/>));
  await act(async()=>vi.advanceTimersByTimeAsync(100));
  await act(async()=>{window.dispatchEvent(new WheelEvent('wheel'));window.dispatchEvent(new Event('scroll'));await vi.advanceTimersByTimeAsync(3002);});
  expect(articleApi.saveProgress).toHaveBeenCalledTimes(1);
  await act(async()=>{window.dispatchEvent(new Event('scroll'));await vi.advanceTimersByTimeAsync(3002);});
  expect(articleApi.saveProgress).toHaveBeenCalledTimes(1);
  await act(async()=>{window.dispatchEvent(new Event('pagehide'));});
  expect(articleApi.saveProgress).toHaveBeenCalledTimes(2);
  expect(vi.mocked(articleApi.saveProgress).mock.calls[1][2]).toBe(true);
});
it('fetches on visibility restoration and requires a conflict choice before overwriting a newer other-session anchor',async()=>{
  let visibility='visible';vi.spyOn(document,'visibilityState','get').mockImplementation(()=>visibility as DocumentVisibilityState);
  vi.mocked(articleApi.getArticleById).mockResolvedValue({...article,progressUpdatedAt:'2026-11-08T01:00:00Z',progressSessionId:'another',progressTextOffset:5,progressBlockIndex:1,progressBlockRatio:.5,progressPercent:63});
  await act(async()=>root.render(<Reader/>));
  visibility='hidden';await act(async()=>document.dispatchEvent(new Event('visibilitychange')));
  visibility='visible';await act(async()=>document.dispatchEvent(new Event('visibilitychange')));
  expect(articleApi.getArticleById).toHaveBeenCalledWith('a');
  expect(readerState.conflict?.percent).toBe(63);
  expect(articleApi.saveProgress).not.toHaveBeenCalled();
  await act(async()=>readerState.chooseConflict(true));
  expect(readerState.conflict).toBeNull();expect(window.scrollTo).toHaveBeenCalledTimes(2);
});
it('honors the first user scroll during initial restore and retains dwell when highlights finish rendering',async()=>{
  await act(async()=>root.render(<Reader/>));
  await act(async()=>{window.dispatchEvent(new WheelEvent('wheel'));window.dispatchEvent(new Event('scroll'));await vi.advanceTimersByTimeAsync(100);host.querySelector('.readable-content')!.dispatchEvent(new Event('reader-highlights-rendered'));});
  await act(async()=>vi.advanceTimersByTimeAsync(3100));
  expect(articleApi.saveProgress).toHaveBeenCalledTimes(1);
});
it('does not resume retries when a visibility refresh finishes after the page is hidden again',async()=>{
  let visibility='visible';vi.spyOn(document,'visibilityState','get').mockImplementation(()=>visibility as DocumentVisibilityState);
  let release!: (value:Article)=>void;
  vi.mocked(articleApi.getArticleById).mockImplementation(()=>new Promise(resolve=>{release=resolve;}));
  vi.mocked(articleApi.saveProgress).mockRejectedValue({status:503});
  await act(async()=>root.render(<Reader/>));
  await act(async()=>vi.advanceTimersByTimeAsync(100));
  await act(async()=>{window.dispatchEvent(new WheelEvent('wheel'));window.dispatchEvent(new Event('scroll'));await vi.advanceTimersByTimeAsync(3002);});
  visibility='hidden';await act(async()=>document.dispatchEvent(new Event('visibilitychange')));
  visibility='visible';await act(async()=>document.dispatchEvent(new Event('visibilitychange')));
  visibility='hidden';await act(async()=>document.dispatchEvent(new Event('visibilitychange')));
  const count=vi.mocked(articleApi.saveProgress).mock.calls.length;
  await act(async()=>{release(article);await vi.advanceTimersByTimeAsync(30000);});
  expect(articleApi.saveProgress).toHaveBeenCalledTimes(count);
});
it('detects a newer server version within the same millisecond on visibility restoration',async()=>{
  const data={...article,progressUpdatedAt:'2026-11-08T01:00:00.000001+00:00'};
  vi.mocked(articleApi.getArticleById).mockResolvedValue({...data,progressUpdatedAt:'2026-11-08T01:00:00.000002+00:00',progressSessionId:'another',progressTextOffset:5,progressBlockIndex:1,progressBlockRatio:.5,progressPercent:63});
  await act(async()=>root.render(<Reader data={data}/>));
  await act(async()=>document.dispatchEvent(new Event('visibilitychange')));
  expect(readerState.conflict?.percent).toBe(63);
});
it('cancels pre-unread dwell and requires fresh active movement after acknowledgement',async()=>{
  await act(async()=>root.render(<Reader/>));await act(async()=>vi.advanceTimersByTimeAsync(100));
  await act(async()=>{window.dispatchEvent(new WheelEvent('wheel'));window.dispatchEvent(new Event('scroll'));await vi.advanceTimersByTimeAsync(2000);});
  await act(async()=>readerState.withStateChange(async()=>{},true));
  await act(async()=>vi.advanceTimersByTimeAsync(6000));
  expect(articleApi.saveProgress).not.toHaveBeenCalled();
  await act(async()=>{window.dispatchEvent(new WheelEvent('wheel'));window.dispatchEvent(new Event('scroll'));await vi.advanceTimersByTimeAsync(3002);});
  expect(articleApi.saveProgress).toHaveBeenCalledTimes(1);
});
it('does not carry active scroll eligibility into another article in the same mounted reader',async()=>{
  await act(async()=>root.render(<Reader/>));await act(async()=>vi.advanceTimersByTimeAsync(100));
  await act(async()=>{window.dispatchEvent(new WheelEvent('wheel'));window.dispatchEvent(new Event('scroll'));await vi.advanceTimersByTimeAsync(3002);});
  expect(articleApi.saveProgress).toHaveBeenCalledTimes(1);
  await act(async()=>root.render(<Reader data={{...article,id:'b'}}/>));
  await act(async()=>{await vi.advanceTimersByTimeAsync(100);host.querySelector('.readable-content')!.dispatchEvent(new Event('reader-highlights-rendered'));});
  await act(async()=>vi.advanceTimersByTimeAsync(4000));
  expect(articleApi.saveProgress).toHaveBeenCalledTimes(1);
});
it('captures the old anchor before a layout change and restores after two frames without creating a progress write',async()=>{
  const frames:FrameRequestCallback[]=[];
  vi.spyOn(window,'requestAnimationFrame').mockImplementation(callback=>{frames.push(callback);return frames.length;});
  await act(async()=>root.render(<Reader/>));
  await act(async()=>{while(frames.length)frames.shift()!(0);});
  vi.mocked(window.scrollTo).mockClear();
  await act(async()=>readerState.preserveLayout());
  expect(window.scrollTo).not.toHaveBeenCalled();
  // The paragraph geometry changes after the old anchor was recorded.
  vi.mocked(HTMLElement.prototype.getBoundingClientRect).mockImplementation(function(this:HTMLElement){const i=Array.from(this.parentElement!.children).indexOf(this);return {top:i*600-200,bottom:i*600+400,height:600} as DOMRect;});
  await act(async()=>frames.shift()!(0));expect(window.scrollTo).not.toHaveBeenCalled();
  await act(async()=>frames.shift()!(0));expect(window.scrollTo).toHaveBeenCalledTimes(1);
  await act(async()=>{window.dispatchEvent(new Event('scroll'));await vi.advanceTimersByTimeAsync(4000);});expect(articleApi.saveProgress).not.toHaveBeenCalled();
});
it('retains the original anchor across rapid preference changes before restoration frames finish',async()=>{
 const frames:FrameRequestCallback[]=[];vi.spyOn(window,'requestAnimationFrame').mockImplementation(callback=>{frames.push(callback);return frames.length;});
 await act(async()=>root.render(<Reader/>));await act(async()=>{while(frames.length)frames.shift()!(0);});vi.mocked(window.scrollTo).mockClear();
 await act(async()=>readerState.preserveLayout());
 vi.mocked(HTMLElement.prototype.getBoundingClientRect).mockReturnValue({top:0,bottom:2000,height:2000} as DOMRect);
 await act(async()=>readerState.preserveLayout());
 await act(async()=>{while(frames.length)frames.shift()!(0);});
 const expected=Math.max(0,((window.innerHeight*.2-100)/300)*2000-window.innerHeight*.2);
 expect(window.scrollTo).toHaveBeenCalledTimes(1);expect(vi.mocked(window.scrollTo).mock.calls[0][0]).toEqual({top:expected,behavior:'instant'});
});
it('pauses obsolete summary coordinates after full-content replacement on visibility and conflict choice', async()=>{
  const summary={...article,contentKind:'summaryOnly' as const};
  const full={...article,contentKind:'full' as const,readableContent:'<p>New full body</p>',progressUpdatedAt:'2026-11-08T01:00:00Z',progressSessionId:null};
  vi.mocked(articleApi.getArticleById).mockResolvedValue(full);
  await act(async()=>root.render(<Reader data={summary}/>));
  await act(async()=>document.dispatchEvent(new Event('visibilitychange')));
  expect(readerState.contentChanged).toBe(true);
  await act(async()=>readerState.chooseConflict(false));
  await act(async()=>{window.dispatchEvent(new WheelEvent('wheel'));window.dispatchEvent(new Event('scroll'));await vi.advanceTimersByTimeAsync(10000);});
  expect(articleApi.saveProgress).not.toHaveBeenCalled();
});
it('discards a failed summary position on reopening full content, so Stay cannot rebase obsolete coordinates', async()=>{
  const summary={...article,contentKind:'summaryOnly' as const};
  vi.mocked(articleApi.saveProgress).mockRejectedValue({status:503});
  await act(async()=>root.render(<Reader data={summary}/>));
  await act(async()=>{window.dispatchEvent(new WheelEvent('wheel'));window.dispatchEvent(new Event('scroll'));await vi.advanceTimersByTimeAsync(3002);});
  expect(sessionStorage.getItem('progress:a')).not.toBeNull();
  await act(async()=>root.render(null));
  expect(sessionStorage.getItem('progress:a')).not.toBeNull();
  vi.mocked(articleApi.saveProgress).mockClear();
  const full={...article,contentKind:'full' as const,readableContent:'<p>Entire new body</p>',progressUpdatedAt:'2026-11-08T01:00:00Z',progressSessionId:null};
  vi.mocked(articleApi.saveProgress).mockRejectedValue({status:409,current:{progressUpdatedAt:full.progressUpdatedAt,blockIndex:null,textOffset:null,blockRatio:null,percent:null}});
  await act(async()=>root.render(<Reader data={full}/>));
  await act(async()=>readerState.chooseConflict(false));
  await act(async()=>vi.advanceTimersByTimeAsync(10000));
  expect(articleApi.saveProgress).not.toHaveBeenCalled();
  expect(sessionStorage.getItem('progress:a')).toBeNull();
  expect(readerState.conflict).toBeNull();
});
it('does not adopt a reset version if completion wins before the reset transaction acknowledgement', async()=>{
  let complete!: (value: import('@/lib/progress-writer').ProgressSaved)=>void;
  vi.mocked(articleApi.resetProgress).mockImplementation(()=>new Promise(resolve=>{complete=resolve;}));
  const summary={...article,contentKind:'summaryOnly' as const};
  await act(async()=>root.render(<Reader data={summary}/>));
  let resetting!:Promise<unknown>;
  await act(async()=>{resetting=readerState.reset().catch(error=>error);});
  await act(async()=>{complete({progressUpdatedAt:'2026-11-08T03:00:00Z',readingStatus:'unread',lastReadAt:null,contentKind:'full'});await resetting;});
  expect(readerState.contentChanged).toBe(true);
  await act(async()=>readerState.chooseConflict(false));
  await act(async()=>{window.dispatchEvent(new WheelEvent('wheel'));window.dispatchEvent(new Event('scroll'));await vi.advanceTimersByTimeAsync(10000);});
  expect(articleApi.saveProgress).not.toHaveBeenCalled();
});
it('still resets and saves a summary when the acknowledgement confirms the same loaded kind', async()=>{
  const summary={...article,contentKind:'summaryOnly' as const};
  vi.mocked(articleApi.resetProgress).mockResolvedValue({progressUpdatedAt:'2026-11-08T03:00:00Z',readingStatus:'unread',lastReadAt:null,contentKind:'summaryOnly'});
  await act(async()=>root.render(<Reader data={summary}/>));
  await act(async()=>readerState.reset());
  expect(readerState.contentChanged).toBe(false);
  await act(async()=>{await vi.advanceTimersByTimeAsync(100);window.dispatchEvent(new WheelEvent('wheel'));window.dispatchEvent(new Event('scroll'));await vi.advanceTimersByTimeAsync(3002);});
  expect(articleApi.saveProgress).toHaveBeenCalledTimes(1);
  expect(vi.mocked(articleApi.saveProgress).mock.calls[0][1].baseUpdatedAt).toBe('2026-11-08T03:00:00Z');
});
it('keeps the loaded summary kind for a409 Stay check even if metadata claims full', async()=>{
  const summary={...article,contentKind:'summaryOnly' as const};
  const fullMetadata={...summary,contentKind:'full' as const};
  const version='2026-11-08T03:00:00Z';
  vi.mocked(articleApi.saveProgress).mockRejectedValueOnce({status:409,current:{progressUpdatedAt:version,blockIndex:null,textOffset:null,percent:null,blockRatio:null}})
    .mockResolvedValue({progressUpdatedAt:'2026-11-08T03:00:01Z',readingStatus:'reading',lastReadAt:version,contentKind:'full'});
  vi.mocked(articleApi.getArticleById).mockResolvedValue({...fullMetadata,readableContent:'<p>New full body</p>',progressUpdatedAt:version});
  await act(async()=>root.render(<Reader data={summary}/>));
  await act(async()=>root.render(<Reader data={fullMetadata}/>));
  await act(async()=>{window.dispatchEvent(new WheelEvent('wheel'));window.dispatchEvent(new Event('scroll'));await vi.advanceTimersByTimeAsync(3002);});
  expect(readerState.conflict).not.toBeNull(); expect(articleApi.saveProgress).toHaveBeenCalledTimes(1);
  await act(async()=>readerState.chooseConflict(false));
  await act(async()=>vi.advanceTimersByTimeAsync(10000));
  expect(readerState.contentChanged).toBe(true); expect(articleApi.saveProgress).toHaveBeenCalledTimes(1);
});
it('requires refresh when a summary reset acknowledgement cannot prove the loaded body kind', async()=>{
  const summary={...article,contentKind:'summaryOnly' as const};
  vi.mocked(articleApi.resetProgress).mockResolvedValue({progressUpdatedAt:'2026-11-08T03:00:00Z',readingStatus:'unread',lastReadAt:null});
  await act(async()=>root.render(<Reader data={summary}/>));
  await act(async()=>{await expect(readerState.reset()).rejects.toThrow('正文已更新');});
  expect(readerState.contentChanged).toBe(true);
});

it('clears the reader status and stops observing progress when the same reader becomes inactive', async()=>{
  const summary={...article,contentKind:'summaryOnly' as const};
  const full={...article,contentKind:'full' as const,progressUpdatedAt:'2026-11-08T01:00:00Z'};
  vi.mocked(articleApi.getArticleById).mockResolvedValue(full);
  const removeListener=vi.spyOn(window,'removeEventListener');
  await act(async()=>root.render(<Reader data={summary}/>));
  await act(async()=>document.dispatchEvent(new Event('visibilitychange')));
  expect(readerState.contentChanged).toBe(true);
  expect(readerState.conflict).not.toBeNull();

  await act(async()=>root.render(<Reader active={false}/>));

  expect(readerState.contentChanged).toBe(false);
  expect(readerState.syncStatus).toBe('synced');
  expect(readerState.conflict).toBeNull();
  expect(readerState.atEnd).toBe(false);
  expect(removeListener).toHaveBeenCalledWith('wheel',expect.any(Function));
  await act(async()=>document.dispatchEvent(new Event('visibilitychange')));
  expect(articleApi.getArticleById).toHaveBeenCalledTimes(1);
});
