import assert from 'node:assert/strict';
import test from 'node:test';
import { JSDOM } from 'jsdom';
import { extractPage } from '../src/extract-page.ts';

function page(extra = '') {
  const paragraphs = Array.from({ length: 12 }, () =>
    '<p>Readable article content explains browser-side extraction, article management, and local storage. This paragraph provides enough real prose for Mozilla Readability to identify the main article. 浏览器正文提取测试。</p>'
  ).join('');
  return new JSDOM(`<!doctype html><html><head><title>Browser extraction</title>
    <meta name="author" content="Test author"><meta name="description" content="Article summary">
    <meta property="og:image" content="/cover.png"></head><body>
    <nav><a href="/menu">Menu only text</a></nav><article><h1>Browser extraction</h1>
    ${paragraphs}<img src="/photo.png"><a href="../more">Read more</a>${extra}
    </article></body></html>`, { url: 'https://example.com/posts/story' });
}

function articleFrom(document: Document) {
  const { suggested, article } = extractPage(document, document.URL);
  assert.equal(suggested, 'article');
  assert.ok(article);
  return article;
}

test('extracts article metadata and readable HTML with absolute resource URLs', () => {
  const dom = page();
  const article = articleFrom(dom.window.document);

  assert.equal(article.url, 'https://example.com/posts/story');
  assert.equal(article.title, 'Browser extraction');
  assert.equal(article.author, 'Test author');
  assert.equal(article.summary, 'Article summary');
  assert.equal(article.imageUrl, 'https://example.com/cover.png');
  assert.match(article.readableContent, /Readable article content/);
  assert.match(article.readableContent, /src="https:\/\/example.com\/photo.png"/);
  assert.match(article.readableContent, /href="https:\/\/example.com\/more"/);
  assert.doesNotMatch(article.readableContent, /Menu only text/);
  dom.window.close();
});

test('leaves the source page unchanged and preserves original HTML', () => {
  const dom = page();
  const before = dom.window.document.documentElement.outerHTML;

  const article = articleFrom(dom.window.document);

  assert.equal(dom.window.document.documentElement.outerHTML, before);
  assert.equal(article.originalContent, before);
  dom.window.close();
});

test('sanitizes executable HTML from the extracted article', () => {
  const dom = page('<img src="/bad.png" onerror="alert(1)"><a href="javascript:alert(1)">unsafe</a><script>alert(1)</script>');

  const article = articleFrom(dom.window.document);

  assert.doesNotMatch(article.readableContent, /onerror|javascript:|<script/i);
  dom.window.close();
});

test('uses the first cover meta tag in document order', () => {
  for (const [first, expected] of [
    ['/twitter.png', 'https://example.com/twitter.png'],
    ['javascript:alert(1)', ''],
  ]) {
    const dom = new JSDOM(`<title>Cover</title>
      <meta name="twitter:image" content="${first}"><meta property="og:image" content="/og.png">
      <article><p>${'An article about choosing a cover in the original order. '.repeat(16)}</p></article>`, { url: 'https://example.com/post' });
    try {
      assert.equal(articleFrom(dom.window.document).imageUrl, expected);
    } finally {
      dom.window.close();
    }
  }
});
