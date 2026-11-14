import assert from 'node:assert/strict';
import test from 'node:test';
import { JSDOM } from 'jsdom';
import { parse } from 'yaml';
import { buildMarkdown, frontMatter, htmlToMarkdown, localDate, yamlString } from '../src/markdown.ts';

test('writes a safe YAML front matter block with optional fields', () => {
  assert.equal(frontMatter({ title: 'T', url: 'https://e.com/p', author: 'A', date: '2026-11-13', tags: ['x', 'y'] }),
    '---\ntitle: "T"\nurl: "https://e.com/p"\nauthor: "A"\ndate: 2026-11-13\ntags: ["x", "y"]\n---\n');

  const withoutOptional = frontMatter({ title: 'T', url: 'https://e.com/p', author: '', date: '2026-11-13', tags: [] });
  assert.doesNotMatch(withoutOptional, /^(author|tags):/m);
});

test('escapes YAML strings so titles and tags round-trip through YAML parsing', () => {
  const title = 'He said: "hi" # not a comment\nline two \\ end\tTabbed';
  const tags = ['a "quoted", tag', 'c: d'];
  const yaml = frontMatter({ title, url: 'https://e.com/p', date: '2026-11-13', tags });
  const parsed = parse(yaml.slice(4, yaml.lastIndexOf('\n---\n')));

  assert.equal(parsed.title, title);
  assert.deepEqual(parsed.tags, tags);
  assert.equal(yamlString('a\u0000b\u0007c'), '"abc"');
});

test('normalizes every CR and LF newline and removes other control characters from YAML strings', () => {
  const c1 = Array.from({ length: 32 }, (_, index) => String.fromCharCode(0x80 + index)).join('');
  const value = 'a\rb\r\nc\nd\te\u007f' + c1 + 'f';
  assert.equal(yamlString(value), '"a\\nb\\nc\\nd\\tef"');
  assert.equal(parse(yamlString(value)), 'a\nb\nc\nd\tef');
});

test('formats dates using the local calendar date', () => {
  assert.equal(localDate(new Date(2026, 10, 3)), '2026-11-03');
});

