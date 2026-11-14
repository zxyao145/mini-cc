"use client";

import { useState, useEffect } from "react";
import Link from "next/link";
import { TagWithArticleCount } from "@/types";
import { tagApi } from "@/lib/api";
import { Search, Tag as TagIcon } from "lucide-react";
import { Button } from "@/components/arc/button/button";
import { Input } from "@/components/arc/input/input";
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

  useEffect(() => {
    loadTags();
  }, []);

  const loadTags = async () => {
    try {
      setLoading(true);
      setError(null);
      const tagsData = await tagApi.getTags();
      setTags(tagsData);
    } catch (err) {
      console.error("Error loading tags:", err);
      setError("加载标签失败");
    } finally {
      setLoading(false);
    }
  };

  const searchTags = async (searchTerm: string) => {
    try {
      setError(null);
      const tagsData = await tagApi.getTags(searchTerm || undefined);
      setTags(tagsData);
    } catch (err) {
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
      </div>

      <div className={styles.main}>
        {filteredTags.length === 0 ? (
          <section className={styles.empty}>
            <h2>{searchTerm ? '没有找到标签' : '还没有标签'}</h2>
            <p>{searchTerm ? '试试其他关键词。' : '给文章添加标签后，它们会显示在这里。'}</p>
          </section>
        ) : (
          <div className={styles.tagGroups}>
            {groupedTags.map(group => (
              <section key={group.letter} className={styles.tagGroup} aria-label={`标签 ${group.letter}`}>
                <h2 className={styles.groupLetter}>{group.letter}</h2>
                <div className={styles.tagsGrid}>
                  {group.tags.map(tag => (
                    <Link key={tag.id} href={`/pages/tags/${tag.id}`} className={styles.tagCard}>
                      <span className={styles.tagColor} style={{ backgroundColor: tag.color }} aria-hidden="true" />
                      <span className={styles.tagInfo}>
                        <span className={styles.tagName}>{tag.name}</span>
                        <span className={styles.articleCount}>{tag.articleCount} 篇</span>
                      </span>
                    </Link>
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
