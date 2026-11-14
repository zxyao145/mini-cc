import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { JSDOM } from 'jsdom';
import type { ArticlePage, LinkPage, PageChoices } from '@minicc/content-handler';
import type { SavePageMessage } from '../src/types';
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

async function withPopup(
  name: string,
  choices: PageChoices,
  run: (popup: Popup) => Promise<void>,
  fetch?: typeof globalThis.fetch,
) {
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
      query: async () => [{ id: 1, url: choices.link.url }],
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
    ...(fetch ? { fetch } : {}),
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

const youtubeLink: LinkPage = {
  kind: 'link',
  url: 'https://www.youtube.com/watch?v=BnSUk0je6oo',
  title: 'YouTube',
  imageUrl: 'https://i.ytimg.com/vi/BnSUk0je6oo/hqdefault.jpg',
};
const youtubeChoices: PageChoices = { suggested: 'link', article: null, link: youtubeLink };

test('merges YouTube oEmbed metadata into the preview and saved link', async () => {
  let respond!: (response: Response) => void;
  const fetch = () => new Promise<Response>(resolve => { respond = resolve; });

  await withPopup('youtube', youtubeChoices, async ({ document, button, saved }) => {
    assert.equal(document.querySelector('#status')?.textContent, 'Extracting content...');
    assert.equal(button('save-link-btn').disabled, true);
    const thumbnail = 'https://i.ytimg.com/vi/BnSUk0je6oo/oembed.jpg';
    respond(Response.json({ title: 'Real title', author_name: 'Channel', thumbnail_url: thumbnail }));
    await settle();
    assert.equal(document.querySelector('#preview-title')?.textContent, 'Real title');
    assert.equal(document.querySelector('#preview-author')?.textContent, 'Channel');

    await withGlobals({ setTimeout: () => {} }, async () => {
      button('save-link-btn').click();
      await settle();
    });
    assert.deepEqual(saved, [{ action: 'savePage', content: {
      ...youtubeLink, title: 'Real title', author: 'Channel', imageUrl: thumbnail,
    } }]);
  }, fetch);
});

test('keeps extracted metadata when YouTube oEmbed fails', async () => {
  let calls = 0;
  const fetch = async () => {
    calls++;
    throw new Error('Network unavailable');
  };
  await withPopup('youtube-failed', youtubeChoices, async ({ document, button, saved }) => {
    assert.equal(calls, 1);
    assert.equal(document.querySelector('#preview-title')?.textContent, 'YouTube');
    await withGlobals({ setTimeout: () => {} }, async () => {
      button('save-link-btn').click();
      await settle();
    });
    assert.deepEqual(saved, [{ action: 'savePage', content: youtubeLink }]);
  }, fetch);
});

test('previews extracted metadata after the five-second oEmbed timeout', async () => {
  const timers: Array<() => void> = [];
  let signal: AbortSignal | null | undefined;
  const fetch: typeof globalThis.fetch = (_input, init) => {
    signal = init?.signal;
    return new Promise(() => {});
  };

  await withGlobals({
    setTimeout: (callback: () => void, delay: number) => {
      assert.equal(delay, 5000);
      timers.push(callback);
    },
  }, async () => {
    await withPopup('youtube-timeout', youtubeChoices, async ({ document, button }) => {
      assert.equal(document.querySelector('#status')?.textContent, 'Extracting content...');
      assert.equal(timers.length, 1);
      timers[0]();
      await settle();
      assert.ok(signal?.aborted);
      assert.equal(document.querySelector('#preview-title')?.textContent, 'YouTube');
      assert.equal(button('save-link-btn').disabled, false);
    }, fetch);
  });
});

test('does not fetch metadata for sites without a URL hook', async () => {
  let calls = 0;
  const fetch = async () => {
    calls++;
    return Response.json({});
  };
  await withPopup('no-url-hook', { suggested: 'link', article: null, link }, async ({ document }) => {
    assert.equal(calls, 0);
    assert.equal(document.querySelector('#preview-title')?.textContent, 'Video');
  }, fetch);
});
