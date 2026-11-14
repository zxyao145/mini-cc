import { hostMatches, youtubeVideoId } from '../page-meta';
import type { SiteHandler } from '../types';

const youtubeOrigin = 'https://www.youtube.com';

function canonicalUrl(url: URL): string | null {
  const id = hostMatches(url, 'youtu.be') ? url.pathname.match(/^\/([\w-]{11})$/)?.[1] : youtubeVideoId(url);
  if (id) {
    const path = url.pathname.startsWith('/shorts/') ? `/shorts/${id}` : `/watch?v=${id}`;
    return youtubeOrigin + path;
  }

  const playlist = url.pathname === '/playlist' && hostMatches(url, 'youtube.com') ? url.searchParams.get('list') : null;
  return playlist && /^[\w-]+$/.test(playlist) ? `${youtubeOrigin}/playlist?list=${playlist}` : null;
}

export const youtube: SiteHandler = {
  name: 'youtube',
  matches: url => hostMatches(url, 'youtube.com') || hostMatches(url, 'youtu.be'),
  async preHandle(url, { fetch, signal }) {
    const canonical = canonicalUrl(url);
    if (!canonical) return null;

    const response = await fetch(`${youtubeOrigin}/oembed?format=json&url=${encodeURIComponent(canonical)}`, { signal });
    if (!response.ok) throw new Error(`YouTube oEmbed returned HTTP ${response.status}`);
    const data = await response.json();
    const title = typeof data?.title === 'string' ? data.title : undefined;
    const author = typeof data?.author_name === 'string' ? data.author_name : undefined;
    const imageUrl = typeof data?.thumbnail_url === 'string' ? data.thumbnail_url : undefined;
    return title || author || imageUrl ? { title, author, imageUrl } : null;
  },
};
