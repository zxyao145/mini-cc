import assert from 'node:assert/strict';
import test from 'node:test';
import { JSDOM } from 'jsdom';
import { buildMarkdown, extractPage } from '../src/index';
import { fixture } from './fixture';

const photosUrl = 'https://www.xiaohongshu.com/explore/6ad1e33b0000000018013151';
const shortUrl = 'https://www.xiaohongshu.com/explore/6ab87df1000000001203946b';
const videoUrl = 'https://www.xiaohongshu.com/explore/6a8fb6860000000005028a76';
// The observed 12-digit timestamps changed across visits. The unsigned CDN paths with the
// original transform returned 200 image/webp without a Referer, including through the MiniCC image proxy.
const cdn = 'https://sns-img-qc.xhscdn.com/';
const photoKeys = [
  'notes_pre_post/1040g3k0326ckvoctlg4g5pg85on2phlim8cjvq0',
  'notes_pre_post/1040g3k0326ckvoctlg505pg85on2phlijtuhko0',
  'notes_pre_post/1040g3k0326ckvoctlg5g5pg85on2phli2pjq9bg',
  'notes_pre_post/1040g3k0326ckvoctlg605pg85on2phliqml8g50',
  'notes_pre_post/1040g3k0326ckvoctlg6g5pg85on2phli744er4o',
  'notes_pre_post/1040g3k0326cl7d7qkq105pg85on2phli2bfsmtg',
  '1040g008326g6jj3456005pg85on2phlirm4dal0',
  'notes_pre_post/1040g3k0326cl7d7qkq205pg85on2phlimkevk3g',
  'notes_pre_post/1040g3k0326cl7d7qkq2g5pg85on2phlipppkr20',
];
const photos = photoKeys.map(key => cdn + key + '!nd_dft_wlteh_webp_3');
const poster = cdn + '1040g008324c11difga004970bb2u814ar8cvl10!nd_dft_wlteh_webp_3';
const lines = ['🍅 第一行文字', '🌿 第二行保留标签 #番茄', '📷 最后一行'];

function body(html: string): HTMLElement {
  return new JSDOM(html).window.document.body;
}

function imageSources(element: HTMLElement): string[] {
  return Array.from(element.querySelectorAll('img'), img => img.getAttribute('src')!);
}

test('saves every distinct carousel image in order and preserves note paragraphs and topic links', () => {
  const { document } = fixture('xiaohongshu', 'multi-image', photosUrl).window;
  const original = document.documentElement.outerHTML;
  const result = extractPage(document, photosUrl);
  assert.equal(result.suggested, 'article');
  assert.ok(result.article);
  assert.equal(result.article.title, '番茄图文');
  assert.equal(result.article.author, '样本作者');
  assert.equal(result.article.imageUrl, photos[0]);
  assert.equal(result.article.originalContent, original);
  assert.equal(document.documentElement.outerHTML, original);
  const readable = body(result.article.readableContent);
  assert.deepEqual(imageSources(readable), photos);
  assert.equal(readable.querySelectorAll('figure > img').length, photos.length);
  assert.deepEqual(Array.from(readable.querySelectorAll('p'), p => p.textContent?.trim()), lines);
  assert.equal(new URL(readable.querySelector('a')!.href).searchParams.get('keyword'), '番茄');
  assert.doesNotMatch(readable.textContent!, /10-19|吉林|江苏/);
  const markdown = buildMarkdown(document, photosUrl);
  assert.deepEqual([...markdown.matchAll(/!\[[^\]]*\]\(([^)]+)\)/g)].map(match => match[1]), photos);
  assert.match(markdown, /🍅 第一行文字\n\n🌿[^\n]*#番茄[^\n]*\n\n📷 最后一行/);
});

test('extracts the current modal note without using feed images or stale head metadata', () => {
  const { document } = fixture('xiaohongshu', 'modal', photosUrl).window;
  const result = extractPage(document, 'https://www.xiaohongshu.com/explore');
  assert.equal(result.article?.url, photosUrl);
  assert.equal(result.link.title, '番茄图文');
  assert.equal(result.link.author, '样本作者');
  assert.deepEqual(imageSources(body(result.article!.readableContent)), photos);
});

