import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { ConfirmProvider } from "@/contexts/ConfirmContext";
import { tagApi } from "@/lib/api";
import { notifyLibraryMutation } from "@/lib/library-mutations";
import type { TagWithArticleCount } from "@/types";
import TagsPage from "./page";

vi.mock("@/lib/api", () => ({ tagApi: { getTags: vi.fn(), createTag: vi.fn(), updateTag: vi.fn(), deleteTag: vi.fn() } }));
vi.mock("@/lib/library-mutations", () => ({ notifyLibraryMutation: vi.fn() }));
vi.mock("@/contexts/ToastContext", () => ({ useToast: () => ({ show }) }));

const show = vi.fn();
const unusedTag = { id: "unused", name: "123", color: "#3b82f6", articleCount: 0, createdAt: "" };
const usedTag = { id: "used", name: "456", color: "#ef4444", articleCount: 3, createdAt: "" };
let host: HTMLDivElement;
let root: Root;

beforeEach(() => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  vi.resetAllMocks();
  vi.mocked(tagApi.getTags).mockResolvedValue([unusedTag, usedTag]);
  host = document.createElement("div");
  document.body.append(host);
  root = createRoot(host);
});

afterEach(async () => {
  await act(async () => root.unmount());
  host.remove();
  vi.useRealTimers();
});

async function renderPage() {
  await act(async () => root.render(<ConfirmProvider><TagsPage /></ConfirmProvider>));
}

function button(container: HTMLElement, name: string) {
  return Array.from(container.querySelectorAll("button")).find(
    item => item.textContent?.trim() === name || item.getAttribute("aria-label") === name,
  );
}

const dialog = () => document.body.querySelector<HTMLElement>('[role="dialog"]')!;
const deleteButton = (name: string) => button(host, `删除标签「${name}」`)!;
const editButton = (name: string) => button(host, `编辑标签「${name}」`)!;

async function fill(input: HTMLInputElement, value: string) {
  await act(async () => {
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!.call(input, value);
    input.dispatchEvent(new Event("input", { bubbles: true }));
  });
}

it("shows 新建标签 next to search, including when there are no tags", async () => {
  vi.mocked(tagApi.getTags).mockResolvedValue([]);
  await renderPage();
  expect(button(host, "新建标签")?.parentElement?.querySelector('input[type="search"]')).not.toBeNull();
  expect(button(host, "新建标签")).toBeDefined();
  expect(host.querySelector("section")?.textContent).toContain("点击「新建标签」");
});

it("reloads with the current search term after creating and restores focus", async () => {
  vi.useFakeTimers();
  const created = { ...unusedTag, name: "阅读" };
  vi.mocked(tagApi.getTags).mockResolvedValueOnce([unusedTag, usedTag]).mockResolvedValueOnce([]).mockResolvedValue([created]);
  vi.mocked(tagApi.createTag).mockResolvedValue(created);
  await renderPage();

  await fill(host.querySelector('input[type="search"]')!, "阅");
  await act(async () => vi.advanceTimersByTime(300));
  expect(tagApi.getTags).toHaveBeenLastCalledWith("阅");
  const openButton = button(host, "新建标签")!;
  openButton.focus();
  await act(async () => openButton.click());
  await fill(dialog().querySelector("input")!, "阅读");
  await act(async () => dialog().querySelector("form")!.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true })));
  await act(async () => vi.advanceTimersByTime(0));

  expect(dialog()).toBeNull();
  expect(document.activeElement).toBe(openButton);
  expect(tagApi.getTags).toHaveBeenCalledTimes(3);
  expect(tagApi.getTags).toHaveBeenLastCalledWith("阅");
  expect(host.querySelector('a[href="/pages/tags/unused"]')?.textContent).toContain("阅读");
  expect(show).toHaveBeenCalledWith("已创建「阅读」");
});

it("keeps the delete button outside the card link and opens confirmation without navigating", async () => {
  await renderPage();
  const href = window.location.href;
  const remove = deleteButton("123");
  expect(remove.closest("a")).toBeNull();

  await act(async () => remove.click());

  expect(dialog().textContent).toContain("删除「123」标签？");
  expect(window.location.href).toBe(href);
  expect(tagApi.deleteTag).not.toHaveBeenCalled();
});

it("confirms with the article count, removes the card and updates the counts", async () => {
  await renderPage();
  await act(async () => deleteButton("456").click());
  expect(dialog().textContent).toContain("删除「456」标签？它会从 3 篇文章中移除，文章本身不受影响。");
  await act(async () => button(dialog(), "删除标签")!.click());

  expect(tagApi.deleteTag).toHaveBeenCalledWith(usedTag.id);
  expect(deleteButton("456")).toBeUndefined();
  expect(host.textContent).toContain("1 个标签");
  expect(host.textContent).toContain("0 篇文章");
  expect(notifyLibraryMutation).toHaveBeenCalledTimes(1);
  expect(tagApi.getTags).toHaveBeenCalledTimes(1);
});

