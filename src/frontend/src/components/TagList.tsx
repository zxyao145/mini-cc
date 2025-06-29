"use client";

import { Tag } from "@/types";
import styles from "./TagList.module.scss";

interface TagListProps {
  tags: Tag[];
  maxTags?: number;
}

export default function TagList({ tags, maxTags = 3 }: TagListProps) {
  const displayTags = tags.slice(0, maxTags);
  const remainingCount = tags.length - maxTags;

  if (tags.length === 0) {
    return null;
  }

  return (
    <div className={styles.container}>
      {displayTags.map((tag) => (
        <span
          key={tag.id}
          className={styles.tag}
          style={{ backgroundColor: tag.color }}
        >
          {tag.name}
        </span>
      ))}
      {remainingCount > 0 && (
        <span className={styles.moreIndicator}>
          +{remainingCount} more
        </span>
      )}
    </div>
  );
}