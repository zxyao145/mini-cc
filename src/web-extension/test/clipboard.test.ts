import assert from 'node:assert/strict';
import { test } from 'node:test';
import { JSDOM } from 'jsdom';
import { copyText } from '../src/clipboard.ts';
import { withGlobals } from './with-globals.ts';

async function withClipboard(clipboard: Pick<Clipboard, 'writeText'>, run: (document: Document) => Promise<void>) {
  const dom = new JSDOM('<body></body>');
  const globals = { document: dom.window.document, navigator: { clipboard } };
  try {
    await withGlobals(globals, () => run(dom.window.document));
  } finally {
    dom.window.close();
  }
}

const unavailable = { writeText: async () => { throw new Error('clipboard unavailable'); } };

test('writes through the clipboard API without adding a textarea', async () => {
  const calls: string[] = [];
  await withClipboard({ writeText: async value => { calls.push(value); } }, async document => {
    await copyText('markdown');
    assert.deepEqual(calls, ['markdown']);
    assert.equal(document.querySelector('textarea'), null);
  });
});

test('falls back to execCommand when clipboard API writing is rejected', async () => {
  await withClipboard(unavailable, async document => {
    let copiedText: string | undefined;
    document.execCommand = command => {
      copiedText = document.querySelector('textarea')?.value;
      return command === 'copy';
    };

    await copyText('markdown');

    assert.equal(copiedText, 'markdown');
    assert.equal(document.querySelector('textarea'), null);
  });
});

for (const failure of ['returns false', 'throws']) {
  test(`reports when execCommand ${failure}`, async () => {
    await withClipboard(unavailable, async document => {
      document.execCommand = () => {
        if (failure === 'throws') throw new Error('clipboard unavailable');
        return false;
      };

      await assert.rejects(copyText('markdown'), { message: 'Clipboard write was blocked.' });
      assert.equal(document.querySelector('textarea'), null);
    });
  });
}
