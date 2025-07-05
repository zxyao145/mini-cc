export interface Article {
    id: number;
    url: string;
    title: string;
    author: string;
    originContent: string;
    readableContent: string;
    textContentLegth: number;
    summary: string;
    createdAt: string;
    readAt?: string ;
    isArchived: boolean;
    isFavorite: boolean;
    imageUrl: string;
    tags: Tag[];
    highlights: Highlight[];
}


export interface Tag {
  id: number;
  name: string;
  color: string;
  createdAt: string;
}

export interface Highlight {
  id: number;
  text: string;
  note: string;
  startOffset: number;
  endOffset: number;
  createdAt: string;
  articleId: number;
}

export interface SaveArticleRequest {
  url: string;
}

export interface AddTagRequest {
  name: string;
  color?: string;
}

export interface LoginRequest {
  username: string;
  password: string;
  rememberMe?: boolean;
}

export interface User {
  username: string;
  isAuthenticated: boolean;
}