import assert from 'node:assert/strict';
import test from 'node:test';
import { JSDOM } from 'jsdom';
import { extractPage } from '../src/extract-page';
import { preHandleUrl } from '../src/url';

const id = 'BnSUk0je6oo';
const playlist = 'PL6D4F6A0D6B0C6A5E';
const host = 'https://www.youtube.com';
const thumbnail = `https://i.ytimg.com/vi/${id}/hqdefault.jpg`;

for (const [url, canonical] of [
  [`${host}/watch?v=${id}&list=${playlist}`, `${host}/watch?v=${id}`],
  [`https://m.youtube.com/watch?v=${id}`, `${host}/watch?v=${id}`],
  [`https://youtu.be/${id}?t=116`, `${host}/watch?v=${id}`],
  [`${host}/shorts/${id}`, `${host}/shorts/${id}`],
  [`${host}/playlist?list=${playlist}`, `${host}/playlist?list=${playlist}`],
]) {
  test(`requests canonical YouTube oEmbed metadata for ${url}`, async () => {
    const calls: string[] = [];
    const fetch: typeof globalThis.fetch = async (input, init) => {
      calls.push(String(input));
      assert.ok(init?.signal);
      return Response.json({ title: 'Title', author_name: 'Channel', thumbnail_url: thumbnail });
    };
    assert.deepEqual(await preHandleUrl(url, { fetch }), { title: 'Title', author: 'Channel', imageUrl: thumbnail });
    assert.deepEqual(calls, [`${host}/oembed?format=json&url=${encodeURIComponent(canonical)}`]);
  });
}

test('ignores channels, invalid video IDs and lookalike hostnames without fetching', async () => {
  const fetch = async () => assert.fail('Unexpected oEmbed request');
  for (const url of [`${host}/@channel`, `${host}/watch?v=short`, `https://notyoutube.com/watch?v=${id}`]) {
    assert.equal(await preHandleUrl(url, { fetch }), null);
  }
});

test('returns null for unsuccessful YouTube oEmbed responses', async () => {
  assert.equal(await preHandleUrl(`${host}/watch?v=${id}`, {
    fetch: async () => new Response(null, { status: 404 }),
  }), null);
});

test('keeps the suggested kind as a link for readable YouTube playlists', () => {
  const document = new JSDOM(`<title>Playlist</title><article><p>${'A playlist description with substantial prose. '.repeat(30)}</p></article>`, {
    url: `${host}/playlist?list=${playlist}`,
  }).window.document;
  assert.equal(extractPage(document, document.URL).suggested, 'link');
});

test('does not classify a YouTube path embedded in another site as a media URL', () => {
  const document = new JSDOM(`<title>Story</title><article><p>${'A story with substantial prose. '.repeat(35)}</p></article>`, {
    url: `https://example.com?source=.youtube.com/watch?v=${id}`,
  }).window.document;
  assert.equal(extractPage(document, document.URL).suggested, 'article');
});
