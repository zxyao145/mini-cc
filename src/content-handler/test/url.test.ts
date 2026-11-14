import assert from 'node:assert/strict';
import test from 'node:test';
import { mergeOverrides, preHandleUrl, resolveUrl } from '../src/url';
import { ARTICLE_LIMITS, type FetchContext, type PageChoices, type SiteHandler } from '../src/types';

const url = 'https://example.com/short';
const resolved = 'https://example.com/full';
const ctx: FetchContext = { fetch: async () => { throw new Error('Unexpected fetch'); } };
const matcher = { name: 'test', matches: () => true };

test('resolveUrl picks the first matching handler with a resolve hook', async () => {
  const handlers: SiteHandler[] = [
    { ...matcher },
    { ...matcher, matches: () => false, resolve: async () => 'https://skipped.example/' },
    { ...matcher, resolve: async () => resolved },
    { ...matcher, resolve: async () => 'https://later.example/' },
  ];
  assert.equal(await resolveUrl(url, ctx, { handlers }), resolved);
});

test('resolveUrl returns the input when resolution fails or produces an unsupported URL', async () => {
  assert.equal(await resolveUrl(url, ctx, { handlers: [] }), url);
  for (const resolve of [async () => 'file:///tmp/article', async () => '/relative', async () => 'http:example.com/story', async () => { throw new Error('failed'); }]) {
    assert.equal(await resolveUrl(url, ctx, { handlers: [{ ...matcher, resolve }] }), url);
  }
});

test('preHandleUrl returns null on errors or when no handler matches', async () => {
  assert.equal(await preHandleUrl(url, ctx, { handlers: [] }), null);
  assert.equal(await preHandleUrl(url, ctx, {
    handlers: [{ ...matcher, preHandle: async () => { throw new Error('failed'); } }],
  }), null);
});

test('URL hooks skip unsupported and malformed input before invoking a handler', async () => {
  let matches = 0;
  const handlers: SiteHandler[] = [{
    ...matcher,
    matches: () => { matches++; return true; },
    resolve: async () => resolved,
    preHandle: async () => ({ title: 'Unexpected metadata' }),
  }];
  for (const input of ['ftp://example.com/short', '/short', 'https:example.com/short']) {
    assert.equal(await resolveUrl(input, ctx, { handlers }), input);
    assert.equal(await preHandleUrl(input, ctx, { handlers }), null);
  }
  assert.equal(matches, 0);
});

test('URL hooks time out and abort even when fetch ignores the signal', async () => {
  const signals: AbortSignal[] = [];
  const fetch: typeof globalThis.fetch = (_input, init) => {
    assert.ok(init?.signal);
    signals.push(init.signal);
    return new Promise(() => {});
  };
  const slow = async (url: URL, ctx: FetchContext): Promise<null> => {
    await ctx.fetch(url, { signal: ctx.signal });
    return null;
  };
  const options = { handlers: [{ ...matcher, resolve: slow, preHandle: slow }], timeoutMs: 20 };
  assert.deepEqual(await Promise.all([resolveUrl(url, { fetch }, options), preHandleUrl(url, { fetch }, options)]), [url, null]);
  assert.equal(signals.length, 2);
  assert.ok(signals.every(signal => signal.aborted));
  assert.notEqual(signals[0], signals[1]);
});

test('mergeOverrides updates both choices within field limits and keeps the suggested kind', () => {
  const choices: PageChoices = {
    suggested: 'link',
    link: { kind: 'link', url, title: 'Original title', imageUrl: 'https://example.com/fallback.png' },
    article: { kind: 'article', url, title: 'Original title', originalContent: '<html></html>', readableContent: '<p>Story</p>' },
  };
  const result = mergeOverrides(choices, { title: 't'.repeat(600), author: '  Author  ', imageUrl: 'file:///tmp/image', suggested: 'article' });

  assert.equal(result.suggested, 'link');
  assert.equal(result.link.title.length, ARTICLE_LIMITS.title);
  assert.equal(result.article?.title.length, ARTICLE_LIMITS.title);
  assert.equal(result.article?.author, 'Author');
  assert.equal(result.link.imageUrl, choices.link.imageUrl);
  assert.equal(result.article?.imageUrl, undefined);
  assert.equal(choices.link.title, 'Original title');
  assert.equal(mergeOverrides({ ...choices, article: null }, { imageUrl: '/new.png' }).link.imageUrl, 'https://example.com/new.png');
});
