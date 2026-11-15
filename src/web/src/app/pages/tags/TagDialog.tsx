"use client";

import axios from "axios";
import { useRef, useState, type FormEvent } from "react";
import { Button } from "@/components/arc/button/button";
import { Dialog, DialogContent } from "@/components/arc/dialog/dialog";
import { Input } from "@/components/arc/input/input";
import { TagColorOptions } from "@/app/components/TagColorOptions";
import { tagApi } from "@/lib/api";
import { DEFAULT_TAG_COLOR } from "@/lib/tag-colors";
import type { TagWithArticleCount } from "@/types";
import styles from "./TagDialog.module.css";

export function getTagErrorMessage(error: unknown, fallback: string): string {
  if (axios.isAxiosError(error)) {
    const { status, data } = error.response ?? {};
    if (status === 409) return "已有同名标签";
    if (status === 400) return typeof data?.error === "string" && data.error ? data.error : "标签名称无效";
  }
  return fallback;
}

/** Creates a tag, or edits `tag` when one is given. */
export default function TagDialog({ tag, onClose, onSaved }: {
  tag?: TagWithArticleCount;
  onClose: () => void;
  onSaved: (tag: TagWithArticleCount) => void;
}) {
  const nameInput = useRef<HTMLInputElement>(null);
  const submitting = useRef(false);
  const [name, setName] = useState(tag?.name ?? "");
  const [color, setColor] = useState(tag?.color.toLowerCase() ?? DEFAULT_TAG_COLOR);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string>();
  const title = tag ? "编辑标签" : "新建标签";

  async function save(trimmed: string) {
    if (!tag) return tagApi.createTag(trimmed, color);
    await tagApi.updateTag(tag.id, trimmed, color);
    return { ...tag, name: trimmed, color };
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const trimmed = name.trim();
    if (submitting.current || !trimmed) return;

    submitting.current = true;
    setPending(true);
    setError(undefined);
    try {
      onSaved(await save(trimmed));
    } catch (error) {
      setError(getTagErrorMessage(error, tag ? "保存失败，请重试" : "创建失败，请重试"));
    } finally {
      submitting.current = false;
      setPending(false);
    }
  }

  function close() {
    if (!submitting.current) onClose();
  }

  return (
    <Dialog open onOpenChange={open => { if (!open) close(); }}>
      <DialogContent
        title={title}
        closeLabel={`关闭${title}对话框`}
        closeDisabled={pending}
        onEscapeKeyDown={event => { if (submitting.current) event.preventDefault(); }}
        onInteractOutside={event => { if (submitting.current) event.preventDefault(); }}
        onOpenAutoFocus={event => {
          event.preventDefault();
          nameInput.current?.focus();
        }}
        footer={<>
          <Button variant="secondary" onClick={close} disabled={pending}>取消</Button>
          <Button type="submit" form="tag-dialog-form" disabled={pending || !name.trim()}>
            {tag ? (pending ? "正在保存…" : "保存") : (pending ? "正在创建…" : "创建")}
          </Button>
        </>}
      >
        <form id="tag-dialog-form" className={styles.form} onSubmit={submit}>
          <Input
            ref={nameInput}
            label="标签名称"
            name="name"
            value={name}
            onChange={event => setName(event.target.value)}
            maxLength={100}
            required
            disabled={pending}
            error={error}
          />
          <fieldset className={styles.colors} disabled={pending}>
            <legend>标签颜色</legend>
            <TagColorOptions color={color} onChange={setColor} />
          </fieldset>
        </form>
      </DialogContent>
    </Dialog>
  );
}
