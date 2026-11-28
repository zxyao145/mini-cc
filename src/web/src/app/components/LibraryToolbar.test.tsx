import { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import LibraryToolbar from './LibraryToolbar';
import { tagApi } from '@/lib/api';
import { parseLibraryQuery } from '@/lib/library-query';
vi.mock('@/contexts/FeedsContext', () => ({ useFeeds: () => ({ feeds: [{ id: '00000000-0000-0000-0000-000000000009', title: 'Chosen feed' }] }) }));
vi.mock('@/lib/api', () => ({ tagApi: { getTags: vi.fn().mockResolvedValue([]) } }));
let container: HTMLDivElement, root: Root;
beforeEach(() => { Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true }); vi.useFakeTimers(); vi.mocked(tagApi.getTags).mockResolvedValue([]); container = document.createElement('div'); document.body.append(container); root = createRoot(container); });
afterEach(async () => { await act(async () => root.unmount()); container.remove(); vi.useRealTimers(); });
const popupTrigger = (name: string) => document.body.querySelector<HTMLButtonElement>(`button[aria-label="${name}"]`)!;
const checkboxLabel = (name: string) => Array.from(document.body.querySelectorAll('label')).find(label => label.textContent?.trim() === name)!;
const checkboxControl = (name: string) => document.getElementById(checkboxLabel(name).htmlFor)!;
const selectTrigger = (name: string) => document.getElementById(checkboxLabel(name).htmlFor) as HTMLButtonElement;
async function openPopover(name: string) { await act(async () => popupTrigger(name).click()); }
async function choose(label: string, optionText: string) {
  const trigger = selectTrigger(label);
  await act(async () => trigger.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true })));
  const option = Array.from(document.body.querySelectorAll<HTMLElement>('[role="option"]')).find(item => item.textContent?.trim() === optionText)!;
  await act(async () => option.click());
}
it('selects and clears the saved links filter', async () => {
  const onChange = vi.fn();
  const render = (search = '') => root.render(<LibraryToolbar query={parseLibraryQuery(new URLSearchParams(search))} onChange={onChange} articleStyle="grid" onStyleChange={() => {}} />);
  await act(async () => render());
  await choose('内容类型', '链接');
  expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ contentKind: 'link' }), false);
  await act(async () => render('contentKind=link'));
  await act(async () => container.querySelector<HTMLButtonElement>('button[aria-label="移除内容类型：链接"]')!.click());
  expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ contentKind: '' }), false);
});
it('debounces search by 300ms and uses replace while filters use push', async () => {
  const onChange = vi.fn();
  await act(async () => root.render(<LibraryToolbar query={parseLibraryQuery(new URLSearchParams('status='))} onChange={onChange} articleStyle="grid" onStyleChange={() => {}} />));
  const input = container.querySelector('input[type=search]')!;
  await act(async () => { Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(input, '缓存'); input.dispatchEvent(new Event('input', { bubbles: true })); });
  await act(async () => vi.advanceTimersByTime(299));
  expect(onChange).not.toHaveBeenCalled();
  await act(async () => vi.advanceTimersByTime(1));
  expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ q: '缓存' }), true);
  await openPopover('阅读状态');
  await act(async () => checkboxLabel('未读').click());
  expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ status: ['unread'] }), false);
});
it('renders individually removable chips and clears filters while preserving view', async () => {
  const onChange = vi.fn(); const query = parseLibraryQuery(new URLSearchParams('view=favorites&q=缓存&status=unread'));
  await act(async () => root.render(<LibraryToolbar query={query} onChange={onChange} articleStyle="list" onStyleChange={() => {}} />));
  expect(container.textContent).toContain('搜索：缓存');
  const clear = Array.from(container.querySelectorAll('button')).find(b => b.textContent === '清除筛选')!;
  expect(clear.querySelector('svg[aria-hidden="true"]')).not.toBeNull();
  await act(async () => clear.click());
  expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ view: 'favorites', q: '', status: [], tags: [] }), false);
});

it.each([
  { range: 'week', now: new Date(2026, 10, 8, 12), from: new Date(2026, 10, 2), to: new Date(2026, 10, 9) },
  { range: 'month', now: new Date(2026, 2, 30, 12), from: new Date(2026, 1, 28), to: new Date(2026, 2, 31) },
])('uses local calendar boundaries for $range including the current day', async ({ range, now, from, to }) => {
  vi.setSystemTime(now);
  const onChange = vi.fn();
  await act(async () => root.render(<LibraryToolbar query={parseLibraryQuery(new URLSearchParams())} onChange={onChange} articleStyle="grid" onStyleChange={() => {}} />));
  await choose('保存时间', range === 'week' ? '最近一周' : '最近一个月');
  expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ range, savedFrom: from.toISOString(), savedTo: to.toISOString() }), false);
});

it('shows the fixed trash ordering without changing the requested sort', async () => {
  const query = parseLibraryQuery(new URLSearchParams('view=trash&sort=read_desc'));
  await act(async () => root.render(<LibraryToolbar query={query} onChange={() => {}} articleStyle="grid" onStyleChange={() => {}} />));
  const select = selectTrigger('排序');
  expect(select.disabled).toBe(true);
  expect(select.textContent).toContain('最近删除');
});

