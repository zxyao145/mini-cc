import assert from 'node:assert/strict';
import { test } from 'node:test';
import { JSDOM } from 'jsdom';
import { extractPage } from '../src/extract-page';
import { wikipedia } from '../src/handlers/wikipedia';
import { buildMarkdown } from '../src/markdown';
import { fixture } from './fixture';

for (const [language, url, title, infoboxValue] of [
  ['en', 'https://en.wikipedia.org/wiki/Grace_Hopper', 'Grace Hopper', 'Presidential Medal of Freedom'],
  ['zh', 'https://zh.wikipedia.org/zh-cn/%E8%89%BE%E4%BC%A6%C2%B7%E5%9B%BE%E7%81%B5', '艾伦·图灵', '沃金火葬场'],
]) {
  test(`Wikipedia ${language} preserves the infobox and strips editing links and footnote markers`, () => {
    const { document } = fixture('wikipedia', language, url).window;
    assert.ok(document.querySelector('.mw-editsection'));
    assert.ok(document.querySelector('sup.reference'));

    const { article, link } = extractPage(document, url, [wikipedia]);
    assert.ok(article);
    assert.equal(article.title, title);
    assert.equal(link.title, title);

    const content = new JSDOM(article.readableContent).window.document;
    assert.ok(content.querySelector('table')?.textContent?.includes(infoboxValue));
    assert.equal(content.querySelector('sup a[href^="#cite_note"]'), null);
    assert.doesNotMatch(content.body.textContent ?? '', /\[(edit|编辑)\]/);

    const markdown = buildMarkdown(document, url, undefined, [wikipedia]);
    assert.ok(markdown.includes(`title: "${title}"`));
    assert.doesNotMatch(markdown, /\[(edit|编辑)\]/);
  });
}

test('Wikipedia removes a site suffix from the heading and rejects look-alike hosts', () => {
  const url = 'https://zh.wikipedia.org/wiki/Test';
  const { document } = new JSDOM('<h1 id="firstHeading">标题 - 维基百科，自由的百科全书</h1>', { url }).window;
  assert.equal(extractPage(document, url, [wikipedia]).link.title, '标题');
  assert.equal(wikipedia.matches(new URL('https://fakewikipedia.org/')), false);
  assert.equal(wikipedia.matches(new URL('https://wikipedia.org.example.org/')), false);
});
