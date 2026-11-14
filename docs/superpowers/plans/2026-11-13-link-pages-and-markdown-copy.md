# 链接型条目与复制 Markdown Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让插件能把非文章网页存成“链接”条目（只存地址和元数据），并能把 Readability 正文以带 front matter 的 Markdown 复制到剪贴板。

**Architecture:** 链接是 `ContentKind` 的第三个值 `Link = 2`，走新端点 `POST /api/articles/link`，去重、回收站、状态全部沿用文章的实现，不加迁移。插件在内容脚本里判断页面类型（文章 / 链接），并用“注入时地址 ≠ 当前地址”识别站内跳转，此时不读 `<head>` 元数据。Markdown 在内容脚本里用 Readability（`keepClasses: true`）+ DOMPurify + Turndown 生成，popup 写入剪贴板。

**Tech Stack:** ASP.NET Core 9 / MediatR / EF Core / PostgreSQL（xUnit、Moq、FluentAssertions、Testcontainers）；Chrome MV3 插件 TypeScript + Vite（`tsx --test` + jsdom）；Next.js 15（Vitest + happy-dom）。新增 `turndown`、`turndown-plugin-gfm`。

**Spec:** `docs/superpowers/specs/2026-11-13-link-pages-and-markdown-copy-design.md`（下文“规格 x.y”指它的章节；执行者两份都要读）

## Global Constraints

- 不新增数据库迁移。`ContentKind.Link = 2`，JSON 值为 `"link"`。
- `POST /api/articles/content` 的契约不变（仍必须带正文）。
- 已存的 `Full` 文章不得被降级；再次保存不得产生重复条目，不得丢失标签、高亮、笔记、状态、阅读进度。
- 链接保存只上传元数据，不上传整页 HTML。
- 地址原样保存（`document.URL`），不做站点专属规范化；去重只按现有 `UrlNormalizer`。
- 元数据不可信时（规格 3.1），不读 `<head>` meta 和 JSON-LD。
- popup 文案用英文，取值见规格 4.4 的表；网站前端文案用中文。
- 插件新增运行时依赖用固定版本（`pnpm add -E`）。
- “访问原文”必须带 `target="_blank" rel="noopener noreferrer"`。
- 复制与保存互不触发；复制不下载文件。
- `src/web-extension/dist/` 是纳入版本管理的构建产物：改了插件源码的任务要在结尾 `pnpm build` 更新它。
- `AddHighlightCommandHandler.cs` 是 UTF-16LE（带 BOM）编码，修改后必须保持原编码（用 `file` 命令确认）。
- 不 stage、不 commit（沿用本仓库既有计划的约定，提交由用户决定）。
- 后端 PostgreSQL 测试需要 Docker 兼容的守护进程；rootless Podman 的环境变量见 `CLAUDE.md` 的 Testing 一节。只有更新的 SDK 时加 `DOTNET_ROLL_FORWARD=Major`。

## Review Focus

规格隐含、但规格的验收场景没有覆盖的输入。每条都已在对应任务里加了测试。

1. 在 `file://`、`about:blank` 等非 http/https 页面点保存：应给出清楚的错误，不发请求。→ Task 3
2. 代码块正文里含三个反引号：围栏必须比正文里最长的反引号串更长，Markdown 不能被截断。→ Task 5
3. JSON-LD 是坏 JSON、数组、`@graph`，或 `@type` 是数组：应忽略或正确展开，不抛错。→ Task 3
4. 页面没有任何标题（`document.title` 为空且无 `og:title`）：链接标题回退为主机名；后端收到空白标题返回 400 而不是 500。→ Task 1、Task 3
5. 正文为空的链接条目出现在文库列表、搜索、批量操作里：这些接口照常工作。→ Task 1

---

### Task 1: 后端：链接条目与 `POST /api/articles/link`

**Files:**
- Modify: `src/backend/MiniCc.Api/Core/ArticleNs/Domain/AggregatesModel/ReadingStatus.cs:5`
- Modify: `src/backend/MiniCc.Api/Core/ArticleNs/Domain/AggregatesModel/Article.cs`
- Modify: `src/backend/MiniCc.Api/Core/ArticleNs/Domain/Services/IArticleDomainService.cs`、`ArticleDomainService.cs`
- Create: `src/backend/MiniCc.Api/Core/ArticleNs/Application/Commands/SaveLinkCommand.cs`、`SaveLinkCommandHandler.cs`
- Modify: `src/backend/MiniCc.Api/Controllers/ArticleNs/ArticlesController.cs`（在 `SaveArticleContent` 之后加动作）
- Test: `test/backend/MiniCc.Api.Test/Core/ArticleNs/Domain/AggregatesModel/ArticleTests.cs`
- Test: `test/backend/MiniCc.Api.Test/Core/ArticleNs/Application/Commands/SaveLinkCommandHandlerTests.cs`（新建，照 `SaveArticleContentCommandHandlerTests.cs` 的 Moq 写法）
- Test: `test/backend/MiniCc.Api.Test/Core/ArticleNs/Application/Commands/SaveLinkCommandValidationTests.cs`（新建，照 `SaveArticleContentCommandValidationTests.cs`）
- Test: `test/backend/MiniCc.Api.Test/Infrastructure/LinkApiTests.cs`（新建，声明 `public partial class LibraryApiTests`，这样能用 `LibraryApiTests.cs` 里私有的 `CreateFactory()` / `AuthenticatedClient()`；`FeedApiTests.cs` 是同样的做法）

**Interfaces:**
- Produces:
  - `enum ContentKind { Full = 0, SummaryOnly = 1, Link = 2 }`
  - `public static Article Article.CreateLink(Url url, string title, string author, string summary, string imageUrl)`
  - `Article IArticleDomainService.CreateLink(string url, string title, string author, string summary, string imageUrl)`
  - `record SaveLinkCommand(string Url, string Title, string? Author, string? Summary, string? ImageUrl) : IRequest<Result<ArticleDto>>`
  - `POST /api/articles/link` → 201 `ArticleDto`；校验失败 400

- [x] **Step 1: 写失败的领域与处理器测试**

