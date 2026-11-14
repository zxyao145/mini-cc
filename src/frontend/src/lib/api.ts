import { ReaderPreferences } from "./reader-preferences";
import axios from 'axios';
import { Article, ArticleListItem, ArticleListPage, ArticleStatePatch, AddTagRequest, Highlight, Tag, TagWithArticleCount, LoginRequest, User, ApiKey, UpdateUserRequest, UpdatePasswordRequest, CreateApiKeyRequest, UpdateApiKeyRequest } from '@/types';

axios.defaults.withCredentials = true;

export const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5001/api';
console.debug("API_BASE_URL", API_BASE_URL);
const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
  withCredentials: true,
});

export const articleApi = {
  async batch(ids: string[], action: import('./library-mutations').BatchAction, tagIds?: string[]): Promise<import('./library-mutations').BatchResult> {
    return (await api.post('/articles/batch', { ids, action, tagIds })).data;
  },
  async emptyTrash(): Promise<{ purged: number }> { return (await api.delete('/trash')).data; },
  async getLibrarySettings(): Promise<{ undoWindowSeconds: number; trashRetentionDays: number }> { return (await api.get('/settings/library')).data; },
  async getArticles(params = new URLSearchParams(), signal?: AbortSignal): Promise<ArticleListPage> {
    const response = await api.get(`/articles?${params}`, { signal });
    return response.data;
  },

  async getArticleById(id: string): Promise<Article> {
    const response = await api.get(`/articles/${id}`);
    return response.data;
  },

  async updateArticle(id: string, article: Partial<Article>): Promise<Article> {
    const response = await api.put(`/articles/${id}`, article);
    return response.data;
  },

  async deleteArticle(id: string): Promise<void> {
    await api.delete(`/articles/${id}`);
  },

  async updateState(id: string, patch: ArticleStatePatch): Promise<ArticleListItem> {
    const response = await api.patch(`/articles/${id}/state`, patch);
    return response.data;
  },

  async saveProgress(id: string, request: import('./progress-writer').ProgressRequest, keepalive = false): Promise<import('./progress-writer').ProgressSaved> {
    const response = await fetch(`${API_BASE_URL}/articles/${id}/progress`, {
      method: 'PUT', headers: { 'Content-Type': 'application/json' }, credentials: 'include',
      keepalive, body: JSON.stringify(request),
    });
    if (!response.ok) {
      const body = await response.json().catch(() => ({}));
      throw { status: response.status, current: body.current };
    }
    return response.json();
  },

  async resetProgress(id: string, sessionId: string): Promise<import('./progress-writer').ProgressSaved> {
    const response = await api.post(`/articles/${id}/progress/reset`, { sessionId });
    return response.data;
  },

  async restoreArticle(id: string): Promise<ArticleListItem> {
    const response = await api.post(`/articles/${id}/restore`);
    return response.data;
  },

  async addHighlight(articleId: string, highlight: Omit<Highlight, 'id' | 'createdAt' | 'articleId'>): Promise<Highlight> {
    const response = await api.post(`/articles/${articleId}/highlights`, highlight);
    return response.data;
  },

  async deleteHighlight(highlightId: string): Promise<void> {
    await api.delete(`/highlights/${highlightId}`);
  },

  async addTag(articleId: string, tag: AddTagRequest): Promise<Tag> {
    const response = await api.post(`/articles/${articleId}/tags`, tag);
    return response.data;
  },

  async removeTag(articleId: string, tagId: string): Promise<void> {
    await api.delete(`/articles/${articleId}/tags/${tagId}`);
  },
};

export const tagApi = {
  async getTags(search?: string): Promise<TagWithArticleCount[]> {
    const params = new URLSearchParams();
    if (search) {
      params.append('search', search);
    }
    
    const response = await api.get(`/tags${params.toString() ? `?${params}` : ''}`);
    return response.data;
  },

  async getTagArticles(id: string, params = new URLSearchParams(), signal?: AbortSignal): Promise<ArticleListPage> {
    const response = await api.get(`/tags/${id}/articles?${params}`, { signal });
    return response.data;
  },

  async deleteTag(id: string): Promise<void> {
    await api.delete(`/tags/${id}`);
  },
};

export const highlightApi = {
  async anchor(id: string, anchor: import("./highlight-anchor").HighlightAnchor): Promise<Highlight> { return (await api.put(`/highlights/${id}/anchor`, anchor)).data; },
  async getHighlights(): Promise<Highlight[]> {
    const response = await api.get('/highlights');
    return response.data;
  },

  async updateHighlight(highlightId: string, note: string): Promise<Highlight> {
    const response = await api.put(`/highlights/${highlightId}`, { note });
    return response.data;
  },

  async deleteHighlight(highlightId: string): Promise<void> {
    await api.delete(`/highlights/${highlightId}`);
  },
};

export const authApi = {
  async login(credentials: LoginRequest): Promise<void> {
    const formData = new FormData();
    formData.append('userName', credentials.userName);
    formData.append('password', credentials.password);
    formData.append('rememberMe', String(credentials.rememberMe ?? true));

    await api.post('/account/login', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
  },

  async logout(): Promise<void> {
    await api.post('/account/logout');
  },

  async getCurrentUser(): Promise<User | null> {
    try {
      const response = await api.get('/account/current');
      return response.data;
    } catch {
      return null;
    }
  },

  async updateUserName(request: UpdateUserRequest): Promise<void> {
    await api.put('/account/username', request);
  },

  async updatePassword(request: UpdatePasswordRequest): Promise<void> {
    await api.put('/account/password', request);
  },
};

export const accessKeyApi = {
  async getApiKeys(): Promise<ApiKey[]> {
    const response = await api.get('/apiKey/list');
    return response.data;
  },

  async createApiKey(request: CreateApiKeyRequest): Promise<ApiKey> {
    const response = await api.post('/apiKey/create', request);
    return response.data;
  },

  async updateApiKey(request: UpdateApiKeyRequest): Promise<void> {
    await api.put('/apiKey/update', request);
  },

  async deleteApiKey(id: string): Promise<void> {
    await api.delete(`/apiKey/delete/${id}`);
  },
};

export const readerPreferencesApi = {
  async get():Promise<ReaderPreferences> { return (await api.get("/account/reader-preferences")).data; },
  async put(value:ReaderPreferences):Promise<ReaderPreferences> { return (await api.put("/account/reader-preferences",value)).data; },
};

export const feedApi = {
  async list(): Promise<import('@/types').Feed[]> { return (await api.get('/feeds')).data; },
  async preview(url: string): Promise<import('@/types').FeedPreview> { return (await api.post('/feeds/preview', { url })).data; },
  async create(feedUrl: string, title: string, tagIds: string[]): Promise<{ feed: import('@/types').Feed; imported: number }> { return (await api.post('/feeds', { feedUrl, title, tagIds })).data; },
  async edit(id: string, patch: { title?: string; tagIds?: string[]; paused?: boolean }): Promise<import('@/types').Feed> { return (await api.patch(`/feeds/${id}`, patch)).data; },
  async refresh(id: string): Promise<{ imported: number; skipped: number; status: string; lastError: string | null }> { return (await api.post(`/feeds/${id}/refresh`)).data; },
  async unsubscribe(id: string): Promise<void> { await api.delete(`/feeds/${id}`); },
  async trash(id: string): Promise<{ trashed: number; skipped: number }> { return (await api.post(`/feeds/${id}/trash-articles`)).data; },
};
