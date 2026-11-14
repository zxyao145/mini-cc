import { act,createRef } from 'react';
import { createRoot } from 'react-dom/client';
import { expect,it,vi } from 'vitest';
import ReaderPanel from './ReaderPanel';
it('uses the TOC1200px drawer breakpoint and updates settings modality and presentation across768px',async()=>{
 Object.assign(globalThis,{IS_REACT_ACT_ENVIRONMENT:true});const host=document.createElement('div');document.body.append(host);const root=createRoot(host),trigger=createRef<HTMLButtonElement>();
 let width=1024;const queries=new Map<string,MediaQueryList>();
 const media=vi.spyOn(window,'matchMedia').mockImplementation(query=>{
  const target=new EventTarget() as MediaQueryList;Object.defineProperty(target,'matches',{get:()=>width<Number(query.match(/\d+/)![0])+1});queries.set(query,target);return target;
 });
 const resize=async(value:number)=>{width=value;await act(async()=>queries.forEach(query=>query.dispatchEvent(new Event('change'))));};
 try{
  await act(async()=>root.render(<ReaderPanel label="目录" drawerBreakpoint={1200} trigger={trigger} onClose={()=>{}}><a href="#section">Section</a></ReaderPanel>));
  expect(host.querySelector('[role="dialog"]')?.getAttribute('aria-modal')).toBe('true');expect(host.querySelector('.reader-panel.reader-drawer')).not.toBeNull();
  await act(async()=>root.render(<ReaderPanel label="阅读设置" drawerBreakpoint={768} trigger={trigger} onClose={()=>{}}><button>Setting</button></ReaderPanel>));
  expect(host.querySelector('[role="dialog"]')?.hasAttribute('aria-modal')).toBe(false);expect(host.querySelector('.reader-panel.reader-drawer')).toBeNull();
  await resize(375);expect(host.querySelector('[role="dialog"]')?.getAttribute('aria-modal')).toBe('true');expect(host.querySelector('.reader-backdrop-visible')).not.toBeNull();
  await resize(1024);expect(host.querySelector('[role="dialog"]')?.hasAttribute('aria-modal')).toBe(false);expect(host.querySelector('.reader-panel.reader-drawer')).toBeNull();
 }finally{await act(async()=>root.unmount());host.remove();media.mockRestore();}
});
