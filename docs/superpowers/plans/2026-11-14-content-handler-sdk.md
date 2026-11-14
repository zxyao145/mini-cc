# Content-Handler SDK and Site Handlers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the extension's extraction code into a Chrome-free SDK package (`@minicc/content-handler`) and add per-site handlers for 小红书, 微信公众号, Wikipedia, YouTube, 知乎, Medium, GitHub, Apple News and Stack Overflow.

**Architecture:** The SDK takes a `Document` (and, for URL-level hooks, an injected `fetch`). Before Readability runs, a handler matched by exact domain rewrites a disposable clone (`preParse`) and returns metadata overrides. The extension imports the SDK via `link:`. Only the popup uses the URL layer, to fetch YouTube oEmbed metadata.

**Tech Stack:** TypeScript (ESM, no build step), `@mozilla/readability` 0.6.0, `dompurify` 3.4.17, `turndown` 7.2.5, `turndown-plugin-gfm` 1.0.2, `tsx --test` + jsdom, Vite (`vite-plugin-web-extension`), pnpm 12.

**Spec:** `docs/designs/2026-11-14-site-content-handlers-design.md`. This plan covers Part A (§3.1–3.4 and the Part A rows of §4). Part B has its own plan, `docs/superpowers/plans/2026-11-14-resave-replaces-content.md`.

## Global Constraints

- SDK lives at `src/content-handler/`, package name `@minicc/content-handler`, `"type": "module"`. `main`, `types` and `exports["."]` all point to `./src/index.ts`, because the extension's `moduleResolution: "Node"` ignores `exports`.
- The extension depends on it as `"@minicc/content-handler": "link:../content-handler"`. Do not create a pnpm workspace. Both packages need their own `pnpm install`.
- Runtime dependencies use exact versions: `@mozilla/readability` `0.6.0`, `dompurify` `3.4.17`, `turndown` `7.2.5`, `turndown-plugin-gfm` `1.0.2`. devDependencies use `^` ranges copied from the extension.
- SDK code must not reference `chrome`, `window`, `location`, `navigator` or global `fetch`. It may use only the passed `Document`, the source document's `defaultView` (for DOMPurify), and `ctx.fetch`. The SDK `tsconfig.json` has `"types": ["node"]` and no `chrome`.
- `preParse` is synchronous and only ever touches a clone. The live document is never mutated, and `originalContent` stays the untouched `document.documentElement.outerHTML`.
- Domain matching is `hostname === domain || hostname.endsWith('.' + domain)`.
- `ARTICLE_LIMITS = { url: 2000, title: 500, author: 200, summary: 1000 }` applies to overrides too. Override image URLs must be http(s) and at most 2000 characters (the same rule as `extractImageUrl`).
- URL-level hooks time out after 5000 ms, using `AbortController` + `setTimeout`, not `AbortSignal.timeout`. On timeout or error, `resolveUrl` returns the input URL and `preHandleUrl` returns `null`.
- The extension message protocol (`extractContent`, `extractMarkdown`, `savePage`) and synchronous content-script responses stay unchanged. Copy Markdown makes no network request.
- Moved tests keep their assertions unchanged. Only import paths may change.
- No backend or frontend changes in this plan.
- **Fixture capture (used by Tasks 5–11):**
  - Open the real page in a browser via the `ego-browser` skill, logged out when possible, wait for render, and save `document.documentElement.outerHTML`.
  - Trim it to the structure the handler reads plus enough surrounding page chrome to prove it gets removed.
  - Delete `<style>`, stylesheet `<link>`s, inline SVG sprites, base64 blobs and tracking scripts. Keep a `<script>` only if the handler reads it.
  - Scrub cookies, tokens and the viewer's own nickname or avatar.
  - Prepend `<!-- source: <url> | captured: <YYYY-MM-DD> -->`.
  - Save to `src/content-handler/test/fixtures/<site>/<case>.html`.
  - If a site shows nothing useful without login, stop and ask the user for help before continuing that task.
- Commits use Conventional Commit messages (`feat:`, `test:`, `refactor:`, `docs:`).

## Review Focus

