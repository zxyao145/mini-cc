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

export interface SiteOverrides {
  title?: string;
  author?: string;
  summary?: string;
  imageUrl?: string;
  suggested?: 'article' | 'link';
}

export interface FetchContext {
  fetch: typeof fetch;
  signal?: AbortSignal;
}

export interface SiteHandler {
  name: string;
  matches(url: URL): boolean;
  resolve?(url: URL, ctx: FetchContext): Promise<string | null>;
  preHandle?(url: URL, ctx: FetchContext): Promise<SiteOverrides | null>;
  preParse?(document: Document): SiteOverrides | void;
}
