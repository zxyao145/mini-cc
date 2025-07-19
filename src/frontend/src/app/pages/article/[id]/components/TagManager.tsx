"use client";

import { useState } from "react";
import { Tag, AddTagRequest } from "@/types";
import { articleApi } from "@/lib/api";
import styles from "./TagManager.module.scss";

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
      alert("Failed to add tag. Please try again.");
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
      alert("Failed to remove tag. Please try again.");
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
    <div className={`${styles.container} ${compact ? styles.compact : ""}`}>
      <div className={styles.tagList}>
        {tags.map((tag) => (
          <span
            key={tag.id}
            className="inline-block bg-blue-500 text-white px-3 py-1 rounded-full text-sm font-semibold"
          >
            <span className="pr-2">{tag.name}</span>
            <button
              className="w-4"
              onClick={() => handleRemoveTag(tag.id)}
              title="Remove tag"
            >
              ×
            </button>
          </span>
        ))}

        {!showAddForm && (
          <button
            onClick={() => setShowAddForm(true)}
            className={styles.addBtn}
            title="Add tag"
          >
            + Add Tag
          </button>
        )}
      </div>

      {showAddForm && (
        <form onSubmit={handleAddTag} className={styles.addForm}>
          <div className={styles.inputGroup}>
            <input
              type="text"
              value={newTagName}
              onChange={(e) => setNewTagName(e.target.value)}
              placeholder="Tag name"
              className={styles.tagInput}
              disabled={isAdding}
              maxLength={20}
            />

            <div className={styles.colorPicker}>
              {predefinedColors.map((color) => (
                <button
                  key={color}
                  type="button"
                  onClick={() => setNewTagColor(color)}
                  className={`${styles.colorOption} ${
                    newTagColor === color ? styles.selected : ""
                  }`}
                  style={{ backgroundColor: color }}
                  title={`Select ${color}`}
                />
              ))}
              <input
                type="color"
                value={newTagColor}
                onChange={(e) => setNewTagColor(e.target.value)}
                className={styles.customColor}
                title="Custom color"
              />
            </div>
          </div>

          <div className={styles.formActions}>
            <button
              type="submit"
              disabled={isAdding || !newTagName.trim()}
              className={styles.saveBtn}
            >
              {isAdding ? "Adding..." : "Add"}
            </button>
            <button
              type="button"
              onClick={() => {
                setShowAddForm(false);
                setNewTagName("");
                setNewTagColor("#3b82f6");
              }}
              className={styles.cancelBtn}
              disabled={isAdding}
            >
              Cancel
            </button>
          </div>
        </form>
      )}
    </div>
  );
}
