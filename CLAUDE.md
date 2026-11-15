# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Quick Start

### Docker (Recommended)
```bash
podman compose -f deploy/docker-compose.yaml up -d --build
```

```bash
podman compose -f deploy/docker-compose.sqlite.yaml up -d --build
```

### Manual Setup

Run these commands from the repository root.

```bash
# Start the PostgreSQL API and apply migrations
dotnet run --project src/server/MiniCc.Api.csproj

# Start the API with a local SQLite file
Database__Provider=sqlite ImageProxy__SignatureKey=your-image-proxy-secret dotnet run --project src/server/MiniCc.Api.csproj

# Install Web, SDK, and extension dependencies
pnpm install --frozen-lockfile

pnpm dev                   # Start Next.js (http://localhost:4000)
pnpm build:ext             # Load src/web-extension/dist in Chrome
```

## Architecture Overview

### Microservices Architecture
- **API**: ASP.NET Core 10.0 with Clean Architecture (CQRS + DDD)
- **Frontend**: Next.js 15 App Router with TypeScript
- **Chrome Extension**: The TypeScript content-handler SDK extracts the loaded page with site-specific DOM handling, Mozilla Readability and DOMPurify. The website handles reading and management.
- **Database**: PostgreSQL 16 with zhparser or SQLite
- **Proxy**: Nginx routing (localhost:5000)

### Clean Architecture Layers
```
src/server/Core/
├── {Domain}/Application/   # CQRS handlers, DTOs
├── {Domain}/Domain/        # Entities, value objects
└── {Domain}/Infrastructure/# Repositories, configurations
```

### Key Patterns
- **CQRS**: Commands/Queries with MediatR handlers
- **Repository**: Generic repository pattern with UoW
- **DDD**: Aggregate roots, value objects, domain events
- **Authentication**: Dual mode (Cookie + AccessKey)

### Frontend Features
- **Article Display**: Enhanced readability with syntax highlighting
- **Code Blocks**: Prism.js integration with multiple language support
- **Copy Functionality**: One-click code copying with visual feedback
- **UI and Styling**: Arc UI primitives in `src/web/src/components/arc`, styled with CSS Modules and a small global reset. The Web frontend builds without Sass, Simple.css or Tailwind. Geist and Inter are bundled locally.
- **Theme**: Light, dark and system modes are available in the sidebar. The app stores the selection in `localStorage["app-theme"]` and applies it before first paint. The reader's system preference follows the resolved app theme.
- **Localization**: App controls and dates use Chinese (`zh-CN`). Shared confirmation dialogs use Arc Dialog with focus restored to the opener on dismissal.
- **Tag Management**: `/pages/tags` creates tags through an Arc Dialog (name 1–100, 10 shared colors in `lib/tag-colors.ts`, 409 shows 已有同名标签) and deletes from each card after confirmation. Each card's edit button opens the same dialog in edit mode to rename and recolor the tag. With a mouse, the card actions float over the card's end on hover instead of reserving space. Deletion and edits call `notifyLibraryMutation()` to refresh library caches and feeds. The reader and library card share an ARIA combobox with case-insensitive matching in `lib/tag-suggestions.ts`; its form opens in an Arc Popover so library cards keep their height. A new name offers 创建「X」 with a color, and the form stays open for the next tag. Selected color swatches draw a `box-shadow` ring, because `foundation.css` removes the outline from a pointer-focused button.
- **Performance**: Lazy loading and tree shaking optimization

### Core Entities
- **Article**: Full articles, feed summaries, and saved links share reading status (unread/reading/read), favorite, archive, tags, and soft deletion. Articles and feed summaries also track reading position.
- **Tag**: Many-to-many with articles, color-coded
- **Feed / FeedEntry**: RSS/Atom subscriptions, default tags, leased refreshes, article provenance and durable dismissed-entry deduplication
- **Highlight**: Text selections with notes and Ok/Pending/Orphaned anchor status
- **User**: Authentication with API keys and nullable JSON reader preferences

### API Structure
```
GET    /api/articles              # Filtered projection: { items, total, nextCursor }
GET    /api/tags/{id}/articles     # Same cursor page contract, view=all + tag AND filter
POST   /api/articles/content      # Save extracted HTML from the extension
POST   /api/articles/link         # Save a URL and its metadata from the extension
GET    /api/articles/{id}         # Get single article
PUT    /api/articles/{id}         # Update metadata
DELETE /api/articles/{id}         # Move article to trash
PATCH  /api/articles/{id}/state   # Explicit favorite/archive/read/unread values
POST   /api/articles/{id}/restore # Restore article with its annotations and state
POST   /api/articles/batch        # Explicit per-item actions: { succeeded, failed }
DELETE /api/trash                 # Permanently delete trash in batches: { purged }
GET    /api/settings/library      # Authenticated retention/undo configuration
GET    /api/account/reader-preferences # Complete reader preferences with defaults
PUT    /api/account/reader-preferences # Replace all five validated preference fields

POST   /api/articles/{id}/tags    # Add tag
DELETE /api/articles/{id}/tags    # Remove tag
POST   /api/articles/{id}/highlights  # Add highlight
GET    /api/tags                  # Tags with article counts (?search=)
POST   /api/tags                  # Create tag { name, color? }: 201 | 409 { existingTagId } | 400 { error }
PUT    /api/tags/{id}             # Edit tag { name, color }: 204 | 404 | 409 { existingTagId } | 400 { error }
DELETE /api/tags/{id}             # Delete tag and its ArticleTags and FeedTags associations
```

