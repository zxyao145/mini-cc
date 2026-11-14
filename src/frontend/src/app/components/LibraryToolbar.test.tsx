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
it('debounces search by 300ms and uses replace while filters use push', async () => {
  const onChange = vi.fn();
  await act(async () => root.render(<LibraryToolbar query={parseLibraryQuery(new URLSearchParams())} onChange={onChange} articleStyle="grid" onStyleChange={() => {}} />));
  const input = container.querySelector('input[type=search]')!;
  await act(async () => { Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(input, '缓存'); input.dispatchEvent(new Event('input', { bubbles: true })); });
  await act(async () => vi.advanceTimersByTime(299));
  expect(onChange).not.toHaveBeenCalled();
  await act(async () => vi.advanceTimersByTime(1));
  expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ q: '缓存' }), true);
  const checkbox = container.querySelector('input[type=checkbox]') as HTMLInputElement;
  await act(async () => checkbox.click());
  expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ status: ['unread'] }), false);
});
it('renders individually removable chips and clears filters while preserving view', async () => {
  const onChange = vi.fn(); const query = parseLibraryQuery(new URLSearchParams('view=favorites&q=缓存&status=unread'));
  await act(async () => root.render(<LibraryToolbar query={query} onChange={onChange} articleStyle="list" onStyleChange={() => {}} />));
  expect(container.textContent).toContain('搜索：缓存');
  await act(async () => (Array.from(container.querySelectorAll('button')).find(b => b.textContent === '清除筛选')!).click());
  expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ view: 'favorites', q: '', status: [], tags: [] }), false);
});

it.each([
  { range: 'week', now: new Date(2026, 10, 8, 12), from: new Date(2026, 10, 2), to: new Date(2026, 10, 9) },
  { range: 'month', now: new Date(2026, 2, 30, 12), from: new Date(2026, 1, 28), to: new Date(2026, 2, 31) },
])('uses local calendar boundaries for $range including the current day', async ({ range, now, from, to }) => {
  vi.setSystemTime(now);
  const onChange = vi.fn();
  await act(async () => root.render(<LibraryToolbar query={parseLibraryQuery(new URLSearchParams())} onChange={onChange} articleStyle="grid" onStyleChange={() => {}} />));
  const select = container.querySelectorAll('select')[1];
  await act(async () => { select.value = range; select.dispatchEvent(new Event('change', { bubbles: true })); });
  expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ range, savedFrom: from.toISOString(), savedTo: to.toISOString() }), false);
});

it('shows the fixed trash ordering without changing the requested sort', async () => {
  const query = parseLibraryQuery(new URLSearchParams('view=trash&sort=read_desc'));
  await act(async () => root.render(<LibraryToolbar query={query} onChange={() => {}} articleStyle="grid" onStyleChange={() => {}} />));
  const select = container.querySelectorAll('select')[2];
  expect(select.disabled).toBe(true);
  expect(select.value).toBe('read_desc');
  expect(select.options[select.selectedIndex].textContent).toBe('最近删除');
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
  const checkbox = (name: string) => Array.from(container.querySelectorAll('label')).find(label => label.textContent === name)!.querySelector('input')!;
  await act(async () => { checkbox('阅读').click(); checkbox('.NET').click(); });
  const source = container.querySelectorAll('select')[0];
  await act(async () => { source.value = 'extension'; source.dispatchEvent(new Event('change', { bubbles: true })); });
  const input = container.querySelector('input[type=search]')!;
  await act(async () => { Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(input, '缓存'); input.dispatchEvent(new Event('input', { bubbles: true })); });
  await act(async () => vi.advanceTimersByTime(300));
  const final = onChange.mock.calls.at(-1)![0];
  expect(final).toMatchObject({ tags: tags.map(tag => tag.id), source: 'extension', q: '缓存', status: ['unread'] });
  expect(onChange.mock.calls.map(([, replace]) => replace)).toEqual([false, false, false, true]);
  await act(async () => render(onChange.mock.calls[0][0])); // Older navigation finished after newer edits.
  expect(checkbox('阅读').checked).toBe(true);
  expect(checkbox('.NET').checked).toBe(true);
  expect(source.value).toBe('extension');
  expect((input as HTMLInputElement).value).toBe('缓存');
  await act(async () => render(final));
  await act(async () => {
    history.replaceState(null, '', `/?status=unread&tags=${tags[0].id}`);
    window.dispatchEvent(new PopStateEvent('popstate'));
  });
  expect(checkbox('阅读').checked).toBe(true);
  expect(checkbox('.NET').checked).toBe(false);
  expect(source.value).toBe('');
  expect((input as HTMLInputElement).value).toBe('');
  await act(async () => {
    history.replaceState(null, '', `/?status=unread&tags=${tags.map(tag => tag.id).join(',')}&source=extension&q=缓存`);
    window.dispatchEvent(new PopStateEvent('popstate'));
  });
  expect(checkbox('.NET').checked).toBe(true);
  expect(source.value).toBe('extension');
  expect((input as HTMLInputElement).value).toBe('缓存');
  history.replaceState(null, '', '/');
});
it('accepts an external URL change while preserving an unsent search during its own latest navigation acknowledgement', async () => {
  const onChange = vi.fn();
  const render = (search = '') => root.render(<LibraryToolbar query={parseLibraryQuery(new URLSearchParams(search))} onChange={onChange} articleStyle="grid" onStyleChange={() => {}} />);
  await act(async () => render());
  const source = container.querySelectorAll('select')[0];
  await act(async () => { source.value = 'extension'; source.dispatchEvent(new Event('change', { bubbles: true })); });
  const input = container.querySelector('input[type=search]')!;
  await act(async () => { Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(input, '缓存'); input.dispatchEvent(new Event('input', { bubbles: true })); });
  await act(async () => render('source=extension'));
  expect((input as HTMLInputElement).value).toBe('缓存');
  await act(async () => vi.advanceTimersByTime(300));
  expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ source: 'extension', q: '缓存' }), true);
  await act(async () => render('view=archive&status=read'));
  expect((input as HTMLInputElement).value).toBe('');
  expect(source.value).toBe('');
});

it('selects a feed without changing the current view or filter intersection and can return to all feeds', async () => {
  const onChange = vi.fn();
  const query = parseLibraryQuery(new URLSearchParams('view=favorites&q=缓存&status=unread&tags=00000000-0000-0000-0000-000000000001&source=feed&savedFrom=2026-11-01T00:00:00Z&savedTo=2026-12-01T00:00:00Z&range=custom&sort=published_desc'));
  await act(async () => root.render(<LibraryToolbar query={query} onChange={onChange} articleStyle="list" onStyleChange={() => {}}/>));
  const select = container.querySelector<HTMLSelectElement>('select[aria-label="订阅源"]');
  expect(select).not.toBeNull();
  await act(async () => { select!.value = '00000000-0000-0000-0000-000000000009'; select!.dispatchEvent(new Event('change', { bubbles: true })); });
  expect(onChange).toHaveBeenLastCalledWith({ ...query, feedId: '00000000-0000-0000-0000-000000000009' }, false);
  await act(async () => { select!.value = ''; select!.dispatchEvent(new Event('change', { bubbles: true })); });
  expect(onChange).toHaveBeenLastCalledWith(query, false);
});
