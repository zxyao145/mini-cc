import { hostMatches, validImageUrl } from '../page-meta';
import type { SiteHandler } from '../types';

export const medium: SiteHandler = {
  name: 'medium',
  matches: url => hostMatches(url, 'medium.com'),
  preParse(document) {
    for (const picture of document.querySelectorAll('picture')) {
      let widest = 0;
      let src = '';
      for (const source of picture.querySelectorAll('source[srcset]')) {
        const srcset = source.getAttribute('srcset') ?? '';
        for (const [, url, descriptor] of srcset.matchAll(/(?:^|,)\s*([^\s,]\S*)\s+(\d+)w(?=\s*(?:,|$))/g)) {
          const width = Number(descriptor);
          if (width <= widest) continue;
          const candidate = validImageUrl(url, document.baseURI);
          if (candidate) {
            widest = width;
            src = candidate;
          }
        }
      }
      if (!src) continue;

      const image = picture.querySelector('img') ?? document.createElement('img');
      image.setAttribute('src', src);
      image.removeAttribute('srcset');
      image.removeAttribute('sizes');
      image.removeAttribute('width');
      image.removeAttribute('height');
      picture.replaceWith(image);
    }
  },
};
