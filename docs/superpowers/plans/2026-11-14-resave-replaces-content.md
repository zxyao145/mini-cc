# Re-save Replaces Content Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When the extension's "Save Article" re-saves a URL that already has full content, replace the body and title with the newly captured version. Readers that are already open must stop writing coordinates computed against the old body.

**Architecture:** A new `Article.ContentRevision` integer is incremented by every `ReplaceContent` call. The detail DTO, progress save/reset responses and 409 `current` body expose it. `SaveArticleContentCommandHandler` gets a Full→Full branch that reuses the existing replacement steps. The reader's "body replaced" protection is currently keyed on `summaryOnly → full`; it is generalized to "revision changed".

**Tech Stack:** ASP.NET Core 9, EF Core + PostgreSQL 16, xUnit/Moq/FluentAssertions, Testcontainers; Next.js 15, Vitest/happy-dom, Playwright.

**Spec:** `docs/designs/2026-11-14-site-content-handlers-design.md`, Part B: §3.5, the Part B rows of §1.3, §4 and §5. Part A is a separate plan, `docs/superpowers/plans/2026-11-14-content-handler-sdk.md`. The two plans are independent; Part A ships first.

## Global Constraints

- Only `POST /api/articles/content` changes behavior. `POST /api/articles/link` and feed imports never change existing content.
- **Full → Full re-save:**
  - When the image-proxy-rewritten readable content differs from the stored readable content, call `ReplaceContent(…, ContentKind.Full)`, reset progress with a strictly newer microsecond version, and set every highlight to `Pending`.
  - The title is always replaced. Author, summary and cover are replaced only when the new value is non-empty (not null or whitespace).
  - When the body is identical, keep progress, highlights and revision unchanged; only the metadata refresh applies.
- `SummaryOnly` / `Link` upgrades keep their current behavior (fill missing metadata only). They do bump the revision, because `ReplaceContent` now does that.
- Always preserved: tags, notes, favorite, archive and reading status. Restoring from trash and setting `SavedFromExtensionAt` only when null both remain.
- Migration name: `M4_ContentRevision`. Column `ContentRevision integer NOT NULL`, default `0` for existing rows. Do not edit `ArticleConfiguration.cs`, which is UTF-16 LE/CRLF; the migration's `AddColumn` default is enough. If you must edit that file, keep its encoding.
- JSON property name: `contentRevision`, camelCase via the existing serializer settings.
- Reader banner text, replacing both summary and full cases: `正文已更新，请刷新后继续阅读。旧正文的进度已暂停同步。`. The button keeps the label `刷新正文`.
- Backend test command: `DOTNET_ROLL_FORWARD=Major dotnet test test/backend/MiniCc.Api.Test/MiniCc.Api.Test.csproj --filter "FullyQualifiedName~<Class>"`. Testcontainers needs a running Docker-compatible daemon; rootless Podman may need `DOCKER_HOST`, `TESTCONTAINERS_HOST_OVERRIDE=localhost` and `TESTCONTAINERS_RYUK_DISABLED=true`.
- Frontend commands, run from `src/frontend`:
  - `pnpm test -- <file>`
  - `pnpm exec tsc --noEmit`
  - `pnpm exec eslint src e2e --ignore-pattern src/app/pages/account/page.tsx`. The existing ArticleCard image warning is the only allowed output.
- The Playwright baseline of 65 original active articles must stay intact; tests use only articles they own.
- Commits use Conventional Commit messages.

## Review Focus

1. **A progress save in flight during a Full replacement:** the replacement waits for the row lock and resets with a strictly newer version. Test in Task 3.
2. **An open reader with already-Pending highlights:** it must not re-anchor them against the old DOM after a replacement. Test in Task 5.
3. **A 409 "Stay" after a replacement:** it must not adopt the new base for old-body coordinates; the reader becomes stale instead. Test in Task 4.
4. **"Save Link" on an existing Full article:** body and revision unchanged. Test in Task 3.
5. **A re-save with a whitespace-only author or summary:** the old values are kept. Test in Task 3.

---

### Task 1: `ContentRevision` on the Article aggregate, plus the migration