```csharp
// ArticleTests.cs
[Fact] public void CreateLink_HasNoBodyAndLinkKind()
{
    var link = Article.CreateLink(Url.Create("https://www.youtube.com/watch?v=abc"), "Video", "", "desc", "");
    link.ContentKind.Should().Be(ContentKind.Link);
    link.ReadableContent.Should().BeEmpty(); link.OriginalContent.Should().BeEmpty(); link.Length.Should().Be(0);
    link.Summary.Should().Be("desc"); link.ReadingStatus.Should().Be(ReadingStatus.Unread);
    link.NormalizedUrl.Should().Be(UrlNormalizer.Normalize("https://www.youtube.com/watch?v=abc"));
}
[Fact] public void CreateLink_WithBlankTitle_Throws() =>
    FluentActions.Invoking(() => Article.CreateLink(Url.Create("https://example.com"), " ", "", "", "")).Should().Throw<ArgumentException>();

// SaveLinkCommandHandlerTests.cs
Handle_WithNewUrl_PersistsLinkMarkedAsSavedFromExtension
//   AddAsync 收到的 Article：ContentKind == Link，SavedFromExtensionAt != null，Title == command.Title
Handle_WithExistingFullArticle_DoesNotDowngradeOrOverwrite
//   existing.ContentKind == Full，ReadableContent 与 Title 不变，AddAsync 从未调用，result.Value.Id == existing.Id
Handle_WithExistingLink_KeepsExistingMetadata
//   用不同 title/summary 再存：existing.Title、existing.Summary 不变
Handle_WithTrashedArticle_RestoresIt
//   existing.MoveToTrash(...) 后保存：existing.DeletedAt == null
```

- [x] **Step 2: 运行，确认失败**

Run: `dotnet test test/backend/MiniCc.Api.Test/MiniCc.Api.Test.csproj --filter "FullyQualifiedName~ArticleTests|FullyQualifiedName~SaveLinkCommandHandlerTests"`
Expected: 编译失败（`CreateLink`、`SaveLinkCommand` 不存在）

- [x] **Step 3: 实现领域与处理器**

- `ContentKind` 加 `Link = 2`。
- `Article.CreateLink`：标题空白抛 `ArgumentException`（与 `Create` 一致）；`OriginalContent`、`ReadableContent` 为 `""`，`Length = 0`，`ContentKind = Link`；触发 `ArticleCreatedEvent`。不要放宽 `Content.Create` 的非空校验，另走一条构造路径。
- `ArticleDomainService.CreateLink`：`Article.CreateLink(Url.Create(url), title, author, summary, imageProxyService.GetUrl(imageUrl))`。
- `SaveLinkCommandHandler`：事务、`LockArticleCreationAsync`、`GetByUrlAsync(UrlNormalizer.Normalize(request.Url))`、取消与异常时回滚，全部照抄 `SaveArticleContentCommandHandler` 的结构。已有条目（任何类型，含回收站）只做 `Restore()` + `MarkSavedFromExtension(now)`，不改内容和元数据。没有才新建并 `MarkSavedFromExtension`。

- [x] **Step 4: 运行，确认通过**

Run: 同 Step 2。Expected: PASS

- [x] **Step 5: 写失败的校验与 HTTP 测试**

```csharp
// SaveLinkCommandValidationTests.cs — [Theory]，用 SaveLinkCommandValidator
("https://example.com/v", "Title", true)
("ftp://example.com/v", "Title", false)          // 只接受 http/https
("https://example.com/v", "", false)
("https://example.com/v", new string('t', 501), false)
("https://example.com/" + new string('a', 2000), "Title", false)
// 另测 Author 201、Summary 1001、ImageUrl 2001 均无效

// LinkApiTests.cs（partial LibraryApiTests）
SaveLink_CreatesSearchableLink_AndDeduplicatesByNormalizedUrl
//   POST /api/articles/link { url = "https://www.youtube.com/watch?v=<guid>", title = "Video", summary = "zqxlinktoken <guid>" }
//   → 201，contentKind == "link"，readableContent == ""，savedFromExtensionAt 非空
//   再 POST url + "&utm_source=x#frag" → 201，id 相同
//   再 POST url + "&list=PL1" → 201，id 不同（规格验收 15：地址原样保存，不合并）
//   GET /api/articles?view=all&q=zqxlinktoken → items 含该 id（Review Focus 5：按摘要搜到）
//   POST /api/articles/batch { ids = [id], action = "markRead" } → succeeded 含该 id（Review Focus 5）
SaveLink_RestoresTrashedItemAndKeepsState
//   存链接 → PATCH state { isFavorite = true } → DELETE → 再存 → 201，id 相同，deletedAt 为 null，isFavorite 为 true
SaveLink_OnFullArticle_DoesNotDowngrade
//   fixture.SeedArticleAsync() → 对它的 URL 存链接 → contentKind == "full"，readableContent == "readable"
SaveLink_RejectsInvalidInput
//   ftp URL → 400；缺 title → 400；title = "   " → 400（Review Focus 4）
```

- [x] **Step 6: 运行，确认失败**

Run: `dotnet test test/backend/MiniCc.Api.Test/MiniCc.Api.Test.csproj --filter "FullyQualifiedName~SaveLinkCommandValidationTests|FullyQualifiedName~LibraryApiTests.SaveLink"`
Expected: FAIL（缺少端点或校验器）

- [x] **Step 7: 实现命令校验与控制器动作**

```csharp
public record SaveLinkCommand(string Url, string Title, string? Author, string? Summary, string? ImageUrl)
    : IRequest<Result<ArticleDto>>;
```

`SaveLinkCommandValidator` 使用 FluentValidation 校验 HTTP(S) URL 和标题必填，URL 与 ImageUrl 最长 2000，标题 500，作者 200，摘要 1000。

控制器：`[HttpPost("link")] SaveLink([FromBody] SaveLinkCommand command)`，失败 `BadRequest(result.Error)`，成功 `CreatedAtAction(nameof(GetArticle), new { id = result.Value.Id }, result.Value)`（与 `SaveArticleContent` 相同）。

- [x] **Step 8: 运行，确认通过，并确认模型无变化**

Run: `dotnet test test/backend/MiniCc.Api.Test/MiniCc.Api.Test.csproj`
Expected: 全部 PASS
Run（在 `src/backend/MiniCc.Api/`）: `dotnet ef migrations has-pending-model-changes`
Expected: 报告没有待处理的模型变化

---

