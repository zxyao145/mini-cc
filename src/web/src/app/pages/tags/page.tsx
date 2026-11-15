"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { TagWithArticleCount } from "@/types";
import { tagApi } from "@/lib/api";
import { Pencil, Plus, Search, Tag as TagIcon, Trash2 } from "lucide-react";
import { Button } from "@/components/arc/button/button";
import { Input } from "@/components/arc/input/input";
import { useConfirm } from "@/contexts/ConfirmContext";
import { useToast } from "@/contexts/ToastContext";
import { notifyLibraryMutation } from "@/lib/library-mutations";
import TagDialog from "./TagDialog";
import styles from "./tags.module.css";

interface TagGroup {
  letter: string;
  tags: TagWithArticleCount[];
}

export default function TagsPage() {
  const [tags, setTags] = useState<TagWithArticleCount[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState("");
  const [dialogOpen, setDialogOpen] = useState(false);
  const [deletingIds, setDeletingIds] = useState<ReadonlySet<string>>(new Set());
  const [editingTag, setEditingTag] = useState<TagWithArticleCount | null>(null);
  const editButtons = useRef(new Map<string, HTMLButtonElement>());
  const refocusTagId = useRef<string | null>(null);
  const deletedIds = useRef(new Set<string>());
  const searchVersion = useRef(0);
  const confirm = useConfirm();
  const toast = useToast();

  const replaceTags = useCallback((next: TagWithArticleCount[]) => {
    setTags(next.filter(tag => !deletedIds.current.has(tag.id)));
  }, []);

  const loadTags = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const tagsData = await tagApi.getTags();
      replaceTags(tagsData);
    } catch (err) {
      console.error("Error loading tags:", err);
      setError("加载标签失败");
    } finally {
      setLoading(false);
    }
  }, [replaceTags]);

  useEffect(() => {
    void loadTags();
  }, [loadTags]);

  // A rename can move the card into another letter group, which replaces the button the dialog would refocus.
  useEffect(() => {
    const id = refocusTagId.current;
    if (!id) return;
    refocusTagId.current = null;
    editButtons.current.get(id)?.focus();
  }, [tags]);

  const searchTags = async (searchTerm: string) => {
    const version = ++searchVersion.current;
    try {
      setError(null);
      const tagsData = await tagApi.getTags(searchTerm || undefined);
      if (version === searchVersion.current) {
        replaceTags(tagsData);
      }
    } catch (err) {
      if (version !== searchVersion.current) return;
      console.error("Error searching tags:", err);
      setError("搜索标签失败");
    }
  };

  const handleSearchChange = (value: string) => {
    setSearchTerm(value);
    // 使用 debounce 来避免频繁的 API 调用
    const timeoutId = setTimeout(() => {
      searchTags(value);
    }, 300);

    return () => clearTimeout(timeoutId);
  };

  const handleCreated = (tag: TagWithArticleCount) => {
    setDialogOpen(false);
    toast.show(`已创建「${tag.name}」`);
    void searchTags(searchTerm);
  };

  const handleEdited = (tag: TagWithArticleCount) => {
    setEditingTag(null);
    refocusTagId.current = tag.id;
    setTags(current => current.map(item => item.id === tag.id ? tag : item));
    notifyLibraryMutation();
    void searchTags(searchTerm);
  };

  const handleDelete = async (tag: TagWithArticleCount) => {
    const message = `删除「${tag.name}」标签？`;
    const detail = tag.articleCount > 0 ? `它会从 ${tag.articleCount} 篇文章中移除，文章本身不受影响。` : "";
    if (!await confirm(`${message}${detail}`, { title: "删除标签", confirmLabel: "删除标签" })) return;

    setDeletingIds(ids => new Set(ids).add(tag.id));
    try {
      await tagApi.deleteTag(tag.id);
      deletedIds.current.add(tag.id);
      setTags(current => current.filter(item => item.id !== tag.id));
      notifyLibraryMutation();
    } catch {
      toast.show("标签删除失败，请重试。");
    } finally {
      setDeletingIds(ids => {
        const remaining = new Set(ids);
        remaining.delete(tag.id);
        return remaining;
      });
    }
  };

  const filteredTags = tags.filter(tag =>
    tag.name.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const groupedTags = filteredTags.reduce((groups: TagGroup[], tag) => {
    const letter = tag.name[0].toUpperCase();
    const existingGroup = groups.find(group => group.letter === letter);
    
    if (existingGroup) {
      existingGroup.tags.push(tag);
    } else {
      groups.push({ letter, tags: [tag] });
    }
    
    return groups;
  }, []).sort((a, b) => a.letter.localeCompare(b.letter));

  if (loading) {
    return <div className={styles.container}><h1 className="sr-only">标签</h1><p className={styles.state}>正在加载标签…</p></div>;
  }

  if (error) {
    return (
      <div className={styles.container}>
        <h1 className="sr-only">标签</h1>
        <p className={styles.error} role="alert">{error}</p>
        <Button onClick={() => void loadTags()}>重试</Button>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <header className={styles.header}>
        <div>
          <h1><TagIcon size={24} aria-hidden="true" />标签</h1>
        </div>
        <div className={styles.stats}>
          <span>{filteredTags.length} 个标签</span>
          <span>{filteredTags.reduce((sum, tag) => sum + tag.articleCount, 0)} 篇文章</span>
        </div>
      </header>

      <div className={styles.controls}>
        <Input
          type="search"
          label="搜索标签"
          hideLabel
          leading={<Search size={16} aria-hidden="true" />}
          value={searchTerm}
          onChange={(e) => handleSearchChange(e.target.value)}
          placeholder="搜索标签…"
          className={styles.searchInput}
          wrapperClassName={styles.searchField}
        />
        <Button onClick={() => setDialogOpen(true)}>
          <Plus size={16} aria-hidden="true" />新建标签
        </Button>
      </div>

      {dialogOpen && <TagDialog onClose={() => setDialogOpen(false)} onSaved={handleCreated} />}
      {editingTag && <TagDialog tag={editingTag} onClose={() => setEditingTag(null)} onSaved={handleEdited} />}

      <div className={styles.main}>
        {filteredTags.length === 0 ? (
          <section className={styles.empty}>
            <h2>{searchTerm ? '没有找到标签' : '还没有标签'}</h2>
            <p>{searchTerm ? '试试其他关键词。' : '点击「新建标签」先建好标签，也可以在阅读文章时添加。'}</p>
          </section>
        ) : (
          <div className={styles.tagGroups}>
            {groupedTags.map(group => (
              <section key={group.letter} className={styles.tagGroup} aria-label={`标签 ${group.letter}`}>
                <h2 className={styles.groupLetter}>{group.letter}</h2>
                <div className={styles.tagsGrid}>
                  {group.tags.map(tag => (
                    <div key={tag.id} className={styles.tagCard}>
                      <Link href={`/pages/tags/${tag.id}`} className={styles.tagLink}>
                        <span className={styles.tagColor} style={{ backgroundColor: tag.color }} aria-hidden="true" />
                        <span className={styles.tagInfo}>
                          <span className={styles.tagName}>{tag.name}</span>
                          <span className={styles.articleCount}>{tag.articleCount} 篇</span>
                        </span>
                      </Link>
                      <div className={styles.cardActions}>
                        <Button
                          ref={node => {
                            if (node) editButtons.current.set(tag.id, node);
                            else editButtons.current.delete(tag.id);
                          }}
                          variant="ghost"
                          size="sm"
                          iconOnly
                          aria-label={`编辑标签「${tag.name}」`}
                          onClick={() => setEditingTag(tag)}
                        >
                          <Pencil size={15} aria-hidden="true" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          iconOnly
                          aria-label={`删除标签「${tag.name}」`}
                          disabled={deletingIds.has(tag.id)}
                          onClick={() => void handleDelete(tag)}
                        >
                          <Trash2 size={15} aria-hidden="true" />
                        </Button>
                      </div>
                    </div>
                  ))}
                </div>
              </section>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
