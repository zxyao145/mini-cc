"use client";

import { useState } from "react";
import "./AddArticleForm.scss";

interface AddArticleFormProps {
  onAdd: (url: string) => Promise<void>;
}

export default function AddArticleForm({ onAdd }: AddArticleFormProps) {
  const [url, setUrl] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!url.trim()) {
      setError("Please enter a URL");
      return;
    }

    if (!isValidUrl(url)) {
      setError("Please enter a valid URL");
      return;
    }

    setLoading(true);
    setError("");

    try {
      await onAdd(url.trim());
      setUrl("");
    } catch (error) {
      setError("Failed to save article. Please try again.");
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  const isValidUrl = (string: string) => {
    try {
      new URL(string);
      return true;
    } catch {
      return false;
    }
  };

  return (
   <form onSubmit={handleSubmit} className="form">
        <div className="inputGroup">
          <input
            type="url"
            value={url}
            onChange={(e) => setUrl(e.target.value)}
            placeholder="Enter article URL..."
            className="input"
            disabled={loading}
          />
          <button 
            type="submit" 
            className="px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-sm hover:bg-blue-700 focus:z-10 focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 submitBtn"
            disabled={loading || !url.trim()}
          >
            {loading ? "Saving..." : "Save Article"}
          </button>
        </div>
        {error && <p className="error">{error}</p>}
      </form>
  );
}