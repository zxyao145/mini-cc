import assert from 'node:assert/strict';
import test from 'node:test';
import { JSDOM } from 'jsdom';
import { extractPage } from '../src/extract-page.ts';
import { pageTitle } from '../src/page-meta.ts';
import { parseReadable } from '../src/readable.ts';

function textArticle(length = 650, metadata = '') {
  const paragraphs = `<article><h1>Readable title</h1><p>${'This article contains enough useful prose for the readability parser to identify its main body. '.repeat(Math.ceil(length / 95))}</p></article>`;
  return `<!doctype html><html><head><title>Document title</title>${metadata}</head><body>${paragraphs}</body></html>`;
}

function doc(html: string, url = 'https://example.com/posts/story') {
  return new JSDOM(html, { url });
}

function linkFrom(document: Document, loadedUrl = document.URL) {
  return extractPage(document, loadedUrl).link;
}

test('extracts link metadata from trusted page meta tags', () => {
  const dom = doc(`<!doctype html><html><head><title>Document title</title>
    <meta property="og:title" content="Open Graph title">
    <meta property="og:description" content="Summary text">
    <meta name="author" content="By Jane">
    <meta property="og:image" content="/cover.png">
  </head><body></body></html>`);

  assert.deepEqual(linkFrom(dom.window.document), {
    kind: 'link', url: 'https://example.com/posts/story', title: 'Open Graph title',
    summary: 'Summary text', author: 'By Jane', imageUrl: 'https://example.com/cover.png',
  });
  dom.window.close();
});

test('uses trusted title fallbacks and the host when the page has no title', () => {
  const twitter = doc('<title>Document</title><meta name="twitter:title" content="Twitter title">');
  assert.equal(pageTitle(twitter.window.document, true), 'Twitter title');
  twitter.window.close();

  const documentTitle = doc('<title>Document title</title>');
  assert.equal(pageTitle(documentTitle.window.document, true), 'Document title');
  documentTitle.window.close();

  const host = doc('<title></title>', 'https://example.com/path');
  assert.equal(pageTitle(host.window.document, true), 'example.com');
  host.window.close();
});

test('trims and truncates link metadata to API field limits', () => {
  const dom = doc(`<meta property="og:title" content=" ${'t'.repeat(510)} ">
    <meta property="og:description" content="${'s'.repeat(1010)}">
    <meta name="author" content="${'a'.repeat(210)}">`);
  const link = linkFrom(dom.window.document);

  assert.equal(link.title.length, 500);
  assert.equal(link.summary?.length, 1000);
  assert.equal(link.author?.length, 200);
  dom.window.close();
});

test('ignores page metadata after an in-page navigation', () => {
  const dom = doc(`<title>Current document title</title>
    <meta property="og:title" content="Stale title">
    <meta property="og:description" content="Stale summary">
    <meta name="author" content="Stale author">
    <meta property="og:image" content="https://example.com/stale.png">`);
  const link = linkFrom(dom.window.document, 'https://example.com/old');

  assert.equal(link.title, 'Current document title');
  assert.equal(link.summary, '');
  assert.equal(link.author, '');
  assert.equal(link.imageUrl, '');
  dom.window.close();
});

test('uses the YouTube thumbnail fallback while keeping the exact URL', () => {
  const url = 'https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PL1';
  const dom = doc('<title>Video title</title><meta property="og:title" content="Stale title">', url);
  const link = linkFrom(dom.window.document, 'https://example.com/old');

  assert.equal(link.url, url);
  assert.equal(link.title, 'Video title');
  assert.equal(link.imageUrl, 'https://i.ytimg.com/vi/dQw4w9WgXcQ/hqdefault.jpg');
  assert.equal(link.summary, '');
  dom.window.close();
});

test('uses trusted Open Graph YouTube image before the generated thumbnail', () => {
  const dom = doc('<meta property="og:image" content="https://example.com/cover.png">', 'https://www.youtube.com/watch?v=dQw4w9WgXcQ');
  assert.equal(linkFrom(dom.window.document).imageUrl, 'https://example.com/cover.png');
  dom.window.close();

  const noImage = doc('<title>Video title</title>', 'https://www.youtube.com/watch?v=dQw4w9WgXcQ');
  assert.equal(linkFrom(noImage.window.document).imageUrl, 'https://i.ytimg.com/vi/dQw4w9WgXcQ/hqdefault.jpg');
  noImage.window.close();
});

