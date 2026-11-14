import type { SiteHandler } from '../types';
import { appleNews } from './apple-news';
import { github } from './github';
import { medium } from './medium';
import { stackOverflow } from './stack-overflow';
import { weixin } from './weixin';
import { wikipedia } from './wikipedia';
import { xiaohongshu } from './xiaohongshu';
import { youtube } from './youtube';
import { zhihu } from './zhihu';

export const SITE_HANDLERS: readonly SiteHandler[] = [
  xiaohongshu, weixin, wikipedia, youtube, zhihu, medium, github, appleNews, stackOverflow,
];
