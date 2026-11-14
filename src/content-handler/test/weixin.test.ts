import assert from 'node:assert/strict';
import test from 'node:test';
import { JSDOM } from 'jsdom';
import { extractPage } from '../src/index';
import { fixture } from './fixture';

const url = 'https://mp.weixin.qq.com/s/lim3WLUdhrg3bnEJjcIEcQ';

// The mmbiz.qpic.cn sample returned a JPEG through the configured image proxy without a Referer.
test('restores lazy images and omits the WeChat article chrome', () => {
  const { document } = fixture('weixin', 'article', url).window;
  const images = Array.from(document.querySelectorAll('img[data-src]'), img => img.getAttribute('data-src'));
  const result = extractPage(document, url);
  assert.equal(result.suggested, 'article');
  assert.equal(result.article?.author, '中国地震局');
  const { body } = new JSDOM(result.article!.readableContent).window.document;
  assert.deepEqual(Array.from(body.querySelectorAll('img'), img => img.getAttribute('src')), images);
  assert.doesNotMatch(body.textContent!, /账号资料卡|文末操作栏|相关推荐|二维码区域|阅读日期/);
  assert.match(body.textContent!, /火山监测工作/);
});

test('prefers the byline, and resolves protocol-relative lazy URLs', () => {
  const { document } = fixture('weixin', 'article', url).window;
  document.querySelector('.rich_media_meta_list')!.insertAdjacentHTML('afterbegin', '<span id="js_author_name">署名作者</span>');
  const img = document.querySelector('img[data-src]')!;
  const original = img.getAttribute('data-src')!;
  img.setAttribute('data-src', original.replace(/^https:/, ''));
  const { article } = extractPage(document, url);
  assert.equal(article?.author, '署名作者');
  assert.equal(extractPage(document, url).link.author, '署名作者');
  assert.equal(new JSDOM(article!.readableContent).window.document.querySelector('img')?.src, original);
});

test('does not match similarly named hosts', () => {
  const url = 'https://mp.weixin.qq.com.example.com/s/example';
  const { document } = fixture('weixin', 'article', url).window;
  assert.deepEqual(extractPage(document, url), extractPage(document, url, []));
});
