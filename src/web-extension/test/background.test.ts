import assert from 'node:assert/strict';
import { before, test } from 'node:test';
import type { SavePageMessage, SavePageResponse } from '../src/types';

type Listener = (message: SavePageMessage, sender: chrome.runtime.MessageSender,
  respond: (response: SavePageResponse) => void) => boolean;
let listener: Listener;
let settings: { apiUrl: string; ak?: string };

before(async () => {
  globalThis.chrome = {
    runtime: { onMessage: { addListener: (callback: Listener) => { listener = callback; } } },
    storage: { sync: { get: async () => settings } },
  } as unknown as typeof chrome;
  await import('../src/background.ts');
});

const message: SavePageMessage = {
  action: 'savePage',
  content: {
    kind: 'article',
    url: 'https://example.com/article', title: 'Article',
    originalContent: '<html>Original</html>', readableContent: '<p>Extracted</p>',
  },
};

test('background sends extracted HTML and authenticates using extension settings', async () => {
  settings = { apiUrl: 'http://localhost:5001/', ak: 'test-api-key' };
  let called = false;
  globalThis.fetch = async (url, options) => {
    called = true;
    assert.equal(url, 'http://localhost:5001/api/articles/content');
    assert.equal(options?.method, 'POST');
    assert.equal((options?.headers as Record<string, string>)['X-Api-Key'], 'test-api-key');
    const { kind: _kind, ...article } = message.content;
    assert.deepEqual(JSON.parse(options?.body as string), article);
    return new Response('{}', { status: 201 });
  };

  const response = await new Promise<SavePageResponse>((resolve) => listener(message, {}, resolve));

  assert.equal(called, true);
  assert.deepEqual(response, { success: true });
});

test('background posts link pages to the link endpoint without article bodies', async () => {
  const linkMessage: SavePageMessage = {
    action: 'savePage',
    content: {
      kind: 'link',
      url: 'https://www.youtube.com/watch?v=dQw4w9WgXcQ',
      title: 'Video',
      summary: 'S',
      author: '',
      imageUrl: '',
    },
  };
  settings = { apiUrl: 'http://localhost:5001/', ak: 'test-api-key' };
  let called = false;
  globalThis.fetch = async (url, options) => {
    called = true;
    assert.equal(url, 'http://localhost:5001/api/articles/link');
    assert.deepEqual(JSON.parse(options?.body as string), {
      url: linkMessage.content.url,
      title: 'Video',
      summary: 'S',
      author: '',
      imageUrl: '',
    });
    return new Response('{}', { status: 201 });
  };

  const response = await new Promise<SavePageResponse>((resolve) => listener(linkMessage, {}, resolve));

  assert.equal(called, true);
  assert.deepEqual(response, { success: true });
});

test('missing API key produces a useful error without making a request', async () => {
  settings = { apiUrl: 'http://localhost:5001' };
  globalThis.fetch = async () => { throw new Error('Unexpected network request'); };

  const response = await new Promise<SavePageResponse>((resolve) => listener(message, {}, resolve));

  assert.equal(response.success, false);
  if (!response.success) assert.match(response.error, /API key/);
});

test('backend rejection is reported to the popup', async () => {
  settings = { apiUrl: 'http://localhost:5001', ak: 'test-api-key' };
  globalThis.fetch = async () => new Response('Invalid content', { status: 400 });

  const response = await new Promise<SavePageResponse>((resolve) => listener(message, {}, resolve));

  assert.deepEqual(response, { success: false, error: 'HTTP 400: Invalid content' });
});

test('reports a link-specific fallback when saving rejects without an Error', async () => {
  settings = { apiUrl: 'http://localhost:5001', ak: 'test-api-key' };
  globalThis.fetch = async () => Promise.reject('Network unavailable');
  const linkMessage: SavePageMessage = {
    action: 'savePage',
    content: { kind: 'link', url: 'https://example.com/video', title: 'Video' },
  };

  const response = await new Promise<SavePageResponse>((resolve) => listener(linkMessage, {}, resolve));

  assert.deepEqual(response, { success: false, error: 'Failed to save link' });
});
