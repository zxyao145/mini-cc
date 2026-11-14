import { CSSProperties } from 'react';
export interface ReaderPreferences {
  fontSize:number; lineHeight:number; width:'narrow'|'standard'|'wide'; fontFamily:'system'|'serif'; theme:'system'|'light'|'dark'|'sepia';
}
export const defaultReaderPreferences:ReaderPreferences={fontSize:18,lineHeight:1.8,width:'standard',fontFamily:'system',theme:'system'};
export function normalizeReaderPreferences(value:Partial<ReaderPreferences>|null):ReaderPreferences {
  const p={...defaultReaderPreferences,...value};
  return {
    fontSize:Number.isInteger(p.fontSize)&&p.fontSize>=16&&p.fontSize<=28?p.fontSize:18,
    lineHeight:Number.isFinite(p.lineHeight)&&p.lineHeight>=1.5&&p.lineHeight<=2.2?p.lineHeight:1.8,
    width:['narrow','standard','wide'].includes(p.width)?p.width:'standard',
    fontFamily:p.fontFamily==='serif'?'serif':'system',theme:['system','light','dark','sepia'].includes(p.theme)?p.theme:'system',
  };
}
export function readerPreferenceStyle(p:ReaderPreferences):CSSProperties {
  return {'--reader-font-size':`${p.fontSize}px`,'--reader-line-height':p.lineHeight,
    '--reader-max-width':`${{narrow:640,standard:760,wide:920}[p.width]}px`,
    '--reader-font':p.fontFamily==='serif'?'"Source Han Serif SC", "Noto Serif SC", Georgia, serif':'system-ui, -apple-system, "PingFang SC", "Microsoft YaHei", sans-serif',
    '--reader-mono':'ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", monospace'} as CSSProperties;
}