### Library State (P0)
- Apply `M1_LibraryStateModel` before using the state API. Startup `DbSeeder` idempotently normalizes legacy URLs, including trashed articles.
- Canonical URLs use standard escaped `Uri.AbsoluteUri` serialization, preserving encoded delimiters and the canonical root slash. Internal `NormalizedUrl` uses PostgreSQL `text` because normalization can expand an accepted 2000-character original URL; the external article URL API limit remains 2000. M3 also uses text for stored Article.Url and ImageUrl to preserve expanded feed-entry and cover URLs. Existing extension URL/image URL input limits remain 2000. PostgreSQL uses a nonunique hash index on `NormalizedUrl` for full-value equality even for long URLs. SQLite uses a B-tree index.
- Article and highlight global query filters hide trash; restoration and URL matching explicitly include it. Extension saves restore the oldest canonical URL match and preserve tags, notes and library state. Saving an article refreshes metadata and replaces a changed full body. Apply `M4_ContentRevision` before deploying this version.
- Favorite/archive/read toggle routes were removed. Use `PATCH /api/articles/{id}/state` with explicit values; `readingStatus` accepts `unread` or `read`. JSON enum values use camel case.
- `Library:TrashRetentionDays` defaults to 30 (1–365); `Library:UndoWindowSeconds` defaults to 10 (3–60). Both validate on startup and support normal environment overrides such as `Library__TrashRetentionDays`.
- State and restoration return `ArticleListItemDto`; detail and extension-save responses keep `ArticleDto`. Progress HTTP endpoints are documented under P2 below; trash cleanup is documented under P4 below.

### Library Views (P1)
- `GET /api/articles` accepts `view` (inbox default, all, favorites, archive, trash), `q` (max 200), comma-separated `status` (union), `tags` (AND, max 10), `source`, `feedId`, inclusive `savedFrom`, exclusive `savedTo`, `sort`, opaque `cursor`, and `pageSize` (1–50, default 20). `view=feeds`, `source=feed`, and `feedId` use real FeedEntry associations, including articles from cancelled subscriptions.
- Lists project metadata, tags, source summaries, and highlight counts without article bodies or highlight text. PostgreSQL search uses `mixed_zh_en` plus an escaped title-substring fallback. SQLite uses escaped `LIKE` on title, summary, author, and readable HTML. Total counts apply all filters before the cursor; keyset ordering uses timestamp + ID, null keys last. `read_desc` uses `LastReadAt`; trash always uses `DeletedAt DESC, Id DESC`, ignoring requested sort.
- `/light` and both old list queries were removed. `GET /api/tags/{id}/articles` delegates to `GetArticleListQuery`. The legacy tag metadata/detail endpoint retains its existing response for compatibility; the tag page now loads paginated article rows through the common list hook.
- The `/` library stores conditions in its URL; defaults are omitted. Search replaces history after 300 ms; view/filter changes push history. RSS filters and the expandable feed sidebar are enabled.
- `useArticleList(query)` exposes `{ items, total, loading, loadingMore, error, hasMore, loadMore, updateItem, removeItems }`. `updateItem(id, partialListItem)` keeps matching rows or removes them and decrements the count. Successful mutations advance a revision, cancel older requests, and start an acknowledged-state refresh; stale responses cannot restore removed rows or overwrite state/counts. Load more waits for reconciliation and retries an invalidated request from the resulting cursor.
- `lib/library-cache.ts` keeps at most five query snapshots for ten minutes in memory and session storage. Returning restores loaded pages and window scroll. A silent first-page metadata merge is used only when ordered first-page IDs and total remain stable and no known membership/sort-key edit needs reconciliation. Otherwise the hook re-fetches the loaded prefix and matching tail cursor, keeping the cached UI/scroll. `updateCachedArticle(id, partialListItem)` marks membership/order changes for reconciliation. Existing reader tag/highlight callbacks and global-highlight deletion patch full tags/highlightCount; reader body/highlight content stays intact. URL controls compose against the latest pending destination, ignore intermediate acknowledgements, and treat browser history/external URL changes as authoritative.
- Frontend verification: `pnpm test` runs Vitest/happy-dom. `E2E_BASE_URL=<isolated deployment> pnpm test:e2e` runs Playwright at 1280×800 and 375×812; it requires an isolated 65-article fixture with `.NET` and `阅读` tags and configured test login (`E2E_USERNAME` / `E2E_PASSWORD`). No default E2E server URL is provided.