1. **小红书 feed leakage:** a note opened as a modal over the explore feed must contain only that note's images and text. Feed-card thumbnails must not appear. Test in Task 5.
2. **Non-absolute image URLs** (protocol-relative `//…` or relative) in handler-rebuilt content: every saved `<img src>` must be an absolute `https://` URL, because the backend proxy rewriting expects absolute URLs. Test in Tasks 5 and 6.
3. **Lazy-load placeholders:** an `<img>` whose `src` is a `data:` placeholder and whose real URL sits in `data-src` / `data-original` / `data-actualsrc` must be saved with the real URL, and no `data:` `src` may remain. Test in Tasks 6 and 8.
4. **Blank or padded overrides:** a handler title of `'   '` must fall back to the default title, and `'  标题 \n'` must be saved trimmed. Test in Task 2.
5. **YouTube watch URL with a `list` parameter:** metadata must describe the video, not the playlist. Test in Task 3.

---

## File Structure

```
src/content-handler/
  package.json  tsconfig.json  eslint.config.mjs  README.md
  src/
    index.ts                 public exports
    types.ts                 ARTICLE_LIMITS, ArticlePage, LinkPage, ExtractedPage, PageChoices, SiteOverrides, FetchContext, SiteHandler
    page-meta.ts             (moved) + hostMatches, validImageUrl
    classify-page.ts         (moved)
    readable.ts              (moved; parses a prepared clone)
    extract-article.ts       (moved; applies overrides)
    extract-page.ts          (moved; uses prepareDocument)
    markdown.ts              (moved; uses prepareDocument)
    turndown-plugin-gfm.d.ts (moved)
    prepare.ts               prepareDocument, applyOverrides
    url.ts                   resolveUrl, preHandleUrl, mergeOverrides
    handlers/index.ts        SITE_HANDLERS registry
    handlers/<site>.ts       one SiteHandler per site
  test/
    (moved) classify-page / extract-article / extract-page / markdown / page-meta .test.ts
    fixture.ts               fixture loader
    prepare.test.ts  url.test.ts  <site>.test.ts
    fixtures/<site>/*.html
src/web-extension/
  src/content.ts  src/popup.ts  src/types.ts   (modified)
  package.json  README.md                      (modified)
CLAUDE.md                                      (modified)
```

The test script stays flat (`tsx --test test/*.test.ts`), so handler tests are named `test/<site>.test.ts`.

---

### Task 1: Create the SDK package and move the extraction code

**Files:**
- Create: `src/content-handler/package.json`, `tsconfig.json`, `eslint.config.mjs`, `README.md`, `src/index.ts`, `src/types.ts`
- Move (`git mv`) from `src/web-extension/src/` to `src/content-handler/src/`: `classify-page.ts`, `extract-article.ts`, `extract-page.ts`, `markdown.ts`, `page-meta.ts`, `readable.ts`, `turndown-plugin-gfm.d.ts`
- Move (`git mv`) from `src/web-extension/test/` to `src/content-handler/test/`: `classify-page.test.ts`, `extract-article.test.ts`, `extract-page.test.ts`, `markdown.test.ts`, `page-meta.test.ts`
- Modify: `src/web-extension/src/types.ts`, `src/content.ts`, `src/popup.ts`, `package.json`, `README.md`; `CLAUDE.md` (Quick Start, Web Extension commands, new "Content-Handler SDK" section)

**Interfaces:**
- Produces:
  - `@minicc/content-handler` exporting everything the extension used before: `extractPage(document: Document, loadedUrl: string): PageChoices`, `buildMarkdown(document: Document, loadedUrl: string, now?: Date): string`, `htmlToMarkdown`, `assertHttpPageUrl`, and the types `ArticlePage`, `LinkPage`, `ExtractedPage`, `PageChoices`, `ARTICLE_LIMITS`;
  - extension `src/types.ts` keeping only `DEFAULT_API_URL`, `SavePageMessage`, `SavePageResponse`. `SavePageMessage.content` imports `ExtractedPage` from the SDK.

- [ ] **Step 1: Record the baseline.** Run `cd src/web-extension && pnpm test && pnpm typecheck && pnpm lint && pnpm build`. Expected: every command exits 0. Record the test count.
- [ ] **Step 2: Scaffold the SDK.**
  - `package.json` scripts: `"test": "tsx --test test/*.test.ts"`, `"typecheck": "tsc --noEmit"`, `"lint": "eslint src --ext .ts"`.
  - Dependencies per Global Constraints. devDependencies: `@types/jsdom`, `@types/node`, `@types/turndown`, `@typescript-eslint/eslint-plugin`, `@typescript-eslint/parser`, `eslint`, `jsdom`, `tsx`, `typescript`, `yaml` (used by `markdown.test.ts`).
  - `tsconfig.json`: the extension's options, with `"types": ["node"]`.
  - `eslint.config.mjs`: a copy of the extension's config.
