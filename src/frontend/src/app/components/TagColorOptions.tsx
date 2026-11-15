"use client";

import { Button } from "@/components/arc/button/button";
import { Tooltip } from "@/components/arc/tooltip/tooltip";
import { TAG_COLORS } from "@/lib/tag-colors";
import styles from "./TagColorOptions.module.css";

interface TagColorOptionsProps {
  color: string;
  onChange: (color: string) => void;
  className?: string;
}

export function TagColorOptions({ color, onChange, className }: TagColorOptionsProps) {
  return TAG_COLORS.map(option => {
    const label = `选择颜色 ${option}`;
    return (
      <Tooltip key={option} content={label}>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          iconOnly
          className={`${styles.colorOption}${className ? ` ${className}` : ""}`}
          style={{ backgroundColor: option }}
          aria-label={label}
          aria-pressed={color === option}
          onClick={() => onChange(option)}
        />
      </Tooltip>
    );
  });
}