### Reading Progress (P2)
- `PUT /api/articles/{id}/progress` accepts `{ textOffset, blockIndex, blockRatio, percent, sessionId, baseUpdatedAt, contentRevision? }`. It returns `{ progressUpdatedAt, readingStatus, lastReadAt, contentKind, contentRevision }`. A stale different-session base or mismatched revision returns 409 with `{ current: { textOffset, blockIndex, blockRatio, percent, progressUpdatedAt, contentRevision } }`. Invalid input returns 400 and missing or trashed articles return 404.
- `POST /api/articles/{id}/progress/reset` accepts `{ sessionId, contentRevision? }`, clears only the position and returns the new progress version, content kind and revision read under the article lock. A mismatched revision returns the same 409 snapshot as a progress save. Both endpoints accept session IDs up to 64 characters. Article details expose `contentRevision`, nullable `progress { textOffset, blockIndex, blockRatio, percent, updatedAt }`, distinct source IDs and titles in `feeds`, and `savedFromExtension`. Flat progress fields, version and session remain available after reset.
- `UpdateProgressCommandHandler` owns an existing `IUnitOfWork` transaction and reads through `GetByIdForProgressUpdateAsync` before checking/applying/saving. The transaction uses `SELECT … FOR UPDATE` on PostgreSQL and acquires the database write lock on SQLite. Different sessions cannot both accept the same stale base; unrelated state writes have no article-wide concurrency token. Server versions use microsecond precision and strictly increase.
- `lib/text-offsets.ts` provides UTF-16 offsets shared with highlights. `lib/reading-position.ts` enumerates innermost/atomic blocks plus anonymous inline runs; empty/image-only blocks have no text anchor and are ignored. Positions use a viewport 20% anchor and text-based percentages. `useReaderBlocks` recomputes offsets after body/highlight rendering; resize reads geometry only.
- The reader starts at the saved anchor (or top), corrects image movement for two seconds until user intent, and saves only after visible, active scrolling reaches 2% or block 2 and dwells for three seconds. Programmatic restoration cannot start reading; real input takes precedence over restoration and block-ref replacement preserves dwell. The memoized `ReadableContent` prevents status renders from replacing highlighted DOM. Decorations are tested to preserve concatenated article text.
- `useReadingProgress` uses one page session and `ProgressWriter` serializes/coalesces PUTs, reset, and explicit state acknowledgements. Before marking unread, already-qualified pending progress must finish successfully; an unresolved failure/conflict blocks that action, and the hook cancels old dwell and requires fresh scrolling afterward. Saves start at most every five seconds; hidden/pagehide/unmount flushes use credentialed JSON `fetch` with `keepalive`. A queued later position stays persisted while an earlier request finishes, then flushes in order. Browser termination can stop that continuation; stored pending data is retried on reopening with its original base and the new page session.
- Failed progress lives in `sessionStorage` at `progress:{id}` with the loaded body revision and retries after 2/5/15/30 seconds while visible. A same-body 409 pauses automatic writes until “跳转” restores the server anchor or “留在这里” adopts the server base. Visibility and focus revalidate the body before resuming retries. When the revision changes, the reader discards stale pending coordinates, pauses progress and highlight re-anchoring, and shows “刷新正文”.
- Reader actions merge acknowledged list metadata into detail without losing body/highlights, then patch library caches. Read/unread, favorite, archive, reset, deletion, back navigation, a text progress bar, synchronization status, and a dismissible end prompt are available. The sticky toolbar/conflict choices measure the mobile application header and toolbar heights; mobile actions retain44px touch targets. No automatic completion; reader preferences/settings and the table of contents are documented under P3 below.
- `e2e/reading-progress.spec.ts` creates and soft-deletes only its own temporary long articles. It covers active dwell/open-only, desktop/mobile/24px geometry restoration, independent state/reset, two sessions/conflict choices, failed-save recovery, and the end prompt. The original 65-article library fixture is retained.