### Task 2: 后端：链接升级为文章；链接拒绝进度与高亮

**Files:**
- Modify: `src/backend/MiniCc.Api/Core/ArticleNs/Domain/AggregatesModel/Article.cs`
- Modify: `src/backend/MiniCc.Api/Core/ArticleNs/Application/Commands/SaveArticleContentCommandHandler.cs:41-51`
- Modify: `src/backend/MiniCc.Api/Core/ArticleNs/Application/Commands/UpdateProgressCommand.cs`（处理器，找到文章之后）
- Modify: `src/backend/MiniCc.Api/Controllers/ArticleNs/ArticlesController.cs:135`（`SaveProgress`）
- Modify: `src/backend/MiniCc.Api/Core/ArticleNs/Application/Commands/AddHighlightCommandHandler.cs`（UTF-16LE，保持编码）
- Test: `ArticleTests.cs`、`SaveArticleContentCommandHandlerTests.cs`、`Infrastructure/LinkApiTests.cs`

**Interfaces:**
- Consumes: Task 1 的 `ContentKind.Link`、`POST /api/articles/link`
- Produces: `public void Article.FillMissingMetadata(string author, string summary, string imageUrl)`（只填当前为空的字段）

- [x] **Step 1: 写失败的测试**

```csharp
// ArticleTests.cs
FillMissingMetadata_FillsOnlyEmptyFields
//   author 为空、summary 为 "kept" 的条目，FillMissingMetadata("New author", "new", "https://img/x")
//   → Author == "New author"，Summary == "kept"，ImageUrl == "https://img/x"

// SaveArticleContentCommandHandlerTests.cs
Handle_WithExistingLink_UpgradesToFullAndFillsEmptyMetadata
//   existing = _domainService.CreateLink(url, "Link title", "", "", "")
//   用 author "Jane"、summary "S" 保存正文 → ContentKind == Full，ReadableContent 为新正文，
//   Title == "Link title"（不覆盖），Author == "Jane"，Summary == "S"

// LinkApiTests.cs
ContentSave_UpgradesLinkToFull_KeepingStateAndTags
//   存链接 → PATCH state { isFavorite = true, isArchived = true } → 加一个标签
//   → POST /api/articles/content 同一 URL → 201，id 相同，contentKind == "full"，
//     readableContent 非空，isFavorite 与 isArchived 为 true，tags 含该标签
ProgressAndHighlights_AreRejectedForLinks
//   PUT /api/articles/{linkId}/progress（合法请求体）→ 400
//   POST /api/articles/{linkId}/progress/reset { sessionId = "s" } → 400
//   POST /api/articles/{linkId}/highlights（合法的 AddHighlightCommand 请求体）→ 400
//   PUT /api/articles/{Guid.NewGuid()}/progress → 仍是 404
//   之后 GET 该链接：progressUpdatedAt 为 null，highlights 为空
```

- [x] **Step 2: 运行，确认失败**

Run: `dotnet test test/backend/MiniCc.Api.Test/MiniCc.Api.Test.csproj --filter "FullyQualifiedName~FillMissingMetadata|FullyQualifiedName~UpgradesToFull|FullyQualifiedName~LibraryApiTests.ContentSave|FullyQualifiedName~LibraryApiTests.ProgressAndHighlights"`
Expected: FAIL

- [x] **Step 3: 实现**

- `SaveArticleContentCommandHandler`：升级条件由 `== ContentKind.SummaryOnly` 改为 `!= ContentKind.Full`；`ReplaceContent` 之后调用 `existingArticle.FillMissingMetadata(replacement.Author, replacement.Summary, replacement.ImageUrl)`（`replacement.ImageUrl` 已经过签名）。其余升级逻辑不动。这也使 `SummaryOnly → Full` 开始补空字段（规格 4.3）。
- `UpdateProgressCommandHandler`：文章是 `Link` 时回滚并返回 `Failure("Link items have no reading position")`。保存与重置共用这个处理器。
- `ArticlesController.SaveProgress`：`result.IsFailure` 时，错误含 `"not found"` 返回 404，否则 400（与同文件 `UpdateArticle` 的写法一致）。
- `AddHighlightCommandHandler`：取到的文章 `ContentKind == Link` 时返回 `Failure("Link items cannot be highlighted")`。控制器已把不含 `"not found"` 的失败映射为 400。
- 两条错误消息都不得包含 `not found`。`HighlightsController.Anchor` 不改（规格 4.3）。

- [x] **Step 4: 运行，确认通过**

Run: `dotnet test test/backend/MiniCc.Api.Test/MiniCc.Api.Test.csproj`
Expected: 全部 PASS（含原有的 SummaryOnly 升级与进度测试）
Run: `file src/backend/MiniCc.Api/Core/ArticleNs/Application/Commands/AddHighlightCommandHandler.cs`
Expected: 仍报告 UTF-16 little-endian

---

### Task 3: 插件：页面判断与链接提取（纯函数）

**Files:**
- Create: `src/web-extension/src/readable.ts`、`page-meta.ts`、`classify-page.ts`、`extract-page.ts`
- Modify: `src/web-extension/src/extract-article.ts`、`types.ts`
- Modify: `src/web-extension/src/popup.ts`（只把类型名 `ExtractedContent` 换成 `ExtractedPage`，行为不变）
- Test: `src/web-extension/test/page-meta.test.ts`、`classify-page.test.ts`、`extract-page.test.ts`（新建）；`extract-article.test.ts`、`background.test.ts`（补 `kind`）

**Interfaces:**
- Produces:

```ts
// types.ts
export interface ArticlePage { kind: 'article'; url: string; title: string; originalContent: string; readableContent: string; summary?: string; author?: string; imageUrl?: string }
export interface LinkPage { kind: 'link'; url: string; title: string; summary?: string; author?: string; imageUrl?: string }
export type ExtractedPage = ArticlePage | LinkPage;
export interface SavePageMessage { action: 'savePage'; content: ExtractedPage }

// readable.ts
export interface ReadableArticle { title: string; byline: string; excerpt: string; html: string; textLength: number }
export function parseReadable(document: Document, mode?: 'save' | 'markdown'): ReadableArticle | null

// page-meta.ts
export function isMetaTrusted(loadedUrl: string, currentUrl: string): boolean
export function metaContent(document: Document, ...selectors: string[]): string
export function extractImageUrl(document: Document): string
export function youtubeVideoId(url: URL): string | null
export function jsonLdArticles(document: Document): Record<string, unknown>[]

export function pageTitle(document: Document, metaTrusted: boolean): string
export function pageAuthor(document: Document): string

// classify-page.ts
export function isArticlePage(document: Document, readable: ReadableArticle, metaTrusted: boolean): boolean

// extract-page.ts
export function extractPage(document: Document, loadedUrl: string): ExtractedPage

// extract-article.ts
export function extractArticle(document: Document, readable: ReadableArticle): ArticlePage
```

