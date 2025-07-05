"use client";

import { useState, useEffect } from "react";
import Link from "next/link";
import styles from "./tags.module.scss";

interface Tag {
  id: number;
  name: string;
  color: string;
  articleCount: number;
}

interface TagGroup {
  letter: string;
  tags: Tag[];
}

export default function TagsPage() {
  const [tags, setTags] = useState<Tag[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState("");

  useEffect(() => {
    // TODO: Load tags from API
    setLoading(false);
    // Mock data for now
    setTags([
      { id: 1, name: "Technology", color: "#3498db", articleCount: 15 },
      { id: 2, name: "Science", color: "#e74c3c", articleCount: 8 },
      { id: 3, name: "Programming", color: "#2ecc71", articleCount: 23 },
      { id: 4, name: "Design", color: "#9b59b6", articleCount: 12 },
      { id: 5, name: "Business", color: "#f39c12", articleCount: 7 },
      { id: 6, name: "AI", color: "#1abc9c", articleCount: 19 },
      { id: 7, name: "Web Development", color: "#34495e", articleCount: 31 },
      { id: 8, name: "Mobile", color: "#e67e22", articleCount: 9 }
    ]);
  }, []);

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
        <h1>Tags</h1>
        <p>Loading tags...</p>
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
            onChange={(e) => setSearchTerm(e.target.value)}
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
            <p>Try adjusting your search or create some tags for your articles.</p>
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
                      href={`/?tag=${tag.id}`} 
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