### Reader Preferences (P3)
- Apply `M2_ReaderPreferences` before deploying P3. `Users.ReaderPreferences` is nullable `jsonb`; GET fills missing fields with font size 18, line height 1.8, standard width, system font and system theme. PUT requires all five fields: integer `fontSize` 16–28, `lineHeight` 1.5–2.2, `width` narrow/standard/wide, `fontFamily` system/serif, and `theme` system/light/dark/sepia. Invalid or incomplete objects return 400.
- `ReaderPreferencesProvider` lives inside `AuthProvider` and exposes `useReaderPreferences`. It initializes from `localStorage["reader-preferences"]`, adopts the server value after authentication, and applies edits locally immediately with a 500ms PUT debounce. A delayed GET cannot replace a newer edit. PUTs serialize and coalesce the latest intent; responses acknowledge revisions without replacing newer local values. Failed writes remain local and show “偏好未同步” plus retry in the settings dialog. A pending PUT remains a serialization barrier across logout/login; its acknowledgement is limited to its original auth epoch, and completion drains any eligible current-login edit.
- The `.reader` root owns font/line-height/width/font-family variables and resolved `data-reader-theme="light|dark|sepia"`. System theme subscribes to `prefers-color-scheme`. The palette covers the title, body, highlights and reader panels without changing sidebar colors. Maximum text widths are 640/760/920px, constrained by available space; mobile uses 100% with 16px side padding. Code keeps a separate monospace stack; images/video scale down and table/pre content scrolls horizontally.
- Before either a user preference edit or authoritative server adoption, the context invokes the mounted reader's `preserveLayout`. It captures the existing text anchor before geometry changes and restores after two animation frames, retaining the original anchor across rapid edits. Programmatic restoration cannot start a progress save; user input cancels stale restoration. Font/width changes reuse block offsets and the memoized `ReadableContent` preserves highlighted DOM.
- Settings use a 320px desktop dropdown and a bottom drawer below 768px, with a backdrop, dialog semantics, keyboard focus containment, Escape/outside dismissal, focus return, all five controls and default reset. Mobile controls retain 44px targets and the existing measured app-header/toolbar offsets.
- The TOC collects h1–h4 at the two highest levels actually present; fewer than two entries hides it. Existing IDs remain; missing heading IDs become collision-free `toc-{n}` values. Decorations add attributes/wrappers without changing article text. The TOC uses `IntersectionObserver` with `-20% 0px -70% 0px` and a geometry fallback for scroll jumps that skip the band. Links scroll to the heading and replace the URL hash without adding history. It is sticky beside the body at 1200px and wider, otherwise available from a toolbar drawer below 1200px, including tablet widths. Drawer presentation, backdrop and `aria-modal` follow the same live media query; settings retain their separate 768px breakpoint. Resizing the TOC to the desktop sidebar closes its drawer.
- The 3px progress bar stays fixed at the viewport top and exposes text-based percent through `aria-valuenow`; title, tags and highlight-list geometry remain outside the calculation. `e2e/reader-preferences.spec.ts` supplies isolated Chinese/English/technical fixtures, settings persistence through reload/article/fresh-browser login, highlight/text/anchor integrity, code/table overflow, TOC navigation, failed-write retry, and delayed server adoption/hydration checks. P3 verification: backend 276/276, frontend 71/71, TypeScript/lint/isolated production build exit 0, plus real HTTP preference round-trip and invalid-range/enum/incomplete-object 400 checks. All 32 unique combined library/progress/preferences Chromium cases passed on the current runtime: 31 passed in the final full run, and the remaining desktop TOC case passed in a targeted rerun after a test-only DOM-unmount wait correction. This is combined coverage, not a single 32/32 run. Parent visual review covered settings/TOC at 375px, 1024px and 1280px. Detailed evidence is in the P3 implementation report; browser coverage uses emulated viewports rather than physical devices.

