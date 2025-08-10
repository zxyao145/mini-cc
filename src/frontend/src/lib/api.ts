import axios from 'axios';
import { Article, SaveArticleRequest, AddTagRequest, Highlight, Tag, TagWithArticleCount, TagWithArticles, LoginRequest, User, ApiKey, UpdateUserRequest, UpdatePasswordRequest, CreateApiKeyRequest, UpdateApiKeyRequest } from '@/types';

axios.defaults.withCredentials = true;

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';
console.debug("API_BASE_URL", API_BASE_URL);
const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
  withCredentials: true,
});

export const articleApi = {
  async getArticles(page = 1, pageSize = 20, search?: string): Promise<Article[]> {
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: pageSize.toString(),
    });
    if (search) {
      params.append('search', search);
    }
    
    const response = await api.get(`/articles?${params}`);
    return response.data;
  },

  async getArticleById(id: string): Promise<Article> {
    const response = await api.get(`/articles/${id}`);
    return response.data;
  },

  async saveArticle(url: string): Promise<Article> {
    const response = await api.post('/articles', { url } as SaveArticleRequest);
    return response.data;
  },

  async updateArticle(id: string, article: Partial<Article>): Promise<Article> {
    const response = await api.put(`/articles/${id}`, article);
    return response.data;
  },

  async deleteArticle(id: string): Promise<void> {
    await api.delete(`/articles/${id}`);
  },

  async toggleFavorite(id: string): Promise<Article> {
    const response = await api.post(`/articles/${id}/favorite`);
    return response.data;
  },

  async toggleArchive(id: string): Promise<Article> {
    const response = await api.post(`/articles/${id}/archive`);
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

  async getTagById(id: string): Promise<TagWithArticles> {
    const response = await api.get(`/tags/${id}`);
    return response.data;
  },

  async getTagArticles(id: string, page = 1, pageSize = 20): Promise<Article[]> {
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: pageSize.toString(),
    });
    
    const response = await api.get(`/tags/${id}/articles?${params}`);
    return response.data;
  },

  async deleteTag(id: string): Promise<void> {
    await api.delete(`/tags/${id}`);
  },
};

export const highlightApi = {
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