- [ ] **Step 3: Move the files.** `git mv` the modules and tests listed above. Split `types.ts`: the extraction types go to the SDK, and the three extension-only types stay. Add `/// <reference path="./turndown-plugin-gfm.d.ts" />` at the top of SDK `markdown.ts`, so the ambient module stays visible when the extension type-checks SDK sources. `index.ts` re-exports the public API.
- [ ] **Step 4: Rewire the extension.**
  - `content.ts` and `popup.ts` import from `@minicc/content-handler`.
  - In `package.json`, remove the four runtime dependencies, remove `@types/turndown` and `yaml` (only the moved code and tests used them), and add the `link:` dependency.
  - Run `pnpm install` in `src/content-handler`, then in `src/web-extension`.
- [ ] **Step 5: Verify the SDK.** Run `cd src/content-handler && pnpm test && pnpm typecheck && pnpm lint`. Expected: exit 0. Record the SDK test count.
- [ ] **Step 6: Prove the SDK is Chrome-free.** Temporarily add `chrome.runtime.id;` to `src/content-handler/src/index.ts` and run `pnpm typecheck`. Expected: `Cannot find name 'chrome'`. Then remove the line.
- [ ] **Step 7: Verify the extension.**
  - Run `cd src/web-extension && pnpm test && pnpm typecheck && pnpm lint && pnpm build`. Expected: exit 0. The remaining tests are background, popup and clipboard, and SDK count + extension count equals the Step 1 baseline.
  - Run `grep -rl "Readability" dist`. Expected: at least one match (the SDK is bundled).
- [ ] **Step 8: Docs.**
  - SDK `README.md`: purpose, public API list, the Chrome-free rule, `pnpm install` / `test` / `typecheck` / `lint`.
  - Extension `README.md` and `CLAUDE.md`: both packages need `pnpm install`; extraction now lives in the SDK; add the SDK's test commands.
- [ ] **Step 9: Commit.** `git add -A src/content-handler src/web-extension CLAUDE.md && git commit -m "refactor: move page extraction into @minicc/content-handler SDK"`

---

### Task 2: Handler contract, DOM pipeline and overrides

**Files:**
- Modify: `src/content-handler/src/types.ts`, `page-meta.ts`, `readable.ts`, `extract-article.ts`, `extract-page.ts`, `markdown.ts`, `index.ts`
- Create: `src/content-handler/src/prepare.ts`, `src/handlers/index.ts`, `test/prepare.test.ts`, `test/fixture.ts`

**Interfaces:**
- Consumes: Task 1 modules.
- Produces:
  - `types.ts`:
    - `interface SiteOverrides { title?: string; author?: string; summary?: string; imageUrl?: string; suggested?: 'article' | 'link' }`
    - `interface FetchContext { fetch: typeof fetch; signal?: AbortSignal }`
    - `interface SiteHandler { name: string; matches(url: URL): boolean; resolve?(url: URL, ctx: FetchContext): Promise<string | null>; preHandle?(url: URL, ctx: FetchContext): Promise<SiteOverrides | null>; preParse?(document: Document): SiteOverrides | void }`
  - `page-meta.ts`:
    - `hostMatches(url: URL, domain: string): boolean`
    - `validImageUrl(raw: string, baseUrl: string): string`, which returns `''` when the URL is invalid, not http(s), or longer than 2000 characters. `extractImageUrl` now delegates to it.
  - `handlers/index.ts`: `export const SITE_HANDLERS: readonly SiteHandler[] = []`. Later tasks append to it.
  - `prepare.ts`:
    - `prepareDocument(document: Document, handlers?: readonly SiteHandler[]): { clone: Document; overrides: SiteOverrides }`. It picks the first handler whose `matches(new URL(document.URL))` is true. If `preParse` throws, it returns a fresh clone with `{}`.
    - `applyOverrides<T extends ExtractedPage>(page: T, overrides: SiteOverrides): T`. It trims each string override, ignores empty ones, truncates to `ARTICLE_LIMITS`, and validates `imageUrl` with `validImageUrl(…, page.url)`. `suggested` is not part of a page and is ignored here.
  - `readable.ts`: `parseReadable(clone: Document, view: Window, mode?: 'save' | 'markdown'): ReadableArticle | null`. It no longer clones; `view` is the source document's `defaultView`, used for DOMPurify.
  - `extract-page.ts`: `extractPage(document: Document, loadedUrl: string, handlers?: readonly SiteHandler[]): PageChoices`, with `suggested = readable ? (overrides.suggested ?? (isArticlePage(…) ? 'article' : 'link')) : 'link'`. Overrides apply to both `link` and `article`, and ignore `isMetaTrusted`.
  - `markdown.ts`: `buildMarkdown(document: Document, loadedUrl: string, now?: Date, handlers?: readonly SiteHandler[]): string`. The override title and author win in the front matter and in the `# heading`, and `overrides.suggested` decides article vs link Markdown when readable content exists.
  - `test/fixture.ts`: `fixture(site: string, name: string, url: string): JSDOM`, which loads `test/fixtures/<site>/<name>.html`.

