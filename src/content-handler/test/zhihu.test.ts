import assert from 'node:assert/strict';
import test from 'node:test';
import { JSDOM } from 'jsdom';
import { extractPage } from '../src/index';
import { fixture } from './fixture';

const question = 'https://www.zhihu.com/question/633912016/answer/3325800164';
const column = 'https://zhuanlan.zhihu.com/p/639632102';

function originalImages(document: Document): (string | null)[] {
  return Array.from(document.querySelectorAll('img[data-original], img[data-actualsrc]'),
    img => img.getAttribute('data-original') ?? img.getAttribute('data-actualsrc'));
}

function savedImages(document: Document): (string | null)[] {
  return Array.from(document.querySelectorAll('img'), img => img.getAttribute('src'));
}

test('keeps the question description and each answer with its own author and images', () => {
  const { document } = fixture('zhihu', 'question', question).window;
  const { article } = extractPage(document, question);
  assert.equal(article?.title, '摄影提问');
  const readable = new JSDOM(article!.readableContent).window.document;
  for (const text of ['摄影提问', '如何安排照片中的线条？', '样本作者甲', '第一个回答', '样本作者乙', '第二个回答']) {
    assert.ok(readable.body.textContent?.includes(text), text);
  }
  assert.doesNotMatch(readable.body.textContent!, /显示全部|相关推荐/);
  assert.deepEqual(savedImages(readable), originalImages(document));
  assert.equal(readable.querySelector('noscript, img[src^="data:"]'), null);
});

test('uses the column title and promotes the original image before Readability', () => {
  const { document } = fixture('zhihu', 'zhuanlan', column).window;
  const { article } = extractPage(document, column);
  assert.equal(article?.title, '手机摄影');
  const readable = new JSDOM(article!.readableContent).window.document;
  assert.match(readable.body.textContent!, /街道中等待行人进入画面/);
  assert.deepEqual(savedImages(readable), originalImages(document));
  assert.equal(readable.querySelector('noscript, img[src^="data:"]'), null);
});

test('does not match a lookalike host', () => {
  const url = 'https://evilzhihu.com/question/633912016';
  const { document } = fixture('zhihu', 'question', url).window;
  assert.deepEqual(extractPage(document, url), extractPage(document, url, []));
});