test('uses the same first cover meta tag for links and articles', () => {
  const metadata = '<meta name="twitter:image" content="/twitter.png"><meta property="og:image" content="/og.png">';
  const article = doc(textArticle(650, metadata));
  const link = doc('<title>Page</title>' + metadata);
  try {
    const articleChoices = extractPage(article.window.document, article.window.document.URL);
    const linkChoices = extractPage(link.window.document, link.window.document.URL);
    assert.equal(articleChoices.suggested, 'article');
    assert.equal(linkChoices.suggested, 'link');
    const linkPage = linkChoices.link;
    assert.equal(linkPage.imageUrl, articleChoices.article?.imageUrl);
    assert.equal(linkPage.imageUrl, 'https://example.com/twitter.png');
  } finally {
    article.window.close();
    link.window.close();
  }
});

test('link extraction contains no article body fields', () => {
  const dom = doc('<title>Video</title>');
  const link = linkFrom(dom.window.document);

  assert.equal('originalContent' in link, false);
  assert.equal('readableContent' in link, false);
  dom.window.close();
});

test('rejects unsupported or overlong page URLs before creating a link', () => {
  const file = doc('<title>Local file</title>', 'file:///tmp/a.html');
  assert.throws(() => extractPage(file.window.document, file.window.document.URL), /http and https/i);
  file.window.close();

  const long = doc('<title>Long URL</title>', `https://example.com/${'x'.repeat(2000)}`);
  assert.throws(() => extractPage(long.window.document, long.window.document.URL), /too long/i);
  long.window.close();
});

test('rejects a readable local page before choosing the article save endpoint', () => {
  const local = doc(textArticle(), 'file:///tmp/article.html');
  const readable = parseReadable(local.window.document);
  assert.ok(readable && readable.textLength >= 500);
  assert.throws(() => extractPage(local.window.document, local.window.document.URL), /http and https/i);
  local.window.close();
});

test('offers only the link choice when the page has no readable content', () => {
  const blank = doc('<title>Untitled page</title><body></body>');
  const choices = extractPage(blank.window.document, blank.window.document.URL);
  assert.equal(choices.suggested, 'link');
  assert.equal(choices.article, null);
  assert.equal(choices.link.title, 'Untitled page');
  blank.window.close();
});

test('offers both choices for a readable page and suggests the detected kind', () => {
  const article = doc(textArticle());
  const before = article.window.document.documentElement.outerHTML;
  const choices = extractPage(article.window.document, article.window.document.URL);

  assert.equal(choices.suggested, 'article');
  assert.equal(choices.article?.kind, 'article');
  assert.ok(choices.article && choices.article.readableContent.length > 0);
  assert.equal(choices.link.kind, 'link');
  assert.equal(choices.link.url, choices.article?.url);
  assert.equal(article.window.document.documentElement.outerHTML, before);
  article.window.close();

  const video = doc(textArticle(650, '<meta property="og:type" content="article">'), 'https://www.youtube.com/watch?v=dQw4w9WgXcQ');
  const readable = parseReadable(video.window.document);
  assert.ok(readable && readable.textLength >= 500);
  const videoChoices = extractPage(video.window.document, video.window.document.URL);
  assert.equal(videoChoices.suggested, 'link');
  assert.ok(videoChoices.article && videoChoices.article.readableContent.length > 0);
  video.window.close();
});

test('uses only document title and current URL after navigation for extracted links', () => {
  const dom = doc('<title>Current page</title><meta property="og:title" content="Previous page">', 'https://example.com/new');
  const { suggested, link } = extractPage(dom.window.document, 'https://example.com/old');

  assert.equal(suggested, 'link');
  assert.equal(link.url, 'https://example.com/new');
  assert.equal(link.title, 'Current page');
  dom.window.close();
});
