export interface Article {
    id: string;
    url: string;
    title: string;
    author: string;
    originContent: string;
    readableContent: string;
    textContentLength: number; // 修复拼写错误
    summary: string;
    createdAt: string;
    readAt?: string ;
    isArchived: boolean;
    isFavorite: boolean;
    imageUrl: string;
    tags: Tag[];
    highlights: Highlight[];
}

export interface ArticleSummary {
    id: string;
    title: string;
    author: string;
    summary: string;
    createdAt: string;
    imageUrl: string;
    isFavorite: boolean;
    isArchived: boolean;
}

export interface Tag {
  id: string;
  name: string;
  color: string;
  createdAt: string;
}

export interface TagWithArticleCount extends Tag {
  articleCount: number;
}

export interface TagWithArticles extends Tag {
  articles: ArticleSummary[];
}

export interface Highlight {
  id: string;
  text: string;
  note: string;
  color: string;
  startOffset: number;
  endOffset: number;
  createdAt: string;
  articleId: string;
}

export interface SaveArticleRequest {
  url: string;
}

export interface AddTagRequest {
  name: string;
  color?: string;
}

export interface LoginRequest {
  userName: string;
  password: string;
  rememberMe?: boolean;
}

export interface User {
  userName: string;
  isAuthenticated: boolean;
}