- [ ] **Step 1: Write the failing tests** in `test/prepare.test.ts`. Use inline fake handlers and pass them as the `handlers` argument.

```ts
test('a throwing preParse falls back to the default result', () => {
  const html = textArticle();                       // reuse the long-article helper pattern from extract-page.test.ts
  const plain = extractPage(doc(html).window.document, URL_);
  const broken = extractPage(doc(html).window.document, URL_, [{ name: 'boom', matches: () => true, preParse: () => { throw new Error('x'); } }]);
  assert.deepEqual(broken, plain);
});
test('preParse never mutates the live document', () => {
  const dom = doc(textArticle()); const before = dom.window.document.documentElement.outerHTML;
  extractPage(dom.window.document, URL_, [{ name: 'strip', matches: () => true, preParse: d => { d.body.replaceChildren(); } }]);
  assert.equal(dom.window.document.documentElement.outerHTML, before);
});
test('overrides win over page metadata for article, link and Markdown, even when meta is untrusted', () => { /* title/author/imageUrl overrides; loadedUrl differs from document.URL; assert both kinds and the Markdown front matter + "# " heading use overrides */ });
test('blank override strings are ignored and padded ones trimmed', () => { /* title '   ' -> default title; author '  A \n' -> 'A' */ });
test('overrides are truncated to ARTICLE_LIMITS and invalid image URLs dropped', () => { /* title 'x'.repeat(600) -> length 500; imageUrl 'javascript:1' -> '' ; imageUrl '/c.png' -> absolute */ });
test('suggested override applies only when readable content exists', () => { /* short text + suggested article -> 'article'; empty body + suggested article -> 'link' with article null */ });
test('hostMatches requires an exact domain or a subdomain', () => {
  assert.equal(hostMatches(new URL('https://www.zhihu.com/q'), 'zhihu.com'), true);
  assert.equal(hostMatches(new URL('https://zhihu.com/q'), 'zhihu.com'), true);
  assert.equal(hostMatches(new URL('https://evilzhihu.com/q'), 'zhihu.com'), false);
});
```

- [ ] **Step 2: Run the tests to verify they fail.** Run `cd src/content-handler && pnpm test`. Expected: FAIL (`prepareDocument`/`hostMatches` not exported; `extractPage` ignores the handlers argument).
- [ ] **Step 3: Implement the interfaces above.** Order inside `extractPage`:
  1. `assertHttpPageUrl`;
  2. `prepareDocument`;
  3. build `link` from the original document;
  4. `parseReadable(clone, document.defaultView!, 'save')`;
  5. `extractArticle`, then `applyOverrides` on both pages.

  `buildMarkdown` calls `prepareDocument` itself and inserts the task markers into the returned clone before `parseReadable(…, 'markdown')`.
- [ ] **Step 4: Run all SDK tests.** Run `pnpm test && pnpm typecheck && pnpm lint`. Expected: PASS, with the moved tests unchanged plus the new ones.
- [ ] **Step 5: Commit.** `git commit -am "feat(content-handler): add site handler contract and DOM override pipeline"` (also `git add` the new files).

---

### Task 3: URL-level pipeline and the YouTube handler

**Files:**
- Create: `src/content-handler/src/url.ts`, `src/handlers/youtube.ts`, `test/url.test.ts`, `test/youtube.test.ts`
- Modify: `src/handlers/index.ts`, `src/index.ts`

