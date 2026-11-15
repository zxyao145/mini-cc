import { AxiosError } from "axios";
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { tagApi } from "@/lib/api";
import type { TagWithArticleCount } from "@/types";
import TagDialog, { getTagErrorMessage } from "./TagDialog";

vi.mock("@/lib/api", () => ({ tagApi: { createTag: vi.fn(), updateTag: vi.fn() } }));

let host: HTMLDivElement;
let root: Root;
const onClose = vi.fn();
const onCreated = vi.fn();
const existing: TagWithArticleCount = { id: "tag-go", name: "Go", color: "#EF4444", createdAt: "", articleCount: 3 };

beforeEach(() => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  vi.resetAllMocks();
  host = document.createElement("div");
  document.body.append(host);
  root = createRoot(host);
});

afterEach(async () => {
  await act(async () => root.unmount());
  host.remove();
});

async function openDialog(tag?: TagWithArticleCount) {
  await act(async () => root.render(<TagDialog tag={tag} onClose={onClose} onSaved={onCreated} />));
  return document.body.querySelector<HTMLElement>('[role="dialog"]')!;
}

function button(dialog: HTMLElement, name: string) {
  return Array.from(dialog.querySelectorAll("button")).find(
    item => item.textContent?.trim() === name || item.getAttribute("aria-label") === name,
  )!;
}

async function fill(input: HTMLInputElement, value: string) {
  await act(async () => {
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!.call(input, value);
    input.dispatchEvent(new Event("input", { bubbles: true }));
  });
}

function submit(dialog: HTMLElement) {
  dialog.querySelector("form")!.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true }));
}

function axiosError(status: number, data?: unknown) {
  return new AxiosError("Create failed", "ERR_BAD_REQUEST", undefined, undefined, { status, data } as never);
}

it("focuses the name input and disables create until a non-blank name is entered", async () => {
  const dialog = await openDialog();
  const input = dialog.querySelector("input")!;
  expect(document.activeElement).toBe(input);
  expect(input.maxLength).toBe(100);
  expect(button(dialog, "创建").disabled).toBe(true);
  expect(dialog.querySelectorAll("button[aria-pressed]")).toHaveLength(10);
  expect(button(dialog, "选择颜色 #3b82f6").getAttribute("aria-pressed")).toBe("true");

  await fill(input, "   ");
  expect(button(dialog, "创建").disabled).toBe(true);
  await fill(input, "阅读");
  expect(button(dialog, "创建").disabled).toBe(false);
});

it("creates with the trimmed name and chosen color, then reports the created tag", async () => {
  const tag = { id: "tag", name: "阅读", color: "#ef4444", createdAt: "", articleCount: 0 };
  vi.mocked(tagApi.createTag).mockResolvedValue(tag);
  const dialog = await openDialog();
  await act(async () => button(dialog, "选择颜色 #ef4444").click());
  await fill(dialog.querySelector("input")!, "  阅读  ");
  await act(async () => submit(dialog));

  expect(tagApi.createTag).toHaveBeenCalledWith("阅读", "#ef4444");
  expect(onCreated).toHaveBeenCalledWith(tag);
});

it("keeps the dialog and name on a duplicate-name conflict", async () => {
  vi.mocked(tagApi.createTag).mockRejectedValue(axiosError(409, { existingTagId: "existing" }));
  const dialog = await openDialog();
  const input = dialog.querySelector("input")!;
  await fill(input, "阅读");
  await act(async () => submit(dialog));

  const error = dialog.querySelector('[role="alert"]');
  expect(error?.textContent).toContain("已有同名标签");
  expect(input.getAttribute("aria-invalid")).toBe("true");
  expect(error?.id).toBe(input.getAttribute("aria-describedby"));
  expect(input.value).toBe("阅读");
  expect(onCreated).not.toHaveBeenCalled();
  expect(onClose).not.toHaveBeenCalled();
});

it("maps bad requests and other failures to the appropriate message", () => {
  expect(getTagErrorMessage(axiosError(400, { error: "标签名称必须为 1–100 个字符" }), "创建失败，请重试")).toBe("标签名称必须为 1–100 个字符");
  expect(getTagErrorMessage(axiosError(400), "创建失败，请重试")).toBe("标签名称无效");
  expect(getTagErrorMessage(axiosError(500), "创建失败，请重试")).toBe("创建失败，请重试");
  expect(getTagErrorMessage(new Error(), "保存失败，请重试")).toBe("保存失败，请重试");
});

it("edits a tag prefilled with its name and color, matching the color case-insensitively", async () => {
  vi.mocked(tagApi.updateTag).mockResolvedValue();
  const dialog = await openDialog(existing);
  const input = dialog.querySelector("input")!;

  expect(dialog.textContent).toContain("编辑标签");
  expect(input.value).toBe("Go");
  expect(document.activeElement).toBe(input);
  expect(button(dialog, "选择颜色 #ef4444").getAttribute("aria-pressed")).toBe("true");
  await fill(input, "  Golang  ");
  await act(async () => button(dialog, "选择颜色 #10b981").click());
  await act(async () => submit(dialog));

  expect(tagApi.updateTag).toHaveBeenCalledWith("tag-go", "Golang", "#10b981");
  expect(tagApi.createTag).not.toHaveBeenCalled();
  expect(onCreated).toHaveBeenCalledWith({ ...existing, name: "Golang", color: "#10b981" });
});

it("keeps the edit dialog open with 已有同名标签 when the new name is taken", async () => {
  vi.mocked(tagApi.updateTag).mockRejectedValue(axiosError(409, { existingTagId: "other" }));
  const dialog = await openDialog(existing);
  await fill(dialog.querySelector("input")!, "Rust");
  await act(async () => submit(dialog));

  expect(dialog.querySelector('[role="alert"]')?.textContent).toContain("已有同名标签");
  expect(dialog.querySelector("input")!.value).toBe("Rust");
  expect(onCreated).not.toHaveBeenCalled();
  expect(dialog.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBe(false);
});

it("submits once and prevents dismissal while the request is pending", async () => {
  vi.mocked(tagApi.createTag).mockReturnValue(new Promise(() => {}));
  const dialog = await openDialog();
  await fill(dialog.querySelector("input")!, "阅读");
  await act(async () => {
    submit(dialog);
    submit(dialog);
  });

  expect(tagApi.createTag).toHaveBeenCalledTimes(1);
  const create = dialog.querySelector<HTMLButtonElement>('button[type="submit"]')!;
  expect(create.textContent).toContain("正在创建…");
  expect(Array.from(dialog.querySelectorAll("button")).every(item => item.disabled || item.closest("fieldset")?.disabled)).toBe(true);
  const escape = new KeyboardEvent("keydown", { key: "Escape", bubbles: true, cancelable: true });
  await act(async () => dialog.dispatchEvent(escape));
  expect(escape.defaultPrevented).toBe(true);
  expect(onClose).not.toHaveBeenCalled();
});