**Files:**
- Modify: `src/backend/MiniCc.Api/Core/ArticleNs/Domain/AggregatesModel/Article.cs`
- Create: `src/backend/MiniCc.Api/Migrations/<timestamp>_M4_ContentRevision.cs` (+ `.Designer.cs`); Modify: `Migrations/MiniCcContextModelSnapshot.cs` (both generated)
- Test: `test/backend/MiniCc.Api.Test/Core/ArticleNs/Domain/ArticleTests.cs` (the existing file with the `ReplaceContent` test at ~L327)

**Interfaces:**
- Produces:
  - `public int ContentRevision { get; private set; }`. `ReplaceContent(Content content, ContentKind kind)` increments it by 1.
  - `public void RefreshMetadata(string title, string author, string summary, string imageUrl)`:
    - throws `ArgumentException` on an empty or whitespace title, like the constructor;
    - sets `Title` to the trimmed title;
    - sets each of the other three only when not null or whitespace;
    - raises no domain event (mirrors `FillMissingMetadata`).

- [ ] **Step 1: Write the failing tests** in `ArticleTests.cs`:
  - `ReplaceContent_IncrementsContentRevision`: create an article and note its revision `r`. After `ReplaceContent` twice, the revision equals `r + 2`.
  - `RefreshMetadata_ReplacesTitleAndOnlyNonEmptyOptionalFields`: start with author `A`, summary `S`, image `https://x/i.png`. Calling `RefreshMetadata(" New ", "  ", "S2", "")` gives title `New`, author `A`, summary `S2`, image `https://x/i.png`.
  - `RefreshMetadata_RejectsEmptyTitle`: `"  "` throws `ArgumentException`.
- [ ] **Step 2: Run the tests to verify they fail.** Run the backend test command with `ArticleTests`. Expected: FAIL (members missing).
- [ ] **Step 3: Implement both members.** Then run `cd src/backend/MiniCc.Api && dotnet ef migrations add M4_ContentRevision`. Check that the generated `Up` adds `ContentRevision integer NOT NULL DEFAULT 0` to `Articles` and that `Down` drops it.
- [ ] **Step 4: Run the tests to verify they pass.** Run `ArticleTests` and `LibraryModelTests`; the latter checks column types and migration scripts. Update `LibraryModelTests` only if it enumerates the Articles columns. Expected: PASS.
- [ ] **Step 5: Commit.** `git add -A src/backend test/backend && git commit -m "feat(api): track article content revision"`

---

### Task 2: Expose `contentRevision` in the detail, progress, reset and 409 responses

**Files:**
- Modify: `src/backend/MiniCc.Api/Core/ArticleNs/Application/DTOs/ArticleDtos.cs` (`ArticleDto`), `Core/ArticleNs/Application/Commands/UpdateProgressCommand.cs` (`ProgressSnapshot`, `ProgressSaveDto`, handler returns)
- Test: `test/backend/MiniCc.Api.Test/Infrastructure/LibraryApiTests.cs`

**Interfaces:**
- Consumes: `Article.ContentRevision` (Task 1).
- Produces:
  - `ArticleDto.ContentRevision` (`int`), mapped by name through Mapster.
  - `ProgressSnapshot(…, DateTimeOffset? ProgressUpdatedAt, int? ContentRevision = null)`.
  - `ProgressSaveDto(…, ContentKind? ContentKind = null, int? ContentRevision = null)`.
  - `UpdateProgressCommandHandler` fills `ContentRevision` from the locked row in the success return, the reset return and the conflict `Current`.
  - The controller's 409 body stays `{ current }`, which now includes `contentRevision`.

- [ ] **Step 1: Write the failing test** `Progress_ResponsesCarryContentRevisionFromLockedRow` in `LibraryApiTests.cs`, following `Progress_HttpConflictValidationAndResetDetailVersion` (~L210). Seed an article, then:
  - `GET` detail → `contentRevision` equals the seeded revision;
  - `PUT` progress → 200 body has the same `contentRevision`;
  - `POST` reset → same value;
  - a stale different-session `PUT` → 409 `current.contentRevision` is the same value.
