import assert from 'node:assert/strict';
import { before, test } from 'node:test';
import type { SaveArticleMessage, SaveArticleResponse } from '../src/types';

type Listener = (message: SaveArticleMessage, sender: chrome.runtime.MessageSender,
  respond: (response: SaveArticleResponse) => void) => boolean;
let listener: Listener;
let settings: { apiUrl: string; ak?: string };

before(async () => {
  globalThis.chrome = {
    runtime: { onMessage: { addListener: (callback: Listener) => { listener = callback; } } },
    storage: { sync: { get: async () => settings } },
  } as unknown as typeof chrome;
  await import('../src/background.ts');
});

const message: SaveArticleMessage = {
  action: 'saveArticle',
  content: {
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
    assert.deepEqual(JSON.parse(options?.body as string), message.content);
    return new Response('{}', { status: 201 });
  };

  const response = await new Promise<SaveArticleResponse>((resolve) => listener(message, {}, resolve));

  assert.equal(called, true);
  assert.deepEqual(response, { success: true });
});

test('missing API key produces a useful error without making a request', async () => {
  settings = { apiUrl: 'http://localhost:5001' };
  globalThis.fetch = async () => { throw new Error('Unexpected network request'); };

  const response = await new Promise<SaveArticleResponse>((resolve) => listener(message, {}, resolve));

  assert.equal(response.success, false);
  if (!response.success) assert.match(response.error, /API key/);
});

test('backend rejection is reported to the popup', async () => {
  settings = { apiUrl: 'http://localhost:5001', ak: 'test-api-key' };
  globalThis.fetch = async () => new Response('Invalid content', { status: 400 });

  const response = await new Promise<SaveArticleResponse>((resolve) => listener(message, {}, resolve));

  assert.deepEqual(response, { success: false, error: 'HTTP 400: Invalid content' });
});
