import { Readability } from '@mozilla/readability';
import createDOMPurify from 'dompurify';
import type { ExtractedContent } from './types';

export function extractArticle(document: Document): ExtractedContent {
  const originalContent = document.documentElement.outerHTML;
  const article = new Readability(document.cloneNode(true) as Document).parse();
  if (!article?.content) {
    throw new Error('This page does not contain a readable article.');
  }

  let imageUrl = '';
  const image = document.querySelector<HTMLMetaElement>('meta[property="og:image"], meta[name="twitter:image"]')?.content;
  if (image) {
    try {
      const url = new URL(image, document.baseURI);
      if (['http:', 'https:'].includes(url.protocol) && url.href.length <= 2000) {
        imageUrl = url.href;
      }
    } catch {
      // Invalid optional image metadata should not prevent saving the article.
    }
  }

  const purifier = createDOMPurify(document.defaultView!);
  return {
    url: document.URL,
    title: (article.title?.trim() || document.title || 'Untitled').slice(0, 500),
    author: (article.byline || '').slice(0, 200),
    summary: (article.excerpt || '').slice(0, 1000),
    imageUrl,
    originalContent,
    readableContent: purifier.sanitize(article.content),
  };
}