### Batch Management and Trash (P4)
- `POST /api/articles/batch` accepts 1–200 distinct nonempty IDs and action `markRead`, `markUnread`, `favorite`, `unfavorite`, `archive`, `unarchive`, `addTags`, `removeTags`, `trash`, `restore` or `purge`. Tag actions require 1–20 distinct existing tag IDs; an unknown tag rejects the whole request with 400 before any article changes. Validation rejects unknown actions and empty/oversized inputs.
- The 200 response contains `succeeded` IDs and `failed: [{ id, reason }]`, with `notFound`, `inTrash`, `notInTrash` or `error`. Every item saves separately; failures clear the EF change tracker and processing continues. Cancellation propagates. State actions call the same explicit aggregate methods as single-item endpoints; trash/restoration preserve annotations, tags and reading position.
- `ArticlePurger.PurgeAsync(id, expiredBefore?, cancellationToken)` is the one permanent-deletion path shared by explicit batch purge, empty-trash and retention cleanup. It owns an `IUnitOfWork` transaction, locks the current row on PostgreSQL or holds the SQLite write lock, reads a fresh untracked snapshot, then rechecks trash/retention eligibility. A restored row is never deleted from a stale tracked snapshot. Related highlights cascade; shared tags remain. P5 dismisses associated FeedEntries and clears their ArticleId inside this existing transaction immediately before article deletion. On PostgreSQL a transaction-scoped advisory lock coordinates import, extension saves and purges.
- `DELETE /api/trash` returns `{ purged }`. `TrashCleanup` reads IDs in batches of 100 and isolates item failures. `TrashPurgeWorker` starts after one minute and repeats every 24 hours, using `Library:TrashRetentionDays` (default 30); manual empty-trash uses the same purge rules without an expiration cutoff.
- The library exposes desktop hover/focus checkboxes and a toolbar `选择` button. The sticky batch toolbar sits before the filters, below the measured mobile app header, and names both selected count and the current loaded scope, selects only those loaded IDs, disables while processing and reports progress. Requests and undo restoration split at 200 IDs; undo aggregates acknowledged results and retries only failed/unacknowledged IDs. Filter/view changes clear selection; a monotonic query-session generation prevents an older in-flight result or retry from restoring selection after an away-and-back navigation. Failed items remain visibly selected for exact-ID retry. Trash mode exposes only restore/purge actions; tag actions use existing tag IDs.
- `ToastProvider` is mounted in `ClientLayout`, supports at most three visible polite announcements, and loads authenticated library settings after login (fallback 10 seconds/30 days). Library cards, tag article lists and reader deletion share successful-ID undo. Reader deletion navigates to the library while its toast remains mounted. Undo calls batch restore only with acknowledged successful deletion IDs.
- `notifyLibraryMutation` marks inactive library caches for reconciliation and signals every mounted `useArticleList`. Active sessions advance their mutation revision, cancel older responses, patch acknowledged rows where possible and re-fetch the loaded prefix with server-authoritative filters, sort, total and tail cursor. Restoring outside the loaded range changes the matching total without appending an unbounded restored row list; full-text membership is never guessed from list titles. Detail body/highlights are never replaced by list metadata.
- Trash cards show deletion date and remaining retention days, plus restore/permanent-delete controls. Permanent deletion confirms irreversibility. Empty-trash fetches the entire trash count before confirming, including when the visible list has filters. `useAppHeaderOffset` shares measured mobile header geometry between the existing reader toolbar and new batch toolbar; batch/toast controls retain 44px minimum targets on screens up to 600px wide and on touch devices; desktop batch buttons use the compact Arc height.
- Local verification covers PostgreSQL partial-write recovery, all explicit actions/idempotence, preservation of shared tags/highlights/position, missing-tag rejection, retention eligibility, concurrent restore/purge and real HTTP contracts. Frontend unit coverage includes undo IDs, login settings refresh, partial retry selection, filter-change races and bounded restored-prefix pagination. `e2e/batch-trash.spec.ts` supplies six owned-fixture desktop/mobile scenarios. Final P4 verification: backend 291/291, frontend 81/81, TypeScript and isolated copied production build exit 0, affected-source lint exit 0 (only the inherited native-img warning), and a single final combined Playwright run 38/38 passed in 2.0 minutes. Parent independently verified real HTTP state/notes/progress restoration, idempotent favorite, partial failures, active-purge refusal and shared tags. Visual review at 375px and 1280px confirmed the selected/partial toolbar precedes filters, all actions remain visible/reachable, toast does not obscure actions and there is no horizontal overflow. Both final review verdicts are Approved; the dedicated acceptance database retains the original 65 active articles and 18 owned temporary trash items. Browser coverage uses Chromium emulated viewports, not physical devices. Detailed RED/GREEN and acceptance evidence is in the P4 implementation report.

## Development Commands

### Backend

Run these commands from the repository root. NuGet package versions are in `Directory.Packages.props`.

```bash
dotnet build
dotnet run --project src/server/MiniCc.Api.csproj
dotnet test
dotnet test --logger "console;verbosity=detailed"

# Single test project
dotnet test test/MiniCc.Api.Test/MiniCc.Api.Test.csproj
```

Run EF commands in `src/server`.

```bash
# Generate both migrations for model changes
Database__Provider=postgres dotnet ef migrations add <name> --context MiniCcDbContext
dotnet ef migrations add <name> --context SqliteMiniCcDbContext --output-dir Migrations/Sqlite

# Manually apply migrations if needed
Database__Provider=postgres dotnet ef database update --context MiniCcDbContext
dotnet ef database update --context SqliteMiniCcDbContext --connection "Data Source=/absolute/path/to/mini_cc.db"
```

### Frontend

Run these commands from the repository root. Next.js reads local environment settings from `src/web/.env.local`.

```bash
pnpm dev                       # Start development server (http://localhost:4000)
pnpm dev:debug                 # Next.js debug mode
pnpm dev:https                 # HTTPS development
pnpm build:web                 # Production build in src/web/.next
pnpm start                     # Start production server
pnpm lint                      # ESLint check
pnpm test                      # Vitest + happy-dom component regressions
```

### Content Handler SDK

The SDK takes a `Document` and clones it before site-specific DOM handling. It preserves the original HTML and uses a caller-supplied fetch for URL hooks. Markdown extraction is synchronous.

```bash
pnpm --filter @minicc/content-handler test
pnpm --filter @minicc/content-handler typecheck
pnpm --filter @minicc/content-handler lint
```

### Web Extension

The extension links to `@minicc/content-handler` through `workspace:*`.

```bash
pnpm build:ext                 # Production build in src/web-extension/dist
pnpm --filter minicc-extension dev
pnpm --filter minicc-extension lint
pnpm --filter minicc-extension test
pnpm --filter minicc-extension typecheck
```

### Docker Development

```bash
bash deploy/docker-build.sh           # Build the API and Web images
podman compose -f deploy/docker-compose.yaml up -d --build
podman compose -f deploy/docker-compose.yaml down
```

## Environment Setup

