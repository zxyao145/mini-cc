import { act } from 'react';
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
    const input=host.querySelector<HTMLInputElement>('input[type="url"]')!;
    await act(async()=>{input.value='https://example.com';input.dispatchEvent(new Event('input',{bubbles:true}));input.dispatchEvent(new Event('change',{bubbles:true}));});
    // Native form data captures typed URL independent of controlled React event simulation.
    await act(async()=>host.querySelector('form')!.dispatchEvent(new Event('submit',{bubbles:true,cancelable:true})));
    expect(feedApi.preview).toHaveBeenCalledWith('https://example.com');expect(feedApi.create).not.toHaveBeenCalled();
    expect(host.textContent).toContain('Recent entry');
    await act(async()=>host.querySelector('form')!.dispatchEvent(new Event('submit',{bubbles:true,cancelable:true})));
    expect(feedApi.create).toHaveBeenCalledWith('https://example.com/feed','Original',[]);
    expect(onDone).toHaveBeenCalledWith('已导入 20 篇');
  } finally { await act(async()=>root.unmount());host.remove(); }
});
