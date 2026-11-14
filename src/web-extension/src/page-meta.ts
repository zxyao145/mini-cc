import { ARTICLE_LIMITS } from './types';

const ARTICLE_TYPES = new Set(['Article', 'NewsArticle', 'BlogPosting', 'TechArticle']);

export function assertHttpPageUrl(url: string | undefined): void {
  if (!url || !/^https?:\/\//i.test(url)) throw new Error('Only http and https pages can be saved.');
}

export function isMetaTrusted(loadedUrl: string, currentUrl: string): boolean {
  return loadedUrl.split('#', 1)[0] === currentUrl.split('#', 1)[0];
}

export function metaContent(document: Document, ...selectors: string[]): string {
  for (const selector of selectors) {
    for (const meta of document.querySelectorAll<HTMLMetaElement>(selector)) {
      const value = meta.content.trim();
      if (value) return value;
    }
  }
  return '';
}

export function pageTitle(document: Document, metaTrusted: boolean): string {
  if (metaTrusted) {
    const title = metaContent(document, 'meta[property="og:title"]', 'meta[name="twitter:title"]');
    if (title) return title;
  }
  return document.title.trim() || new URL(document.URL).hostname || 'Untitled';
}

export function pageAuthor(document: Document): string {
  return metaContent(document, 'meta[name="author"]', 'meta[property="article:author"]');
}

export function extractImageUrl(document: Document): string {
  const image = document.querySelector<HTMLMetaElement>('meta[property="og:image"], meta[name="twitter:image"]')?.content;
  if (!image) return '';
  try {
    const url = new URL(image, document.baseURI);
    return ['http:', 'https:'].includes(url.protocol) && url.href.length <= ARTICLE_LIMITS.url ? url.href : '';
  } catch {
    // Optional page metadata may contain an invalid URL.
    return '';
  }
}

export function youtubeVideoId(url: URL): string | null {
  if (!['youtube.com', 'www.youtube.com', 'm.youtube.com'].includes(url.hostname)) return null;

  const id = url.pathname === '/watch'
    ? url.searchParams.get('v')
    : url.pathname.match(/^\/shorts\/([^/]+)$/)?.[1] ?? null;
  return id && /^[\w-]{11}$/.test(id) ? id : null;
}

export function jsonLdArticles(document: Document): Record<string, unknown>[] {
  const articles: Record<string, unknown>[] = [];
  const visit = (value: unknown) => {
    if (Array.isArray(value)) {
      value.forEach(visit);
      return;
    }
    if (!value || typeof value !== 'object') return;

    const object = value as Record<string, unknown>;
    const types = Array.isArray(object['@type']) ? object['@type'] : [object['@type']];
    if (types.some(type => typeof type === 'string' && ARTICLE_TYPES.has(type))) articles.push(object);
    if (object['@graph'] !== undefined) visit(object['@graph']);
  };

  for (const script of document.querySelectorAll('script[type="application/ld+json"]')) {
    try {
      visit(JSON.parse(script.textContent ?? ''));
    } catch {
      // Malformed structured data is optional metadata.
    }
  }

  return articles;
}

export function extractPageTags(document: Document): string[] {
  const values: string[] = [];
  const splitKeywords = (value: string) => value.split(/[,，;；]/);

  for (const tag of document.querySelectorAll<HTMLMetaElement>('meta[property="article:tag"]')) {
    values.push(tag.content);
  }
  const keywords = metaContent(document, 'meta[name="keywords"]');
  if (keywords) values.push(...splitKeywords(keywords));
  for (const article of jsonLdArticles(document)) {
    const value = article.keywords;
    if (typeof value === 'string') values.push(...splitKeywords(value));
    else if (Array.isArray(value)) values.push(...value.filter((item): item is string => typeof item === 'string'));
  }

  const tags: string[] = [];
  const seen = new Set<string>();
  for (const raw of values) {
    const tag = raw.trim();
    const normalized = tag.toLowerCase();
    if (!tag || tag.length > 50 || seen.has(normalized)) continue;
    seen.add(normalized);
    tags.push(tag);
    if (tags.length === 20) break;
  }
  return tags;
}
