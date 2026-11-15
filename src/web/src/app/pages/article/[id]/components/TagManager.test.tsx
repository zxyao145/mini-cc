import { act, useState } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { articleApi, tagApi } from '@/lib/api';
import { Tag, type TagWithArticleCount } from '@/types';
import TagManager from './TagManager';

vi.mock('@/lib/api', () => ({
  articleApi: { removeTag: vi.fn(), addTag: vi.fn() },
  tagApi: { getTags: vi.fn() },
}));
vi.mock('@/contexts/ToastContext', () => ({ useToast: () => ({ show }) }));

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

const show = vi.fn();
const onTagsChange = vi.fn();
const [go, gopher, dotnet, reading] = [
  { id: 'tag-go', name: 'Go', color: '#3b82f6' },
  { id: 'tag-gopher', name: 'Gopher', color: '#10b981' },
  { id: 'tag-dotnet', name: '.NET', color: '#8b5cf6' },
  { id: 'tag-reading', name: '阅读', color: '#ef4444' },
].map((tag): TagWithArticleCount => ({ ...tag, createdAt: '', articleCount: 0 }));
const initialTags = [dotnet];
let host: HTMLDivElement;
let root: Root;

beforeEach(() => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  vi.resetAllMocks();
  vi.mocked(tagApi.getTags).mockResolvedValue([go, gopher, dotnet, reading]);
  vi.mocked(articleApi.addTag).mockImplementation(async (_, { name, color }) => ({
    id: name, name, color: color ?? '#3b82f6', createdAt: '',
  }));
  vi.mocked(articleApi.removeTag).mockResolvedValue();
  host = document.createElement('div');
  document.body.append(host);
  root = createRoot(host);
});

afterEach(async () => {
  await act(async () => root.unmount());
  host.remove();
});

function Harness() {
  const [tags, setTags] = useState<Tag[]>(initialTags);
  return <TagManager articleId="article" tags={tags} onTagsChange={updated => { setTags(updated); onTagsChange(updated); }} />;
}

async function render() {
  await act(async () => root.render(<Harness />));
}

function button(name: string) {
  return Array.from(document.body.querySelectorAll('button')).find(element => {
    const label = element.getAttribute('aria-label');
    return (label ?? element.textContent?.trim()) === name;
  });
}

function options() {
  return Array.from(document.body.querySelectorAll<HTMLElement>('[role="option"]'));
}

function option(name: string) {
  return options().find(element => element.textContent?.trim() === name)!;
}

async function open() {
  await act(async () => button('添加标签')!.click());
  const input = document.body.querySelector<HTMLInputElement>('[role="combobox"]');
  expect(input).not.toBeNull();
  return input!;
}

async function fill(input: HTMLInputElement, value: string) {
  await act(async () => {
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(input, value);
    input.dispatchEvent(new Event('input', { bubbles: true }));
  });
}

function keydown(input: HTMLInputElement, key: string, init?: KeyboardEventInit) {
  return input.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, ...init }));
}

async function press(input: HTMLInputElement, key: string, init?: KeyboardEventInit) {
  await act(async () => { keydown(input, key, init); });
}

it('opens a focused combobox listing unassigned tags and loads only on open', async () => {
  await render();
  expect(tagApi.getTags).not.toHaveBeenCalled();
  const input = await open();

  expect(tagApi.getTags).toHaveBeenCalledTimes(1);
  expect(document.activeElement).toBe(input);
  expect(input.placeholder).toBe('搜索或新建标签…');
  expect(input.maxLength).toBe(100);
  expect(input.getAttribute('aria-expanded')).toBe('true');
  expect(document.getElementById(input.getAttribute('aria-controls')!)?.getAttribute('role')).toBe('listbox');
  expect(options().map(element => element.textContent?.trim())).toEqual(['Go', 'Gopher', '阅读']);
});

it('adds a clicked option by its original name and keeps the form open', async () => {
  vi.mocked(articleApi.addTag).mockResolvedValue(go);
  await render();
  const input = await open();
  await fill(input, 'g');
  await act(async () => option('Go').click());

  expect(articleApi.addTag).toHaveBeenCalledWith('article', { name: 'Go', color: '#3b82f6' });
  expect(onTagsChange).toHaveBeenCalledWith([dotnet, go]);
  expect(input.value).toBe('');
  expect(document.activeElement).toBe(input);
  expect(button('完成')).toBeDefined();
  expect(button('添加标签')?.getAttribute('aria-expanded')).toBe('true');
  expect(option('Go')).toBeUndefined();
});

it('adds the case-insensitive exact match on Enter without a highlight', async () => {
  await render();
  const input = await open();
  await fill(input, ' go ');

  expect(options().map(element => element.textContent?.trim())).toEqual(['Go', 'Gopher']);
  expect(document.body.querySelector('legend')).toBeNull();
  await press(input, 'Enter');
  expect(articleApi.addTag).toHaveBeenCalledWith('article', { name: 'Go', color: '#3b82f6' });
});

it('moves the highlight with the arrow keys and selects it with Enter', async () => {
  await render();
  const input = await open();
  await fill(input, 'go');
  await press(input, 'ArrowDown');
  const gopherOption = option('Gopher');
  await press(input, 'ArrowDown');

  expect(input.getAttribute('aria-activedescendant')).toBe(gopherOption.id);
  expect(gopherOption.getAttribute('aria-selected')).toBe('true');
  await press(input, 'Enter');
  expect(articleApi.addTag).toHaveBeenCalledWith('article', { name: 'Gopher', color: '#10b981' });
});

