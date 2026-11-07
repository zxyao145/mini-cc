export const DEFAULT_API_URL = 'http://localhost:5000';

export interface ExtractedContent {
  url: string;
  title: string;
  originalContent: string;
  readableContent: string;
  summary?: string;
  author?: string;
  imageUrl?: string;
}

export interface SaveArticleMessage {
  action: 'saveArticle';
  content: ExtractedContent;
}

export type SaveArticleResponse =
  | { success: true }
  | { success: false; error: string };
