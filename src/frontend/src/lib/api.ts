import axios from 'axios';
import { Article, SaveArticleRequest, AddTagRequest, Highlight, Tag } from '@/types';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'https://localhost:5001/api';

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
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

  async getArticleById(id: number): Promise<Article> {
    const response = await api.get(`/articles/${id}`);
    return response.data;
  },

  async saveArticle(url: string): Promise<Article> {
    const response = await api.post('/articles', { url } as SaveArticleRequest);
    return response.data;
  },

  async updateArticle(id: number, article: Partial<Article>): Promise<Article> {
    const response = await api.put(`/articles/${id}`, article);
    return response.data;
  },

  async deleteArticle(id: number): Promise<void> {
    await api.delete(`/articles/${id}`);
  },

  async toggleFavorite(id: number): Promise<Article> {
    const response = await api.post(`/articles/${id}/favorite`);
    return response.data;
  },

  async toggleArchive(id: number): Promise<Article> {
    const response = await api.post(`/articles/${id}/archive`);
    return response.data;
  },

  async addHighlight(articleId: number, highlight: Omit<Highlight, 'id' | 'createdAt' | 'articleId'>): Promise<Highlight> {
    const response = await api.post(`/articles/${articleId}/highlights`, highlight);
    return response.data;
  },

  async deleteHighlight(highlightId: number): Promise<void> {
    await api.delete(`/highlights/${highlightId}`);
  },

  async addTag(articleId: number, tag: AddTagRequest): Promise<Tag> {
    const response = await api.post(`/articles/${articleId}/tags`, tag);
    return response.data;
  },

  async removeTag(articleId: number, tagId: number): Promise<void> {
    await api.delete(`/articles/${articleId}/tags/${tagId}`);
  },
};