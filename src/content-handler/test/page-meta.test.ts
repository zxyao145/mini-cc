import assert from 'node:assert/strict';
import test from 'node:test';
import { JSDOM } from 'jsdom';
import { extractImageUrl, extractPageTags, isMetaTrusted, jsonLdArticles, youtubeVideoId } from '../src/page-meta.ts';

test('trusts metadata only when loaded and current URLs differ by a hash', () => {
  assert.equal(isMetaTrusted('https://a.com/x?v=1', 'https://a.com/x?v=1#t=3'), true);
  assert.equal(isMetaTrusted('https://a.com/x?v=1', 'https://a.com/x?v=2'), false);
  assert.equal(isMetaTrusted('https://a.com/x', 'https://a.com/y'), false);
});

test('extracts YouTube video IDs from watch and shorts URLs only', () => {
  assert.equal(youtubeVideoId(new URL('https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PL1')), 'dQw4w9WgXcQ');
  assert.equal(youtubeVideoId(new URL('https://m.youtube.com/shorts/abcdefghijk')), 'abcdefghijk');
  assert.equal(youtubeVideoId(new URL('https://www.youtube.com/feed/subscriptions')), null);
  assert.equal(youtubeVideoId(new URL('https://example.com/watch?v=dQw4w9WgXcQ')), null);
  assert.equal(youtubeVideoId(new URL('https://youtu.be/dQw4w9WgXcQ')), null);
});

test('resolves safe image metadata against the document URL', () => {
  const dom = new JSDOM('<meta property="og:image" content="/cover.png">', { url: 'https://example.com/posts/story' });
  assert.equal(extractImageUrl(dom.window.document), 'https://example.com/cover.png');
  dom.window.close();
});

test('ignores unsafe or oversized image metadata', () => {
  for (const image of ['data:image/png;base64,AAAA', 'javascript:alert(1)', `https://example.com/${'x'.repeat(2000)}`]) {
    const dom = new JSDOM(`<meta property="og:image" content="${image}">`, { url: 'https://example.com/' });
    assert.equal(extractImageUrl(dom.window.document), '');
    dom.window.close();
  }
});

test('finds article JSON-LD objects inside arrays and graphs while ignoring malformed or unrelated data', () => {
  const dom = new JSDOM(`<!doctype html><html><head>
    <script type="application/ld+json">{not json</script>
    <script type="application/ld+json">${JSON.stringify([
      { '@type': 'WebSite', name: 'site' },
      { '@type': ['Thing', 'BlogPosting'], headline: 'post' },
    ])}</script>
    <script type="application/ld+json">${JSON.stringify({ '@graph': [
      { '@type': 'Article', headline: 'article' },
      { '@type': 'WebSite', name: 'another site' },
    ] })}</script>
  </head><body></body></html>`);

  assert.deepEqual(jsonLdArticles(dom.window.document).map(item => item.headline), ['post', 'article']);
  dom.window.close();
});

test('merges page tags in source order and deduplicates case-insensitively', () => {
  const dom = new JSDOM(`<!doctype html><html><head>
    <meta property="article:tag" content="React">
    <meta property="article:tag" content="Web Dev">
    <meta name="keywords" content="react, CSS；前端;  ">
    <script type="application/ld+json">${JSON.stringify({ '@type': 'BlogPosting', keywords: ['css', 'Hooks'] })}</script>
  </head></html>`);

  assert.deepEqual(extractPageTags(dom.window.document), ['React', 'Web Dev', 'CSS', '前端', 'Hooks']);
  dom.window.close();
});

test('splits JSON-LD keyword strings and drops tags longer than fifty characters', () => {
  const dom = new JSDOM(`<!doctype html><html><head>
    <meta property="article:tag" content="${'x'.repeat(51)}">
    <meta property="article:tag" content="first">
    <script type="application/ld+json">${JSON.stringify({ '@type': 'Article', keywords: 'a, b；c; d' })}</script>
  </head></html>`);

  const extracted = extractPageTags(dom.window.document);
  assert.equal(extracted.includes('x'.repeat(51)), false);
  assert.deepEqual(extracted, ['first', 'a', 'b', 'c', 'd']);
  dom.window.close();
});

test('returns at most twenty unique page tags', () => {
  const tags = Array.from({ length: 25 }, (_, index) => `tag${index}`);
  const dom = new JSDOM(`<html><head><meta name="keywords" content="${tags.join(',')}"></head></html>`);

  const extracted = extractPageTags(dom.window.document);
  assert.equal(extracted.length, 20);
  assert.deepEqual(extracted[0], 'tag0');
  assert.deepEqual(extracted[19], 'tag19');
  dom.window.close();
});

test('returns no tags when no supported tag metadata exists', () => {
  const dom = new JSDOM('<html><head><meta name="description" content="summary"></head></html>');
  assert.deepEqual(extractPageTags(dom.window.document), []);
  dom.window.close();
});