it("omits the count for unused tags and does nothing when cancelled", async () => {
  await renderPage();
  await act(async () => deleteButton("123").click());
  expect(dialog().querySelector("p")?.textContent).toBe("删除「123」标签？");
  await act(async () => button(dialog(), "取消")!.click());

  expect(tagApi.deleteTag).not.toHaveBeenCalled();
  expect(deleteButton("123")).toBeDefined();
});

it("keeps the card and shows a toast when delete fails", async () => {
  vi.mocked(tagApi.deleteTag).mockRejectedValue(new Error("Offline"));
  await renderPage();
  await act(async () => deleteButton("456").click());
  await act(async () => button(dialog(), "删除标签")!.click());

  expect(deleteButton("456")).toBeDefined();
  expect(show).toHaveBeenCalledWith("标签删除失败，请重试。");
  expect(notifyLibraryMutation).not.toHaveBeenCalled();
});

it("edits a tag from its card, updates the card and refreshes the library", async () => {
  vi.mocked(tagApi.updateTag).mockResolvedValue();
  const renamed = { ...unusedTag, name: "789", color: "#10b981" };
  vi.mocked(tagApi.getTags).mockResolvedValueOnce([unusedTag, usedTag]).mockReturnValue(new Promise(() => {}));
  await renderPage();
  const edit = editButton("123");
  expect(edit.closest("a")).toBeNull();
  edit.focus();
  await act(async () => edit.click());
  expect(dialog().textContent).toContain("编辑标签");

  await fill(dialog().querySelector("input")!, "789");
  await act(async () => button(dialog(), "选择颜色 #10b981")!.click());
  await act(async () => dialog().querySelector("form")!.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true })));
  // Radix restores focus on the next macrotask; the renamed card has moved from group 1 to group 7 by then.
  await act(async () => new Promise(resolve => setTimeout(resolve, 0)));

  expect(tagApi.updateTag).toHaveBeenCalledWith("unused", "789", "#10b981");
  expect(dialog()).toBeNull();
  expect(document.activeElement).toBe(editButton("789"));
  expect(host.querySelector('a[href="/pages/tags/unused"]')?.textContent).toContain(renamed.name);
  expect(host.querySelector<HTMLElement>('a[href="/pages/tags/unused"] span')?.getAttribute("style")).toContain("#10b981");
  expect(notifyLibraryMutation).toHaveBeenCalledTimes(1);
  expect(tagApi.getTags).toHaveBeenCalledTimes(2);
});

it("keeps the card unchanged when the edit is cancelled", async () => {
  await renderPage();
  await act(async () => editButton("456").click());
  await fill(dialog().querySelector("input")!, "Changed");
  await act(async () => button(dialog(), "取消")!.click());

  expect(tagApi.updateTag).not.toHaveBeenCalled();
  expect(editButton("456")).toBeDefined();
  expect(notifyLibraryMutation).not.toHaveBeenCalled();
});

it("does not restore a deleted card from a search already in progress", async () => {
  vi.useFakeTimers();
  let completeSearch!: (tags: TagWithArticleCount[]) => void;
  vi.mocked(tagApi.getTags).mockResolvedValueOnce([unusedTag, usedTag]).mockReturnValueOnce(
    new Promise(resolve => { completeSearch = resolve; }),
  );
  await renderPage();
  await fill(host.querySelector('input[type="search"]')!, "456");
  await act(async () => vi.advanceTimersByTime(300));

  await act(async () => deleteButton("456").click());
  await act(async () => button(dialog(), "删除标签")!.click());
  await act(async () => completeSearch([usedTag]));

  expect(deleteButton("456")).toBeUndefined();
});

it("keeps the refreshed list when a search started before creation returns last", async () => {
  vi.useFakeTimers();
  const created = { ...unusedTag, name: "阅读" };
  let finishSearch!: (tags: TagWithArticleCount[]) => void;
  vi.mocked(tagApi.getTags).mockResolvedValueOnce([]).mockReturnValueOnce(
    new Promise(resolve => { finishSearch = resolve; }),
  ).mockResolvedValue([created]);
  vi.mocked(tagApi.createTag).mockResolvedValue(created);
  await renderPage();
  await fill(host.querySelector('input[type="search"]')!, "阅");
  await act(async () => vi.advanceTimersByTime(300));

  await act(async () => button(host, "新建标签")!.click());
  await fill(dialog().querySelector("input")!, "阅读");
  await act(async () => dialog().querySelector("form")!.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true })));
  await act(async () => finishSearch([]));

  expect(host.querySelector('a[href="/pages/tags/unused"]')?.textContent).toContain("阅读");
});