- [x] **Step 1: 写失败的测试**

沿用 `extract-article.test.ts` 的 `JSDOM` 写法（`new JSDOM(html, { url })`，用完 `dom.window.close()`）。夹具需要三档正文：长文（去空白后 ≥ 500 字符）、中等（≥ 200 且 < 500）、短文（< 200）；每个测试先断言 `parseReadable(doc)!.textLength` 落在预期区间，再断言结果。

```ts
// page-meta.test.ts
isMetaTrusted('https://a.com/x?v=1', 'https://a.com/x?v=1#t=3') === true
isMetaTrusted('https://a.com/x?v=1', 'https://a.com/x?v=2') === false
isMetaTrusted('https://a.com/x', 'https://a.com/y') === false
youtubeVideoId(new URL('https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PL1')) === 'dQw4w9WgXcQ'
youtubeVideoId(new URL('https://www.youtube.com/shorts/abcdefghijk')) === 'abcdefghijk'
youtubeVideoId(new URL('https://www.youtube.com/feed/subscriptions')) === null
youtubeVideoId(new URL('https://example.com/watch?v=dQw4w9WgXcQ')) === null
extractImageUrl: og:image "/cover.png" → 'https://example.com/cover.png'；data: 或 javascript: → ''；超过 2000 字符 → ''
jsonLdArticles: 顶层数组、@graph、@type 为 ['Thing','BlogPosting'] 都能取到；
                坏 JSON 的 <script> 与 @type 为 'WebSite' 的对象被忽略且不抛错   // Review Focus 3

// classify-page.test.ts（第三个参数是 metaTrusted，true 表示文章）
YouTube watch 地址 + 长文 + og:type=article            → false       // 规则 2 的 URL 模式先于规则 3
普通地址 + og:type=video.other + 长文                  → false       // 规则 2
og:type=article + 中等                                 → true        // 规则 3
JSON-LD BlogPosting + 中等                             → true        // 规则 3
og:type=article + 短文                                 → false       // 不足 200，落到规则 4
无信号 + 中等                                          → false       // 规则 4
无信号 + 长文                                          → true        // 规则 5
metaTrusted=false: og:type=article + 中等              → false       // 规则 3 被跳过
metaTrusted=false: og:type=video.other + 长文          → true        // og:type 被忽略
metaTrusted=false: bilibili.com/video/BV1xx 地址 + 长文 → false       // URL 模式照常生效

// extract-page.test.ts（通过 extractPage 验证链接）
extractPage 可信: og:title 优先于 <title>；summary 取 og:description；author 取 meta[name=author]；imageUrl 取 og:image
extractLink 标题回退: 无 og:title 用 twitter:title；再无用 document.title；都没有用主机名     // Review Focus 4
extractLink 截断: title 500、summary 1000、author 200
extractLink 不可信: title === document.title；summary、author、imageUrl 均为 ''
extractLink YouTube 不可信: url 'https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PL1'
   → imageUrl === 'https://i.ytimg.com/vi/dQw4w9WgXcQ/hqdefault.jpg'，url 与 document.URL 完全相同
extractLink YouTube 可信且有 og:image → 用 og:image；可信但无 og:image → 用视频 ID 拼的地址
extractLink 的结果没有 originalContent、readableContent 键
extractPage: 链接 URL 超过 2000 字符 → 抛错 /too long/
extractPage: file:///tmp/a.html → 抛错 /http/                                            // Review Focus 1
extractPage: 长文博客 → kind 'article' 且有 readableContent；空页面 → kind 'link'（不抛错）；
             YouTube 地址 → kind 'link'；调用前后 document.documentElement.outerHTML 不变
```

- [x] **Step 2: 运行，确认失败**

Run（在 `src/web-extension/`）: `pnpm test`
Expected: FAIL（模块不存在）

- [x] **Step 3: 实现**

- `parseReadable` 在克隆文档上执行 Readability。复制 Markdown 时保留 class 和任务列表状态。解析无正文时返回 `null`。返回的 `html` 经过 DOMPurify 净化，`textLength` 为 Readability 返回的 `textContent` 去掉空白后的长度。
- `extractArticle(document, readable)` 接收已解析的正文并返回 `kind: 'article'`。文章和链接共用 `extractImageUrl`，按文档顺序取第一个封面 meta。
- `isMetaTrusted`：两个地址去掉 hash 后相等。
- `youtubeVideoId`：主机为 `youtube.com` / `www.youtube.com` / `m.youtube.com`，路径 `/watch` 取 `v` 参数，路径 `/shorts/<id>` 取 `<id>`；ID 须匹配 `/^[\w-]{11}$/`，否则 `null`。
- `jsonLdArticles`：返回 `@type` 含 `Article` / `NewsArticle` / `BlogPosting` / `TechArticle` 的对象。
- `MEDIA_URL_PATTERNS` 与判断顺序按规格 4.1：

```ts
const MEDIA_URL_PATTERNS = [
  /^https?:\/\/(?:www\.|m\.)?youtube\.com\/watch\b/,
  /^https?:\/\/(?:www\.|m\.)?youtube\.com\/shorts\//,
  /^https?:\/\/youtu\.be\//,
  /^https?:\/\/(?:www\.)?vimeo\.com\/\d+/,
  /^https?:\/\/(?:www\.)?bilibili\.com\/video\//,
];
```

