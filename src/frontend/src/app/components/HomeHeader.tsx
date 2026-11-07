"use client";

import { useState, useEffect, type FormEvent } from "react";
import { LayoutGrid, List, Search, X } from "lucide-react";
import "./HomeHeader.scss";

type ArticleStyle = "grid" | "list";

interface SearchBarProps {
  value: string;
  onChange: (value: string) => void;
  onStyleChange: (value: ArticleStyle) => void;
  placeholder?: string;
}

export default function SearchBar({ value, onChange, onStyleChange, placeholder = "Search articles..." }: SearchBarProps) {
  const [articleStyle, setArticleStyle] = useState<ArticleStyle>("grid");
  const [localValue, setLocalValue] = useState(value);

  useEffect(() => { setLocalValue(value); }, [value]);

  const handleSearch = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    onChange(localValue);
  };

  const handleClear = () => {
    setLocalValue("");
    onChange("");
  };

  const changeStyle = (style: ArticleStyle) => {
    setArticleStyle(style);
    onStyleChange(style);
  };

  return (
    <div className="library-toolbar">
      <form className="library-search" role="search" onSubmit={handleSearch}>
        <label className="sr-only" htmlFor="search-input">Search articles</label>
        <Search size={19} className="library-search-icon" aria-hidden="true" />
        <input id="search-input" type="search" value={localValue} onChange={(event) => setLocalValue(event.target.value)} placeholder={placeholder} />
        {localValue && (
          <button type="button" className="library-clear" onClick={handleClear} aria-label="Clear search" title="Clear search"><X size={16} aria-hidden="true" /></button>
        )}
        <button type="submit" className="library-search-submit" aria-label="Search articles" title="Search articles"><span>Search</span><Search size={18} aria-hidden="true" /></button>
      </form>
      <div className="library-view-switch" role="group" aria-label="Article view">
        <button type="button" onClick={() => changeStyle("grid")} aria-pressed={articleStyle === "grid"} aria-label="Grid view" title="Grid view"><LayoutGrid size={18} aria-hidden="true" /></button>
        <button type="button" onClick={() => changeStyle("list")} aria-pressed={articleStyle === "list"} aria-label="List view" title="List view"><List size={19} aria-hidden="true" /></button>
      </div>
    </div>
  );
}
