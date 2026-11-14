export const DEFAULT_API_URL = 'http://localhost:5000';

export const ARTICLE_LIMITS = { url: 2000, title: 500, author: 200, summary: 1000 };

export interface ArticlePage {
  kind: 'article';
  url: string;
  title: string;
  originalContent: string;
  readableContent: string;
  summary?: string;
  author?: string;
  imageUrl?: string;
}

export interface LinkPage {
  kind: 'link';
  url: string;
  title: string;
  summary?: string;
  author?: string;
  imageUrl?: string;
}

export type ExtractedPage = ArticlePage | LinkPage;

// Both ways to save the page; the user picks one, and `suggested` is the detected default.
export type PageChoices =
  | { suggested: 'article'; article: ArticlePage; link: LinkPage }
  | { suggested: 'link'; article: ArticlePage | null; link: LinkPage };

export interface SavePageMessage {
  action: 'savePage';
  content: ExtractedPage;
}

export type SavePageResponse =
  | { success: true }
  | { success: false; error: string };
