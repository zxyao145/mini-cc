import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { JSDOM } from 'jsdom';
import type { ArticlePage, LinkPage, PageChoices, SavePageMessage } from '../src/types';
import { withGlobals } from './with-globals.ts';

const link: LinkPage = { kind: 'link', url: 'https://example.com/video', title: 'Video' };
const article: ArticlePage = {
  kind: 'article', url: link.url, title: 'Transcript', originalContent: '<html></html>', readableContent: '<p>Text</p>',
};
const settle = () => new Promise<void>(resolve => setImmediate(resolve));

interface Popup {
  document: Document;
  button: (id: string) => HTMLButtonElement;
  saved: SavePageMessage[];
  clipboard: string[];
  messages: string[];
  closed: () => number;
}

async function withPopup(name: string, choices: PageChoices, run: (popup: Popup) => Promise<void>) {
  const html = readFileSync(new URL('../src/popup.html', import.meta.url), 'utf8');
  const dom = new JSDOM(html);
  const { document } = dom.window;
  const saved: SavePageMessage[] = [];
  const clipboard: string[] = [];
  const messages: string[] = [];
  let closed = 0;
  const closeWindow = dom.window.close;
  dom.window.close = () => { closed++; };
  const chromeMock = {
    storage: { sync: { get: async () => ({}) } },
    tabs: {
      query: async () => [{ id: 1, url: link.url }],
      sendMessage: async (_tab: number, { action }: { action: string }) => {
        messages.push(action);
        return action === 'extractContent' ? { success: true, content: choices } : { success: true, markdown: '# Video\n' };
      },
    },
    runtime: {
      sendMessage: async (message: SavePageMessage) => {
        saved.push(message);
        return { success: true };
      },
    },
  };
  const globals = {
    window: dom.window,
    document,
    chrome: chromeMock,
    navigator: { clipboard: { writeText: async (value: string) => { clipboard.push(value); } } },
  };

  await withGlobals(globals, async () => {
    if (document.readyState === 'loading') {
      await new Promise<void>(resolve => document.addEventListener('DOMContentLoaded', () => resolve(), { once: true }));
    }
    // A fresh module instance registers its DOMContentLoaded listener on this test's document.
    await import(`../src/popup.ts?${name}`);

    document.dispatchEvent(new dom.window.Event('DOMContentLoaded'));
    await settle();
    await run({
      document,
      button: id => document.querySelector<HTMLButtonElement>(`#${id}`)!,
      saved, clipboard, messages,
      closed: () => closed,
    });
  }).finally(() => closeWindow.call(dom.window));
}

test('copy and save stay independent in the popup', async () => {
  await withPopup('independent', { suggested: 'link', article: null, link }, async ({ document, button, saved, clipboard, messages, closed }) => {
    const save = button('save-link-btn');
    const scheduled: Array<() => void> = [];

    button('copy-md-btn').click();
    await settle();
    assert.deepEqual(clipboard, ['# Video\n']);
    assert.deepEqual(saved, []);
    assert.equal(save.disabled, false);
    assert.equal(document.querySelector('#status')?.textContent, 'Markdown copied');
    assert.equal(closed(), 0);

    await withGlobals({
      setTimeout: (callback: () => void, delay: number) => {
        assert.equal(delay, 2000);
        scheduled.push(callback);
      },
    }, async () => {
      save.click();
      await settle();
    });
    assert.deepEqual(saved, [{ action: 'savePage', content: link }]);
    assert.deepEqual(clipboard, ['# Video\n']);
    assert.equal(document.querySelector('#status')?.textContent, 'Link saved successfully!');
    assert.deepEqual(messages, ['extractContent', 'extractMarkdown']);
    assert.equal(scheduled.length, 1);
    scheduled[0]();
    assert.equal(closed(), 1);
  });
});

test('highlights the suggested link and disables the article without readable content', async () => {
  await withPopup('link-only', { suggested: 'link', article: null, link }, async ({ document, button }) => {
    assert.ok(button('save-link-btn').classList.contains('btn-primary'));
    assert.equal(button('save-link-btn').disabled, false);
    assert.ok(button('save-article-btn').classList.contains('btn-secondary'));
    assert.equal(button('save-article-btn').disabled, true);
    assert.equal(document.querySelector('#preview-title')?.textContent, 'Video');
  });
});

test('lets the user save an article when the link is suggested', async () => {
  await withPopup('override', { suggested: 'link', article, link }, async ({ document, button, saved }) => {
    const save = button('save-article-btn');
    assert.ok(button('save-link-btn').classList.contains('btn-primary'));
    assert.ok(save.classList.contains('btn-secondary'));
    assert.equal(save.disabled, false);

    await withGlobals({ setTimeout: () => {} }, async () => {
      save.click();
      await settle();
    });
    assert.deepEqual(saved, [{ action: 'savePage', content: article }]);
    assert.equal(document.querySelector('#status')?.textContent, 'Article saved successfully!');
    assert.equal(button('save-link-btn').disabled, true);
  });
});

test('highlights the suggested article and previews its metadata', async () => {
  await withPopup('article', { suggested: 'article', article, link }, async ({ document, button }) => {
    assert.ok(button('save-article-btn').classList.contains('btn-primary'));
    assert.ok(button('save-link-btn').classList.contains('btn-secondary'));
    assert.equal(button('save-link-btn').disabled, false);
    assert.equal(document.querySelector('#preview-title')?.textContent, 'Transcript');
  });
});