- `pageTitle`：可信时 `og:title → twitter:title → document.title → 主机名`，不可信时 `document.title → 主机名`；trim。`pageAuthor`：可信时 `meta[name=author] → meta[property="article:author"]`，不可信时 `''`。
- `extractLink`：字段来源与长度限制按规格 4.2 的表；`imageUrl` 为空且 `youtubeVideoId(new URL(document.URL))` 有值时用 `https://i.ytimg.com/vi/<id>/hqdefault.jpg`。地址不是 http/https 抛 `Only http and https pages can be saved.`；超过 2000 字符抛 `This page URL is too long to save.`
- `extractPage` 先校验地址协议，再判断元数据是否可信并解析正文。有正文且满足 `isArticlePage` 时调用 `extractArticle(document, readable)`，其余页面走 `extractLink`。

- [x] **Step 4: 运行，确认通过**

Run: `pnpm test && pnpm typecheck && pnpm lint`
Expected: 全部通过；原有的 `extract-article` 与 `background` 测试在补上 `kind: 'article'` 后仍通过

---

### Task 4: 插件：保存链接全流程接通

**Files:**
- Modify: `src/web-extension/src/content.ts`、`background.ts`、`popup.ts`、`popup.html`、`popup.scss`
- Test: `src/web-extension/test/background.test.ts`
- Rebuild: `src/web-extension/dist/`

**Interfaces:**
- Consumes: Task 3 的 `extractPage(document, loadedUrl)`、`ExtractedPage`；Task 1 的 `POST /api/articles/link`
- Produces: `extractContent` 消息的响应为 `{ success: true, content: ExtractedPage }`；内容脚本模块顶层常量 `loadedUrl`（Task 5 复用）

- [x] **Step 1: 写失败的测试**

```ts
// background.test.ts
// 现有第一个用例改为：发往 /api/articles/content 的请求体等于 content 去掉 kind
test('background posts link pages to the link endpoint without article bodies', async () => {
  const link = { kind: 'link', url: 'https://www.youtube.com/watch?v=dQw4w9WgXcQ', title: 'Video', summary: 'S', author: '', imageUrl: '' } as const;
  // fetch 桩内断言：
  assert.equal(url, 'http://localhost:5001/api/articles/link');
  assert.deepEqual(JSON.parse(options?.body as string), { url: link.url, title: 'Video', summary: 'S', author: '', imageUrl: '' });
  // 响应
  assert.deepEqual(response, { success: true });
});
```

- [x] **Step 2: 运行，确认失败**

Run: `pnpm test`
Expected: 新用例 FAIL（仍发往 `/content`，请求体带 `kind`）

- [x] **Step 3: 实现**

- `background.ts`：按 `message.content.kind` 选 `/api/articles/content` 或 `/api/articles/link`；请求体是去掉 `kind` 的内容。鉴权与错误处理不变。
- `content.ts`：模块顶层 `const loadedUrl = location.href;`；`extractContent` 改为返回 `extractPage(document, loadedUrl)`。
- `popup.html`：预览区加 `<p id="preview-kind" class="preview-kind"></p>`。
- `popup.ts` 文案按类型切换：

| 位置 | article | link |
|---|---|---|
| `#preview-kind` | `Will save as: Article` | `Will save as: Link` |
| `#save-btn` | `Save Article` | `Save Link` |
| 保存中 | `Saving article...` | `Saving link...` |
| 成功 | `Article saved successfully!` | `Link saved successfully!` |
| 失败前缀 | `Error saving article: ` | `Error saving link: ` |

- [x] **Step 4: 运行并构建**

Run: `pnpm test && pnpm typecheck && pnpm lint && pnpm build`
Expected: 全部通过；`dist/` 已更新（构建若因下载 manifest schema 失败，按上一份计划的记录加 `NODE_USE_ENV_PROXY=1`）

---

### Task 5: 插件：复制 Markdown

**Files:**
- Modify: `src/web-extension/package.json`（依赖）、`src/manifest.json`（权限加 `"clipboardWrite"`）
- Modify: `src/web-extension/src/page-meta.ts`（加 `extractPageTags`）、`content.ts`、`popup.ts`、`popup.html`
- Create: `src/web-extension/src/markdown.ts`、`clipboard.ts`
- Test: `src/web-extension/test/markdown.test.ts`、`clipboard.test.ts`（新建）；`page-meta.test.ts`（补标签用例）
- Rebuild: `src/web-extension/dist/`

**Interfaces:**
- Consumes: Task 3 的 `parseReadable`、`isArticlePage`、`isMetaTrusted`、`jsonLdArticles`、`metaContent`、`pageTitle`、`pageAuthor`；Task 4 的 `loadedUrl`
- Produces:

```ts
// page-meta.ts
export function extractPageTags(document: Document): string[]
// markdown.ts
export function htmlToMarkdown(html: string): string
export function yamlString(value: string): string
export function localDate(date: Date): string
export function frontMatter(meta: { title: string; url: string; author?: string; date: string; tags?: string[] }): string
export function buildMarkdown(document: Document, loadedUrl: string, now?: Date): string
// clipboard.ts
export function copyText(text: string): Promise<void>
// content.ts：消息 { action: 'extractMarkdown' } → { success: true, markdown: string } | { success: false, error: string }
```

- [x] **Step 1: 安装依赖**

Run（在 `src/web-extension/`）: `pnpm add -E turndown turndown-plugin-gfm && pnpm add -D @types/turndown yaml`
Expected: `package.json` 的 `dependencies` 里两个新包是不带 `^` 的确切版本。`turndown-plugin-gfm` 若不带类型，新建 `src/turndown-plugin-gfm.d.ts` 声明该模块。

- [x] **Step 2: 写失败的测试**

