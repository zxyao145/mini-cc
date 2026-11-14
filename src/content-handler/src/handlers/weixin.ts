import { hostMatches, validImageUrl } from '../page-meta';
import type { SiteHandler } from '../types';

export const weixin: SiteHandler = {
  name: 'weixin',
  matches: url => hostMatches(url, 'mp.weixin.qq.com'),
  preParse(document) {
    const content = document.querySelector('#js_content');
    if (!content) return;

    const author = document.querySelector('#js_author_name')?.textContent?.trim()
      || document.querySelector('#js_name')?.textContent?.trim();
    for (const img of content.querySelectorAll('img[data-src]')) {
      const src = validImageUrl(img.getAttribute('data-src')!, document.baseURI);
      if (src) {
        img.setAttribute('src', src);
        img.removeAttribute('srcset');
      }
    }
    document.querySelectorAll([
      '.rich_media_title', '.rich_media_meta_list', '#js_row_immersive_cover_img',
      'mp-common-profile', '.profile_container', '.profile_card_container',
      '#content_bottom_area', '.rich_media_area_extra', '#js_pc_qr_code',
    ].join(', ')).forEach(element => element.remove());

    return { author, suggested: 'article' };
  },
};
