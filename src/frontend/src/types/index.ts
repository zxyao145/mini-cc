export interface Article {
    id: string;
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
  id: string;
  name: string;
  color: string;
  createdAt: string;
}

export interface Highlight {
  id: string;
  text: string;
  note: string;
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