```ts
// page-meta.test.ts — extractPageTags
两个 article:tag（'React'、'Web Dev'）+ keywords 'react, CSS；前端;  ' + JSON-LD keywords ['css', 'Hooks']
   → ['React', 'Web Dev', 'CSS', '前端', 'Hooks']        // 顺序：article:tag、keywords、JSON-LD；大小写不敏感去重，保留首次写法
JSON-LD keywords 为字符串 'a, b' → 也按分隔符切分
51 个字符的标签被丢弃；25 个不同标签 → 只返回前 20 个；没有任何来源 → []

// markdown.test.ts
frontMatter({ title: 'T', url: 'https://e.com/p', author: 'A', date: '2026-11-13', tags: ['x', 'y'] })
   === '---\ntitle: "T"\nurl: "https://e.com/p"\nauthor: "A"\ndate: 2026-11-13\ntags: ["x", "y"]\n---\n'
author 为 '' 且 tags 为 [] → 输出里没有 'author:' 与 'tags:' 行
YAML 往返（用 yaml 包解析去掉首尾 '---' 的内容）:
   title = 'He said: "hi" # not a comment\nline two \\ end\tTabbed'，tags = ['a "quoted", tag', 'c: d']
   → 解析出的 title、tags 与输入完全相等
yamlString('a\u0000b\u0007c') === '"abc"'
localDate(new Date(2026, 10, 3)) === '2026-11-03'

htmlToMarkdown:
   '<h2>Sub</h2>' → 含 '## Sub'
   '<ul><li>a</li><li>b</li></ul>' → 每项以 '-' 开头（/^-\s+a$/m）
   '<p><strong>b</strong> <em>i</em> <a href="https://e.com/x">l</a></p>' → 含 '**b** *i* [l](https://e.com/x)'
   '<img src="https://e.com/a.png" alt="pic">' → 含 '![pic](https://e.com/a.png)'
   '<img src="data:image/png;base64,AAAA" alt="x">' → 输出不含 'data:'
   '<pre><code class="language-ts">const a = 1;</code></pre>' → 含 '```ts\nconst a = 1;\n```'
   '<pre class="lang-python">print(1)</pre>'（没有 <code> 子元素）→ 含 '```python\nprint(1)\n```'
   '<pre><code>plain</code></pre>' → 含 '```\nplain\n```'
   正文含 '```' 的代码块 → 围栏是四个反引号，正文完整保留                       // Review Focus 2
   两行两列的 <table>（带 <thead>）→ 含 '| a | b |' 与分隔行
   '<del>gone</del>' → 匹配 /~+gone~+/；'<script>x()</script><style>p{}</style><p>ok</p>' → 只有 'ok'
   Markdown 块间最多两个 '\n'；代码块内部保留空行和缩进；文末恰好一个 '\n'

buildMarkdown（now 传 new Date(2026, 10, 13)）:
   长文博客页（<title>Post</title>，meta author Jane，两个 article:tag，
               正文里有 <pre><code class="language-js">const a = 1;</code></pre>）, loadedUrl === document.URL
     → 以 '---\ntitle: "Post"' 开头；含 'url: "<document.URL>"'、'author: "Jane"'、'date: 2026-11-13'、'tags: ['
     → front matter 之后是 '# Post'；含 '```js\nconst a = 1;\n```'      // 验证 keepClasses 真的穿过了 Readability 与 DOMPurify
   同一页面但 loadedUrl 不同（站内跳转）→ title 等于 document.title；没有 'author:' 与 'tags:' 行
   YouTube 地址的短页面（<title>My [Video]</title>）
     → front matter 之后只有一行 '[My \[Video\]](<document.URL>)'，没有 '# ' 标题

// clipboard.test.ts（jsdom 文档 + 假 clipboard）
writeText 成功 → 只调用 writeText(text)，文档里没有残留 <textarea>
writeText 拒绝 → 回退到 document.execCommand('copy')（桩返回 true），Promise 成功，<textarea> 已移除
writeText 拒绝且 execCommand 返回 false → Promise 以 /Clipboard write was blocked/ 拒绝
```

- [x] **Step 3: 运行，确认失败**

Run: `pnpm test`
Expected: FAIL（模块不存在）

- [x] **Step 4: 实现**

- `extractPageTags`：来源、切分与清洗规则按规格 5.3（分隔符 `,` `，` `;` `；`；`article:tag` 不切分；trim、丢空、大小写不敏感去重、单个 ≤ 50 字符、最多 20 个）。
- `yamlString` 用双引号包裹并转义 `\` 和 `"`。CRLF、CR 和 LF 写成 `\n`，制表符写成 `\t`。清除其余 C0、DEL 和 C1 控制字符。
- `htmlToMarkdown` 使用 Turndown 的 ATX 标题、`-` 列表、行内链接以及 GFM 删除线。自定义规则将克隆文档里的任务状态转成列表项开头的复选框，跳过没有单元格的表格行，并为无表头的表格插入空表头。转换时移除 `data:` 图片、`script` 和 `style`。
- 所有 `<pre>` 走统一的围栏规则。`language-xxx` 和 `lang-xxx` 优先，其次取 GitHub 高亮包装的语言，如 `highlight-text-html-basic` 对应 `html`。围栏至少三个反引号，且长于正文里的最长反引号串。保留代码里的空行和缩进，输出使用 LF 并以单个换行结尾。
- `buildMarkdown` 以 `parseReadable(document, 'markdown')` 解析正文，有正文且满足 `isArticlePage` 时生成文章 Markdown，其余页面生成链接。

| | title | author | tags | 正文 |
|---|---|---|---|---|
| article，可信 | `readable.title`，空则 `document.title` | `readable.byline` | `extractPageTags` | `# <单行标题>` + 空行 + `htmlToMarkdown(readable.html)` |
| article，不可信 | `document.title` | 无 | 无 | 同上 |
| link | `pageTitle(document, trusted)` | 可信时 `pageAuthor(document)`，否则无 | 可信时 `extractPageTags`，否则无 | `[<单行标题，[ 和 ] 前加反斜杠>](document.URL)` |

  “单行标题”指把连续空白（含换行）压成一个空格。`date` 用 `localDate(now)`。输出为 `frontMatter(...)` + 空行 + 正文 + 结尾换行。Markdown 不截断标题，也不限制地址协议或长度。
- `copyText`：先 `clipboard.writeText(text)`；被拒绝时创建隐藏 `<textarea>`，选中后 `document.execCommand('copy')`，用完移除；仍失败抛 `Clipboard write was blocked.`
- `content.ts`：处理 `extractMarkdown`，返回 `buildMarkdown(document, loadedUrl)`，异常时返回 `{ success: false, error }`。现有监听器对其他 action 直接 `return false`，要改成两个 action 都处理。
- `popup.html`：`#save-btn` 与 `#settings-btn` 之间加 `<button id="copy-md-btn" class="btn btn-secondary">Copy Markdown</button>`。
- `popup.ts`：点击后向当前标签页发 `extractMarkdown`，拿到字符串立即 `copyText`。成功显示 `Markdown copied`（success 状态），失败显示 `Error copying Markdown: <message>`。不关闭弹窗，不调用保存，不改变保存按钮状态；保存流程也不调用复制。按钮在弹窗初始化后即可用，不依赖 `extractContent` 是否成功。

