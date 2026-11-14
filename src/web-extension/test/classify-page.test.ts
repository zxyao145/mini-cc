import assert from 'node:assert/strict';
import test from 'node:test';
import { JSDOM } from 'jsdom';
import { isArticlePage } from '../src/classify-page.ts';
import { parseReadable } from '../src/readable.ts';

function page(options: { url?: string; metadata?: string; textLength?: number } = {}) {
  const paragraphs = options.textLength === undefined ? '' : `<article><h1>Story</h1><p>${'Readable article words and details. '.repeat(Math.ceil(options.textLength / 35))}</p></article>`;
  const dom = new JSDOM(`<!doctype html><html><head>${options.metadata ?? ''}</head><body>${paragraphs}</body></html>`, {
    url: options.url ?? 'https://example.com/story',
  });
  const readable = parseReadable(dom.window.document);
  return { dom, readable };
}

test('known media URLs take precedence over article metadata and long readable text', () => {
  const { dom, readable } = page({
    url: 'https://www.youtube.com/watch?v=dQw4w9WgXcQ',
    metadata: '<meta property="og:type" content="article">',
    textLength: 650,
  });
  assert.ok(readable && readable.textLength >= 500);
  assert.equal(isArticlePage(dom.window.document, readable, true), false);
  dom.window.close();
});

test('media og:type takes precedence over long readable text', () => {
  const { dom, readable } = page({ metadata: '<meta property="og:type" content="video.other">', textLength: 650 });
  assert.ok(readable && readable.textLength >= 500);
  assert.equal(isArticlePage(dom.window.document, readable, true), false);
  dom.window.close();
});

test('article metadata signals classify medium readable text as an article', () => {
  for (const metadata of [
    '<meta property="og:type" content="article">',
    `<script type="application/ld+json">${JSON.stringify({ '@type': 'BlogPosting', headline: 'Story' })}</script>`,
  ]) {
    const { dom, readable } = page({ metadata, textLength: 300 });
    assert.ok(readable && readable.textLength >= 200 && readable.textLength < 500);
    assert.equal(isArticlePage(dom.window.document, readable, true), true);
    dom.window.close();
  }
});

test('article metadata does not override text shorter than the signal threshold', () => {
  const { dom, readable } = page({ metadata: '<meta property="og:type" content="article">', textLength: 150 });
  assert.ok(readable && readable.textLength < 200);
  assert.equal(isArticlePage(dom.window.document, readable, true), false);
  dom.window.close();
});

test('unmarked medium text is a link and unmarked long text is an article', () => {
  const medium = page({ textLength: 300 });
  assert.ok(medium.readable && medium.readable.textLength >= 200 && medium.readable.textLength < 500);
  assert.equal(isArticlePage(medium.dom.window.document, medium.readable, true), false);
  medium.dom.window.close();

  const long = page({ textLength: 650 });
  assert.ok(long.readable && long.readable.textLength >= 500);
  assert.equal(isArticlePage(long.dom.window.document, long.readable, true), true);
  long.dom.window.close();
});

test('untrusted metadata signals are ignored but media URL patterns still apply', () => {
  const article = page({ metadata: '<meta property="og:type" content="article">', textLength: 300 });
  assert.ok(article.readable && article.readable.textLength >= 200 && article.readable.textLength < 500);
  assert.equal(isArticlePage(article.dom.window.document, article.readable, false), false);
  article.dom.window.close();

  const video = page({ metadata: '<meta property="og:type" content="video.other">', textLength: 650 });
  assert.ok(video.readable && video.readable.textLength >= 500);
  assert.equal(isArticlePage(video.dom.window.document, video.readable, false), true);
  video.dom.window.close();

  const bilibili = page({ url: 'https://www.bilibili.com/video/BV1xx', textLength: 650 });
  assert.ok(bilibili.readable && bilibili.readable.textLength >= 500);
  assert.equal(isArticlePage(bilibili.dom.window.document, bilibili.readable, false), false);
  bilibili.dom.window.close();
});