- [ ] **Step 2: Run the test to verify it fails.** Run the backend test command with `LibraryApiTests.Progress_ResponsesCarryContentRevisionFromLockedRow`. Expected: FAIL (property missing).
- [ ] **Step 3: Implement the DTO fields and the three handler returns.**
- [ ] **Step 4: Run the tests to verify they pass.** Run `LibraryApiTests`, `ReadingProgressPostgresTests` and `FeedApiTests`. Expected: PASS.
- [ ] **Step 5: Commit.** `git commit -am "feat(api): return content revision with detail and progress responses"`

---

### Task 3: Full → Full replacement in `SaveArticleContentCommandHandler`

**Files:**
- Modify: `src/backend/MiniCc.Api/Core/ArticleNs/Application/Commands/SaveArticleContentCommandHandler.cs`
- Test: `test/backend/MiniCc.Api.Test/Core/ArticleNs/Application/Commands/SaveArticleContentCommandHandlerTests.cs`, `test/backend/MiniCc.Api.Test/Infrastructure/LibraryApiTests.cs`

**Interfaces:**
- Consumes: `ContentRevision`, `RefreshMetadata` (Task 1); the DTO field (Task 2).

- [ ] **Step 1: Update the existing unit test and write the failing ones.**
  - Replace `Handle_WithDuplicateUrl_ReturnsExistingArticleWithoutOverwritingIt` (~L55) with:
    - `Handle_WithDuplicateFullUrlAndNewBody_ReplacesBodyAndTitleAndResetsProgress`:
      - readable content, title and revision change;
      - `ProgressUpdatedAt` is greater than before and all `Progress*` position fields are null;
      - every highlight is `Pending`;
      - tags, notes, `IsFavorite`, `IsArchived` and `ReadingStatus` are unchanged.
    - `Handle_WithDuplicateFullUrlAndSameBody_KeepsProgressHighlightsAndRevision`: identical readable content plus a different title → only the title changed; progress, highlight statuses and revision are unchanged.
    - `Handle_WithDuplicateFullUrl_KeepsOldAuthorAndSummaryWhenNewOnesAreBlank` (Review Focus 5).
  - Keep `Handle_WithExistingLink_UpgradesToFullAndFillsEmptyMetadata` (~L72) and `Handle_WithCanonicalUrlInTrash_RestoresOriginalWithNotesAndState` (~L104) passing unchanged. The Link upgrade still keeps its title.
  - HTTP / PostgreSQL tests in `LibraryApiTests.cs`:
    - `ExtensionResave_FullWithNewBody_ReplacesContentAndPreservesState`:
      - POST content, add a tag and a highlight with a note, favorite it, save progress;
      - POST the same URL with a new body and title;
      - the detail shows the new body/title and revision + 1, tags/note/favorite are kept, the highlight is `pending`, and `progress` is null with a newer `progressUpdatedAt`.
    - `ExtensionResave_TrashedFull_RestoresAndReplaces`.
    - `LinkSave_OnExistingFull_LeavesBodyAndRevisionUnchanged` (Review Focus 4).
    - `FullResave_WaitsForProgressLockAndResetsWithNewerVersion` (Review Focus 1). Mirror `FeedApiTests.FeedUpgrade_WaitsForProgressLockAndResetsWithNewerVersion` (~L77), with a Full article as the starting point.
- [ ] **Step 2: Run the tests to verify they fail.** Run the backend test command with `SaveArticleContentCommandHandlerTests` and with `LibraryApiTests`. Expected: the new tests FAIL.
- [ ] **Step 3: Implement.**
  1. Extract the existing version computation (current L47–50) into `private static DateTimeOffset NextProgressVersion(Article article)` and use it in both branches.
  2. In the `existingArticle != null` path, build `replacement` once via `CreateArticleFromContent`.
  3. Keep the non-Full branch as it is.
  4. For a Full article: if `replacement.ReadableContent != existingArticle.ReadableContent`, run `ReplaceContent` + `ResetProgress(null, NextProgressVersion(existingArticle))` + set all highlights `Pending`. Then always call `RefreshMetadata(replacement.Title, replacement.Author, replacement.Summary, replacement.ImageUrl)`.