**Interfaces:**
- Consumes: `SiteHandler`, `SiteOverrides`, `FetchContext`, `applyOverrides`, `SITE_HANDLERS`.
- Produces:
  - `url.ts`:
    - `resolveUrl(url: string, ctx: FetchContext, options?: { handlers?: readonly SiteHandler[]; timeoutMs?: number }): Promise<string>`
    - `preHandleUrl(url: string, ctx: FetchContext, options?: { handlers?: readonly SiteHandler[]; timeoutMs?: number }): Promise<SiteOverrides | null>`
    - `mergeOverrides(choices: PageChoices, overrides: SiteOverrides): PageChoices`, which applies `applyOverrides` to `link` and to `article` when present, and never changes `suggested`.
    - The default `timeoutMs` is `5000`. Each hook gets `{ fetch: ctx.fetch, signal }` from a fresh `AbortController`, and is also raced against the timer, in case a `fetch` ignores the signal.
  - `handlers/youtube.ts`: `export const youtube: SiteHandler`. It matches `youtube.com` and `youtu.be`.
    - `preHandle` builds the canonical URL in this order: the video id from `/watch?v=`, `/shorts/<id>` or `youtu.be/<id>` (11 characters, `[\w-]`) → `https://www.youtube.com/watch?v=<id>` (Shorts → `https://www.youtube.com/shorts/<id>`); otherwise `/playlist?list=<id>` → `https://www.youtube.com/playlist?list=<id>`; otherwise `null`.
    - It then GETs `https://www.youtube.com/oembed?format=json&url=<encodeURIComponent(canonical)>` and returns `{ title: title, author: author_name, imageUrl: thumbnail_url }`.
    - A non-2xx response throws.

- [ ] **Step 1: Write the failing tests.**
  - `url.test.ts`, using fake handlers:
    - `resolveUrl returns the handler result when it is http(s)`;
    - `resolveUrl returns the input for a non-http result, an error, a timeout (timeoutMs: 20, fetch never settles) and no matching handler`;
    - `preHandleUrl returns null on error, timeout and no handler`;
    - `mergeOverrides updates link and article but not suggested, and truncates/validates like applyOverrides`.
  - `youtube.test.ts`, with a recording fetch mock:

```ts
test('watch URL with list describes the video, not the playlist', async () => {
  const calls: string[] = [];
  const fetch = (async (u: string) => { calls.push(u); return new Response(JSON.stringify({ title: 'T', author_name: 'Chan', thumbnail_url: 'https://i.ytimg.com/vi/BnSUk0je6oo/hqdefault.jpg' })); }) as typeof globalThis.fetch;
  const result = await preHandleUrl('https://www.youtube.com/watch?v=BnSUk0je6oo&list=PL6D4F6A0D6B0C6A5E', { fetch });
  assert.deepEqual(result, { title: 'T', author: 'Chan', imageUrl: 'https://i.ytimg.com/vi/BnSUk0je6oo/hqdefault.jpg' });
  assert.equal(calls[0], 'https://www.youtube.com/oembed?format=json&url=' + encodeURIComponent('https://www.youtube.com/watch?v=BnSUk0je6oo'));
});
// also: youtu.be/<id>?t=116 -> watch?v=<id>; /shorts/<id> -> /shorts/<id>; /playlist?list=PL… -> playlist URL;
// m.youtube.com matches; www.youtube.com/@channel -> null without fetching; HTTP 404 -> null; notyoutube.com not matched
```

- [ ] **Step 2: Run the tests to verify they fail.** Run `pnpm test`. Expected: FAIL (modules missing).
- [ ] **Step 3: Implement `url.ts` and `youtube.ts`, then register `youtube` in `SITE_HANDLERS`.**
- [ ] **Step 4: Run the tests to verify they pass.** Run `pnpm test && pnpm typecheck && pnpm lint`. Expected: PASS.
- [ ] **Step 5: Commit.** `git add -A src/content-handler && git commit -m "feat(content-handler): add URL-level pipeline and YouTube oEmbed handler"`

---

### Task 4: Extension popup applies URL-level overrides

**Files:**
- Modify: `src/web-extension/src/popup.ts`, `test/popup.test.ts`

**Interfaces:**
- Consumes: `preHandleUrl`, `mergeOverrides` from `@minicc/content-handler`.

- [ ] **Step 1: Write the failing tests** in `popup.test.ts`. Extend `withPopup` with an optional `fetch` global.
  - `merges YouTube oEmbed metadata into the preview and the saved link`: choices for `https://www.youtube.com/watch?v=BnSUk0je6oo` with title `YouTube`; the fetch mock returns `{ title: 'Real title', author_name: 'Chan', thumbnail_url: 'https://i.ytimg.com/vi/BnSUk0je6oo/hqdefault.jpg' }`. Assert that `#preview-title` shows `Real title`, `#preview-author` shows `Chan`, and clicking Save Link sends `title: 'Real title'`.
  - `keeps the extracted metadata when oEmbed fails`: the fetch mock rejects. Assert the preview title is `YouTube` and saving still works.
  - `does not fetch for sites without a URL-level handler`: the existing `example.com` choices. Assert the fetch mock was called 0 times.