it('composes rapid tag/source/search edits before navigation commits and ignores intermediate URL acknowledgements', async () => {
  const tags = [
    { id: '00000000-0000-0000-0000-000000000001', name: '阅读', color: '#112233', createdAt: '2026-11-08', articleCount: 1 },
    { id: '00000000-0000-0000-0000-000000000002', name: '.NET', color: '#112233', createdAt: '2026-11-08', articleCount: 1 },
  ];
  vi.mocked(tagApi.getTags).mockResolvedValue(tags);
  const onChange = vi.fn();
  const render = (query = parseLibraryQuery(new URLSearchParams('status=unread'))) => root.render(<LibraryToolbar query={query} onChange={onChange} articleStyle="grid" onStyleChange={() => {}} />);
  await act(async () => render());
  const checkbox = (name: string) => checkboxLabel(name);
  await openPopover('标签');
  await act(async () => { checkbox('阅读').click(); checkbox('.NET').click(); });
  await choose('来源', '浏览器收藏');
  const input = container.querySelector('input[type=search]')!;
  await act(async () => { Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(input, '缓存'); input.dispatchEvent(new Event('input', { bubbles: true })); });
  await act(async () => vi.advanceTimersByTime(300));
  const final = onChange.mock.calls.at(-1)![0];
  expect(final).toMatchObject({ tags: tags.map(tag => tag.id), source: 'extension', q: '缓存', status: ['unread'] });
  expect(onChange.mock.calls.map(([, replace]) => replace)).toEqual([false, false, false, true]);
  await act(async () => render(onChange.mock.calls[0][0])); // Older navigation finished after newer edits.
  await openPopover('标签');
  expect(checkboxControl('阅读').getAttribute('data-state')).toBe('checked');
  expect(checkboxControl('.NET').getAttribute('data-state')).toBe('checked');
  expect(selectTrigger('来源').textContent).toContain('浏览器收藏');
  expect((input as HTMLInputElement).value).toBe('缓存');
  await act(async () => render(final));
  await act(async () => {
    history.replaceState(null, '', `/?status=unread&tags=${tags[0].id}`);
    window.dispatchEvent(new PopStateEvent('popstate'));
  });
  expect(checkboxControl('阅读').getAttribute('data-state')).toBe('checked');
  expect(checkboxControl('.NET').getAttribute('data-state')).toBe('unchecked');
  expect(selectTrigger('来源').textContent).toContain('全部');
  expect((input as HTMLInputElement).value).toBe('');
  await act(async () => {
    history.replaceState(null, '', `/?status=unread&tags=${tags.map(tag => tag.id).join(',')}&source=extension&q=缓存`);
    window.dispatchEvent(new PopStateEvent('popstate'));
  });
  expect(checkboxControl('.NET').getAttribute('data-state')).toBe('checked');
  expect(selectTrigger('来源').textContent).toContain('浏览器收藏');
  expect((input as HTMLInputElement).value).toBe('缓存');
  history.replaceState(null, '', '/');
});
it('accepts an external URL change while preserving an unsent search during its own latest navigation acknowledgement', async () => {
  const onChange = vi.fn();
  const render = (search = '') => root.render(<LibraryToolbar query={parseLibraryQuery(new URLSearchParams(search))} onChange={onChange} articleStyle="grid" onStyleChange={() => {}} />);
  await act(async () => render());
  await choose('来源', '浏览器收藏');
  const input = container.querySelector('input[type=search]')!;
  await act(async () => { Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(input, '缓存'); input.dispatchEvent(new Event('input', { bubbles: true })); });
  await act(async () => render('source=extension'));
  expect((input as HTMLInputElement).value).toBe('缓存');
  await act(async () => vi.advanceTimersByTime(300));
  expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ source: 'extension', q: '缓存' }), true);
  await act(async () => render('view=archive&status=read'));
  expect((input as HTMLInputElement).value).toBe('');
  expect(selectTrigger('来源').textContent).toContain('全部');
});

it('selects a feed without changing the current view or filter intersection and can return to all feeds', async () => {
  const onChange = vi.fn();
  const query = parseLibraryQuery(new URLSearchParams('view=favorites&q=缓存&status=unread&tags=00000000-0000-0000-0000-000000000001&source=feed&savedFrom=2026-11-01T00:00:00Z&savedTo=2026-12-01T00:00:00Z&range=custom&sort=published_desc'));
  await act(async () => root.render(<LibraryToolbar query={query} onChange={onChange} articleStyle="list" onStyleChange={() => {}}/>));
  const select = selectTrigger('订阅源');
  expect(select).not.toBeNull();
  await choose('订阅源', 'Chosen feed');
  expect(onChange).toHaveBeenLastCalledWith({ ...query, feedId: '00000000-0000-0000-0000-000000000009' }, false);
  await choose('订阅源', '全部订阅');
  expect(onChange).toHaveBeenLastCalledWith(query, false);
});

it('keeps the tag filter limit at ten and enables another tag after removing one', async () => {
  const tags = Array.from({ length: 11 }, (_, index) => ({ id: `00000000-0000-0000-0000-${String(index + 1).padStart(12, '0')}`, name: `标签 ${index + 1}`, color: '#112233', createdAt: '2026-11-08', articleCount: 1 }));
  vi.mocked(tagApi.getTags).mockResolvedValue(tags);
  const onChange = vi.fn();
  const query = parseLibraryQuery(new URLSearchParams(`tags=${tags.slice(0, 10).map(tag => tag.id).join(',')}`));
  await act(async () => root.render(<LibraryToolbar query={query} onChange={onChange} articleStyle="grid" onStyleChange={() => {}} />));
  await openPopover('标签');
  expect((checkboxControl('标签 11') as HTMLButtonElement).disabled).toBe(true);
  await act(async () => checkboxLabel('标签 1').click());
  expect((checkboxControl('标签 11') as HTMLButtonElement).disabled).toBe(false);
  expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ tags: tags.slice(1, 10).map(tag => tag.id) }), false);
});