test('uses direct-page state to recover missing images only when the note ID matches', () => {
  const { document } = fixture('xiaohongshu', 'multi-image', photosUrl).window;
  document.querySelector('.swiper-wrapper')!.replaceChildren();
  const script = document.createElement('script');
  const id = photosUrl.split('/').at(-1)!;
  const imageList = photos.map(urlDefault => ({ urlDefault }));
  script.text = 'window.__INITIAL_STATE__=' + JSON.stringify({ note: { noteDetailMap: { [id]: { note: { noteId: id, imageList } } } } });
  document.head.append(script);
  const sources = () => imageSources(body(extractPage(document, photosUrl).article!.readableContent));
  assert.deepEqual(sources(), photos);
  script.text = script.text.replace('"noteId":"' + id + '"', '"noteId":"not-current"');
  assert.deepEqual(sources(), []);
  script.text = script.text.replace('"noteId":"not-current"', '"noteId":"' + id + '"');
  document.querySelector('.note-detail-mask')!.classList.remove('undertake-ssr');
  assert.deepEqual(sources(), []);
});

test('keeps the carousel URLs in order when state fills in a missing slide', () => {
  const { document } = fixture('xiaohongshu', 'multi-image', photosUrl).window;
  document.querySelector('.swiper-slide[data-swiper-slide-index="1"] img')!.remove();
  const script = document.createElement('script');
  const id = photosUrl.split('/').at(-1)!;
  const imageList = photos.map((url, index) => ({
    urlDefault: url.replace('!nd_dft_wlteh_webp_3', '!state-' + index),
  }));
  script.text = 'window.__INITIAL_STATE__=' + JSON.stringify({ note: { noteDetailMap: { [id]: { note: { noteId: id, imageList } } } } });
  document.head.append(script);
  const expected = photos.with(1, imageList[1].urlDefault);
  const { article } = extractPage(document, photosUrl);
  assert.deepEqual(imageSources(body(article!.readableContent)), expected);
});

test('keeps only the rendered note whose ID matches a transitional page', () => {
  const { document } = fixture('xiaohongshu', 'multi-image', photosUrl).window;
  const previous = document.querySelector('#noteContainer')!.cloneNode(true) as HTMLElement;
  document.querySelector('#noteContainer')!.setAttribute('data-note-id', photosUrl.split('/').at(-1)!);
  previous.setAttribute('data-note-id', 'previous-note');
  previous.querySelector('#detail-title')!.textContent = '旧笔记标题';
  document.body.prepend(previous);
  assert.equal(extractPage(document, photosUrl).article?.title, '番茄图文');
});

test('keeps an attached place as the final paragraph and excludes dates, regions and distance', () => {
  const { document } = fixture('xiaohongshu', 'location', shortUrl).window;
  const article = extractPage(document, shortUrl).article;
  assert.ok(article);
  const readable = body(article.readableContent);
  assert.equal(Array.from(readable.querySelectorAll('p')).at(-1)?.textContent, '📍 城市公园');
  assert.doesNotMatch(readable.textContent!, /09-27|上海|江苏|1 km/);
});

test('keeps the video cover and text without embedding the video', () => {
  const { document } = fixture('xiaohongshu', 'video', videoUrl).window;
  const article = extractPage(document, videoUrl).article;
  assert.equal(article?.imageUrl, poster);
  const readable = body(article!.readableContent);
  assert.deepEqual(imageSources(readable), [poster]);
  assert.match(readable.textContent!, /视频附带一段文字/);
  assert.equal(readable.querySelector('video, iframe'), null);
});

test('suggests an article for a one-line note and removes the fallback title suffix', () => {
  const { document } = fixture('xiaohongshu', 'short', shortUrl).window;
  const result = extractPage(document, shortUrl);
  assert.equal(result.suggested, 'article');
  assert.equal(result.article?.title, '一行笔记');
  assert.match(result.article!.readableContent, /只有一行文字✨/);
});

test('leaves similar domains and routes without a current note on the default pipeline', () => {
  for (const url of ['https://fakexiaohongshu.com/explore/note', 'https://xiaohongshu.com.evil.example/explore/note', 'https://www.xiaohongshu.com/explore']) {
    const { document } = fixture('xiaohongshu', 'short', url).window;
    assert.deepEqual(extractPage(document, url), extractPage(document, url, []));
  }
});
