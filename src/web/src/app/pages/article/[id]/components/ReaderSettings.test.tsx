import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { expect,it,vi } from 'vitest';
import ReaderSettings from './ReaderSettings';
import { defaultReaderPreferences } from '@/lib/reader-preferences';
const update=vi.fn();
vi.mock('@/contexts/ReaderPreferencesContext',()=>({useReaderPreferences:()=>({preferences:{fontSize:20,lineHeight:1.8,width:'standard',fontFamily:'system',theme:'system'},update,unsynced:true,retry:vi.fn()})}));
it('opens an accessible settings dialog, restores focus on Escape/outside and exposes default reset and sync retry',async()=>{
 Object.assign(globalThis,{IS_REACT_ACT_ENVIRONMENT:true});const host=document.createElement('div');document.body.append(host);const root=createRoot(host);
 try{
  await act(async()=>root.render(<ReaderSettings/>));const trigger=host.querySelector<HTMLButtonElement>('[aria-label="阅读设置"]')!;
  await act(async()=>trigger.click());const dialog=host.querySelector('[role="dialog"]')!;expect(dialog).not.toBeNull();expect(dialog.contains(document.activeElement)).toBe(true);
  expect(dialog.textContent).toContain('偏好未同步');expect(dialog.querySelector('input')?.getAttribute('step')).toBe('0.1');
  await act(async()=>Array.from(dialog.querySelectorAll('button')).find(b=>b.textContent==='恢复默认')!.click());expect(update).toHaveBeenCalledWith(defaultReaderPreferences);
  await act(async()=>document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape'})));expect(host.querySelector('[role="dialog"]')).toBeNull();expect(document.activeElement).toBe(trigger);
  await act(async()=>trigger.click());await act(async()=>document.body.dispatchEvent(new PointerEvent('pointerdown',{bubbles:true})));expect(host.querySelector('[role="dialog"]')).toBeNull();
 }finally{await act(async()=>root.unmount());host.remove();}
});
