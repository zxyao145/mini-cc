import assert from 'node:assert/strict';
import test from 'node:test';
import { JSDOM } from 'jsdom';
import { ARTICLE_LIMITS, buildMarkdown, extractPage, type SiteHandler } from '../src/index.ts';
import * as pageMeta from '../src/page-meta.ts';

const url = 'https://example.com/posts/current';
const loadedUrl = 'https://example.com/posts/previous';
const date = new Date(2026, 10, 14);
const prose = 'The current story contains enough meaningful prose for the extraction to recover the article. ';
const textArticle = '<article><p>' + prose.repeat(10) + '</p></article>';

function doc(body = textArticle): Document {
  return new JSDOM('<title>Current story</title><meta property="og:title" content="Stale title">' + body, { url }).window.document;
}

function handler(preParse: NonNullable<SiteHandler['preParse']>): SiteHandler {
  return { name: 'test', matches: () => true, preParse };
}

test('a throwing preParse falls back to a clean clone', () => {
  const broken = handler(document => {
    document.body.replaceChildren();
    throw new Error('rewrite failed');
  });

  assert.deepEqual(extractPage(doc(), loadedUrl, [broken]), extractPage(doc(), loadedUrl, []));
  assert.equal(buildMarkdown(doc(), loadedUrl, date, [broken]), buildMarkdown(doc(), loadedUrl, date, []));
});

test('preParse never mutates the live document or the saved original HTML', () => {
  const document = doc();
  const original = document.documentElement.outerHTML;
  const rewritten = handler(clone => {
    clone.body.innerHTML = '<article><p>Rewritten content.</p></article>';
  });
  const choices = extractPage(document, loadedUrl, [rewritten]);

  assert.match(choices.article?.readableContent ?? '', /Rewritten content\./);
  assert.equal(choices.article?.originalContent, original);
  assert.equal(document.documentElement.outerHTML, original);
});

test('preParse retries a clean clone when the rewritten body yields no readable content', () => {
  const empty = handler(document => {
    document.body.replaceChildren();
    return { title: 'Unusable rewrite' };
  });

  assert.deepEqual(extractPage(doc(), loadedUrl, [empty]), extractPage(doc(), loadedUrl, []));
  assert.equal(buildMarkdown(doc(), loadedUrl, date, [empty]), buildMarkdown(doc(), loadedUrl, date, []));
});

test('current DOM overrides supersede untrusted metadata in article, link and Markdown', () => {
  const overrides = { title: 'Handler title', author: 'Handler author', summary: 'Handler summary', imageUrl: '/cover.png' };
  const handlers = [handler(() => overrides), handler(() => ({ title: 'Second handler' }))];
  const choices = extractPage(doc(), loadedUrl, handlers);
  assert.ok(choices.article);

  for (const page of [choices.link, choices.article]) {
    assert.equal(page.title, overrides.title);
    assert.equal(page.author, overrides.author);
    assert.equal(page.summary, overrides.summary);
    assert.equal(page.imageUrl, 'https://example.com/cover.png');
  }

  const markdown = buildMarkdown(doc(), loadedUrl, date, handlers);
  assert.match(markdown, /title: "Handler title"/);
  assert.match(markdown, /author: "Handler author"/);
  assert.match(markdown, /^# Handler title$/m);
});

test('blank override strings are ignored and padded ones are trimmed', () => {
  const choices = extractPage(doc(), loadedUrl, [handler(() => ({ title: '   ', author: '  A \n' }))]);
  assert.equal(choices.link.title, 'Current story');
  assert.equal(choices.link.author, 'A');
});

test('limits override lengths and ignores invalid image URLs', () => {
  const overrides = {
    title: 't'.repeat(ARTICLE_LIMITS.title + 1),
    author: 'a'.repeat(ARTICLE_LIMITS.author + 1),
    summary: 's'.repeat(ARTICLE_LIMITS.summary + 1),
    imageUrl: 'javascript:1',
  };
  const choices = extractPage(doc(), loadedUrl, [handler(() => overrides)]);
  assert.equal(choices.link.title.length, ARTICLE_LIMITS.title);
  assert.equal(choices.link.author?.length, ARTICLE_LIMITS.author);
  assert.equal(choices.link.summary?.length, ARTICLE_LIMITS.summary);
  assert.equal(choices.link.imageUrl, '');
  const overlongImage = '/' + 'x'.repeat(ARTICLE_LIMITS.url);
  assert.equal(extractPage(doc(), loadedUrl, [handler(() => ({ imageUrl: overlongImage }))]).link.imageUrl, '');
});

test('a suggested kind override requires readable content', () => {
  const articleHandler = handler(() => ({ suggested: 'article' }));
  const short = '<article><p>Short story.</p></article>';
  assert.equal(extractPage(doc(short), loadedUrl, [articleHandler]).suggested, 'article');
  assert.match(buildMarkdown(doc(short), loadedUrl, date, [articleHandler]), /^# Current story$/m);
  assert.equal(extractPage(doc(''), loadedUrl, [articleHandler]).suggested, 'link');
  assert.equal(extractPage(doc(''), loadedUrl, [articleHandler]).article, null);

  const linkHandler = handler(() => ({ suggested: 'link', title: 'Link title' }));
  assert.equal(extractPage(doc(), loadedUrl, [linkHandler]).suggested, 'link');
  assert.match(buildMarkdown(doc(), loadedUrl, date, [linkHandler]), /\[Link title\]\(/);
});

test('hostMatches requires an exact domain or a subdomain', () => {
  assert.equal(pageMeta.hostMatches(new URL('https://www.zhihu.com/q'), 'zhihu.com'), true);
  assert.equal(pageMeta.hostMatches(new URL('https://zhihu.com/q'), 'zhihu.com'), true);
  assert.equal(pageMeta.hostMatches(new URL('https://evilzhihu.com/q'), 'zhihu.com'), false);
  assert.equal(pageMeta.hostMatches(new URL('https://zhihu.com.evil.example/q'), 'zhihu.com'), false);
});

test('a throwing domain match leaves extraction and Markdown on the default path', () => {
  const broken = { ...handler(() => undefined), matches: () => { throw new Error('match failed'); } };
  assert.deepEqual(extractPage(doc(), loadedUrl, [broken]), extractPage(doc(), loadedUrl, []));
  assert.equal(buildMarkdown(doc(), loadedUrl, date, [broken]), buildMarkdown(doc(), loadedUrl, date, []));
});
