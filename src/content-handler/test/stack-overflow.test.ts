import assert from 'node:assert/strict';
import { test } from 'node:test';
import { JSDOM } from 'jsdom';
import { extractPage } from '../src/extract-page';
import { stackOverflow } from '../src/handlers/stack-overflow';
import { buildMarkdown } from '../src/markdown';
import { fixture } from './fixture';

const url = 'https://stackoverflow.com/questions/123/sorting-records-by-two-keys';

test('Stack Overflow saves vote headings, authors, reputation, and comments in post order', () => {
  const { document } = fixture('stack-overflow', 'question', url).window;
  const { article, link } = extractPage(document, url, [stackOverflow]);
  assert.ok(article);
  assert.equal(article.title, 'Sorting records by two keys');
  assert.equal(link.title, article.title);

  const { body } = new JSDOM(article.readableContent).window.document;
  const headings = Array.from(body.querySelectorAll('h2'), h => h.textContent);
  assert.deepEqual(headings, ['Question: 42 votes', 'Accepted Answer: 17 votes', 'Answer: 3 votes']);
  assert.match(body.textContent ?? '', /River\s+- 1,234 reputation/);
  assert.match(body.textContent ?? '', /Jordan.*Are the timestamps already normalized to UTC\?/);
  assert.match(body.textContent ?? '', /Casey\s+- 576 reputation/);
  assert.doesNotMatch(body.textContent ?? '', /Related questions/);

  const markdown = buildMarkdown(document, url, undefined, [stackOverflow]);
  assert.match(markdown, /## Accepted Answer: 17 votes/);
  assert.match(markdown, /records\.sort/);
});

test('Stack Overflow excludes look-alike hosts', () => {
  assert.equal(stackOverflow.matches(new URL('https://fakestackoverflow.com/')), false);
  assert.equal(stackOverflow.matches(new URL('https://stackoverflow.com.example.org/')), false);
});