- [ ] **Step 2: Run the tests to verify they fail.** Run `cd src/web-extension && pnpm test`. Expected: the two YouTube tests FAIL.
- [ ] **Step 3: Implement.** In `extractContent()`, after a successful response: `const overrides = await preHandleUrl(content.link.url, { fetch: (...args) => fetch(...args) })`. If it returns overrides, set `this.choices = mergeOverrides(content, overrides)`. Then `displayPreview`. The existing `Extracting content...` status stays visible while waiting.
- [ ] **Step 4: Run the tests to verify they pass.** Run `pnpm test && pnpm typecheck && pnpm lint && pnpm build`. Expected: exit 0.
- [ ] **Step 5: Commit.** `git commit -am "feat(extension): enrich YouTube links with oEmbed metadata in the popup"`

---

### Task 5: 小红书 handler

**Files:**
- Create: `src/content-handler/src/handlers/xiaohongshu.ts`, `test/xiaohongshu.test.ts`, `test/fixtures/xiaohongshu/{multi-image,location,modal,video,short}.html`
- Modify: `src/handlers/index.ts`

**Interfaces:**
- Produces: `export const xiaohongshu: SiteHandler`, matching `xiaohongshu.com`, with `preParse` only.

- [ ] **Step 1: Capture the fixtures.** Use the fixture-capture procedure in Global Constraints.
  - `multi-image`: a note with ≥3 images and a multi-line description, opened directly by URL. Use the user's screenshot note (「皖南秋色仅20天，错过等一年」, author 老唐旅行) if you can find it; otherwise a similar note.
  - `location`: a note where the author attached a place.
  - `modal`: open the explore feed, click a note, and capture while the modal is open over the feed.
  - `video`: a video note.
  - `short`: a note with one or two lines of text.

  Record for each case: note id, title, author, the ordered distinct carousel image URLs, and the place name. Starting selectors to confirm or replace: container `#noteContainer` / `.note-container`; slides `.swiper-slide:not(.swiper-slide-duplicate) img` or `.note-slider-img`; title `#detail-title`; description `#detail-desc .note-text`; author `.author-wrapper .username`; date + IP `.bottom-container .date`.
  - If the web page does not render an author-attached place anywhere (DOM or the `<script>` state JSON), stop and report it to the user before going further.
- [ ] **Step 2: Check whether image URLs expire.**
  - Inspect a captured image URL for a timestamp or signature path segment (for example `/<yyyyMMddHHmm>/<hash>/`).
  - If one is present, try candidate unsigned forms built from the trailing image id (for example `https://sns-img-qc.xhscdn.com/<id>` and `https://ci.xiaohongshu.com/<id>`). Run `curl -sI` with no `Referer` and keep the first candidate that returns `200` with `content-type: image/*`.
  - Also run `curl -sI` on the original URL with no `Referer` to confirm the image proxy will not be hotlink-blocked.
  - Write the result into the test file as a comment: evidence, chosen form, or "no expiry".
  - If URLs are signed but no unsigned form works, stop and report to the user.
- [ ] **Step 3: Write the failing tests** in `test/xiaohongshu.test.ts`. Run `extractPage` and `buildMarkdown` on each fixture, and parse `article.readableContent` with JSDOM.
  - `multi-image`:
    - `images` equals the recorded ordered list (after the Step 2 rewrite, if any);
    - every `src` starts with `https://`;
    - each description line is its own `<p>`, with an empty line kept as a paragraph break;
    - topic tags keep their `<a href>`;
    - `article.title` equals the note title and does not end with ` - 小红书`;
    - `article.author` is the author nickname;
    - `article.imageUrl` is the first image;
    - the text contains neither the date (e.g. `10-21`) nor the IP region;
    - `suggested === 'article'`;
    - the Markdown contains every image URL and one line per description line.
  - `location`: the last paragraph is `📍 <place>`.
  - `modal`:
    - same image/title assertions for the open note;
    - no image URL from a feed card appears (Review Focus 1);
    - `article.url` keeps the note URL.
  - `video`: the poster image and the text are present, and there is no `<video>`/`<iframe>`.
  - `short`: `suggested === 'article'`.
  - `the handler does not match xiaohongshu.com.evil.example or fakexiaohongshu.com`.
