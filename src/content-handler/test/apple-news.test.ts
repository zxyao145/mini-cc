import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { appleNews } from '../src/handlers/apple-news';
import { resolveUrl } from '../src/url';

const url = 'https://apple.news/AxjzaZaPvSn23b67LhXI5EQ';
const html = readFileSync(new URL('./fixtures/apple-news/redirect.html', import.meta.url), 'utf8');
const publisherUrl = 'https://www.reuters.com/article/uk-health-coronavirus-global-casualties-idUSKBN2GS034';

test('Apple News resolves the click-here link using a desktop browser User-Agent', async () => {
  const result = await resolveUrl(url, {
    fetch: async (input, init) => {
      assert.equal(String(input), url);
      assert.match(new Headers(init?.headers).get('User-Agent') ?? '', /Macintosh.+Chrome\//);
      assert.ok(init?.signal);
      return new Response(html);
    },
  }, { handlers: [appleNews] });
  assert.equal(result, publisherUrl);
});

test('Apple News preserves an escaped query string in the publisher address', async () => {
  const result = await resolveUrl(url, {
    fetch: async () => new Response(
      `<a href="https://example.com/decoy"><span data-class="click-here">Other link</span></a>
       <a data-href="/" href='https://publisher.example/?a=1&amp;b=2'><span class = "desktop click-here">Click here</span></a>`,
    ),
  }, { handlers: [appleNews] });
  assert.equal(result, 'https://publisher.example/?a=1&b=2');
});

test('Apple News keeps the input when the publisher link is missing, unsafe, or unavailable', async () => {
  const responses = [
    new Response('<a href="https://publisher.example/"><span class="tap-here">Tap here</span></a>'),
    new Response('<a href="javascript:alert(1)"><span class="click-here">Click here</span></a>'),
    new Response(html, { status: 404 }),
  ];
  for (const response of responses) {
    assert.equal(await resolveUrl(url, {
      fetch: async () => response,
    }, { handlers: [appleNews] }), url);
  }
});

test('Apple News matches only the exact hostname', () => {
  assert.equal(appleNews.matches(new URL(url)), true);
  assert.equal(appleNews.matches(new URL('https://www.apple.news/')), false);
  assert.equal(appleNews.matches(new URL('https://fakeapple.news/')), false);
});
