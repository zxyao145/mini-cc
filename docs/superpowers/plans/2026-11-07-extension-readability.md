# Extension Readability Migration Implementation Plan

**Goal:** Collect articles exclusively through Chrome, with browser-side extraction and no Node readability service.

**Architecture:** The content script clones the current document and uses the official Mozilla Readability package plus DOMPurify. The background worker sends the extracted HTML and original HTML to `POST /api/articles/content`. The .NET API validates and stores that content and signs image-proxy URLs without fetching or extracting pages.

**Constraints:** Preserve existing articles and database schema. Preserve image-proxy signing on the server. Remove web URL collection and the obsolete server fetch/extraction path. Keep unrelated code unchanged. Use the existing checkout. Do not stage or commit changes.

## Task 1: Browser extraction and saving

- [x] Add real-DOM tests for extraction, unchanged source DOM, metadata/relative URLs, sanitization, and unreadable pages; run them red before implementation.
- [x] Implement `extractArticle(document: Document): ExtractedContent` in `src/web-extension/src/extract-article.ts`.
- [x] Extend `ExtractedContent` with `originalContent`, `readableContent`, `summary`, and `imageUrl`; retain URL, title, and author.
- [x] Route `saveArticle` messages through `background.ts`, using the configured API URL and API key. Keep credentials out of page messages.
- [x] Build and type-check the extension, then update its tracked distribution files.

## Task 2: Direct server persistence and image signing

- [x] Add failing tests for accepting extracted content, rejecting empty content, deriving text length, URL deduplication, and signed image/srcset URLs.
- [x] Extend `SaveArticleContentCommand` with required `ReadableContent`; preserve original HTML and existing metadata fields.
- [x] Replace remote extraction in `ArticleDomainService` with direct content creation and server image rewriting.
- [x] Remove the URL-only controller action/command/handler and replace its tests with content-save tests.
- [x] Remove the Readability HTTP client/options, Node service files, and orphaned backend page-fetch project/tests/references.
- [x] Replace `ReadabilityApi` configuration with `ImageProxy` configuration; pass the same image signature secret to API and image proxy in Compose.

## Task 3: Reading-only website

- [x] Remove the add-URL button/modal/form, client API method, request type, and their now-unused imports/state/styles.
- [x] Update the empty library message to direct users to the Chrome extension.
- [x] Verify the website with TypeScript and targeted ESLint; leave unrelated existing lint failures unchanged.

## Task 4: Deployment, documentation, and end-to-end validation

- [x] Remove readability service build/start references and document Chrome-only collection and local image signing configuration.
- [x] Run `DOTNET_ROLL_FORWARD=Major dotnet test mini-cc.slnx`, extension tests/type-check/build, frontend type-check, and diff review.
- [x] Stop the old readability process, restart the local API with image-proxy settings, refresh the installed extension, and save/read/delete a temporary article using the built extension scripts in an isolated Chromium browser with no listener on port 5002. The installed extension was reloaded during the follow-up diagnosis and an actual Chrome popup save was verified successfully; existing article tabs need a refresh for the updated content script.
- [x] Review the final diff for correctness, regressions, and leftover service references.

## Verification evidence

- Backend: 160 tests passed (the existing EF Core Relational version warning remains). MVC record-validation and entity-containing image URLs have regression coverage.
- Extension: 7 tests, TypeScript, ESLint and production build passed (`NODE_USE_ENV_PROXY=1` used for the manifest schema download in this environment).
- Frontend: TypeScript and targeted ESLint passed; two existing hook-dependency warnings remain. Existing account-page lint errors still block the full production build.
- Runtime: API restarted on 5001, website running on 3000, imageproxy on 5050, no listener on 5002. Valid content POST returns 201; missing readableContent returns 400; URL-only POST returns 405.
- Browser: executed built content script against a real DOM and built worker against real HTTP with a mocked Chrome messaging/storage boundary. Source DOM unchanged; navigation and executable attributes absent; saved article readable in the website; signed cover and responsive images loaded. Temporary article removed.
- Read-only review identified record validation and URL entity encoding issues; both reproduced and fixed. No commits or staging.

## Follow-up: Chrome worker reload

The save connection error came from the new popup being read from disk while Chrome still had the old service worker registered (only action.onClicked/runtime.onInstalled). Reloaded the installed unpacked extension in Chrome. The actual popup saved a uniquely named local test article successfully, and API readback confirmed the extracted body. Removed the temporary article and test server. No source change was needed.
