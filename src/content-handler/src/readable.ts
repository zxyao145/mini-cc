import { Readability } from '@mozilla/readability';
import createDOMPurify from 'dompurify';

export interface ReadableArticle {
  title: string;
  byline: string;
  excerpt: string;
  html: string;
  textLength: number;
}

export function parseReadable(
  clone: Document,
  view: NonNullable<Document['defaultView']>,
  mode: 'save' | 'markdown' = 'save',
): ReadableArticle | null {
  if (mode === 'markdown') {
    // Readability removes form controls. Keep task state as an inert marker in the clone.
    const beforeCheckbox = clone.createRange();
    for (const input of clone.querySelectorAll<HTMLInputElement>('li > input[type="checkbox"]:first-child, li > p:first-child > input[type="checkbox"]:first-child')) {
      beforeCheckbox.setStart(input.closest('li')!, 0);
      beforeCheckbox.setEndBefore(input);
      if (beforeCheckbox.toString().trim()) continue;

      const marker = clone.createElement('wbr');
      marker.setAttribute('data-minicc-task', input.checked ? 'checked' : 'unchecked');
      input.replaceWith(marker, ' ');
    }
  }

  const article = new Readability(clone, { keepClasses: mode === 'markdown' }).parse();
  if (!article?.content) return null;

  const html = createDOMPurify(view).sanitize(article.content);

  return {
    title: article.title ?? '',
    byline: article.byline ?? '',
    excerpt: article.excerpt ?? '',
    html,
    textLength: (article.textContent ?? '').replace(/\s/g, '').length,
  };
}
