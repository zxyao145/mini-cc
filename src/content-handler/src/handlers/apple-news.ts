import type { SiteHandler } from '../types';

const desktopUserAgent =
  'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36';

export const appleNews: SiteHandler = {
  name: 'apple-news',
  matches: url => url.hostname === 'apple.news',
  async resolve(url, { fetch, signal }) {
    const response = await fetch(url.href, {
      headers: { 'User-Agent': desktopUserAgent },
      signal,
    });
    if (!response.ok) return null;

    const html = await response.text();
    for (const [, attributes, body] of html.matchAll(/<a\s+([^>]*)>([\s\S]*?)<\/a\s*>/gi)) {
      const clickHere = Array.from(body.matchAll(/<span\b[^>]*?\sclass\s*=\s*(["'])([^"']*)\1[^>]*>/gi))
        .some(([, , classes]) => classes.split(/\s+/).includes('click-here'));
      if (!clickHere) continue;

      const href = ` ${attributes}`.match(/\shref\s*=\s*(["'])(.*?)\1/i)?.[2];
      if (href) return href.replace(/&(?:amp|#38|#x26);/gi, '&');
    }
    return null;
  },
};
