import { extractImageUrl } from './page-meta';
import type { ReadableArticle } from './readable';
import { ARTICLE_LIMITS, type ArticlePage } from './types';

export function extractArticle(document: Document, readable: ReadableArticle): ArticlePage {
  return {
    kind: 'article',
    url: document.URL,
    title: (readable.title.trim() || document.title || 'Untitled').slice(0, ARTICLE_LIMITS.title),
    author: (readable.byline || '').slice(0, ARTICLE_LIMITS.author),
    summary: (readable.excerpt || '').slice(0, ARTICLE_LIMITS.summary),
    imageUrl: extractImageUrl(document),
    originalContent: document.documentElement.outerHTML,
    readableContent: readable.html,
  };
}
