import { act, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, it, vi } from 'vitest';
import { feedApi, tagApi } from '@/lib/api';
import FeedDialog from './FeedDialog';
vi.mock('@/lib/api', () => ({feedApi:{preview:vi.fn(),create:vi.fn(),edit:vi.fn()},tagApi:{getTags:vi.fn()}}));
it('previews before saving, accepts confirmed title and tags, and reports the imported count', async()=>{
  Object.assign(globalThis,{IS_REACT_ACT_ENVIRONMENT:true});
  vi.mocked(tagApi.getTags).mockResolvedValue([{id:'t',name:'Tech',color:'#000',createdAt:'',articleCount:0}]);
  vi.mocked(feedApi.preview).mockResolvedValue({feedUrl:'https://example.com/feed',title:'Original',siteUrl:'https://example.com',format:'rss',items:[{title:'Recent entry',url:'https://example.com/1',publishedAt:null,contentKind:'summaryOnly'}]});
  vi.mocked(feedApi.create).mockResolvedValue({feed:{} as never,imported:20});
  const onDone=vi.fn(), onClose=vi.fn(); const host=document.createElement('div');document.body.append(host);const root=createRoot(host);
  try {
    await act(async()=>root.render(<FeedDialog onDone={onDone} onClose={onClose}/>));
    const dialog=document.body.querySelector<HTMLElement>('[role="dialog"]');
    expect(dialog).not.toBeNull();
    if (!dialog) return;
    const input=dialog.querySelector<HTMLInputElement>('input[type="url"]')!;
    expect(host.contains(dialog)).toBe(false);
    expect(document.activeElement).toBe(input);
    expect(dialog.textContent).toContain('添加订阅 · 1 / 2');
    await act(async()=>{input.value='https://example.com';input.dispatchEvent(new Event('input',{bubbles:true}));input.dispatchEvent(new Event('change',{bubbles:true}));});
    // Native form data captures typed URL independent of controlled React event simulation.
    await act(async()=>dialog.querySelector('form')!.dispatchEvent(new Event('submit',{bubbles:true,cancelable:true})));
    expect(feedApi.preview).toHaveBeenCalledWith('https://example.com');expect(feedApi.create).not.toHaveBeenCalled();
    expect(dialog.textContent).toContain('Recent entry');
    expect(dialog.textContent).toContain('添加订阅 · 2 / 2');
    await act(async()=>dialog.querySelector('form')!.dispatchEvent(new Event('submit',{bubbles:true,cancelable:true})));
    expect(feedApi.create).toHaveBeenCalledWith('https://example.com/feed','Original',[]);
    expect(onDone).toHaveBeenCalledWith('已导入 20 篇');
  } finally { await act(async()=>root.unmount());host.remove(); }
});

it('restores focus to the button that opened the dialog', async()=>{
  Object.assign(globalThis,{IS_REACT_ACT_ENVIRONMENT:true});
  vi.mocked(tagApi.getTags).mockResolvedValue([]);
  const host=document.createElement('div');document.body.append(host);const root=createRoot(host);
  function Harness(){
    const [open,setOpen]=useState(false);
    return <><button onClick={()=>setOpen(true)}>添加订阅</button>{open && <FeedDialog onDone={()=>{}} onClose={()=>setOpen(false)}/>}</>;
  }
  try {
    await act(async()=>root.render(<Harness/>));
    const opener=host.querySelector<HTMLButtonElement>('button')!;
    opener.focus();
    await act(async()=>opener.click());
    const dialog=document.body.querySelector<HTMLElement>('[role="dialog"]')!;
    expect(document.activeElement).toBe(dialog.querySelector('input[type="url"]'));
    await act(async()=>dialog.querySelector<HTMLButtonElement>('[aria-label="关闭订阅对话框"]')!.click());
    await vi.waitFor(() => expect(document.activeElement).toBe(opener));
  } finally { await act(async()=>root.unmount());host.remove(); }
});

it('blocks outside and escape dismissal while a request is pending', async()=>{
  Object.assign(globalThis,{IS_REACT_ACT_ENVIRONMENT:true});
  vi.mocked(tagApi.getTags).mockResolvedValue([]);
  vi.mocked(feedApi.preview).mockReturnValue(new Promise(() => {}));
  const onDone=vi.fn(), onClose=vi.fn(); const host=document.createElement('div');document.body.append(host);const root=createRoot(host);
  try {
    await act(async()=>root.render(<FeedDialog onDone={onDone} onClose={onClose}/>));
    const dialog=document.body.querySelector<HTMLElement>('[role="dialog"]')!;
    const input=dialog.querySelector<HTMLInputElement>('input[type="url"]')!;
    await act(async()=>{input.value='https://example.com';input.dispatchEvent(new Event('input',{bubbles:true}));input.dispatchEvent(new Event('change',{bubbles:true}));});
    await act(async()=>dialog.querySelector('form')!.dispatchEvent(new Event('submit',{bubbles:true,cancelable:true})));
    const escape=new KeyboardEvent('keydown',{key:'Escape',bubbles:true,cancelable:true});
    await act(async()=>dialog.dispatchEvent(escape));
    expect(escape.defaultPrevented).toBe(true);
    const outside=document.createElement('button');document.body.append(outside);
    await act(async()=>outside.dispatchEvent(new Event('pointerdown',{bubbles:true,cancelable:true})));
    expect(onClose).not.toHaveBeenCalled();
    expect(dialog.isConnected).toBe(true);
    expect(dialog.querySelector<HTMLButtonElement>('[aria-label="关闭订阅对话框"]')?.disabled).toBe(true);
    outside.remove();
  } finally { await act(async()=>root.unmount());host.remove(); }
});