### Database Configuration
- `Database:Provider` accepts `postgres` (default) or `sqlite`, case-insensitively. Set `Database__Provider` in the environment.
- The API reads `MiniCC_Db` first, then `ConnectionStrings:DefaultConnection` for PostgreSQL or `ConnectionStrings:Sqlite` for SQLite.
- SQLite defaults to `Data Source=data/mini_cc.db`. The path is relative to the API content root and the API creates the directory on startup.

### External Services
- **Image Proxy**: Runs on port 5050 with secret key authentication. Set API `ImageProxy__SignatureKey` to the same imageproxy signing key and `ImageProxy__ProxyUrl` to `/imageProxy` (nginx) or `http://localhost:5050` (manual local setup). Compose shares `IMAGE_PROXY_SECRET` between both services.
- **Page collection**: The Chrome extension sends extracted HTML to `POST /api/articles/content` and link metadata to `POST /api/articles/link`. Extraction runs on the loaded page in Chrome.
- **CORS**: Configured for localhost:4000, localhost:5000

### Authentication
- **Cookie Authentication**: 30-minute sessions with sliding expiration
- **API Key Authentication**: Header-based for programmatic access
- **Encryption**: AES encryption for sensitive data

## SQLite Support

- Use SQLite for new deployments on local storage with a single API instance. Compose mounts the named `sqlite-data` volume at `/app/data`. The two Compose stacks share container names, so stop the active stack with `down` before switching.
- The API enables WAL and uses `BEGIN IMMEDIATE` for write transactions that read before updating. Feed refresh still fetches up to four feeds concurrently. SQLite stores timestamps as integer UTC ticks and reader preferences as JSON text.
- Article search uses escaped `LIKE` on title, summary, author and readable HTML. It ignores ASCII letter case and respects non-ASCII letter case. It scans these fields for both the list and the total. HTML tags can match, and text stored as entities may need the encoded spelling.
- SQLite uses binary ordering for tag names and feed titles. Column length limits are enforced by API validation.
- Stop the API and copy the database and any remaining WAL files together for backup. A host with the SQLite CLI can use `sqlite3 /path/to/mini_cc.db ".backup /path/to/backup.db"` while the API runs.
- After a migration is killed, startup can wait on `__EFMigrationsLock`. Stop the API and confirm no migration is running before clearing its rows with `DELETE FROM "__EFMigrationsLock"`.

## Clean Architecture Implementation

### Domain Layer
- **Aggregate Roots**: Article, Tag, Highlight, User
- **Value Objects**: Url, Content, TagColor
- **Domain Events**: ArticleEvents, HighlightEvents, TagEvents
- **Domain Services**: IArticleDomainService

### Application Layer
- **Commands**: SaveArticleContent, DeleteArticle, AddHighlight, etc.
- **Queries**: GetArticleById, GetHighlights, etc.
- **DTOs**: ArticleDto, HighlightDto, TagDto
- **Validation**: FluentValidation integration

### Infrastructure Layer
- **Repositories**: Generic repository with EF Core
- **Entity Configurations**: Fluent API configurations
- **Image URLs**: Server-side HMAC signing and HTML image rewriting through ImageProxyService

### API Layer
- **Controllers**: RESTful endpoints with attribute routing
- **Authentication**: Dual authentication schemes
- **Documentation**: OpenAPI and Scalar UI

