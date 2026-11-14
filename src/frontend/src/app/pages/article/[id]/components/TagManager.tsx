"use client";

import { useId, useState } from "react";
import { Plus, X } from "lucide-react";
import { Button } from "@/components/arc/button/button";
import { Input } from "@/components/arc/input/input";
import { Tooltip } from "@/components/arc/tooltip/tooltip";
import { Tag, AddTagRequest } from "@/types";
import { articleApi } from "@/lib/api";
import { useToast } from "@/contexts/ToastContext";
import styles from "./TagManager.module.css";

interface TagManagerProps {
  articleId: string;
  tags: Tag[];
  onTagsChange: (tags: Tag[]) => void;
  compact?: boolean;
}

export default function TagManager({
  articleId,
  tags,
  onTagsChange,
  compact = false,
}: TagManagerProps) {
  const tagInputId = useId();
  const toast = useToast();
  const [newTagName, setNewTagName] = useState("");
  const [newTagColor, setNewTagColor] = useState("#3b82f6");
  const [isAdding, setIsAdding] = useState(false);
  const [showAddForm, setShowAddForm] = useState(false);

  const handleAddTag = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newTagName.trim()) return;

    setIsAdding(true);
    try {
      const tagRequest: AddTagRequest = {
        name: newTagName.trim(),
        color: newTagColor,
      };

      const newTag = await articleApi.addTag(articleId, tagRequest);
      onTagsChange([...tags, newTag]);

      setNewTagName("");
      setNewTagColor("#3b82f6");
      setShowAddForm(false);
    } catch (error) {
      console.error("Failed to add tag:", error);
      toast.show("添加标签失败，请重试");
    } finally {
      setIsAdding(false);
    }
  };

  const handleRemoveTag = async (tagId: string) => {
    try {
      await articleApi.removeTag(articleId, tagId);
      onTagsChange(tags.filter((tag) => tag.id !== tagId));
    } catch (error) {
      console.error("Failed to remove tag:", error);
      toast.show("移除标签失败，请重试");
    }
  };

  const predefinedColors = [
    "#3b82f6",
    "#ef4444",
    "#10b981",
    "#f59e0b",
    "#8b5cf6",
    "#f97316",
    "#06b6d4",
    "#84cc16",
    "#ec4899",
    "#6b7280",
  ];

  return (
    <div className={`${styles.container}${compact ? ` ${styles.compact}` : ""}`}>
      <div className={styles.tagList}>
        {tags.map((tag) => (
          <span key={tag.id} className={styles.tag}>
            <span className={styles.tagDot} style={{ backgroundColor: tag.color || "var(--text-muted)" }} aria-hidden="true" />
            <span className={styles.tagName}>{tag.name}</span>
            <Tooltip content={`移除 ${tag.name} 标签`}>
              <Button className={styles.removeBtn} variant="ghost" size="sm" iconOnly onClick={() => handleRemoveTag(tag.id)} aria-label={`移除 ${tag.name} 标签`}>
                <X size={14} aria-hidden="true" />
              </Button>
            </Tooltip>
          </span>
        ))}

        {!showAddForm && <Button variant="ghost" size="sm" onClick={() => setShowAddForm(true)} className={styles.addBtn} aria-expanded={showAddForm}>
          <Plus size={14} aria-hidden="true" />添加标签
        </Button>}
      </div>

      {showAddForm && (
        <form onSubmit={handleAddTag} className={styles.addForm} aria-label="添加标签">
          <div className={styles.inputGroup}>
            <Input id={tagInputId} label="标签名称" hideLabel type="text" value={newTagName} onChange={(event) => setNewTagName(event.target.value)} placeholder="标签名称" wrapperClassName={styles.tagInputField} autoFocus disabled={isAdding} maxLength={20} />
            <fieldset className={styles.colorPicker}>
              <legend>标签颜色</legend>
              {predefinedColors.map((color) => <Tooltip key={color} content={`选择颜色 ${color}`}>
                <Button type="button" variant="ghost" size="sm" iconOnly onClick={() => setNewTagColor(color)} className={`${styles.colorOption}${newTagColor === color ? ` ${styles.selected}` : ""}`} style={{ backgroundColor: color }} aria-label={`选择颜色 ${color}`} aria-pressed={newTagColor === color} />
              </Tooltip>)}
              <label className={styles.customColorLabel}>
                <span>自定义</span>
                <input type="color" value={newTagColor} onChange={(event) => setNewTagColor(event.target.value)} className={styles.customColor} aria-label="自定义标签颜色" />
              </label>
            </fieldset>
          </div>

          <div className={styles.formActions}>
            <Button type="submit" variant="primary" size="sm" disabled={isAdding || !newTagName.trim()} className={styles.saveBtn}>{isAdding ? "添加中…" : "添加"}</Button>
            <Button type="button" variant="secondary" size="sm" onClick={() => { setShowAddForm(false); setNewTagName(""); setNewTagColor("#3b82f6"); }} className={styles.cancelBtn} disabled={isAdding}>取消</Button>
          </div>
        </form>
      )}
    </div>
  );
}
