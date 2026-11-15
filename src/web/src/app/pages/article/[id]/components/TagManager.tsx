"use client";

import { useEffect, useId, useRef, useState, type KeyboardEvent } from "react";
import { Plus, X } from "lucide-react";
import { Button } from "@/components/arc/button/button";
import { Input } from "@/components/arc/input/input";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/arc/popover/popover";
import { Tooltip } from "@/components/arc/tooltip/tooltip";
import { TagColorOptions } from "@/app/components/TagColorOptions";
import type { Tag } from "@/types";
import { articleApi, tagApi } from "@/lib/api";
import { DEFAULT_TAG_COLOR } from "@/lib/tag-colors";
import { tagSuggestions } from "@/lib/tag-suggestions";
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
  const listId = `${tagInputId}-options`;
  const tagInput = useRef<HTMLInputElement>(null);
  const adding = useRef(false);
  const toast = useToast();
  const [query, setQuery] = useState("");
  const [color, setColor] = useState(DEFAULT_TAG_COLOR);
  const [isAdding, setIsAdding] = useState(false);
  const [showAddForm, setShowAddForm] = useState(false);
  const [allTags, setAllTags] = useState<Tag[]>([]);
  const [loadState, setLoadState] = useState<"loading" | "ready" | "error">("loading");
  const [activeIndex, setActiveIndex] = useState(-1);

  const suggestions = tagSuggestions(allTags, tags, query);
  const { attachedMatch, exact } = suggestions;
  const createOption = loadState !== "loading" && suggestions.canCreate
    ? { id: null, name: query.trim(), color }
    : null;
  const options = createOption ? [...suggestions.options, createOption] : suggestions.options;
  const activeOptionId = options[activeIndex] ? `${listId}-${activeIndex}` : undefined;
  const defaultOption = attachedMatch ? null : exact ?? createOption;

  useEffect(() => {
    if (!showAddForm) return;

    let active = true;
    void tagApi.getTags().then(result => {
      if (!active) return;
      setAllTags(result);
      setLoadState("ready");
    }).catch(() => {
      if (active) setLoadState("error");
    });
    return () => { active = false; };
  }, [showAddForm]);

  useEffect(() => {
    if (showAddForm && !isAdding) tagInput.current?.focus({ preventScroll: true });
  }, [showAddForm, isAdding]);

  useEffect(() => {
    if (activeOptionId) {
      document.getElementById(activeOptionId)?.scrollIntoView({ block: "nearest" });
    }
  }, [activeOptionId]);

  function openForm() {
    setAllTags([]);
    setLoadState("loading");
    setShowAddForm(true);
  }

  function resetForm() {
    setQuery("");
    setColor(DEFAULT_TAG_COLOR);
    setActiveIndex(-1);
  }

  function closeForm() {
    if (adding.current) return;
    resetForm();
    setShowAddForm(false);
  }

  async function add(option: Pick<Tag, "name" | "color"> | null) {
    if (!option || adding.current || loadState === "loading") return;
    const { name, color: tagColor } = option;
    adding.current = true;
    setIsAdding(true);
    try {
      const addedTag = await articleApi.addTag(articleId, { name, color: tagColor });
      onTagsChange([...tags, addedTag]);
      setAllTags(current => current.some(tag => tag.id === addedTag.id) ? current : [...current, addedTag]);
      resetForm();
    } catch (error) {
      console.error("Failed to add tag:", error);
      toast.show("添加标签失败，请重试");
    } finally {
      adding.current = false;
      setIsAdding(false);
    }
  }

  function handleInputKey(event: KeyboardEvent<HTMLInputElement>) {
    if (event.nativeEvent.isComposing) return;

    if (event.key === "Enter") {
      event.preventDefault();
      void add(options[activeIndex] ?? defaultOption);
    } else if ((event.key === "ArrowDown" || event.key === "ArrowUp") && options.length > 0) {
      event.preventDefault();
      const last = options.length - 1;
      setActiveIndex(index => event.key === "ArrowDown"
        ? Math.min(index + 1, last)
        : index < 0 ? last : Math.max(index - 1, 0));
    }
  }

  const handleRemoveTag = async (tagId: string) => {
    try {
      await articleApi.removeTag(articleId, tagId);
      onTagsChange(tags.filter((tag) => tag.id !== tagId));
      setActiveIndex(-1);
    } catch (error) {
      console.error("Failed to remove tag:", error);
      toast.show("移除标签失败，请重试");
    }
  };

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

        <Popover open={showAddForm} onOpenChange={open => (open ? openForm() : closeForm())}>
          <PopoverTrigger asChild>
            <Button variant="ghost" size="sm" className={styles.addBtn}>
              <Plus size={14} aria-hidden="true" />添加标签
            </Button>
          </PopoverTrigger>
          <PopoverContent
            className={styles.addPanel}
            aria-label="添加标签"
            onEscapeKeyDown={event => { if (event.isComposing) event.preventDefault(); }}
          >
            <form onSubmit={event => { event.preventDefault(); void add(defaultOption); }} className={styles.addForm}>
              <div>
                <Input
                  id={tagInputId}
                  ref={tagInput}
                  label="标签名称"
                  hideLabel
                  role="combobox"
                  aria-autocomplete="list"
                  aria-expanded={showAddForm}
                  aria-controls={listId}
                  aria-activedescendant={activeOptionId}
                  value={query}
                  onChange={event => { setQuery(event.target.value); setActiveIndex(-1); }}
                  onKeyDown={handleInputKey}
                  placeholder="搜索或新建标签…"
                  wrapperClassName={styles.tagInputField}
                  disabled={isAdding}
                  maxLength={100}
                />
                <ul role="listbox" id={listId} className={styles.listbox} aria-label="标签建议" aria-busy={loadState === "loading"}>
                  {loadState === "loading" && <li role="presentation" className={styles.listStatus}>正在加载标签…</li>}
                  {loadState === "error" && <li role="presentation" className={styles.listStatus}>标签加载失败</li>}
                  {attachedMatch
                    ? <li role="presentation" className={styles.listStatus}>已添加</li>
                    : loadState === "ready" && suggestions.options.length === 0 &&
                      <li role="presentation" className={styles.listStatus}>没有匹配的标签</li>}
                  {options.map((option, index) => {
                    const isCreateOption = option.id === null;
                    return (
                      <li
                        key={option.id ?? "create"}
                        id={`${listId}-${index}`}
                        role="option"
                        aria-selected={activeIndex === index}
                        aria-disabled={isAdding || undefined}
                        className={`${styles.option}${isCreateOption ? ` ${styles.createOption}` : ""}`}
                        onMouseDown={event => event.preventDefault()}
                        onClick={() => void add(option)}
                      >
                        {isCreateOption ? `创建「${option.name}」` : (
                          <>
                            <span className={styles.tagDot} style={{ backgroundColor: option.color }} aria-hidden="true" />
                            <span>{option.name}</span>
                          </>
                        )}
                      </li>
                    );
                  })}
                </ul>
              </div>

              {createOption && (
                <fieldset className={styles.colorPicker} disabled={isAdding}>
                  <legend>新标签颜色</legend>
                  <TagColorOptions color={color} onChange={setColor} className={styles.colorOption} />
                  <label className={styles.customColorLabel}>
                    <span>自定义</span>
                    <input type="color" value={color} onChange={event => setColor(event.target.value)} className={styles.customColor} aria-label="自定义标签颜色" />
                  </label>
                </fieldset>
              )}

              <div className={styles.formActions}>
                <Button type="submit" variant="primary" size="sm" disabled={isAdding || !defaultOption} className={styles.saveBtn}>
                  {isAdding ? "添加中…" : "添加"}
                </Button>
                <Button type="button" variant="secondary" size="sm" onClick={closeForm} className={styles.cancelBtn} disabled={isAdding}>完成</Button>
              </div>
            </form>
          </PopoverContent>
        </Popover>
      </div>
    </div>
  );
}