test('converts common semantic HTML and GFM tables into Markdown', () => {
  assert.match(htmlToMarkdown('<h2>Sub</h2>'), /## Sub/);
  assert.match(htmlToMarkdown('<ul><li>a</li><li>b</li></ul>'), /^-\s+a$/m);
  assert.match(htmlToMarkdown('<p><strong>b</strong> <em>i</em> <a href="https://e.com/x">l</a></p>'), /\*\*b\*\* \*i\* \[l\]\(https:\/\/e\.com\/x\)/);
  assert.match(htmlToMarkdown('<img src="https://e.com/a.png" alt="pic">'), /!\[pic\]\(https:\/\/e\.com\/a\.png\)/);
  assert.match(htmlToMarkdown('<table><thead><tr><th>a</th><th>b</th></tr></thead><tbody><tr><td>c</td><td>d</td></tr></tbody></table>'), /\| a \| b \|[\s\S]*\| --- \| --- \|/);
  assert.match(htmlToMarkdown('<del>gone</del>'), /~+gone~+/);
});

test('drops data images and executable or styling elements', () => {
  const markdown = htmlToMarkdown('<script>x()</script><style>p{}</style><p>ok</p><img src="data:image/png;base64,AAAA" alt="x">');
  assert.match(markdown, /^ok\n$/);
  assert.doesNotMatch(markdown, /data:|x\(\)|p\{\}/);
});

test('converts code blocks with languages and supports pre blocks without code children', () => {
  assert.match(htmlToMarkdown('<pre><code class="language-ts">const a = 1;</code></pre>'), /```ts\nconst a = 1;\n```/);
  assert.match(htmlToMarkdown('<pre class="lang-python">print(1)</pre>'), /```python\nprint\(1\)\n```/);
  assert.match(htmlToMarkdown('<pre><code>plain</code></pre>'), /```\nplain\n```/);
});

test('uses a code fence longer than every backtick run in the source', () => {
  const markdown = htmlToMarkdown('<pre><code>before ``` middle\nconst value = 1;</code></pre>');

  assert.match(markdown, /^````\n[\s\S]*before ``` middle[\s\S]*\n````\n$/);
  assert.match(markdown, /const value = 1;/);
});

test('preserves multiple blank lines inside fenced code', () => {
  const source = 'def one():\r\n    pass\r\n\r\n\r\ndef two():\r\n    pass\r\n';
  const fence = '```';
  assert.equal(htmlToMarkdown('<pre class="language-python">' + source + '</pre>'),
    fence + 'python\ndef one():\n    pass\n\n\ndef two():\n    pass\n' + fence + '\n');
});

test('uses a longer fence for highlighted code with a GitHub-style wrapper', () => {
  const source = 'before\n```\n\n\nafter';
  assert.equal(htmlToMarkdown('<div class="highlight highlight-source-md"><pre>' + source + '</pre></div>'),
    '````md\n' + source + '\n````\n');
});

test('preserves languages from GitHub highlight wrappers with suffixes or punctuation', () => {
  const fence = '```';
  assert.equal(htmlToMarkdown('<div class="highlight-text-html-basic"><pre>content</pre></div>'),
    fence + 'html\ncontent\n' + fence + '\n');
  assert.equal(htmlToMarkdown('<div class="highlight-source-c++"><pre>content</pre></div>'),
    fence + 'c++\ncontent\n' + fence + '\n');
});

test('keeps table rows contiguous when the HTML includes spacer rows', () => {
  const markdown = htmlToMarkdown('<table><thead><tr></tr><tr><th>A</th><th>B</th></tr></thead>'
    + '<tbody><tr><td>one</td><td>two</td></tr><tr> </tr><tr><td>three</td><td>four</td></tr></tbody></table>');
  assert.equal(markdown, '| A | B |\n| --- | --- |\n| one | two |\n| three | four |\n');
  const headerless = htmlToMarkdown('<table><tr><td>one</td><td>two</td></tr><tr></tr><tr><td>three</td><td>four</td></tr></table>');
  assert.equal(headerless, '|   |   |\n| --- | --- |\n| one | two |\n| three | four |\n');
});

test('converts a table without headers and keeps its first row as data', () => {
  const markdown = htmlToMarkdown('<table class="site-table"><tr><td>a</td><td>b</td></tr><tr><td>c</td><td>d</td></tr></table>');
  assert.match(markdown, /^\|\s*\|\s*\|\n\| --- \| --- \|\n\| a \| b \|\n\| c \| d \|\n$/);
});

test('preserves checked and unchecked task lists through article extraction', () => {
  const dom = new JSDOM(`<title>Tasks</title><article>
    ${'<p>Readable body content gives this page enough useful text to be treated as an article.</p>'.repeat(8)}
    <ul>
      <li> <input type="checkbox" checked> Done</li>
      <li><p><input type="checkbox">Next</p></li>
      <li>Accept <input type="checkbox"> the terms</li>
      <li><p>Review <input type="checkbox"> the draft</p></li>
      <li><strong>Read</strong><input type="checkbox"> the details</li>
      <li>Ordinary item</li>
    </ul>
  </article>`, { url: 'https://example.com/tasks' });
  try {
    const { document } = dom.window;
    const markdown = buildMarkdown(document, document.URL);
    assert.match(markdown, /^- +\[x\] Done$/m);
    assert.match(markdown, /^- +\[ \] Next$/m);
    assert.match(markdown, /^- +Accept the terms$/m);
    assert.match(markdown, /^- +Review the draft$/m);
    assert.match(markdown, /^- +\*\*Read\*\* the details$/m);
    assert.match(markdown, /^- +Ordinary item$/m);
    assert.equal(document.querySelectorAll('input').length, 5);
  } finally {
    dom.window.close();
  }
});

test('preserves the caption above a table and skips empty tables', () => {
  assert.equal(htmlToMarkdown('<table></table>'), '\n');
  const markdown = htmlToMarkdown('<table><caption>Results</caption><tr><td>one</td><td>two</td></tr></table>');
  assert.equal(markdown, 'Results\n\n|   |   |\n| --- | --- |\n| one | two |\n');
  const headed = htmlToMarkdown('<table><caption>Results</caption><tr><th align="center">First</th><th>Second</th></tr><tr><td>one</td><td>two</td></tr></table>');
  assert.equal(headed, 'Results\n\n| First | Second |\n| :-: | --- |\n| one | two |\n');
});

test('escapes pipes inside table cells', () => {
  const markdown = htmlToMarkdown('<table><tr><td>x | y</td><td><code>a | b</code></td></tr></table>');
  assert.ok(markdown.includes('| x \\| y | `a \\| b` |'));
});

test('normalizes line endings, blank lines, and the trailing newline', () => {
  const markdown = htmlToMarkdown('<p>one</p><p>two</p>');
  assert.doesNotMatch(markdown, /\r|\n{3,}/);
  assert.match(markdown, /\n$/);
  assert.equal(markdown.endsWith('\n\n'), false);
});

test('builds article Markdown with trusted metadata and preserved code language classes', () => {
  const dom = new JSDOM(`<!doctype html><html><head><title>Post</title>
    <meta name="author" content="Jane"><meta property="article:tag" content="One"><meta property="article:tag" content="Two">
  </head><body><article><h1>Post</h1>
    ${'<p>Readable body content gives this page enough useful text to be treated as an article. </p>'.repeat(8)}
    <pre><code class="language-js">const a = 1;</code></pre>
  </article></body></html>`, { url: 'https://example.com/post' });
  const markdown = buildMarkdown(dom.window.document, dom.window.document.URL, new Date(2026, 10, 13));

  assert.match(markdown, /^---\ntitle: "Post"/);
  assert.match(markdown, /url: "https:\/\/example\.com\/post"/);
  assert.match(markdown, /author: "Jane"/);
  assert.match(markdown, /date: 2026-11-13/);
  assert.match(markdown, /tags: \[/);
  assert.match(markdown, /\n---\n\n# Post\n/);
  assert.match(markdown, /```js\nconst a = 1;\n```/);
  dom.window.close();
});

test('omits untrusted author and tags and uses the current document title', () => {
  const dom = new JSDOM(`<!doctype html><html><head><title>Current title</title>
    <meta name="author" content="Stale author"><meta property="article:tag" content="stale">
  </head><body><article><h1>Stale title</h1>${'<p>Enough readable article words and useful details. </p>'.repeat(12)}</article></body></html>`, {
    url: 'https://example.com/new',
  });
  const markdown = buildMarkdown(dom.window.document, 'https://example.com/old', new Date(2026, 10, 13));
  const frontMatterBlock = markdown.slice(0, markdown.indexOf('\n---\n', 4));

  assert.match(markdown, /^---\ntitle: "Current title"/);
  assert.doesNotMatch(frontMatterBlock, /author:|tags:/);
  assert.match(markdown, /\n---\n\n# Current title\n/);
  dom.window.close();
});

test('builds a Markdown link for short media pages and escapes link title brackets', () => {
  const url = 'https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PL1';
  const dom = new JSDOM('<!doctype html><html><head><title>My [Video]</title></head><body></body></html>', { url });
  const markdown = buildMarkdown(dom.window.document, url, new Date(2026, 10, 13));
  const body = markdown.slice(markdown.indexOf('\n---\n', 4) + 5).trim();

  assert.equal(body, `[My \\[Video\\]](${url})`);
  dom.window.close();
});

test('escapes parentheses in a raw URL used as a Markdown link destination', () => {
  const url = 'https://example.com/a_(b)';
  const dom = new JSDOM('<title>Page</title>', { url });
  const markdown = buildMarkdown(dom.window.document, url, new Date(2026, 10, 13));

  assert.match(markdown, /\[Page\]\(https:\/\/example\.com\/a_\\\(b\\\)\)/);
  dom.window.close();
});

test('copies Markdown for readable pages on non-HTTP URLs without saving restrictions', () => {
  const dom = new JSDOM(`<!doctype html><html><head><title>Local article</title></head><body>
    <article><h1>Local article</h1>${'<p>Local readable body content supports exporting this document to Markdown. </p>'.repeat(12)}</article>
  </body></html>`, { url: 'file:///tmp/article.html' });

  const markdown = buildMarkdown(dom.window.document, dom.window.document.URL, new Date(2026, 10, 13));
  assert.match(markdown, /url: "file:\/\/\/tmp\/article\.html"/);
  assert.match(markdown, /\n---\n\n# Local article\n/);
  dom.window.close();
});