- [ ] **Step 4: Run the tests to verify they fail.** Run `pnpm test`. Expected: the xiaohongshu tests FAIL.
- [ ] **Step 5: Implement `xiaohongshu.preParse`.**
  1. Find the note container whose note id matches the URL path (`/explore/<id>` or `/discovery/item/<id>`).
  2. Rebuild the body as `<article>`:
     - one `<figure><img src></figure>` per distinct image, in order, with swiper duplicates excluded;
     - description paragraphs split on newlines, keeping inline tag links;
     - `<p>📍 <place></p>` when a place exists.
     - Do not add an `<h1>`: the reader shows the title separately, and Readability drops duplicate titles anyway.
  3. If the DOM lacks images on a directly opened page, fall back to the inline initial-state JSON only when its note id equals the URL's id. In the modal case, keep whatever the DOM has.
  4. Return `{ title, author, imageUrl: first image, suggested: 'article' }`. If the title is empty, fall back to `document.title` with ` - 小红书` removed.
  5. Apply the Step 2 URL rewrite.
- [ ] **Step 6: Run the tests to verify they pass.** Run `pnpm test && pnpm typecheck && pnpm lint`. Expected: PASS.
- [ ] **Step 7: Commit.** `git add -A src/content-handler && git commit -m "feat(content-handler): add Xiaohongshu note handler"`

---

### Tasks 6–10: DOM handlers for the other sites

Every task below follows the same five steps as Task 5 Steps 1, 3, 4, 5 and 6, and finishes with its own commit:

1. Capture the fixtures.
2. Write the failing tests against `extractPage` / `buildMarkdown`.
3. Watch them fail.
4. Implement `preParse` and register the handler in `SITE_HANDLERS`.
5. Run `pnpm test && pnpm typecheck && pnpm lint` to see them pass.

Each test file also asserts that a look-alike domain does not match. The selectors come from omnivore and must be confirmed against the fixture.

#### Task 6: 微信公众号 (`weixin.ts`, matches `mp.weixin.qq.com`)
- Fixture `article.html`: an article with images below the first screen (lazy).
- Tests:
  - every image appears with its `data-src` URL;
  - no `<img src="data:…">` remains (Review Focus 3);
  - every `src` starts with `https://`;
  - the text excludes the account profile card, the QR-code block and the bottom area;
  - `article.author` equals `#js_author_name` (or `#js_name`);
  - `suggested === 'article'`.
- Also run `curl -sI` on one `mmbiz.qpic.cn` image with no `Referer`, and record the result as a comment.
- Implementation:
  - copy `data-src` to `src`;
  - remove `.rich_media_title`, `.rich_media_meta_list`, `#js_row_immersive_cover_img`, `.profile_container`, `.profile_card_container`, `#content_bottom_area`, `.rich_media_area_extra`, `#js_pc_qr_code`;
  - return `{ author, suggested: 'article' }`.
- Commit: `feat(content-handler): add WeChat article handler`

#### Task 7: Wikipedia (`wikipedia.ts`, matches `wikipedia.org`)
- Fixtures `en.html` and `zh.html`: articles with an infobox, footnotes and section edit links.
- Tests:
  - no `[edit]` / `[编辑]` link text;
  - no `sup.reference` markers;
  - infobox text still present if Readability keeps it (assert one infobox value);
  - `article.title` equals `#firstHeading` text, without ` - Wikipedia` / ` - 维基百科…`.
- Implementation: remove `.mw-editsection` and `sup.reference`; return `{ title }`.
- Commit: `feat(content-handler): add Wikipedia handler`

#### Task 8: 知乎 (`zhihu.ts`, matches `zhihu.com`)
- Fixtures `question.html` (at least two answers, lazy images) and `zhuanlan.html` (`zhuanlan.zhihu.com` article).
- Tests:
  - question page:
    - text contains the question title and both answers;
    - each answer has its author name;
    - no `(N 封私信` prefix or ` - 知乎` suffix in `article.title`;
  - both pages: images use the `data-original`/`data-actualsrc` URL, no `data:` `src` remains, no `<noscript>` duplicate images;
  - zhuanlan: `article.title` equals the article title.
- Implementation:
  - question/answer pages: rebuild the body from the question header plus the answer items, with author names;
  - zhuanlan: lazy-image fix only;
  - return `{ title }`.
- Commit: `feat(content-handler): add Zhihu handler`

#### Task 9: Medium (`medium.ts`, matches `medium.com`) and GitHub (`github.ts`, matches `github.com`)
These go in separate commits within the same task, because both are small.
- **Medium**:
  - Fixture `picture.html`. Test: every `<picture>` became one `<img>` whose `src` is the widest `srcset` candidate. Assert it for one known image.
  - Implementation: replace each `<picture>` with that `<img>`.
  - Commit: `feat(content-handler): add Medium handler`.
