import { isArticlePage } from './classify-page';
import { extractArticle } from './extract-article';
import { assertHttpPageUrl, extractImageUrl, isMetaTrusted, metaContent, pageAuthor, pageTitle, youtubeVideoId } from './page-meta';
import { applyOverrides, readDocument } from './prepare';
import { ARTICLE_LIMITS, type LinkPage, type PageChoices, type SiteHandler } from './types';

function extractLink(document: Document, metaTrusted: boolean): LinkPage {
  const url = document.URL;
  if (url.length > ARTICLE_LIMITS.url) throw new Error('This page URL is too long to save.');

  let summary = '';
  let author = '';
  let imageUrl = '';
  if (metaTrusted) {
    summary = metaContent(document, 'meta[property="og:description"]', 'meta[name="twitter:description"]',
      'meta[name="description"]');
    author = pageAuthor(document);
    imageUrl = extractImageUrl(document);
  }
  if (!imageUrl) {
    const videoId = youtubeVideoId(new URL(url));
    if (videoId) imageUrl = `https://i.ytimg.com/vi/${videoId}/hqdefault.jpg`;
  }

  return {
    kind: 'link',
    url,
    title: pageTitle(document, metaTrusted).slice(0, ARTICLE_LIMITS.title),
    summary: summary.slice(0, ARTICLE_LIMITS.summary),
    author: author.slice(0, ARTICLE_LIMITS.author),
    imageUrl,
  };
}

export function extractPage(document: Document, loadedUrl: string, handlers?: readonly SiteHandler[]): PageChoices {
  assertHttpPageUrl(document.URL);

  const metaTrusted = isMetaTrusted(loadedUrl, document.URL);
  const fallbackLink = extractLink(document, metaTrusted);
  const { readable, overrides } = readDocument(document, 'save', handlers);
  const link = applyOverrides(fallbackLink, overrides);
  if (!readable) return { suggested: 'link', article: null, link };

  const suggested = overrides.suggested ?? (isArticlePage(document, readable, metaTrusted) ? 'article' : 'link');
  return { suggested, article: applyOverrides(extractArticle(document, readable), overrides), link };
}
