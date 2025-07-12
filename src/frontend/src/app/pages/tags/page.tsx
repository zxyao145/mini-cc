"use client";

import { useState, useEffect } from "react";
import Link from "next/link";
import { TagWithArticleCount } from "@/types";
import { tagApi } from "@/lib/api";
import styles from "./tags.module.scss";

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
      setError("Failed to load tags");
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
      setError("Failed to search tags");
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
    return (
      <div className={styles.container}>
        <div className={styles.loading}>
          <h1>Tags</h1>
          <p>Loading tags...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className={styles.container}>
        <div className={styles.error}>
          <h1>Tags</h1>
          <p>{error}</p>
          <button onClick={loadTags} className={styles.retryButton}>
            Retry
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <header className={styles.header}>
        <h1>All Tags</h1>
        <p>Organize your articles with tags</p>
      </header>

      <div className={styles.controls}>
        <div className={styles.searchContainer}>
          <input
            type="text"
            placeholder="Search tags..."
            value={searchTerm}
            onChange={(e) => handleSearchChange(e.target.value)}
            className={styles.searchInput}
          />
        </div>
        
        <div className={styles.stats}>
          <span>{filteredTags.length} tags</span>
          <span>{filteredTags.reduce((sum, tag) => sum + tag.articleCount, 0)} articles</span>
        </div>
      </div>

      <main className={styles.main}>
        {filteredTags.length === 0 ? (
          <div className={styles.empty}>
            <h2>No tags found</h2>
            <p>
              {searchTerm 
                ? "Try adjusting your search terms." 
                : "Start adding tags to your articles to see them here."
              }
            </p>
          </div>
        ) : (
          <div className={styles.tagGroups}>
            {groupedTags.map((group) => (
              <div key={group.letter} className={styles.tagGroup}>
                <h3 className={styles.groupLetter}>{group.letter}</h3>
                <div className={styles.tagsGrid}>
                  {group.tags.map((tag) => (
                    <Link 
                      key={tag.id} 
                      href={`/pages/tags/${tag.id}`} 
                      className={styles.tagCard}
                    >
                      <div 
                        className={styles.tagColor} 
                        style={{ backgroundColor: tag.color }}
                      />
                      <div className={styles.tagInfo}>
                        <span className={styles.tagName}>{tag.name}</span>
                        <span className={styles.articleCount}>
                          {tag.articleCount} article{tag.articleCount !== 1 ? 's' : ''}
                        </span>
                      </div>
                    </Link>
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
      </main>
    </div>
  );
}