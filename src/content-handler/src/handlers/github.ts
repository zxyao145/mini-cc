import { hostMatches, metaContent } from '../page-meta';
import type { SiteHandler } from '../types';

export const github: SiteHandler = {
  name: 'github',
  matches: url => hostMatches(url, 'github.com'),
  preParse(document) {
    const article = document.querySelector('article');
    if (!article) return;

    const owner = new URL(document.URL).pathname.split('/')[1];
    const breadcrumb = Array.from(document.querySelectorAll('a[data-component="Breadcrumbs.Item"]'))
      .find(anchor => anchor.getAttribute('href')?.toLowerCase() === `/${owner.toLowerCase()}`);
    const author = document.querySelector('[itemprop="author"]')?.textContent?.trim()
      || breadcrumb?.textContent?.trim();
    const title = metaContent(document, 'meta[name="twitter:title"]', 'meta[property="og:title"]')
      .replace(/^GitHub - [^/]+\//i, '');
    document.body.replaceChildren(article);
    return { author, title };
  },
};
