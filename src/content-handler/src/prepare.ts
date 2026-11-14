import { SITE_HANDLERS } from './handlers';
import { validImageUrl } from './page-meta';
import { parseReadable } from './readable';
import { ARTICLE_LIMITS, type ExtractedPage, type SiteHandler, type SiteOverrides } from './types';

export function prepareDocument(document: Document, handlers: readonly SiteHandler[] = SITE_HANDLERS) {
  const url = new URL(document.URL);
  const clone = document.cloneNode(true) as Document;
  try {
    const handler = handlers.find(handler => handler.matches(url));
    if (!handler?.preParse) return { clone, overrides: {}, preParsed: false };
    return { clone, overrides: handler.preParse(clone) ?? {}, preParsed: true };
  } catch {
    return { clone: document.cloneNode(true) as Document, overrides: {}, preParsed: false };
  }
}

export function readDocument(document: Document, mode: 'save' | 'markdown', handlers?: readonly SiteHandler[]) {
  const { clone, overrides, preParsed } = prepareDocument(document, handlers);
  const view = document.defaultView!;
  const readable = parseReadable(clone, view, mode);
  if (!readable && preParsed) {
    // A stale selector can leave the rewrite empty. Try the original page before giving up.
    const fallback = parseReadable(document.cloneNode(true) as Document, view, mode);
    if (fallback) return { readable: fallback, overrides: {} };
  }

  return { readable, overrides };
}

export function applyOverrides<T extends ExtractedPage>(page: T, overrides: SiteOverrides): T {
  const result = { ...page };
  for (const field of ['title', 'author', 'summary'] as const) {
    const value = overrides[field]?.trim();
    if (value) result[field] = value.slice(0, ARTICLE_LIMITS[field]);
  }

  const image = overrides.imageUrl?.trim();
  const imageUrl = image && validImageUrl(image, page.url);
  if (imageUrl) result.imageUrl = imageUrl;
  return result;
}
