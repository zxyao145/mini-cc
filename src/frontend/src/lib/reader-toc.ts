import { decorateReaderContent } from './reader-content';
export interface ReaderHeading { id:string; text:string; level:number; node:HTMLElement; }
export function collectReaderHeadings(root:HTMLElement):ReaderHeading[] {
  decorateReaderContent(root);
  const headings=Array.from(root.querySelectorAll<HTMLElement>('h1,h2,h3,h4'));
  const levels=Array.from(new Set(headings.map(h=>Number(h.tagName[1])))).sort().slice(0,2);
  const entries=headings.filter(h=>levels.includes(Number(h.tagName[1]))).map(node=>({node,id:node.id,text:node.textContent||'',level:Number(node.tagName[1])}));
  return entries.length<2?[]:entries;
}