it('offers a create option and the chosen color for a new name', async () => {
  await render();
  const input = await open();
  await fill(input, 'Rust');

  expect(document.body.textContent).toContain('没有匹配的标签');
  expect(option('创建「Rust」')).toBeDefined();
  expect(document.body.querySelector('legend')?.textContent).toBe('新标签颜色');
  await act(async () => button('选择颜色 #ef4444')!.click());
  await press(input, 'Enter');

  expect(articleApi.addTag).toHaveBeenCalledWith('article', { name: 'Rust', color: '#ef4444' });
  expect(input.value).toBe('');
  expect(document.body.querySelector('legend')).toBeNull();
  await fill(input, 'Another');
  expect(button('选择颜色 #3b82f6')?.getAttribute('aria-pressed')).toBe('true');
});

it('shows 已添加 and ignores Enter for a tag already on the article', async () => {
  await render();
  const input = await open();
  await fill(input, '.net');

  expect(document.body.textContent).toContain('已添加');
  expect(options()).toHaveLength(0);
  expect(document.body.textContent).not.toContain('没有匹配的标签');
  expect(button('添加')?.disabled).toBe(true);
  await press(input, 'Enter');
  expect(articleApi.addTag).not.toHaveBeenCalled();
});

it('adds a highlighted partial match when the typed name is already attached', async () => {
  const dotnetCore = { ...dotnet, id: 'tag-dotnet-core', name: '.NET Core' };
  vi.mocked(tagApi.getTags).mockResolvedValue([dotnet, dotnetCore]);
  await render();
  const input = await open();
  await fill(input, '.net');

  expect(document.body.textContent).toContain('已添加');
  expect(button('添加')?.disabled).toBe(true);
  await press(input, 'ArrowDown');
  expect(input.getAttribute('aria-activedescendant')).toBe(option('.NET Core').id);
  await press(input, 'Enter');

  expect(articleApi.addTag).toHaveBeenCalledExactlyOnceWith('article', { name: '.NET Core', color: dotnetCore.color });
});

it('waits for existing tags before treating the query as a new name', async () => {
  let finishLoading!: (tags: TagWithArticleCount[]) => void;
  vi.mocked(tagApi.getTags).mockReturnValue(new Promise(resolve => { finishLoading = resolve; }));
  await render();
  const input = await open();
  await fill(input, 'go');
  expect(document.body.textContent).toContain('正在加载标签…');
  expect(option('创建「go」')).toBeUndefined();
  await press(input, 'Enter');
  expect(articleApi.addTag).not.toHaveBeenCalled();

  await act(async () => finishLoading([go, gopher]));
  await press(input, 'Enter');
  expect(articleApi.addTag).toHaveBeenCalledWith('article', { name: 'Go', color: '#3b82f6' });
});

it('creates a new tag even if the available tags could not load', async () => {
  vi.mocked(tagApi.getTags).mockRejectedValue(new Error('Offline'));
  await render();
  const input = await open();

  expect(document.body.textContent).toContain('标签加载失败');
  await fill(input, 'New');
  await press(input, 'Enter');
  expect(articleApi.addTag).toHaveBeenCalledWith('article', { name: 'New', color: '#3b82f6' });
});

it('puts a removed tag back in the options while the form is open', async () => {
  await render();
  await open();
  expect(option('.NET')).toBeUndefined();
  await act(async () => button('移除 .NET 标签')!.click());
  expect(option('.NET')).toBeDefined();
});

it('adds once while pending and keeps the input on failure', async () => {
  let fail!: (error: Error) => void;
  vi.mocked(articleApi.addTag).mockReturnValue(new Promise((_, reject) => { fail = reject; }));
  await render();
  const input = await open();
  await fill(input, 'Rust');
  await act(async () => {
    keydown(input, 'Enter');
    keydown(input, 'Enter');
  });

  expect(articleApi.addTag).toHaveBeenCalledTimes(1);
  expect(input.disabled).toBe(true);
  expect(button('完成')?.disabled).toBe(true);
  expect(option('创建「Rust」').getAttribute('aria-disabled')).toBe('true');
  await act(async () => fail(new Error('Offline')));
  expect(show).toHaveBeenCalledWith('添加标签失败，请重试');
  expect(input.value).toBe('Rust');
  expect(input.disabled).toBe(false);
});

it('creates the typed name on Enter when the only match is partial', async () => {
  await render();
  const input = await open();
  await fill(input, 'Goph');
  expect(options().map(element => element.textContent?.trim())).toEqual(['Gopher', '创建「Goph」']);
  await press(input, 'Enter');
  expect(articleApi.addTag).toHaveBeenCalledWith('article', { name: 'Goph', color: '#3b82f6' });
});

it('lets Enter confirm a composition in the input without submitting a tag', async () => {
  await render();
  const input = await open();
  await fill(input, '中');
  await press(input, 'Enter', { isComposing: true });
  expect(articleApi.addTag).not.toHaveBeenCalled();
});

it('closes on Escape and loads the tags again when reopened', async () => {
  await render();
  const input = await open();
  await press(input, 'Escape');
  expect(document.body.querySelector('[role="combobox"]')).toBeNull();
  expect(button('添加标签')).toBeDefined();
  await open();
  expect(tagApi.getTags).toHaveBeenCalledTimes(2);
});

it('floats the form outside the card so opening it keeps the card height', async () => {
  await render();
  const input = await open();

  expect(host.contains(input)).toBe(false);
  expect(input.closest('[role="dialog"]')?.getAttribute('aria-label')).toBe('添加标签');
  expect(button('添加标签')?.getAttribute('aria-expanded')).toBe('true');
});

it('keeps the form open when Escape cancels an IME composition', async () => {
  await render();
  const input = await open();
  await fill(input, '中');
  await press(input, 'Escape', { isComposing: true });
  expect(document.body.querySelector('[role="combobox"]')).toBe(input);
});