### RSS / Atom Subscriptions (P5)
- Apply `M3_Feeds`: Feeds, FeedEntries, FeedTags, Highlights.AnchorStatus, and Article.Url/ImageUrl text storage. Full canonical feed URLs and entry keys remain text; persisted C# SHA-256 keys provide unique indexes (`FeedUrlHash`, `FeedId + EntryKeyHash`) without PostgreSQL B-tree key-width failures. Every hash match checks the full identity. API article URL inputs remain limited to 2000; feed inputs follow http/https validation and accept canonical preview URLs.
- Authenticated endpoints: `POST /api/feeds/preview`, `POST /api/feeds` (201 `{ feed, imported }`, 409 `{ existingFeedId }`), `GET /api/feeds`, `PATCH /api/feeds/{id}` (`title`, `tagIds`, `paused`), `POST /api/feeds/{id}/refresh` (409 if leased/paused), `DELETE /api/feeds/{id}` (unsubscribe, keep articles), and `POST /api/feeds/{id}/trash-articles` (`{ trashed, skipped }`, excludes plugin saves and other-feed sources). Preview returns at most five entries; first subscribe/resubscribe imports at most 20 new entries and permanently dismisses the excess.
- `FeedParser` uses Syndication with DTD ignored and no XML resolver, RSS2/Atom content precedence (Atom text/default content and summaries are HTML-encoded for literal rendering while OriginalContent stays raw), valid http/https links, nullable malformed dates, ttl/syndication intervals, safe Unicode field limits, summary classification, HtmlSanitizer and signed image rewriting. Feed/site/entry/link/content and nested XHTML xml:base values inherit in order; empty bases inherit rather than reset. Content without an explicit base keeps the entry URL fallback. `feeds` HttpClient follows at most five redirects. A linked 20-second deadline includes streamed body reading; byte counting aborts above 10MB. Byte decoding honors BOM first, then explicit HTTP charset, then XML declaration/default before string conversion. HTML discovery follows one alternate RSS/Atom link.
- `FeedRefreshWorker` ticks every 60 seconds. It atomically claims at most four due rows with a five-minute lease and ownership token, then runs independent scopes concurrently. PostgreSQL claims rows with `FOR UPDATE SKIP LOCKED`. SQLite claims them in one `UPDATE … RETURNING` statement. Completion locks and rechecks current status/ownership/expiry before importing or changing metadata; acknowledged pause/unsubscribe revokes in-flight work. Success/304 clears failures; supplied 304 ETag/LastModified headers replace stored validators while omitted ones are retained; 429/503 honors Retry-After and failures back off 30 minutes to 24 hours. Caller cancellation propagates and conditionally releases only its own lease.
- Imports run in one transaction per feed. PostgreSQL uses a feed-row lock and a transaction-scoped advisory lock for canonical article creation across feeds and extension saves. SQLite acquires the database write lock with `BEGIN IMMEDIATE`. Same-feed entry writes serialize before identity lookup; no swallowed unique exception can poison a transaction. Existing article content/tags/state are never changed by feed updates, and trash is never restored. Every permanent-deletion path dismisses linked entries through ArticlePurger.
- Saving a feed summary through the extension replaces its body and fills missing metadata. The reader matches Pending highlights against the refreshed body using UTF-16 text offsets. `PUT /api/highlights/{id}/anchor` applies offsets or `{ orphaned: true }` to Pending highlights and accepts `contentRevision`. Creating a highlight accepts the same optional revision. Both highlight operations return 409 with the current revision when it differs from the loaded body. Orphaned notes remain visible.
- `FeedsProvider` loads after authentication, refreshes every five minutes and after subscription/library mutations. `/pages/feeds` supplies two-step preview/confirmation, default tags, editing, pause/resume, manual refresh and confirmed unsubscribe/trash actions. A scanned `ArticleFeedMapping : IRegister` supplies feed metadata to the injected ArticleDto mapper; detail, extension-save and metadata-update responses share it. Explicit `source=extension` plus `feedId` is an AND filter in both URL parsing and the backend. Choosing RSS in the library exposes a per-feed selector with an all-feeds option; changing it preserves view/search/tags/status/dates/sort. Library feed filters/source projections remain body-free; reader source metadata and the summary banner use the same article state and caches.
- Verification: backend tests include real PostgreSQL storage/concurrent imports, plugin races, stale leases, pause/unsubscribe during fetch, 304/backoff, Unicode boundaries and real HTTP API status/state preservation; fetch tests include a real fast-header/stalled-body 20-second deadline. `e2e/feeds.spec.ts` uses unique mock run URLs, returned article IDs and finally cleanup so the 65-article baseline remains intact. Final P5 verification: actual .NET 9 + PostgreSQL backend 327/327, frontend 89/89, TypeScript exit 0, EF model alignment, real HTTP lifecycle and isolated production `next build --no-lint` exit 0. Scoped `eslint src e2e --ignore-pattern src/app/pages/account/page.tsx` passed with the existing ArticleCard image warning. The complete 42-case Chromium suite (38 prior + 4 P5) passed at 1280×800 and 375×812 in 2.2 minutes. Parent screenshot review verified source text, resolved/orphaned notes, bounded desktop navigation and the 126px mobile header. The 65 original active article IDs remained intact. P5 phase review and final whole-branch review are approved; final correction-wave evidence follows below. Evidence is recorded in the P5 implementation report.

### Saved Links and Markdown Copy
- `ContentKind.Link = 2` serializes as `"link"` and stores empty original and readable content. The existing schema supports it.
- `POST /api/articles/link` requires an HTTP(S) URL and title. The saved address is the page's original `document.URL`. The existing `UrlNormalizer` handles deduplication, keeping YouTube playlist parameters distinct. Link saves restore existing matches and preserve their content, metadata, and state. Full content saves upgrade `Link` and `SummaryOnly` and fill missing metadata. Re-saving a `Full` article refreshes the title and any nonempty author, summary and cover. A changed body after image-proxy rewriting increments `contentRevision`, resets progress with a strictly newer version, and marks highlights Pending. An identical body keeps progress, highlights and the revision.
- Link details show the domain, save date, available cover and summary, and an original-page button. Reading progress saves, resets, and new highlights on links return 400.
- The popup shows both `Save Article` and `Save Link`; the user chooses. Extraction returns `PageChoices { suggested, article, link }`: classification only picks the highlighted primary button, and `Save Article` is disabled when Readability finds no content. Settings open from the top-right gear icon.
- After an in-page navigation changes the URL beyond the fragment, extraction skips stale head metadata. The YouTube popup requests oEmbed for supported video, Shorts and playlist links with a five-second deadline. It uses the extracted metadata and the watch/Shorts thumbnail fallback when oEmbed is unavailable.
- `Copy Markdown` writes YAML front matter and the converted article or one Markdown link to the clipboard. It includes `title`, `url`, the local `date`, and any available `author` and page `tags`. Article conversion uses Readability, DOMPurify, `turndown`, and `turndown-plugin-gfm`. The extension declares `clipboardWrite`.