- [ ] **Step 4: Run the full backend suite.** Run `DOTNET_ROLL_FORWARD=Major dotnet test test/backend/MiniCc.Api.Test/MiniCc.Api.Test.csproj`. Expected: all pass. Record the count.
- [ ] **Step 5: Commit.** `git commit -am "feat(api): replace full content when the extension re-saves an article"`

---

### Task 4: Reader treats a revision change as a replaced body

**Files:**
- Modify: `src/frontend/src/types/index.ts` (`Article.contentRevision: number`), `src/lib/progress-writer.ts` (`ProgressSaved.contentRevision?: number`, `ProgressSnapshot.contentRevision?: number`), `src/hooks/useReadingProgress.ts`, `src/app/pages/article/[id]/components/Client.tsx`
- Test: `src/hooks/useReadingProgress.test.tsx`, `src/app/pages/article/[id]/components/Client.test.tsx`

**Interfaces:**
- Consumes: the `contentRevision` JSON fields (Task 2).
- Produces:
  - `observeContent(next: { contentKind?: Article['contentKind']; contentRevision?: number }, current?: ProgressSnapshot): boolean`. It replaces `observeContentKind`: the hook returns `observeContent` instead, and every caller, including `Client.tsx`, is renamed. It returns `true` (and pauses exactly as today) when either:
    - `loadedKind === 'summaryOnly' && next.contentKind === 'full'` (existing rule), or
    - `next.contentRevision !== undefined && loadedRevision !== undefined && next.contentRevision !== loadedRevision`.
  - `storedProgress(id, contentKind, contentRevision)` also discards an entry whose stored `contentRevision` is defined and differs.
  - The persisted pending value includes `contentRevision: article.contentRevision`.

- [ ] **Step 1: Write the failing tests.**
  - In `useReadingProgress.test.tsx`, beside the summary tests (~L141–208):
    - `pauses full-body coordinates when a visibility refresh reports a newer content revision`;
    - `a 409 whose current carries a different revision pauses instead of offering Stay` (Review Focus 3): after the 409, `chooseConflict('stay')` sends no PUT;
    - `a successful save acknowledging a different revision pauses further writes`;
    - `a reset acknowledgement with a different revision requires refresh and does not adopt the version`;
    - `discards stored failed progress from an older content revision on reopening`.
  - In `Client.test.tsx`:
    - `shows the generic replacement banner and keeps the loaded revision through list/progress patches`: the banner text equals the Global Constraints string, and `patchArticle` keeps `contentRevision` unless `readableContent` changes.
  - Update existing assertions on the old banner text `正文已补全…` to the new string.
- [ ] **Step 2: Run the tests to verify they fail.** Run `pnpm test -- src/hooks/useReadingProgress.test.tsx src/app/pages/article/[id]/components/Client.test.tsx`. Expected: the new tests FAIL.
- [ ] **Step 3: Implement.**
  - Add `loadedRevision = useRef(article?.contentRevision)` and reset it with the other refs at the per-article effect (~L94).
  - Call `observeContent` from the visibility refresh (~L128), from `onSaved` (~L112), from the reset acknowledgement (~L100–109, in addition to the existing kind check), and in `chooseConflict` (~L196–206). In `chooseConflict`, check `current.contentRevision` first; keep the summary refetch path for summaries.
  - `Client.tsx` L80 passes `{ contentKind: updated.contentKind }`.
  - `patchArticle` (L52–56) keeps `contentRevision` exactly the way it keeps `contentKind`.
  - Replace the banner text (L156).
- [ ] **Step 4: Run the tests to verify they pass.** Run `pnpm test && pnpm exec tsc --noEmit`. Expected: PASS.
- [ ] **Step 5: Commit.** `git commit -am "feat(reader): pause stale coordinates on any content revision change"`

---

### Task 5: Highlight re-anchoring respects a stale body

**Files:**
- Modify: `src/frontend/src/app/pages/article/[id]/components/HighlightManager.tsx` (effect ~L137–152), `Client.tsx` (pass the prop)
- Test: `src/app/pages/article/[id]/components/HighlightManager.test.tsx`