- [x] **Step 5: 运行并构建**

Run: `pnpm test && pnpm typecheck && pnpm lint && pnpm build`
Expected: 全部通过；`dist/manifest.json` 的 `permissions` 含 `clipboardWrite`

---

### Task 6: 前端：链接徽标与链接详情视图

**Files:**
- Modify: `src/frontend/src/types/index.ts:26`
- Modify: `src/frontend/src/app/components/ArticleCard.tsx:118`
- Modify: `src/frontend/src/app/pages/article/[id]/components/Client.tsx`、`ReaderToolbar.tsx`、`client.scss`
- Test: `src/frontend/src/app/components/ArticleCard.test.tsx`、`src/frontend/src/app/pages/article/[id]/components/Client.test.tsx`

**Interfaces:**
- Consumes: 后端的 `contentKind: "link"`
- Produces: `Article['contentKind']` 为 `'full' | 'summaryOnly' | 'link'`；`ReaderToolbar` 的 `onReset` 变为可选，不传时不渲染“从头阅读”按钮

- [x] **Step 1: 写失败的测试**

```tsx
// ArticleCard.test.tsx
it('shows a 链接 badge only for link items')
//   contentKind 'link' → container.textContent 含 '链接'；'full' → 不含

// Client.test.tsx
it('renders a link item without reader chrome or progress traffic')
//   getArticleById → { ...details, id: 'link', contentKind: 'link', readableContent: '', summary: 'Video description',
//                      imageUrl: 'https://img.example/cover.jpg', url: 'https://www.youtube.com/watch?v=abc',
//                      createdAt: '2026-11-13T00:00:00Z' }
//   updateState → 列表项形状，不带 readableContent / originalContent / highlights（真实接口返回的是 ArticleListItemDto）：
//                 { id: 'link', title, url, readingStatus: 'read', contentKind: 'full', isFavorite: false, isArchived: false,
//                   tags: [], feeds: [], highlightCount: 0, savedFromExtension: true }
expect(container.querySelector('[role="progressbar"]')).toBeNull();
expect(container.querySelector('button[aria-label="从头阅读"]')).toBeNull();
expect(container.textContent).not.toContain('Remove reader highlight');     // HighlightList 的桩没有渲染
// 阅读器设置与目录按钮不存在：用 ReaderSettings.test / ReaderToc.test 里查询触发按钮的同一选择器断言
const visit = container.querySelector<HTMLAnchorElement>('a.visit-original-btn.primary')!;
expect(visit.href).toBe('https://www.youtube.com/watch?v=abc');
expect(visit.target).toBe('_blank'); expect(visit.rel).toBe('noopener noreferrer');
expect(container.textContent).toContain('Video description');
expect(container.textContent).toContain('youtube.com');
expect(container.querySelector('img[src="https://img.example/cover.jpg"]')).not.toBeNull();
// 点击 button[aria-label="标记已读"]：
expect(articleApi.updateState).toHaveBeenCalledWith('link', { readingStatus: 'read' });
expect(container.querySelector('button[aria-label="标记未读"]')).not.toBeNull();
expect(container.querySelector('[role="progressbar"]')).toBeNull();          // 响应报告 full，页面仍是链接视图，刷新前不变
// document.dispatchEvent(new Event('visibilitychange')) 之后：
expect(articleApi.getArticleById).toHaveBeenCalledTimes(1);                  // 进度 hook 没有发起刷新
expect(articleApi.saveProgress).not.toHaveBeenCalled();
expect(articleApi.resetProgress).not.toHaveBeenCalled();
```

- [x] **Step 2: 运行，确认失败**

Run（在 `src/frontend/`）: `pnpm test -- ArticleCard Client`
Expected: 新用例 FAIL

- [x] **Step 3: 实现**

- `types/index.ts`：`contentKind` 加 `'link'`。
- `ArticleCard.tsx`：在“仅摘要”徽标旁加 `{currentArticle.contentKind === "link" && <span className={styles.archiveLabel}>链接</span>}`。
- `ReaderToolbar.tsx`：`onReset?: () => void`，有值才渲染“从头阅读”按钮。
- `Client.tsx`：`const isLink = article?.contentKind === 'link';`
  - `useReadingProgress(isLink ? undefined : article, …)`、`useReaderToc(contentRef, isLink ? undefined : article?.readableContent)`。hook 收到 `undefined` 时处于空闲状态（加载期间本来就是这样）：不建进度会话、不发请求，`withStateChange` 直接执行动作。这是规格 4.5“不挂载 `useReadingProgress`”在本计划里的落实方式，避免为拆分组件重写整个阅读器。
  - 工具栏：链接时不传 `onReset` 和 `extra`。
  - 链接时不渲染：`.reader-progress`、`.reader-body-row`（`HighlightManager`、`ReadableContent`、`HighlightList`、`ReaderTocSidebar`）。
  - 链接时标题下方显示 `<p className="reader-source">{域名} · 保存于 {new Date(article.createdAt).toLocaleDateString("zh-CN")}</p>`，域名取 `new URL(article.url).hostname` 去掉 `www.`（解析失败则只显示保存时间）。
  - “访问原文”链接保留在 `article-actions`，链接时类名为 `visit-original-btn primary`。
  - 标签管理之后渲染 `<div className="link-detail">`：有 `imageUrl` 时一张 `<img className="link-cover" alt="" />`，有 `summary` 时一段 `<p className="link-summary">`。
- `client.scss`：`.visit-original-btn.primary` 用 `color: var(--surface); background: var(--brand); border-color: var(--brand);`，悬停 `background: var(--brand-hover)`（与 `globals.scss` 的 `.btn-navy` 一致）；`.link-cover` 限制 `max-width: 100%`、圆角 10px；`.link-summary` 用正文颜色与 1.7 行高。

- [x] **Step 4: 运行，确认通过**

Run: `pnpm test && npx tsc --noEmit && npx eslint src e2e --ignore-pattern src/app/pages/account/page.tsx`
Expected: 测试与类型检查通过；lint 只有 `CLAUDE.md` 记录的既有 ArticleCard 图片警告

---

### Task 7: 文档与整体验收

**Files:**
- Modify: `CLAUDE.md`、`src/web-extension/README.md`

