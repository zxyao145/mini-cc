import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, it, vi } from 'vitest';
import { articleApi } from '@/lib/api';
import { Tag } from '@/types';
import TagManager from './TagManager';

vi.mock('@/lib/api', () => ({ articleApi: { removeTag: vi.fn() } }));

it('keeps the localized tag removal name and removes the selected tag', async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const tag = { id: 'tag-dotnet', name: '.NET', color: '#3b82f6', createdAt: '' } as Tag;
  const onTagsChange = vi.fn();
  const host = document.createElement('div'); document.body.append(host); const root = createRoot(host);
  vi.mocked(articleApi.removeTag).mockResolvedValue(undefined as never);
  try {
    await act(async () => root.render(<TagManager articleId="article" tags={[tag]} onTagsChange={onTagsChange}/>));
    const remove = host.querySelector<HTMLButtonElement>('button[aria-label="移除 .NET 标签"]')!;
    expect(remove).not.toBeNull();
    await act(async () => remove.click());
    expect(articleApi.removeTag).toHaveBeenCalledWith('article', 'tag-dotnet');
    expect(onTagsChange).toHaveBeenCalledWith([]);
  } finally { await act(async () => root.unmount()); host.remove(); }
});
