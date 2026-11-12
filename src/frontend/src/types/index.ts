export interface Article {
    id: string;
    url: string;
    title: string;
    author: string;
    originalContent: string;
    readableContent: string;
    textContentLength: number; // 修复拼写错误
    summary: string;
    createdAt: string;
    readAt?: string | null;
    readingStatus: 'unread' | 'reading' | 'read';
    lastReadAt: string | null;
    progressTextOffset: number | null;
    progressBlockIndex: number | null;
    progressBlockRatio: number | null;
    progressPercent: number | null;
    progressUpdatedAt: string | null;
    progressSessionId: string | null;
    progress: { textOffset: number | null; blockIndex: number; blockRatio: number; percent: number; updatedAt: string | null } | null;
    savedFromExtension: boolean;
    feeds: { id: string; title: string }[];
    deletedAt: string | null;
    publishedAt: string | null;
    normalizedUrl: string;
    contentKind: 'full' | 'summaryOnly';
    savedFromExtensionAt: string | null;
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

export interface TagWithArticleCount extends Tag {
  articleCount: number;
}

export interface Highlight {
  anchorStatus?: "ok" | "pending" | "orphaned";
  id: string;
  text: string;
  note: string;
  color: string;
  startOffset: number;
  endOffset: number;
  createdAt: string;
  articleId: string;
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

export interface ApiKey {
  id: string;
  userId: string;
  name: string;
  key: string;
  expiredTime?: string;
  disabled: boolean;
}

export interface UpdateUserRequest {
  userName: string;
}

export interface UpdatePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface CreateApiKeyRequest {
  name: string;
  expiredTime?: string;
}

export interface UpdateApiKeyRequest {
  id: string;
  name: string;
  expiredTime?: string;
  disabled: boolean;
}

export interface ArticleStatePatch {
  isFavorite?: boolean;
  isArchived?: boolean;
  readingStatus?: 'unread' | 'read';
}

export type ArticleListItem = Pick<Article,
  'id' | 'url' | 'title' | 'author' | 'summary' | 'imageUrl' | 'createdAt' | 'publishedAt' |
  'readAt' | 'lastReadAt' | 'deletedAt' | 'readingStatus' | 'progressPercent' |
  'isFavorite' | 'isArchived' | 'contentKind' | 'tags'> & {
  savedFromExtension: boolean;
  feeds: { id: string; title: string }[];
  highlightCount: number;
};
export interface ArticleListPage { items: ArticleListItem[]; total: number; nextCursor: string | null; }

export interface Feed {
  id: string; title: string; originalTitle: string; feedUrl: string; siteUrl: string | null; format: 'rss' | 'atom';
  status: 'active' | 'paused'; health: 'ok' | 'failing'; lastSuccessAt: string | null; lastFetchedAt: string | null;
  lastError: string | null; unreadCount: number; articleCount: number; tags: Tag[];
}
export interface FeedPreview {
  feedUrl: string; title: string; siteUrl: string | null; format: 'rss' | 'atom'; existingFeedId?: string;
  items: { title: string; url: string; publishedAt: string | null; contentKind: Article['contentKind'] }[];
}
