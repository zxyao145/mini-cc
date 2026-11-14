import assert from 'node:assert/strict';
import { test } from 'node:test';
import { JSDOM } from 'jsdom';
import { extractPage } from '../src/extract-page';
import { medium } from '../src/handlers/medium';
import { buildMarkdown } from '../src/markdown';
import { fixture } from './fixture';

const url = 'https://medium.com/airbnb-engineering/airflow-a-workflow-management-platform-46318b977fd8';
const widest = 'https://miro.medium.com/v2/resize:fit:1100/format:webp/0*277Imf2r7ouTXOVy.png';

test('Medium saves the widest picture candidate as a single image', () => {
  const { document } = fixture('medium', 'picture', url).window;
  // The minimized fixture needs the article signal to exercise the Markdown body.
  const type = document.createElement('meta');
  type.setAttribute('property', 'og:type');
  type.content = 'article';
  document.head.append(type);
  const { article } = extractPage(document, url, [medium]);
  assert.ok(article);

  const { body } = new JSDOM(article.readableContent).window.document;
  assert.equal(body.querySelector('picture'), null);
  assert.deepEqual(Array.from(body.querySelectorAll('img'), img => img.getAttribute('src')), [widest]);
  assert.equal(body.querySelector('img')?.getAttribute('width'), null);
  assert.ok(buildMarkdown(document, url, undefined, [medium]).includes(widest));
});

test('Medium checks candidates across all sources and preserves the fallback image attributes', () => {
  const { document } = fixture('medium', 'picture', url).window;
  const image = document.querySelector<HTMLImageElement>('picture img');
  assert.ok(image);
  image.alt = 'Airflow diagram';
  const source = document.createElement('source');
  source.srcset = '/medium.png 800w,/wide,diagram.png 1600w';
  document.querySelector('picture')?.prepend(source);

  const { article } = extractPage(document, url, [medium]);
  assert.ok(article);
  const saved = new JSDOM(article.readableContent).window.document.querySelector('img');
  assert.equal(saved?.getAttribute('src'), 'https://medium.com/wide,diagram.png');
  assert.equal(saved?.alt, 'Airflow diagram');
  assert.equal(medium.matches(new URL('https://fakemedium.com/')), false);
});