- **GitHub**:
  - Fixtures `repo.html` (repo home with README) and `issue.html`.
  - Tests:
    - repo: the text is the README only (no file list or sidebar);
    - `article.author` equals the `[itemprop=author]` text;
    - `article.title` lacks `GitHub - owner/`;
    - issue: the result `deepEqual`s `extractPage` with `handlers: []`.
  - Implementation: when an `<article>` exists, replace the body with it and return `{ author, title }`.
  - Commit: `feat(content-handler): add GitHub handler`.

#### Task 10: Stack Overflow (`stack-overflow.ts`, matches `stackoverflow.com`)
- Fixture `question.html`: a question with an accepted answer, a second answer and comments.
- Tests:
  - section headings `Question: <n> votes`, `Accepted Answer: <n> votes` and `Answer: <n> votes` appear in order, with the fixture's vote numbers;
  - author, reputation and at least one comment text appear;
  - `article.title` equals `#question-header h1` text.
- Implementation: port omnivore's `parseText` / `parseAuthors` / `parseComments` with fixture-confirmed selectors. Do not leave dead helpers behind.
- Commit: `feat(content-handler): add Stack Overflow handler`

---

### Task 11: Apple News URL resolver

**Files:**
- Create: `src/content-handler/src/handlers/apple-news.ts`, `test/apple-news.test.ts`, `test/fixtures/apple-news/redirect.html`
- Modify: `src/handlers/index.ts`

**Interfaces:**
- Produces: `export const appleNews: SiteHandler`, matching `hostname === 'apple.news'`, with `resolve` only.

- [ ] **Step 1: Capture the fixture.**
  - Run `curl -sL -A '<desktop Chrome UA>' https://apple.news/AxjzaZaPvSn23b67LhXI5EQ` (the sample from omnivore's test). If it is dead, find another public `apple.news` link.
  - Save the HTML with the source comment, and record the publisher URL that the `span.click-here` link points to.
- [ ] **Step 2: Write the failing tests.**
  - `resolves to the click-here link`: the mock returns the fixture body, and `resolveUrl` returns the recorded URL.
  - `returns the input when the link is missing or not http(s)`: use mock bodies without the span, and with `href="javascript:…"`.
  - `sends the desktop User-Agent header`: assert the mock received it.
- [ ] **Step 3: Run the tests to verify they fail.** Run `pnpm test`. Expected: FAIL.
- [ ] **Step 4: Implement.** `resolve` fetches with the UA header and `ctx.signal`, then extracts the `href` of the `<a>` that contains `class="click-here"` by regex, because the URL layer has no DOM. It returns `null` if nothing is found; `resolveUrl` validates http(s).
- [ ] **Step 5: Run the tests to verify they pass.** Run `pnpm test && pnpm typecheck && pnpm lint`. Expected: PASS.
- [ ] **Step 6: Commit.** `git add -A src/content-handler && git commit -m "feat(content-handler): add Apple News URL resolver"`

---

### Task 12: Final verification, docs and real-browser acceptance

**Files:**
- Modify: `CLAUDE.md` (Web Extension / SDK section: the handler list and per-site behavior in one line each; Copy Markdown runs handlers; the popup uses oEmbed for YouTube), `src/content-handler/README.md` (handler table, how to add a handler and its fixture), `src/web-extension/README.md`

- [ ] **Step 1: Full local verification.** Run `cd src/content-handler && pnpm test && pnpm typecheck && pnpm lint` and `cd ../web-extension && pnpm test && pnpm typecheck && pnpm lint && pnpm build`. Expected: all exit 0. Record the test counts.
- [ ] **Step 2: Update the docs** listed above.
- [ ] **Step 3: Real-browser acceptance (needs the user).**
  - Ask the user to reload the unpacked `src/web-extension/dist` in Chrome, with the local stack running.
  - Save one sample for each of the 9 sites.
  - Check in the reader:
    - 小红书: all images visible through the image proxy, line breaks kept, no title suffix;
    - 微信: lazy images present;
    - other sites: the per-site rules hold.
  - Copy Markdown once on the 小红书 note and inspect the clipboard.
  - The screenshot note was saved before. Until the Part B plan lands, it must be moved to the trash and purged before re-saving.
  - Record the outcomes, with failures stated as failures.
- [ ] **Step 4: Commit.** `git commit -am "docs: document content-handler SDK and site handlers"`
