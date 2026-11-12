import { act,useRef } from 'react';
import { createRoot } from 'react-dom/client';
import { expect,it,vi } from 'vitest';
import { useReaderToc } from './ReaderToc';
it('tracks the current section when a scroll jumps past the observer band, observes the required margins and jumps without adding history',async()=>{
 Object.assign(globalThis,{IS_REACT_ACT_ENVIRONMENT:true});const host=document.createElement('div');document.body.append(host);const root=createRoot(host);let toc!:ReturnType<typeof useReaderToc>;
 const html='<h3>First</h3><p>Body</p><h4>Second</h4><p>More</p>';
 function Reader(){const ref=useRef<HTMLDivElement>(null);toc=useReaderToc(ref,html);return <div ref={ref} dangerouslySetInnerHTML={{__html:html}}/>;}
 const observe=vi.fn(),disconnect=vi.fn(),config=vi.fn();const Original=globalThis.IntersectionObserver;
 globalThis.IntersectionObserver=class {constructor(_callback:unknown,options:unknown){config(options);}observe=observe;disconnect=disconnect;} as unknown as typeof IntersectionObserver;
 const replace=vi.spyOn(history,'replaceState').mockImplementation(()=>{});const jump=vi.spyOn(HTMLElement.prototype,'scrollIntoView').mockImplementation(()=>{});
 let top=500;const geometry=vi.spyOn(HTMLElement.prototype,'getBoundingClientRect').mockImplementation(function(this:HTMLElement){return {top:this.textContent==='First'?-500:top} as DOMRect;});
 try{
  await act(async()=>root.render(<Reader/>));expect(config).toHaveBeenCalledWith({rootMargin:'-20% 0px -70% 0px'});expect(observe).toHaveBeenCalledTimes(2);
  top=-50;await act(async()=>window.dispatchEvent(new Event('scroll')));expect(toc.active).toBe(toc.headings[1].id);
  await act(async()=>toc.jump(toc.headings[0]));expect(jump).toHaveBeenCalled();expect(replace).toHaveBeenCalledWith(history.state,'',`#${toc.headings[0].id}`);
 }finally{await act(async()=>root.unmount());host.remove();geometry.mockRestore();replace.mockRestore();jump.mockRestore();globalThis.IntersectionObserver=Original;}
});
