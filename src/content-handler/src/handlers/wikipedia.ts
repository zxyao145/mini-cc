import { hostMatches } from '../page-meta';
import type { SiteHandler } from '../types';

export const wikipedia: SiteHandler = {
  name: 'wikipedia',
  matches: url => hostMatches(url, 'wikipedia.org'),
  preParse(document) {
    document.querySelectorAll('.mw-editsection, sup.reference').forEach(element => element.remove());
    const title = document.querySelector('#firstHeading')?.textContent?.trim()
      .replace(/\s+-\s+(?:Wikipedia|[维維]基百科.*)$/i, '');
    return { title };
  },
};
