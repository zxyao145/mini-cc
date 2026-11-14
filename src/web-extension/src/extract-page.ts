import { isArticlePage } from './classify-page';
import { extractArticle } from './extract-article';
import { assertHttpPageUrl, extractImageUrl, isMetaTrusted, metaContent, pageAuthor, pageTitle, youtubeVideoId } from './page-meta';
import { parseReadable } from './readable';
import { ARTICLE_LIMITS, type LinkPage, type PageChoices } from './types';

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

export function extractPage(document: Document, loadedUrl: string): PageChoices {
  assertHttpPageUrl(document.URL);

  const metaTrusted = isMetaTrusted(loadedUrl, document.URL);
  const link = extractLink(document, metaTrusted);
  const readable = parseReadable(document);
  if (!readable) return { suggested: 'link', article: null, link };

  const suggested = isArticlePage(document, readable, metaTrusted) ? 'article' : 'link';
  return { suggested, article: extractArticle(document, readable), link };
}
