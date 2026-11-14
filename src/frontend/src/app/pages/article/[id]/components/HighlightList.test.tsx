import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, it, vi } from 'vitest';
import { highlightApi } from '@/lib/api';
import { Highlight } from '@/types';
import HighlightList from './HighlightList';

vi.mock('@/lib/api', () => ({ articleApi: { deleteHighlight: vi.fn() }, highlightApi: { updateHighlight: vi.fn() } }));

it('keeps the localized note editor and saves the edited note', async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const highlight = { id: 'highlight', articleId: 'article', text: 'Selected text', note: '', color: '#ffeb3b', startOffset: 0, endOffset: 13, createdAt: '' } as Highlight;
  const updated = { ...highlight, note: 'Saved note' };
  const onHighlightsChange = vi.fn();
  const host = document.createElement('div'); document.body.append(host); const root = createRoot(host);
  vi.mocked(highlightApi.updateHighlight).mockResolvedValue(updated);
  try {
    await act(async () => root.render(<HighlightList highlights={[highlight]} onHighlightsChange={onHighlightsChange}/>));
    const heading = host.querySelector<HTMLButtonElement>('button[aria-expanded="false"]')!;
    expect(heading.textContent).toContain('高亮笔记');
    await act(async () => heading.click());
    await act(async () => Array.from(host.querySelectorAll('button')).find(button => button.textContent?.includes('添加笔记'))!.click());
    const textarea = host.querySelector<HTMLTextAreaElement>('textarea')!;
    expect(textarea.labels?.[0]?.textContent).toBe('笔记');
    const setValue = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value')?.set;
    await act(async () => { setValue?.call(textarea, 'Saved note'); textarea.dispatchEvent(new Event('input', { bubbles: true })); });
    await act(async () => Array.from(host.querySelectorAll('button')).find(button => button.textContent === '保存')!.click());
    expect(highlightApi.updateHighlight).toHaveBeenCalledWith('highlight', 'Saved note');
    expect(onHighlightsChange).toHaveBeenCalledWith([updated]);
  } finally { await act(async () => root.unmount()); host.remove(); }
});