## Testing

### Test Structure
```
test/
└── MiniCc.Api.Test/            # API, article persistence, image signing tests

src/content-handler/test/       # Site fixtures and SDK regressions
src/web-extension/test/        # Popup, background and clipboard tests
```

### Testing Framework
- **xUnit**: Test framework
- **Moq**: Mocking library
- **FluentAssertions**: Assertion library
- **EF Core InMemory**: Existing isolated tests
- **Testcontainers.PostgreSql**: Real PostgreSQL 16 + zhparser migration, query filter, canonical matching, search, and HTTP state tests; requires a running Docker-compatible daemon. The shared fixture runs `DbSeeder` to initialize `mixed_zh_en` and apply migrations. Rootless Podman may require `DOCKER_HOST`, `TESTCONTAINERS_HOST_OVERRIDE=localhost`, and `TESTCONTAINERS_RYUK_DISABLED=true`.

The SQLite tests run with temporary local database files and require only the .NET SDK.

### Running Tests
```bash
# All tests
dotnet test

# Specific project
dotnet test test/MiniCc.Api.Test/

# SQLite and provider configuration tests, using temporary database files
dotnet test test/MiniCc.Api.Test --filter "FullyQualifiedName~Sqlite|FullyQualifiedName~Database"

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Docker Services

### Service Architecture
- **db**: PostgreSQL 16 with zhparser for Chinese search in the default Compose stack
- **minicc-api**: ASP.NET Core backend
- **minicc-web**: Next.js frontend
- **image-proxy**: Image proxy service for secure image loading
- **lb**: Nginx load balancer (port 5000)

### Service Communication
- **Internal**: Services communicate via Docker network
- **External**: All external traffic through nginx (port 5000)
- **Database**: Persistent volume for data storage

## Code Quality Standards

### Backend Standards
- **C#**: Modern C# with nullable reference types
- **Clean Architecture**: Strict separation of concerns
- **Dependency Injection**: Constructor injection pattern
- **Logging**: Serilog with structured logging
- **Validation**: FluentValidation for request validation

### Frontend Standards
- **TypeScript**: Strict type checking
- **React**: Functional components with hooks
- **Styling**: Arc UI and CSS Modules with light, dark, and system themes
- **Performance**: Code splitting and lazy loading
- **Accessibility**: WCAG compliant components

### Security Practices
- **Input Validation**: All user input validated
- **XSS Protection**: Content sanitization with DOMPurify
- **CORS**: Restricted to trusted origins
- **Authentication**: Secure cookie handling
- **Secrets**: Environment variable configuration

## Performance Considerations

### Database Optimization
- **Article Search**: PostgreSQL tsvector or SQLite substring matching
- **Chinese Support**: zhparser for Chinese text segmentation
- **Indexing**: Proper indexes on frequently queried fields
- **Connection Pooling**: EF Core connection management

### Frontend Optimization
- **Code Splitting**: Next.js automatic code splitting
- **Image Optimization**: Lazy loading and proxy service
- **Caching**: Browser and CDN caching strategies
- **Bundle Analysis**: Regular bundle size monitoring

## Troubleshooting

### Common Issues
- **Database Migrations**: Generate both migrations for schema changes. Use `--context` to select a provider when running `dotnet ef`.
- **Port Conflicts**: Ensure ports 4000, 5000, 5050 are available
- **Docker Issues**: Use `podman` instead of `docker` if configured
- **CORS Problems**: Verify frontend URL in CORS configuration

### Development Tips
- **Hot Reload**: Both backend and frontend support hot reload
- **Debugging**: Use `pnpm dev:debug` for frontend debugging
- **Database**: Use pgAdmin or DBeaver for database management
- **API Testing**: Use Scalar UI at http://localhost:5000/scalar
### Final whole-review correction wave
- Focused corrections to reader body/reset coherence, inherited XML bases, XML byte encodings, the library feed selector and the 64-character session boundary passed local backend 340/340, frontend 96/96, TypeScript and scoped lint (existing ArticleCard image warning only). No schema or dependency change was needed; the progress success response adds the body kind from its locked transaction.
- Final corrected-runtime acceptance passed all 46 Chromium cases (prior 42 plus desktop/mobile cases for unseen completion during reset/state actions and feed selection preserving the Favorites filter intersection) in 2.3 minutes. Parent independently verified backend 340/340 and frontend 96/96, TypeScript, scoped lint, isolated production `next build --no-lint`, EF model alignment, real HTTP Latin1/XML-base identity and lifecycle checks, and 64/65-character save/reset boundaries. The original 65 active article IDs remained intact. Whole-diff review passed the specification and quality checks. See `/tmp/mini-cc-implementation/final-fix-report.md` for exact RED/GREEN and environment evidence.
