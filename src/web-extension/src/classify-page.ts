import { jsonLdArticles, metaContent } from './page-meta';
import type { ReadableArticle } from './readable';

const ARTICLE_SIGNAL_MIN_TEXT = 200;
const ARTICLE_MIN_TEXT = 500;
const MEDIA_URL_PATTERNS = [
  /^https?:\/\/(?:www\.|m\.)?youtube\.com\/watch\b/,
  /^https?:\/\/(?:www\.|m\.)?youtube\.com\/shorts\//,
  /^https?:\/\/youtu\.be\//,
  /^https?:\/\/(?:www\.)?vimeo\.com\/\d+/,
  /^https?:\/\/(?:www\.)?bilibili\.com\/video\//,
];

export function isArticlePage(document: Document, readable: ReadableArticle, metaTrusted: boolean): boolean {
  if (MEDIA_URL_PATTERNS.some(pattern => pattern.test(document.URL))) return false;

  if (metaTrusted) {
    const ogType = metaContent(document, 'meta[property="og:type"]').toLowerCase();
    if (/^(video|music)/.test(ogType)) return false;

    const hasArticleSignal = ogType === 'article' || jsonLdArticles(document).length > 0;
    if (hasArticleSignal && readable.textLength >= ARTICLE_SIGNAL_MIN_TEXT) return true;
  }

  return readable.textLength >= ARTICLE_MIN_TEXT;
}
