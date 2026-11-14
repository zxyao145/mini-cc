import assert from 'node:assert/strict';
import { test } from 'node:test';
import { extractPage } from '../src/extract-page';
import { github } from '../src/handlers/github';
import { buildMarkdown } from '../src/markdown';
import { fixture } from './fixture';

const repoUrl = 'https://github.com/markedjs/marked';
const title = 'marked: A markdown parser and compiler. Built for speed.';

test('GitHub saves the README with the current repository owner and clean title', () => {
  const { document } = fixture('github', 'repo', repoUrl).window;
  const { article, link } = extractPage(document, repoUrl, [github]);
  assert.ok(article);
  assert.equal(article.title, title);
  assert.equal(article.author, 'markedjs');
  assert.equal(link.title, title);
  assert.match(article.readableContent, /low-level compiler/);
  assert.doesNotMatch(article.readableContent, /README\.md|>About</);

  const markdown = buildMarkdown(document, repoUrl, undefined, [github]);
  assert.ok(markdown.includes(`title: "${title}"`));
  assert.ok(markdown.includes('author: "markedjs"'));
});

test('GitHub prefers the explicit author and falls back to Open Graph when Twitter title is missing', () => {
  const { document } = fixture('github', 'repo', repoUrl).window;
  document.querySelector('meta[name="twitter:title"]')?.remove();
  const author = document.createElement('span');
  author.setAttribute('itemprop', 'author');
  author.textContent = 'Repository author';
  document.body.prepend(author);

  const { article } = extractPage(document, repoUrl, [github]);
  assert.equal(article?.author, 'Repository author');
  assert.equal(article?.title, title);
});

test('GitHub issues use the default extraction and look-alike hosts are rejected', () => {
  const url = `${repoUrl}/issues/2735`;
  const { document } = fixture('github', 'issue', url).window;
  assert.deepEqual(extractPage(document, url, [github]), extractPage(document, url, []));
  assert.equal(github.matches(new URL('https://fakegithub.com/')), false);
});
