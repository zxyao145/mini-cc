import { expect,it } from 'vitest';
import { collectReaderHeadings } from './reader-toc';
it('selects the highest two levels actually present and preserves heading text and existing IDs',()=>{
 const root=document.createElement('div');root.innerHTML='<h3 id="existing">三😀</h3><p>Text</p><h4>四</h4><h2>二</h2><h3>另一个三</h3><h5>excluded</h5>';
 const text=root.textContent;const entries=collectReaderHeadings(root);expect(entries.map(h=>h.level)).toEqual([3,2,3]);expect(entries[0].id).toBe('existing');expect(entries[1].id).toBeTruthy();expect(root.textContent).toBe(text);
 expect(collectReaderHeadings(root).map(h=>h.id)).toEqual(entries.map(h=>h.id));
});
it('uses h3/h4 when h1/h2 are absent and omits a directory with fewer than two entries',()=>{
 const root=document.createElement('div');root.innerHTML='<h3>One</h3><h4>Two</h4>';expect(collectReaderHeadings(root)).toHaveLength(2);
 root.innerHTML='<h2>Only</h2><h5>Not eligible</h5>';expect(collectReaderHeadings(root)).toEqual([]);
});
