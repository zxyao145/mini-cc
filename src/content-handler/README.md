# MiniCC content handler

`@minicc/content-handler` extracts article and link data from a `Document` and applies site-specific metadata. The extension consumes the package's TypeScript sources directly through a `workspace:*` dependency.

## Public API

- `extractPage(document, loadedUrl)` returns `PageChoices` with a suggested kind and available save options.
- `buildMarkdown(document, loadedUrl, now?)` copies the same page as Markdown. It runs synchronously.
- `htmlToMarkdown(html)` converts an HTML fragment to Markdown.
- `resolveUrl(url, { fetch })` resolves supported short links. On failure or timeout, it returns the input URL.
- `preHandleUrl(url, { fetch })` returns optional site metadata. On failure or timeout, it returns `null`.
- `mergeOverrides(choices, overrides)` validates and applies metadata to the available save options.
- `assertHttpPageUrl(url)`, `hostMatches(url, domain)`, and `validImageUrl(raw, baseUrl)` validate or match URLs.
- `SiteHandler`, `SiteOverrides`, `FetchContext`, `ArticlePage`, `LinkPage`, `ExtractedPage`, `PageChoices`, and `ARTICLE_LIMITS` define the shared extraction contracts and field limits.

DOM extraction works on a clone and preserves the original document. The SDK operates independently of Chrome APIs and takes an injected fetch for network requests.

## Development

Run these commands from the repository root.

```sh
pnpm install --frozen-lockfile
pnpm --filter @minicc/content-handler test
pnpm --filter @minicc/content-handler typecheck
pnpm --filter @minicc/content-handler lint
```