- [x] **Step 1: 更新文档**

- `CLAUDE.md`：Core Entities 的 Article 说明加上三种内容类型；API Structure 加 `POST /api/articles/link`；External Services 的 Article collection 一条补上链接保存；新增一小节记录本期规则：`ContentKind.Link`、无迁移、去重与升级语义、进度与高亮对链接返回 400、站内跳转不读 `<head>`、YouTube 封面、地址原样保存、复制 Markdown 的 front matter 字段、`clipboardWrite` 权限、`turndown` 依赖。只写已验证的事实。
- `src/web-extension/README.md`：Features 加“保存链接”和“Copy Markdown”。

- [x] **Step 2: 全量自动化验证**

Run（仓库根）: `dotnet test mini-cc.slnx`
Run（`src/web-extension/`）: `pnpm test && pnpm typecheck && pnpm lint && pnpm build`
Run（`src/frontend/`）: `pnpm test && npx tsc --noEmit && npx eslint src e2e --ignore-pattern src/app/pages/account/page.tsx`
Expected: 全部通过；记录各自的通过数量

- [x] **Step 3: 真实浏览器验收**

启动完整环境（`./docker-build.sh && ./docker-run.sh`，或按 `CLAUDE.md` 手动启动），在 Chrome 加载 `src/web-extension/dist`，逐条走规格第 9 节的场景 1、2、5、8、11、12、13、14、15，记录每条的实际结果。使用测试用的文章与链接，结束后清理，不动现有文库数据。

- [x] **Step 4: 核对规格里两处留待实测的事项**

- 规格 3.1 的前提：打开一个 YouTube 视频，站内点到另一个，在控制台读 `document.querySelector('meta[property="og:title"]').content`，把结果（过期 / 已更新）写进 `CLAUDE.md` 的本期小节。
- 规格第 8 节的封面尺寸：看 `hqdefault.jpg` 在卡片里的黑边是否可接受；不可接受则把 `extractLink` 与对应测试里的文件名改为 `mqdefault.jpg`，重跑插件测试与构建。

- [x] **Step 5: 汇报**

列出：各套测试的通过数、浏览器验收逐条结果、Step 4 的两项结论、未做或未通过的事项。不 stage、不 commit。

#### 实施验收记录

- 后端在 .NET 9.0.23 和 Testcontainers PostgreSQL 16 环境下通过 366 项测试。EF Core 报告无待处理的模型变化，`AddHighlightCommandHandler.cs` 保持 UTF-16LE。
- 扩展通过 51 项测试、TypeScript 检查、ESLint 和生产构建。构建使用 `NODE_USE_ENV_PROXY=1`，产物的 manifest 包含 `clipboardWrite`。Chrome 中 `about:blank` 的保存按钮不可用，弹窗显示 HTTP(S) 限制。
- 前端通过 99 项测试及 TypeScript 检查。按计划运行的 ESLint 检查仅报告已有的 ArticleCard 图片警告。

| 规格场景 | Chrome 实测 |
|---|---|
| 1 | YouTube 视频从扩展保存为链接。收件箱显示标题、封面、描述和「链接」徽标。 |
| 2 | 链接详情以「访问原文」为主操作，新标签页打开原地址；打标签、收藏、归档、删除、撤销和永久删除均通过。页面未发出阅读进度请求。 |
| 5 | 同一博客夹具 URL 从链接升级为文章，保留 ID、标签、收藏和归档状态，详情显示正文。 |
| 8 | 复制博客夹具后，从 Chrome 剪贴板读回并解析 YAML。日期、标题、网址、作者、标签和围栏语言符合预期。无作者或标签时省略相应字段。 |
| 11 | 仅复制时文库没有新增条目，保存后剪贴板保持原内容。 |
| 12 | 从 YouTube 复制得到 front matter 和独立的一行 Markdown 链接。 |
| 13 | 真实 YouTube 站内跳转后保存了 B 的 URL 和封面，简介为空。B 触发 YouTube 登录验证，页面标题为 `YouTube`。另用带明确 B 标题的同文档 Chrome 夹具验证标题也随 B 保存。 |
| 14 | 真实 YouTube 跳转和同文档夹具均从当前 `document.title` 取标题，省略旧作者和标签。 |
| 15 | 同一视频带播放列表参数时单独保存，hash 和跟踪参数变体复用原有条目。 |

同一次 YouTube 站内跳转后，`og:title` 仍为 A 的标题。在桌面和 375 px 宽度下检查了真实回退封面卡片，`hqdefault.jpg` 未显示明显黑边，保留该尺寸。

#### 评审修订验证

- 后端复跑 368 项测试，包含真实 PostgreSQL 上的 `SummaryOnly` 保留、回收站恢复后保留标签，以及新保存接口的 FluentValidation 校验。EF Core 确认无待处理的模型变化。
- 扩展复跑 60 项测试，前端复跑 99 项测试。两套 TypeScript 检查、扩展 ESLint 与生产构建通过，前端 ESLint 保留原有的 ArticleCard 图片警告。
- Chrome 加载重建产物后，夹具的正文图片请求次数在页面加载、预览和复制三个阶段均为 1。实测剪贴板保留围栏内空行与较长围栏，能保留无表头表格的表题以及已勾选、未勾选任务。复制没有发出保存请求，保存通过后台发送一次带测试 API Key 的请求，剪贴板内容保持原样。

#### 第二轮评审验证

- 扩展 64 项、前端 100 项、后端 368 项测试全部通过。后端使用 .NET 9 和 Testcontainers PostgreSQL，覆盖保存链接时保留 `SummaryOnly` 摘要、恢复后保留标签和状态。EF Core 检查无待处理的模型变化。
- TypeScript 检查和扩展 ESLint 通过。前端改动文件的 ESLint 检查只报告 ArticleCard 既有的 `<img>` 警告。
- Chrome 加载重建后的扩展。实际复制的 Markdown 保留了带空行夹具的表格数据、任务标记、`html` 和 `c++` 语言标记以及代码中的空行。复制过程中没有保存请求或重复加载正文图片。文章和链接的保存请求使用相同的首个封面，剪贴板内容保持原样。
- 回归测试覆盖同一阅读器从文章切到链接后清除进度提示、卸除监听，以及 YAML 的 CRLF、CR、LF 和 C1 控制字符。