**Interfaces:**
- Consumes: `progress.contentChanged` (Task 4).
- Produces: a `HighlightManager` prop `bodyStale: boolean`. When it is true, the Pending re-anchoring effect sends no `highlightApi.anchor` requests.

- [ ] **Step 1: Write the failing test** `does not re-anchor pending highlights while the body is stale` (Review Focus 2): render with a pending highlight and `bodyStale`. Assert `highlightApi.anchor` was called 0 times. Re-render with `bodyStale={false}` and assert it is called once.
- [ ] **Step 2: Run the test to verify it fails.** Run `pnpm test -- src/app/pages/article/[id]/components/HighlightManager.test.tsx`. Expected: FAIL.
- [ ] **Step 3: Implement.** Add the early return in the effect, add `bodyStale` to the effect's dependencies, and pass `progress.contentChanged` from `Client.tsx`.
- [ ] **Step 4: Run the tests to verify they pass.** Run `pnpm test && pnpm exec tsc --noEmit && pnpm exec eslint src e2e --ignore-pattern src/app/pages/account/page.tsx`. Expected: PASS; lint shows only the known ArticleCard warning.
- [ ] **Step 5: Commit.** `git commit -am "fix(reader): skip highlight re-anchoring against a replaced body"`

---

### Task 6: End-to-end scenario, docs and final verification

**Files:**
- Modify: `src/frontend/e2e/reading-progress.spec.ts`, `src/frontend/e2e/feeds.spec.ts` (banner text assertion), `CLAUDE.md`

- [ ] **Step 1: Write the Playwright test** `re-saving a full article from the extension pauses the open reader until reload` in `reading-progress.spec.ts`. Reuse its `beforeEach`/`afterEach` temporary-article helpers.
  1. Open the article and scroll past the dwell threshold so one PUT succeeds.
  2. `POST /api/articles/content` with the same `url`, a different body and a new title.
  3. Dispatch `visibilitychange`.
  4. Expect the banner text from Global Constraints.
  5. Count `PUT /progress` requests via `page.route` while scrolling for 6 seconds: expect 0.
  6. Click `刷新正文`. Expect the new body's first paragraph text and the new title.
  - Update the `feeds.spec.ts` banner text assertion to the new string.
- [ ] **Step 2: Run it.** Run `E2E_BASE_URL=<isolated deployment> pnpm test:e2e -- e2e/reading-progress.spec.ts e2e/feeds.spec.ts`. Expected: PASS on desktop and mobile. If no isolated deployment is available, say so in the report; do not claim it ran.
- [ ] **Step 3: Update `CLAUDE.md`.**
  - In Library State (P0), replace "Extension saves restore the oldest canonical URL match and retain existing full content, tags, highlights, notes, and state." with the new rule: identical body keeps everything; a different body replaces body/title, refreshes non-empty metadata, resets progress, sets highlights Pending and bumps `ContentRevision`.
  - In Saved Links, add the Full→Full case.
  - In Reading Progress (P2), note that responses carry `contentRevision` and that the reader pauses on any revision change.
  - Apply `M4_ContentRevision` before deploying.
- [ ] **Step 4: Final verification.**
  - Backend: run the full suite; also run `dotnet ef migrations has-pending-model-changes` (or the project's EF model alignment check) from `src/backend/MiniCc.Api` and expect no pending changes.
  - Frontend: `pnpm test`, `pnpm exec tsc --noEmit`, the scoped lint, and `pnpm build`.
  - Record all counts and exit codes.
- [ ] **Step 5: Real-browser acceptance (needs the user, after Part A ships).** In Chrome, re-save the user's previously broken 小红书 note **without deleting it first**. Confirm:
  - every image is present;
  - line breaks are kept;
  - the title has no ` - 小红书` suffix;
  - its tags and reading state survived;
  - a reader tab open during the re-save shows the banner, and the new body appears after `刷新正文`.
- [ ] **Step 6: Commit.** `git add -A src/frontend CLAUDE.md && git commit -m "test(reader): cover full-content re-save and document the new save rule"`
