# 图片持久化存储 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** API 在保存和导入时把文章图片下载到本地目录或 S3 兼容存储，返回文章时再替换图片地址；默认部署不再需要 image-proxy，image-proxy 保留为可选模式。

**Architecture:**
- 正文只存规范化后的原图地址。下载结果记在 `Articles.Images`（JSON）和 `Articles.ImagesDueAt` 两列。
- 后台任务负责回填旧数据、下载图片和写回结果；每天的回收站清理之后，再做一次标记-清除的存储清理。
- 所有修改正文图片地址的操作（规范化、旧代理前缀剥离、代理签名、返回时替换）共用 `ImageHtml.Rewrite` 这一个遍历函数，保证输出逐字一致。
- `ImageNs` 遵循 `CLAUDE.md` 的目录约定，按 `Application`、`Domain` 和 `Infrastructure` 分层。

**Tech Stack:** ASP.NET Core 10、EF Core 10（Npgsql / SQLite）、HtmlAgilityPack、AWSSDK.S3、xUnit + FluentAssertions + Moq + Testcontainers（PostgreSQL、MinIO）、Next.js + Vitest + Playwright、nginx、Podman Compose。

**Spec:** `docs/designs/2026-11-21-image-storage-design.md`。执行者需要同时阅读设计文档和本计划。

**基于：** 提交 `33b0380`（分支 `docs/readme-restructure`）。第 12 个任务修改的 README 是这个分支上重构过的版本。

**设计第 6 节两个待定问题，按文档当前的默认处理：**
- 不从 image-proxy 缓存救图，只在升级说明里要求保留 `image-proxy-data` 卷；
- 不支持多个实例共用同一个存储桶，没有对象键前缀配置。

## Global Constraints

- 配置节和默认值：
  - `Images:Mode` = `Store` | `ImageProxy`，默认 `Store`
  - `Images:Delivery` = `Authenticated` | `Public`，默认 `Authenticated`
  - `Images:ApiUrl` 默认 `/api/images`
  - `Images:PublicBaseUrl` 默认空
  - `Images:AllowPrivateNetworks` 默认 `false`
  - `Images:Storage:Provider` = `Local` | `S3`，默认 `Local`
  - `Images:Storage:LocalPath` 默认 `data/images`，相对 API 内容根目录
  - `Images:Storage:S3` 下有 `ServiceUrl`、`Bucket`、`Region`（默认 `auto`）、`AccessKeyId`、`SecretAccessKey`
  - `ImageProxy:ProxyUrl`、`ImageProxy:SignatureKey` 只在 `Mode=ImageProxy` 时使用
- 存储键：`{hash前2位}/{hash}.{ext}`。`hash` 是图片字节的 SHA-256，64 位小写十六进制；`ext` ∈ `jpg png gif webp avif svg`。
- 图片地址：
  - API 提供的图片：`{ApiUrl}/{hash}.{ext}`
  - 公开直链：`{PublicBaseUrl}/{hash前2位}/{hash}.{ext}`
- 下载限制：
  - 取得并发名额后开始计算 30 秒期限，涵盖请求、重定向、带 Referer 重试和读取响应体。最多重定向 5 次，大小上限 20 MB。
  - 进程内同时最多 4 个下载，同一个域名同时最多 2 个；
  - 第一次请求不带 Referer，得到 401 或 403 时带 `Referer: <文章地址>` 重试一次；
  - User-Agent 用 `Mozilla/5.0 (Macintosh; Intel Mac OS X 11_1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/102.0.4280.141 Safari/537.36`；
  - `Accept: image/avif,image/webp,image/apng,image/*,*/*;q=0.8`。
- 下载任务：
  - 每 30 秒运行一次，每次最多领取 10 篇文章，租约 10 分钟；
  - 回填每批 50 篇，回填完才开始下载；
  - 暂时性失败的重试间隔依次是 1 分钟、10 分钟、1 小时、6 小时、24 小时，之后每 24 小时一次；第 10 次失败后记为 `failed`；`Retry-After` 是下次重试的最早时间。
- 清理：只在 `Mode=Store` 运行，宽限期 24 小时；有 `Images IS NULL` 的文章（包括回收站里的）时跳过；只处理键名完全匹配存储键格式的对象。
- 图片响应头：
  - `X-Content-Type-Options: nosniff`
  - `Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'; sandbox`
  - `ETag: "{hash}"`
  - `Cache-Control: private, max-age=31536000, immutable`（登录后访问），或 `public, max-age=31536000, immutable`（公开访问）
- 迁移名：PostgreSQL 和 SQLite 都叫 `M5_ImageStorage`。
- 回填和下载**绝不**修改 `ContentRevision`、进度字段、`ProgressUpdatedAt` 和高亮的 `AnchorStatus`。
- 以下文件是 **UTF-16 LE 带 BOM**，修改时保持原编码，搜索时用 `grep -a` 再去掉 `\0`：
  - `src/server/Core/DependencyInjection.cs`
  - `src/server/Shared/Config/MappingProfile.cs`
  - `src/server/Shared/Data/DbSeeder.cs`
  - `src/server/Shared/Data/MiniCcDbContext.cs`
  - `src/server/Core/TagNs/Application/Services/TagService.cs`
- 新增包：
  - `AWSSDK.S3`：当前最新的 4.x 稳定版，用于 `src/server`；
  - 测试项目使用 NuGet 已发布的 `Testcontainers.Minio 4.15.0`，与 `Testcontainers.PostgreSql 4.16.0` 一起验证。
  - 两个版本都写进 `Directory.Packages.props`。
- PostgreSQL 和 MinIO 测试需要能运行容器。Rootless Podman 下要设置 `DOCKER_HOST`、`TESTCONTAINERS_HOST_OVERRIDE=localhost`、`TESTCONTAINERS_RYUK_DISABLED=true`。

## Review Focus

1. **正文里的图片地址带 HTML 实体、IDN 域名或中文路径。** 写入时用作映射键的规范化地址，必须和返回时查找用的地址完全一致，已下载的图片才能被替换（任务 5 的 `ResolveHtml_ResolvesEntityEncodedAndUnicodeUrls`）。
2. **同一个地址在正文里出现多次，或者同时是封面。** 只记录一条、只下载一次，所有出现的位置都被替换（任务 2 的 `CollectDownloadUrls_DeduplicatesBodyAndCover`；任务 5 的 `ResolveHtml_ReplacesEveryOccurrence`）。
3. **图片服务器对缺失的图片返回状态 200 的 HTML 错误页。** 应该记为永久失败 `unsupportedType`，而不是把 HTML 当图片存起来（任务 7 的 `Download_Html200_IsUnsupportedType`）。
4. **图片端点收到大写十六进制、路径穿越（`..%2f`）或不支持的扩展名。** 一律返回 404，不读取存储（任务 9 的 `Get_MalformedNames_Return404`）。
5. **image-proxy 模式下，还没回填（`Images` 为 NULL）的旧文章。** 返回的正文必须和升级前逐字相同（任务 5 的 `ResolveHtml_ProxyMode_LegacyRowUnchanged`）。

---

### Task 1: 图片配置和启动校验

**Files:**
- Create: `src/server/Core/ImageNs/Application/ImageOptions.cs`
- Create: `src/server/Core/ImageNs/Infrastructure/ImageServiceCollectionExtensions.cs`、`src/server/Core/ImageNs/Infrastructure/ImageOptionsValidator.cs`
- Modify: `src/server/Core/DependencyInjection.cs:66-70`（UTF-16）
- Modify: `src/server/appsettings.json`
- Test: `test/MiniCc.Api.Test/Images/ImageOptionsTests.cs`

**Interfaces:**
- Produces:
  - `enum ImageMode { Store, ImageProxy }`
  - `enum ImageDelivery { Authenticated, Public }`
  - `enum ImageStorageProvider { Local, S3 }`
  - `class ImageOptions`：`Mode`、`Delivery`、`ApiUrl`、`PublicBaseUrl`、`AllowPrivateNetworks`、`ImageStorageOptions Storage`
  - `class ImageStorageOptions`：`Provider`、`LocalPath`、`S3StorageOptions S3`
  - `class S3StorageOptions`：`ServiceUrl`、`Bucket`、`Region`、`AccessKeyId`、`SecretAccessKey`
  - `class ImageOptionsValidator(string contentRootPath) : IValidateOptions<ImageOptions>`
  - `static IServiceCollection AddImageServices(this IServiceCollection services, IConfiguration configuration, string contentRootPath)`。后续任务的服务都注册在这里。

- [x] **Step 1: 写失败的测试**

`ImageOptionsTests` 参照 `LibraryOptionsTests` 的写法，用 `AddInMemoryCollection` 加 `AddInfrastructureServices`，再取 `IOptions<ImageOptions>.Value` 触发校验。

```csharp
[Fact] void Defaults_AreStoreAuthenticatedLocal()
// Mode == Store, Delivery == Authenticated, ApiUrl == "/api/images", Storage.Provider == Local,
// Storage.LocalPath == "data/images", Storage.S3.Region == "auto", AllowPrivateNetworks == false

[Fact] void StoreMode_DoesNotRequireSignatureKey()      // 没有 ImageProxy:SignatureKey 也不抛异常
[Fact] void ImageProxyMode_RequiresSignatureKey()       // OptionsValidationException，消息包含 "ImageProxy:SignatureKey"
[Fact] void EnumValues_AreCaseInsensitive()             // "imageproxy" / "PUBLIC" / "s3" 都能解析

[Theory]
[InlineData("ServiceUrl")] [InlineData("Bucket")] [InlineData("AccessKeyId")] [InlineData("SecretAccessKey")]
void S3Provider_RequiresSetting(string missing)         // 消息包含 "Images:Storage:S3:" + missing

[Fact] void PublicBaseUrl_WithAuthenticated_Fails()     // 消息包含 "Images:PublicBaseUrl"
[Fact] void PublicBaseUrl_WithLocal_Fails()
[Fact] void PublicBaseUrl_MustBeAbsoluteHttp()          // "img.example.com" 和 "ftp://x" 都失败
[Fact] void LocalStore_CreatesDirectoryUnderContentRoot()   // 临时目录作为内容根目录，校验后 {root}/data/images 存在
[Fact] void LocalStore_UnwritableDirectory_FailsWithFullPath()  // LocalPath 指向一个已存在的文件；消息包含完整路径
```

- [x] **Step 2: 运行测试，确认失败**

运行：`dotnet test test/MiniCc.Api.Test --filter "FullyQualifiedName~ImageOptionsTests"`
预期：编译失败，提示 `ImageOptions` 未定义。

- [x] **Step 3: 实现**

- `ImageOptionsValidator` 只在 `Mode=Store` 且 `Provider=Local` 时检查目录：创建目录，写入再删除一个探测文件，失败时报出包含完整路径的错误。
- `AddImageServices` 做三件事：
  1. 绑定 `Images` 配置节，加上 validator，并调用 `ValidateOnStart`；
  2. 把原来 `ImageProxyOptions` 的注册从 `DependencyInjection.cs` 搬过来，校验改成 `.Validate<IOptions<ImageOptions>>((proxy, images) => images.Value.Mode != ImageMode.ImageProxy || !string.IsNullOrWhiteSpace(proxy.SignatureKey), "ImageProxy:SignatureKey is required when Images:Mode is ImageProxy.")`；
  3. 保留 `services.AddSingleton<ImageProxyService>()`。
- `DependencyInjection.cs` 里删掉第 66-70 行，改为调用 `services.AddImageServices(configuration, contentRootPath ?? Directory.GetCurrentDirectory())`。
- `appsettings.json` 新增 `Images` 节，写出所有默认值；S3 的密钥类字段留空。

- [x] **Step 4: 运行测试，确认通过**

运行：`dotnet test test/MiniCc.Api.Test --filter "FullyQualifiedName~ImageOptionsTests|FullyQualifiedName~LibraryOptionsTests|FullyQualifiedName~DatabaseConfigurationTests"`
预期：全部 PASS。

- [ ] **Step 5: 提交**

```bash
git add src/server/Core/ImageNs src/server/Core/DependencyInjection.cs src/server/appsettings.json test/MiniCc.Api.Test/Images/ImageOptionsTests.cs
git commit -m "feat(images): add image storage options and startup validation"
```

---

### Task 2: 共用的 HTML 图片地址遍历、规范化和旧代理前缀剥离

**Files:**
- Create: `src/server/Core/ImageNs/Domain/ImageHtml.cs`
- Create: `src/server/Core/ImageNs/Application/LegacyImageUrls.cs`
- Modify: `src/server/Infra/ImageProxyService.cs:36-90`（`RewriteImages` 改为调用 `ImageHtml.Rewrite(html, GetUrl)`；`RewriteSrcset` 搬到 `ImageHtml`）
- Modify: `src/server/Core/ImageNs/Infrastructure/ImageServiceCollectionExtensions.cs`（注册 `LegacyImageUrls` 单例）
- Test: `test/MiniCc.Api.Test/Images/ImageHtmlTests.cs`、`test/MiniCc.Api.Test/Images/LegacyImageUrlsTests.cs`、`test/MiniCc.Api.Test/Images/TestImages.cs`

**Interfaces:**
- Consumes：`ImageProxyOptions`、`ImageProxyService.GetUrl(string url, int width = 0, int height = 0)`
- Produces：
  - `delegate string ImageUrlMap(string url, int width, int height)`
  - `static class ImageHtml`：
    - `string Rewrite(string html, ImageUrlMap map, Func<string, string, bool>? needsNoReferrer = null)`
    - `string NormalizeUrl(string url)`
    - `string Normalize(string html)`
    - `IReadOnlyList<string> CollectDownloadUrls(string html, string coverUrl)`
  - `class LegacyImageUrls(IOptions<ImageProxyOptions> proxy)`：`string StripUrl(string url)`、`string StripHtml(string html)`
  - 测试辅助 `static class TestImages`：
    - `ImageProxyService Proxy(string key = "test-signing-key")`
    - `LegacyImageUrls Legacy(string proxyUrl = "/imageProxy")`
    - `string LegacyRewrite(string html)`：等于旧版 `RewriteImages`，用签名密钥 `"test-signing-key"`

**行为约定：**
- `Rewrite` 照搬现有 `RewriteImages` 的逻辑：
  - 遍历 `img`、`source`，`src` 带元素的 `width`/`height`，`srcset` 的每个候选都用 `0,0`；
  - 先对属性值做 `HtmlEntity.DeEntitize`，调用 `map`，再用 `WebUtility.HtmlEncode` 写回；
  - `srcset` 的解析和 `", "` 连接方式不变；去掉 `crossorigin`；输出 `DocumentNode.OuterHtml`。
- `map` 会收到所有地址（包括 `data:` 和相对地址），由 `map` 自己决定要不要原样返回。
- `needsNoReferrer(original, mapped)` 对某个元素的任一地址返回 true 时，就给负责取图的那个 `<img>` 设置 `referrerpolicy="no-referrer"`。对 `img` 来说是它自己；对 `source` 来说，是父元素 `<picture>` 里的 `<img>`。
- `NormalizeUrl`：http/https 绝对地址返回 `Uri.AbsoluteUri`，其他原样返回。
- `Normalize(html)` = `Rewrite(html, (u, _, _) => NormalizeUrl(u))`。
- `CollectDownloadUrls` 只收集以下地址，规范化以后去重，并保持首次出现的顺序：
  - `img` 的 `src` 和 `srcset`；
  - 父元素是 `<picture>` 的 `source` 的 `src` 和 `srcset`；
  - `coverUrl`。

  只要 http/https 绝对地址。`<video>`、`<audio>` 里的 `source` 不收集。
- `StripUrl`：
  - 识别当前配置的 `ProxyUrl.TrimEnd('/')` 和默认的 `/imageProxy`。对于其他旧的绝对前缀，要求完整的 43 字符 Base64URL 签名格式，每次只剥最外层。
  - 用 `^{前缀}/\d+x\d+,s[A-Za-z0-9_-]+/(https?://.+)$` 匹配，返回第 1 组；不匹配就原样返回。
- `StripHtml(html)` = `Rewrite(html, (u, _, _) => StripUrl(u))`。

- [x] **Step 1: 写失败的测试**

```csharp
// ImageHtmlTests
[Theory, MemberData(nameof(Fixtures))]   // 现有 ImageProxyServiceTests 里的 HTML，再加上：微信 data-src 已解析的 img、
                                         // 带 &amp; 查询参数、IDN 域名 https://例子.测试/a.png、中文路径、
                                         // picture+source srcset、video>source、data: URI、相对地址
void Normalize_EqualsLegacyRewriteWithPrefixStripped(string html)
    => ImageHtml.Normalize(html).Should().Be(TestImages.Legacy().StripHtml(TestImages.LegacyRewrite(html)));

[Theory, MemberData(nameof(Fixtures))]
void ProxyRewriteOfNormalized_EqualsLegacyRewrite(string html)
    => TestImages.Proxy().RewriteImages(ImageHtml.Normalize(html)).Should().Be(TestImages.LegacyRewrite(html));

[Fact] void Normalize_KeepsDataAndRelativeUrls_RemovesCrossorigin()
[Fact] void CollectDownloadUrls_IncludesImgSrcsetAndPictureSources_ExcludesVideoSources()
[Fact] void CollectDownloadUrls_DeduplicatesBodyAndCover()
// 正文里 <img src="https://a.test/x.png"> 出现两次，封面 "https://a.test/x.png"
// → new[] { "https://a.test/x.png" }
[Fact] void Rewrite_NeedsNoReferrer_MarksImgAndPictureImg()

// LegacyImageUrlsTests
[Fact] void StripUrl_DefaultPrefix()            // "/imageProxy/320x180,sAbC-_/https://a.test/x.png?q=1" → "https://a.test/x.png?q=1"
[Fact] void StripUrl_ConfiguredAbsolutePrefix() // ProxyUrl "http://localhost:5050/" 能剥离 "http://localhost:5050/0x0,sX/https://a.test/x.png"
[Fact] void StripUrl_NonMatching_Unchanged()    // "/imageProxy/notsigned/https://a.test" 和 "https://a.test/imageProxy/0x0,sX/https://b" 都原样返回
[Fact] void StripHtml_IgnoresTextContainingPrefix()   // <p>/imageProxy/0x0,sX/https://a.test</p> 保持不变
```

`TestImages.LegacyRewrite` 必须在改动之前就有：Step 1 先提交一个复制了当前 `RewriteImages` 和 `RewriteSrcset` 的私有副本，作为"旧版本"的参照实现，放在测试项目里。

- [x] **Step 2: 运行测试，确认失败**

运行：`dotnet test test/MiniCc.Api.Test --filter "FullyQualifiedName~ImageHtmlTests|FullyQualifiedName~LegacyImageUrlsTests"`
预期：编译失败，提示 `ImageHtml`、`LegacyImageUrls` 未定义。

- [x] **Step 3: 实现上面约定的 `ImageHtml` 和 `LegacyImageUrls`；`ImageProxyService.RewriteImages(html)` 改成 `ImageHtml.Rewrite(html, GetUrl)`**

- [x] **Step 4: 运行测试，确认通过**

运行：`dotnet test test/MiniCc.Api.Test --filter "FullyQualifiedName~ImageHtmlTests|FullyQualifiedName~LegacyImageUrlsTests|FullyQualifiedName~ImageProxyServiceTests"`
预期：全部 PASS，现有的 `ImageProxyServiceTests` 不用改。

- [ ] **Step 5: 提交**

```bash
git add src/server/Core/ImageNs src/server/Infra/ImageProxyService.cs test/MiniCc.Api.Test/Images
git commit -m "refactor(images): share one image URL traversal for normalize, sign and legacy strip"
```

---

### Task 3: Article 的 Images 模型和 M5 迁移

**Files:**
- Create: `src/server/Core/ArticleNs/Domain/AggregatesModel/ArticleImage.cs`
- Create: `src/server/Core/ArticleNs/Infrastructure/EntityConfigurations/ArticleImagesJson.cs`
- Modify: `src/server/Core/ArticleNs/Domain/AggregatesModel/Article.cs`（加两个属性；私有构造函数里设置 `Images = new()`；加下面列出的方法）
- Modify: `src/server/Core/ArticleNs/Infrastructure/EntityConfigurations/ArticleConfiguration.cs`
- Modify: `src/server/Shared/Data/SqliteMiniCcDbContext.cs:19-23`
- Create: `src/server/Migrations/*_M5_ImageStorage.cs`、`src/server/Migrations/Sqlite/*_M5_ImageStorage.cs`（用 `dotnet ef` 生成）
- Test: `test/MiniCc.Api.Test/Core/ArticleNs/Domain/ArticleImagesTests.cs`、`test/MiniCc.Api.Test/Infrastructure/ArticleImagesStorageTests.cs`

**Interfaces:**
- Produces：
  - `enum ArticleImageState { Pending, Done, Failed }`
  - `sealed record ArticleImage(ArticleImageState State, string? Hash = null, string? Ext = null, int Attempts = 0, DateTimeOffset? NextAttemptAt = null, string? Reason = null)`
  - `Article.Images : Dictionary<string, ArticleImage>?`（private set）：只有 M5 之前就存在的行才是 NULL
  - `Article.ImagesDueAt : DateTimeOffset?`（private set）
  - `void Article.SyncImages(bool resetFailed, DateTimeOffset now)` 从正文和封面收集原图地址，再调用接受 URL 集合的重载。
  - `bool Article.ApplyImageResult(string url, ArticleImage result)`：只有记录存在且是 `Pending` 时才写入，并重新计算到期时间
  - `void Article.NormalizeLegacyImages(string readableContent, string imageUrl, DateTimeOffset now)` 写回正文和封面，再执行 `SyncImages(resetFailed: false, now)`，保留 `ContentRevision`、`Length` 和进度字段。
  - `int Article.ResetMissingImages(IReadOnlySet<string> missingHashes, DateTimeOffset now)`：把这些哈希对应的 `Done` 记录改回 `Pending(Attempts 0, NextAttemptAt now)`，返回改动的条数
  - `void Article.RecomputeImagesDue()`：取所有 `Pending` 记录里最小的 `NextAttemptAt`，没有就是 null
  - `static class ArticleImagesJson`：`ValueConverter<Dictionary<string, ArticleImage>, string> Converter`、`ValueComparer<Dictionary<string, ArticleImage>> Comparer`。序列化用 camelCase 属性名、camelCase 字符串枚举，并设置 `DefaultIgnoreCondition = WhenWritingNull`。`NextAttemptAt` 持久化为 `next`。

**`SyncImages` 规则**（设计 3.3）：
1. 已有记录的地址保留原记录；`resetFailed` 为 true 时，把 `Failed` 改成 `Pending(Attempts 0, NextAttemptAt now)`。
2. 新地址记为 `Pending(Attempts 0, NextAttemptAt now)`。
3. 删除不在 `urls` 里的记录。
4. `Images` 为 null 时视为空字典。
5. 最后调用 `RecomputeImagesDue()`。

- [x] **Step 1: 写失败的测试**

```csharp
// ArticleImagesTests（纯领域测试）
[Fact] void Create_StartsWithEmptyImagesAndNoDueTime()
[Fact] void SyncImages_AddsPending_RemovesMissing_KeepsDone()
[Fact] void SyncImages_ResetFailed_OnlyWhenRequested()
[Fact] void SyncImages_DueIsEarliestPendingNext()
[Fact] void ApplyImageResult_IgnoresRemovedOrNonPendingEntries()   // 返回 false，字典不变
[Fact] void NormalizeLegacyImages_KeepsRevisionProgressAndLength()
// 先 ApplyProgress，记下 ContentRevision、Progress*、ProgressUpdatedAt、Length；
// 调用后正文和封面变了，这些值全部不变
[Fact] void ResetMissingImages_ResetsOnlyMatchingDoneEntries()

// ArticleImagesStorageTests（SQLite 用 SqliteFixture；PostgreSQL 用 PostgresFixture，放在 "Postgres" 集合里）
[Fact] async Task Sqlite_RoundTripsImagesAndDetectsInPlaceChanges()
// 保存 → 新上下文读取，字典内容相等；改其中一条记录后 SaveChanges，确认写进了数据库
[Fact] async Task Postgres_RoundTripsImagesAsJsonb()
[Fact] async Task MigratedLegacyRow_HasNullImages()
// 用 ExecuteSqlRaw 把一行的 Images 设成 NULL，读出来 Images == null
```

`DatabaseModelTests` 现有的三个快照测试，迁移生成之后必须仍然通过。

- [x] **Step 2: 运行测试，确认失败**

运行：`dotnet test test/MiniCc.Api.Test --filter "FullyQualifiedName~ArticleImages"`
预期：编译失败。

- [x] **Step 3: 实现模型和 EF 配置**

- `ArticleConfiguration` 里配置：

  ```csharp
  builder.Property(a => a.Images)
      .HasConversion(ArticleImagesJson.Converter, ArticleImagesJson.Comparer)
      .HasColumnType("jsonb");
  builder.HasIndex(a => a.ImagesDueAt).HasFilter("\"ImagesDueAt\" IS NOT NULL");
  ```
- `SqliteMiniCcDbContext.ConfigureProviderModel` 里加上 `modelBuilder.Entity<Article>().Property(a => a.Images).HasColumnType("TEXT");`。

- [x] **Step 4: 生成两套迁移**

```bash
cd src/server
Database__Provider=postgres dotnet ef migrations add M5_ImageStorage --context MiniCcDbContext
dotnet ef migrations add M5_ImageStorage --context SqliteMiniCcDbContext --output-dir Migrations/Sqlite
```

检查生成的迁移：两列都可为空，没有默认值，所以现有行都是 NULL；部分索引带过滤条件。

- [x] **Step 5: 运行测试，确认通过**

运行：`dotnet test test/MiniCc.Api.Test --filter "FullyQualifiedName~ArticleImages|FullyQualifiedName~DatabaseModelTests"`
预期：全部 PASS。

- [ ] **Step 6: 提交**

```bash
git add src/server/Core/ArticleNs src/server/Shared/Data/SqliteMiniCcDbContext.cs src/server/Migrations test/MiniCc.Api.Test
git commit -m "feat(images): store per-article image download state"
```

---

### Task 4: 写入路径改成"规范化 + 同步映射"，并处理旧数据

**Files:**
- Modify: `src/server/Core/ImageNs/Application/LegacyImageUrls.cs`（加 `Backfill`）
- Modify: `src/server/Core/ArticleNs/Domain/Services/ArticleDomainService.cs`（不再依赖 `ImageProxyService`）
- Modify: `src/server/Core/ArticleNs/Application/Commands/SaveArticleContentCommandHandler.cs:36-56`
- Modify: `src/server/Core/ArticleNs/Application/Commands/SaveLinkCommandHandler.cs:21-29`
- Modify: `src/server/Core/FeedNs/Infrastructure/FeedParser.cs:24,76-79`（构造函数改为无参数；用 `ImageHtml.Normalize`；封面用 `ImageHtml.NormalizeUrl`）
- Modify: `src/server/Core/FeedNs/Application/FeedImportService.cs:59-61`
- Modify（测试的构造方式和断言）：
  - `test/.../Core/ArticleNs/Domain/Services/ArticleDomainServiceTests.cs`（第 46 行）
  - `test/.../Core/ArticleNs/Application/Commands/SaveArticleContentCommandHandlerTests.cs`（第 144 行）
  - `test/.../Core/ArticleNs/Application/Commands/SaveLinkCommandHandlerTests.cs`
  - `test/.../Feeds/FeedParserTests.cs`、`test/.../Feeds/FeedFetcherTests.cs`
  - `test/.../Infrastructure/FeedRefreshPostgresTests.cs`、`test/.../Infrastructure/SqliteConcurrencyTests.cs`
- Test: `test/MiniCc.Api.Test/Images/ImageWritePathTests.cs`（SQLite），以及现有的 `SaveArticleContentCommandHandlerTests`

**Interfaces:**
- Consumes：任务 2 的 `ImageHtml.Normalize`、`ImageHtml.NormalizeUrl`、`ImageHtml.CollectDownloadUrls`、`LegacyImageUrls.StripHtml`、`LegacyImageUrls.StripUrl`；任务 3 的 `Article.SyncImages`、`Article.NormalizeLegacyImages`
- Produces：
  - `void LegacyImageUrls.Backfill(Article article, DateTimeOffset now)` 仅处理 `article.Images == null`。正文剥离旧代理前缀，封面仅在已有扩展保存标记时剥离，然后调用 `NormalizeLegacyImages(body, cover, now)`。
  - `ArticleDomainService()` 构造函数没有参数
  - `FeedParser()` 构造函数没有参数
  - `SaveArticleContentCommandHandler` 和 `SaveLinkCommandHandler` 新增依赖 `LegacyImageUrls`

**行为约定：**
- **扩展保存全文（新文章）：** 领域服务用 `Normalize` 处理正文，用 `NormalizeUrl` 处理封面；创建后调用 `SyncImages(resetFailed: true, now)`。
- **扩展保存全文（已有文章）：**
  1. 如果 `existing.Images == null`，先 `legacy.Backfill(existing, now)`，然后设置扩展保存标记。
  2. 再执行第 44 行的比较；
  3. 替换正文和刷新/补全封面之后，调用 `SyncImages(resetFailed: true, now)`。
- **保存链接：** 新链接的封面用 `NormalizeUrl`，再 `SyncImages(resetFailed: true)`。已有文章先回填，再设置扩展保存标记并同步映射。
- **RSS 导入：** 新建文章时，封面用 `NormalizeUrl(item.ImageUrl)`，再调用 `SyncImages(resetFailed: false, now)`。

- [x] **Step 1: 写失败的测试，并修改现有断言**

```csharp
// ImageWritePathTests（SQLite，直接调用各个 handler 或 FeedImportService）
[Fact] async Task SaveContent_RecordsPendingImagesAndDueTime()
// 正文 <img src="https://a.test/x.png">，封面 https://a.test/c.jpg
// → Images 的键是这两个地址，状态都是 Pending，ImagesDueAt 不为 null
[Fact] async Task SaveContent_StoresNormalizedUrlsWithoutProxyPrefix()   // 正文和封面里都没有 "/imageProxy/"
[Fact] async Task Resave_ResetsFailedToPending()
[Fact] async Task FeedImport_DoesNotResetFailed()
// 先把记录设成 Failed，用同一个地址再导入一次，仍然是 Failed
[Fact] async Task SaveLink_ExistingLegacyRow_BackfillsBeforeSync()
[Fact] async Task Resave_LegacyProxiedArticleWithSameContent_KeepsRevisionAndProgress()
// 种子数据：正文是 TestImages.LegacyRewrite(html)，Images 设为 NULL，有阅读进度；
// 用相同的 html 重新保存 → ContentRevision、Progress*、ProgressUpdatedAt 都不变；
// Images 不为 null；正文 == ImageHtml.Normalize(html)
```

现有断言要改成新的写入格式：
- `SaveArticleContentCommandHandlerTests.cs:144` 改成 `existing.ImageUrl.Should().Be("https://example.com/new.jpg")`；
- `ArticleDomainServiceTests.cs:46` 改成 `article.ImageUrl.Should().Be("https://example.com/cover.png")`。

- [x] **Step 2: 运行测试，确认失败**

运行：`dotnet test test/MiniCc.Api.Test --filter "FullyQualifiedName~ImageWritePathTests|FullyQualifiedName~SaveArticleContentCommandHandlerTests|FullyQualifiedName~ArticleDomainServiceTests"`
预期：新测试 FAIL（`Images` 为空，或者仍带代理前缀）。

- [x] **Step 3: 按上面的约定实现；同时修改所有构造 `ArticleDomainService` 和 `FeedParser` 的测试**

- [x] **Step 4: 运行后端全部测试，确认通过**

运行：`dotnet test test/MiniCc.Api.Test`
预期：全部 PASS。`LinkApiTests.cs:145` 现在应该失败，因为 API 响应还没有经过解析服务；这条断言在任务 5 里改，本任务先暂时改成 `.EndWith("https://example.com/cover.png")`。

- [ ] **Step 5: 提交**

```bash
git add src/server test/MiniCc.Api.Test
git commit -m "feat(images): normalize image URLs on write and sync download state"
```

---

### Task 5: 返回文章时替换图片地址

**Files:**
- Create: `src/server/Core/ImageNs/Application/ImageUrlResolver.cs`
- Create: `src/server/Core/ImageNs/Domain/ImageKeys.cs`
- Create: `src/server/Shared/Config/ArticleImageMapping.cs`
- Modify: `src/server/Core/ArticleNs/Application/DTOs/ArticleListItemDto.cs`（加 `[JsonIgnore] public Dictionary<string, ArticleImage>? Images { get; set; }`，两个投影里都写上 `Images = a.Images`）
- Modify（替换返回的地址）：
  - `GetArticleByIdQueryHandler.cs:34`
  - `SaveArticleContentCommandHandler.cs:55,73`
  - `SaveLinkCommandHandler.cs:33`
  - `UpdateArticleCommandHandler.cs:49`
  - `UpdateArticleStateCommandHandler.cs:21`
  - `RestoreArticleCommandHandler.cs:28`
  - `GetArticleListQueryHandler.cs:12,46`（用 `ImageUrlResolver` 替换 `ImageProxyService`）
  - `TagService.cs:72`（UTF-16）
- Modify: `src/server/Core/ImageNs/Infrastructure/ImageServiceCollectionExtensions.cs`（注册单例 `ImageUrlResolver`）
- Modify: `test/.../Infrastructure/ArticleListFeedProjectionPostgresTests.cs:30`、`ArticleListPostgresTests.cs:26`（改为 `TestImages.Resolver()`）；`LinkApiTests.cs:145`
- Test: `test/MiniCc.Api.Test/Images/ImageUrlResolverTests.cs`、`test/MiniCc.Api.Test/Infrastructure/ImageResolutionApiTests.cs`

**Interfaces:**
- Consumes：`ImageOptions`、`ImageProxyService`、`LegacyImageUrls`、`ImageHtml.Rewrite`、`ArticleImage`
- Produces：
  - `static class ImageKeys`：
    - `string For(string hash, string ext)`
    - `bool TryParseKey(string key, out string hash, out string ext)`：键名必须匹配 `^[0-9a-f]{2}/[0-9a-f]{64}\.(ext)$`，并且目录名等于哈希的前 2 位
    - `bool TryParseName(string name, out string hash, out string ext)`：匹配 `^[0-9a-f]{64}\.(ext)$`
    - `string ContentType(string ext)`：`image/jpeg`、`image/png`、`image/gif`、`image/webp`、`image/avif`、`image/svg+xml`
  - `class ImageUrlResolver(IOptions<ImageOptions> options, ImageProxyService proxy, LegacyImageUrls legacy)`：
    - `string ResolveHtml(string html, IReadOnlyDictionary<string, ArticleImage>? images)`
    - `string ResolveUrl(string url, IReadOnlyDictionary<string, ArticleImage>? images, bool savedFromExtension)`
    - `string StoredUrl(string hash, string ext)`：按设计 3.1 的表格选择 `{ApiUrl}/…` 或 `{PublicBaseUrl}/…`
  - `ArticleImageMapping : IRegister` 为 `ArticleDto`、`ArticleListItemDto` 和 `ArticleLightDto` 注册图片地址映射。
  - 测试辅助：`TestImages.Resolver(Action<ImageOptions>? configure = null)`

**行为约定：**
- **ImageProxy 模式：** 使用 `ImageProxyService` 签名。回填前保留正文和已有扩展保存标记的封面中已识别的签名地址，包括历史绝对前缀。
- **Store 模式：**
  - `images == null` 时按回填规则临时剥离前缀。订阅封面保留来源地址。
  - 然后用 `ImageHtml.Rewrite` 替换：`map` 对 `NormalizeUrl(url)` 查字典，状态为 `Done` 就返回 `StoredUrl`，否则原样返回；
  - `needsNoReferrer = (o, m) => m == o && IsHttp(o)`。

- [x] **Step 1: 写失败的测试**

```csharp
// ImageUrlResolverTests：设计 3.1 表格里的每一行
[Fact] void Store_Authenticated_DoneUsesApiUrl()          // "/api/images/{hash}.png"
[Fact] void Store_PublicWithoutBase_DoneUsesApiUrl()
[Fact] void Store_PublicWithBase_DoneUsesDirectKey()      // "https://img.test/9f/{hash}.png"
[Fact] void Store_PendingOrFailed_KeepsOriginalAndAddsNoReferrer()
[Fact] void Store_LegacyNullImages_StripsPrefixAndAddsNoReferrer()
[Fact] void ResolveHtml_ProxyMode_LegacyRowUnchanged()
// html = TestImages.LegacyRewrite(x) → ResolveHtml(html, null) == html
[Fact] void ResolveHtml_ProxyMode_EqualsLegacyRewriteOfNormalized()
[Fact] void ResolveHtml_ResolvesEntityEncodedAndUnicodeUrls()
// <img src="https://例子.测试/图/a.png?x=1&amp;y=2">；字典的键是 NormalizeUrl(解码后的地址)，状态 Done → 地址被替换
[Fact] void ResolveHtml_ReplacesEveryOccurrence()
// 同一地址出现在 img src、另一个 img 的 srcset、picture>source 里 → 三处都被替换
[Fact] void ResolveUrl_EmptyCover_ReturnsEmpty()
[Fact] void ImageKeys_ParseAndFormat()   // For/TryParseKey/TryParseName 的往返；拒绝大写、目录名和哈希不一致、未知扩展名

// ImageResolutionApiTests（SQLite 工厂 SqliteApi.CreateFactory）
[Fact] async Task Detail_DoneImage_ReturnsStoredUrl()
// 通过 API 保存文章，直接改数据库把记录设成 Done → GET /api/articles/{id} 的正文和封面里出现 "/api/images/"
[Fact] async Task List_Covers_AreResolved_AndImagesNotSerialized()
// GET /api/articles：imageUrl 已替换；响应 JSON 里没有 "images" 属性
[Fact] async Task StateAndRestore_Responses_ResolveCover()
[Fact] async Task ImageProxyMode_DetailAndListReturnSignedUrls()
// 设置 Images:Mode=ImageProxy → 正文 == TestImages.LegacyRewrite(原始 html)，封面带签名
```

`LinkApiTests.cs:145` 改成：在 Store 模式下，封面还没下载完，返回 `https://example.com/cover.png`。

- [x] **Step 2: 运行测试，确认失败**

运行：`dotnet test test/MiniCc.Api.Test --filter "FullyQualifiedName~ImageUrlResolverTests|FullyQualifiedName~ImageResolutionApiTests"`
预期：编译失败。

- [x] **Step 3: 实现解析服务和 `ImageKeys`，通过 Mapster 注册集中映射文章响应。列表的 EF 投影在取回后解析封面**

- [x] **Step 4: 运行后端全部测试，确认通过**

运行：`dotnet test test/MiniCc.Api.Test`
预期：全部 PASS。

- [ ] **Step 5: 提交**

```bash
git add src/server test/MiniCc.Api.Test
git commit -m "feat(images): resolve image URLs when returning articles"
```

---

### Task 6: 图片存储（本地目录和 S3）

**Files:**
- Create: `src/server/Core/ImageNs/Application/IImageStore.cs`
- Create: `src/server/Core/ImageNs/Infrastructure/Storage/LocalImageStore.cs`
- Create: `src/server/Core/ImageNs/Infrastructure/Storage/S3ImageStore.cs`
- Modify: `src/server/Core/ImageNs/Infrastructure/ImageServiceCollectionExtensions.cs`（按 `Storage.Provider` 注册 `IImageStore` 单例；S3 时注册 `IAmazonS3`）
- Modify: `Directory.Packages.props`、`src/server/MiniCc.Api.csproj`（`AWSSDK.S3`）、`test/MiniCc.Api.Test/MiniCc.Api.Test.csproj`（`Testcontainers.Minio` 4.15.0）
- Test: `test/MiniCc.Api.Test/Images/ImageStoreContract.cs`、`LocalImageStoreTests.cs`、`S3ImageStoreTests.cs`、`S3ImageStoreRequestTests.cs`

**Interfaces:**
- Produces：
  - `interface IImageStore`：
    - `Task PutAsync(string key, byte[] content, string contentType, CancellationToken ct)`：对象不存在就创建；已存在就刷新它的修改时间
    - `Task<Stream?> OpenReadAsync(string key, CancellationToken ct)`：不存在时返回 null
    - `Task<DateTimeOffset?> GetLastModifiedAsync(string key, CancellationToken ct)`
    - `IAsyncEnumerable<StoredImage> ListAsync(CancellationToken ct)`
    - `Task DeleteAsync(string key, CancellationToken ct)`
  - `sealed record StoredImage(string Key, DateTimeOffset LastModified)`
  - `LocalImageStore(string rootPath)`：构造函数里不做任何 IO
  - `S3ImageStore(IAmazonS3 client, string bucket)`

**行为约定：**
- **Local：**
  - 写入时先在目标目录写 `{key}.{guid}.tmp`，再 `File.Move(tmp, target, overwrite: false)`；目标已存在时删除临时文件，改用 `File.SetLastWriteTimeUtc` 刷新时间；
  - `ListAsync` 返回相对根目录的路径，统一用 `/` 分隔，并跳过 `.tmp` 文件。
- **S3：**
  - `AmazonS3Config { ServiceURL, ForcePathStyle = true, AuthenticationRegion = Region, RequestChecksumCalculation = WHEN_REQUIRED, ResponseChecksumValidation = WHEN_REQUIRED }`；
  - PutObject 设置 `ContentType` 和 `UseChunkEncoding = false`。HTTPS 端点也按 Cloudflare 示例关闭负载签名和默认校验。测试使用生产注册的客户端和存储实现。MinIO 已通过实测，R2 专用桶实测待第 4 节验收。
  - `PutAsync` 直接覆盖写入；`ListAsync` 用 ListObjectsV2 分页；`GetLastModifiedAsync` 用 HEAD，返回 404 时为 null。

- [x] **Step 1: 写失败的测试**

`ImageStoreContract` 是抽象基类，两个实现各继承一次：

```csharp
[Fact] async Task Put_ThenOpen_ReturnsSameBytes()
[Fact] async Task Open_Missing_ReturnsNull()
[Fact] async Task Put_Existing_RefreshesLastModified()   // 第二次 Put 之后，修改时间晚于第一次
[Fact] async Task List_ReturnsKeysWithForwardSlashes()
[Fact] async Task Delete_RemovesObject()

// LocalImageStoreTests 额外：
[Fact] async Task List_SkipsTempFiles()

// S3ImageStoreTests 额外：用 MinioBuilder 启动容器，放在新的 "Minio" 集合里
```

- [x] **Step 2: 运行测试，确认失败**

运行：`dotnet test test/MiniCc.Api.Test --filter "FullyQualifiedName~ImageStoreTests"`
预期：编译失败。

- [x] **Step 3: 添加包，实现两个存储，按配置注册**

运行：
```bash
dotnet add src/server package AWSSDK.S3
dotnet add test/MiniCc.Api.Test package Testcontainers.Minio --version 4.15.0
```
确认版本写进了 `Directory.Packages.props`。

实际使用 `AWSSDK.S3 4.1.2` 和 NuGet 已发布的 `Testcontainers.Minio 4.15.0`，后者与现有 `Testcontainers 4.16.0` 一起通过实测。

- [x] **Step 4: 运行测试，确认通过**

运行：`dotnet test test/MiniCc.Api.Test --filter "FullyQualifiedName~ImageStoreTests"`
预期：Local 和 MinIO 两组都 PASS。

- [ ] **Step 5: 提交**

```bash
git add Directory.Packages.props src/server test/MiniCc.Api.Test
git commit -m "feat(images): add local and S3-compatible image stores"
```

---

### Task 7: 下载器（内网拦截、类型识别、失败分类、重试间隔）

**Files:**
- Create: `src/server/Core/ImageNs/Infrastructure/Download/PrivateNetworkGuard.cs`
- Create: `src/server/Core/ImageNs/Domain/ImageSniffer.cs`
- Create: `src/server/Core/ImageNs/Domain/ImageRetry.cs`
- Create: `src/server/Core/ImageNs/Application/IImageDownloader.cs`、`src/server/Core/ImageNs/Infrastructure/Download/ImageDownloader.cs`
- Modify: `src/server/Core/ImageNs/Infrastructure/ImageServiceCollectionExtensions.cs`（注册命名 HttpClient `"images"`，以及单例 `AddSingleton<IImageDownloader, ImageDownloader>()`）
- Test: `test/MiniCc.Api.Test/Images/PrivateNetworkGuardTests.cs`、`ImageSnifferTests.cs`、`ImageRetryTests.cs`、`ImageDownloaderTests.cs`

**Interfaces:**
- Produces：
  - `static class PrivateNetworkGuard`：`bool IsBlocked(IPAddress address)`；`ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)`
  - `class BlockedAddressException(string host) : Exception`
  - `static class ImageSniffer`：`string? Detect(ReadOnlySpan<byte> content)`，返回扩展名或 null
  - `static class ImageRetry`：`const int MaxAttempts = 10`；`DateTimeOffset? NextAttempt(int attempts, DateTimeOffset now, DateTimeOffset? retryAfter)`，返回 null 表示放弃
  - `enum DownloadOutcome { Success, Permanent, Transient }`
  - `sealed record DownloadResult(DownloadOutcome Outcome, byte[]? Content = null, string? Ext = null, string? Reason = null, DateTimeOffset? RetryAfter = null)`
  - `interface IImageDownloader`：`Task<DownloadResult> DownloadAsync(string url, string articleUrl, CancellationToken ct)`。任务 8 的测试通过这个接口替换成假实现。
  - `class ImageDownloader(IHttpClientFactory clients) : IImageDownloader`：`const int MaxBytes = 20 * 1024 * 1024`

**行为约定：**
- `IsBlocked` 拦截的地址：
  - IPv4：`0.0.0.0/8`、`10/8`、`100.64/10`、`127/8`、`169.254/16`、`172.16/12`、`192.168/16`、`224/4`、`255.255.255.255`；
  - IPv6：`::`、`::1`、`fc00::/7`、`fe80::/10`、`ff00::/8`；
  - 映射到 IPv4 的 IPv6 地址，按对应的 IPv4 地址判断。
- `ConnectAsync`：先 `Dns.GetHostAddressesAsync(host)`，只要有一个地址被拦截，就抛 `BlockedAddressException`；否则用 `Socket` 连接第一个地址，返回 `NetworkStream`。
- HttpClient `"images"` 使用 `SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = true, MaxAutomaticRedirections = 5, AutomaticDecompression = All }`。`AllowPrivateNetworks` 为 false 时使用 `PrivateNetworkGuard.ConnectAsync`。API 需要直连图片来源。
- `DownloadAsync`：
  - 使用与调用方取消令牌关联的 30 秒期限，从取得域名名额后开始计时。
  - 请求时使用 `ResponseHeadersRead`，读取时边读边计数，超过 `MaxBytes` 就中止；
  - `ImageWorkService` 的全局信号量限制 4 个传输，名额保留到上传结束。`ImageDownloader` 限制每个域名同时下载 2 张图。
- 失败分类：

  | 情况 | 结果 | `reason` |
  |---|---|---|
  | 地址不是 http/https 绝对地址 | Permanent | `invalidUrl` |
  | `BlockedAddressException`（包括包在 `HttpRequestException` 里的） | Permanent | `blockedAddress` |
  | 404、410 | Permanent | `notFound` |
  | 带 Referer 重试后仍是 401 或 403 | Permanent | `forbidden` |
  | 除 408、429 以外的其他 4xx | Permanent | `http{code}` |
  | 超过大小上限 | Permanent | `tooLarge` |
  | `ImageSniffer` 返回 null | Permanent | `unsupportedType` |
  | 超时 | Transient | `timeout` |
  | 其他网络错误 | Transient | `network` |
  | 408、429、5xx | Transient | `http{code}`，并带上 `RetryAfter` |
- `ImageRetry.NextAttempt` 的间隔依次是 1 分钟、10 分钟、1 小时、6 小时、24 小时，之后一直是 24 小时；`attempts >= 10` 返回 null；结果取计算值和 `retryAfter` 中较晚的那个。
- `ImageSniffer` 识别的类型：
  - JPEG：`FF D8 FF`
  - PNG：`89 50 4E 47 0D 0A 1A 0A`
  - GIF：`GIF87a` / `GIF89a`
  - WebP：`RIFF????WEBP`
  - AVIF：第 4–11 字节是 `ftypavif` 或 `ftypavis`
  - SVG：去掉 UTF-8 BOM 和空白后以 `<svg` 开头；或者以 `<?xml` 开头，且前 4KB 内出现 `<svg`

- [x] **Step 1: 写失败的测试**

```csharp
// PrivateNetworkGuardTests
[Theory]
[InlineData("127.0.0.1")] [InlineData("10.1.2.3")] [InlineData("172.31.255.1")] [InlineData("192.168.1.10")]
[InlineData("169.254.169.254")] [InlineData("100.64.0.1")] [InlineData("0.0.0.0")] [InlineData("224.0.0.1")]
[InlineData("::1")] [InlineData("fd00::1")] [InlineData("fe80::1")] [InlineData("::ffff:192.168.0.1")]
void IsBlocked_PrivateAddresses(string ip)
[Theory] [InlineData("1.1.1.1")] [InlineData("172.32.0.1")] [InlineData("2606:4700::1111")] void IsBlocked_PublicAddresses(string ip)
[Fact] async Task Client_LoopbackServer_IsBlockedUnlessAllowed()
// 在 127.0.0.1 上用 TcpListener 写一个最小 HTTP 服务器：
// AllowPrivateNetworks=false 时得到 Permanent/blockedAddress；=true 时 Success

// ImageSnifferTests：每种类型的文件头；伪装成 .png 的 HTML 返回 null；SVG 的两种写法

// ImageRetryTests
[Fact] void Schedule_FollowsSpec()   // attempts 1..9 → +1m, +10m, +1h, +6h, +24h, +24h, +24h, +24h, +24h；10 → null
[Fact] void RetryAfter_IsLowerBound()

// ImageDownloaderTests（注入假的 HttpMessageHandler，通过 IHttpClientFactory mock 提供）
[Fact] async Task Download_Png_Success()
[Fact] async Task Download_FirstRequestHasNoReferer_RetriesWithArticleRefererOn403()
[Fact] async Task Download_404_IsPermanentNotFound()
[Fact] async Task Download_503WithRetryAfter_IsTransient()
[Fact] async Task Download_Html200_IsUnsupportedType()
[Fact] async Task Download_OverMaxBytes_IsTooLarge()   // 响应流在超过 20MB 后中止，不能整个读进内存
[Fact] async Task Download_SendsUserAgentAndAccept()
```

- [x] **Step 2: 运行测试，确认失败**

运行：`dotnet test test/MiniCc.Api.Test --filter "FullyQualifiedName~PrivateNetworkGuardTests|FullyQualifiedName~ImageSnifferTests|FullyQualifiedName~ImageRetryTests|FullyQualifiedName~ImageDownloaderTests"`
预期：编译失败。

- [x] **Step 3: 按上面的约定实现**

- [x] **Step 4: 运行测试，确认通过**

运行：同 Step 2。预期：全部 PASS。

- [ ] **Step 5: 提交**

```bash
git add src/server/Core/ImageNs test/MiniCc.Api.Test/Images
git commit -m "feat(images): download images with SSRF guard, sniffing and retry schedule"
```

---

### Task 8: 后台任务（回填、领取、下载、写回）

**Files:**
- Create: `src/server/Core/ImageNs/Infrastructure/Worker/ImageLeases.cs`
- Create: `src/server/Core/ImageNs/Application/IImageArticleRows.cs`、`src/server/Core/ImageNs/Infrastructure/Persistence/ImageArticleRows.cs`
- Create: `src/server/Core/ImageNs/Application/ImageWorkService.cs`
- Create: `src/server/Core/ImageNs/Infrastructure/Worker/ImageDownloadWorker.cs`
- Modify: `src/server/Core/ImageNs/Infrastructure/ImageServiceCollectionExtensions.cs`（注册行锁实现、工作服务和后台任务）
- Test: `test/MiniCc.Api.Test/Infrastructure/ImageWorkSqliteTests.cs`、`test/MiniCc.Api.Test/Infrastructure/ImageWorkPostgresTests.cs`

**Interfaces:**
- Consumes：`IImageArticleRows.UpdateLockedAsync`、`LegacyImageUrls.Backfill`、`IImageDownloader.DownloadAsync`、`ImageRetry.NextAttempt`、`IImageStore.PutAsync`、`ImageKeys.For` / `ContentType`、`Article.ApplyImageResult`、`Article.RecomputeImagesDue`
- Produces：
  - `static class ImageLeases`：`Task<List<Guid>> ClaimDueAsync(MiniCcDbContext db, DateTimeOffset now, CancellationToken ct)`
  - `sealed record ImageProcessResult(int Done, int Failed, int Retrying)`
  - `class ImageWorkService(MiniCcDbContext db, IImageArticleRows articleRows, LegacyImageUrls legacy, IImageDownloader downloader, IImageStore store, ILogger<ImageWorkService> logger)`：
    - `Task<int> BackfillBatchAsync(DateTimeOffset now, CancellationToken ct)`：每批最多 50 篇，返回处理的篇数，并记录剩余篇数
    - `Task<ImageProcessResult> ProcessAsync(Guid articleId, DateTimeOffset now, CancellationToken ct)`
  - `class ImageDownloadWorker(IServiceScopeFactory scopes, IOptions<ImageOptions> options, ILogger<ImageDownloadWorker> logger) : BackgroundService`

**领取 SQL**（订阅源的写法见 `FeedLeases.cs`；这里一律传参数，不用 `now()`。PostgreSQL 传 `DateTimeOffset`，SQLite 传 `UtcTicks`）：

```sql
-- PostgreSQL
UPDATE "Articles" SET "ImagesDueAt" = {until}
WHERE "Id" IN (SELECT "Id" FROM "Articles" WHERE "ImagesDueAt" <= {now}
  ORDER BY "ImagesDueAt" LIMIT 10 FOR UPDATE SKIP LOCKED)
RETURNING "Id" AS "Value"
-- SQLite：同样的语句，去掉 FOR UPDATE SKIP LOCKED；{now}/{until} 用 ticks
```

`until = now + 10 分钟`。这条语句不过滤 `DeletedAt`，回收站里的文章也会被领取。

**行为约定：**
- **`BackfillBatchAsync`：**
  - 用 `IgnoreQueryFilters` 查出 `Images == null` 的前 50 个 Id；
  - 每篇在**自己的**事务里处理：`FromSqlForUpdate($"SELECT * FROM \"Articles\" WHERE \"Id\" = {id}").IgnoreQueryFilters()` 锁住文章，仍是 null 才调用 `legacy.Backfill`，保存并提交；
  - 结束时记录日志：已回填 X 篇，剩余 Y 篇。
- **`ProcessAsync`：**
  1. 不跟踪地读取文章地址和 `Images`；
  2. 对 `Pending` 且 `NextAttemptAt <= now` 的地址并发调用 `DownloadAsync`；
  3. 成功时计算 SHA-256（小写十六进制），调用 `store.PutAsync(ImageKeys.For(...))`，结果是 `Done(hash, ext)`；`PutAsync` 抛异常按暂时性失败处理，`reason` 写 `storage`；
  4. 暂时性失败时，`attempts = 原值 + 1`，`next = NextAttempt(...)`；`next` 为 null 就记为 `Failed`；
  5. 永久失败直接记为 `Failed(reason)`；
  6. 最后在**一个**事务里锁住文章，对每个结果调用 `ApplyImageResult`，**无论有没有结果**都调用 `RecomputeImagesDue()`（用真实的到期时间替换租约值），保存并提交。文章已被永久删除时，回滚并返回。
- **`ImageDownloadWorker`：**
  - `PeriodicTimer(30 秒)`；
  - 每一轮先循环调用 `BackfillBatchAsync`，直到返回 0；
  - 然后，只有 `Mode=Store` 时才领取，并用 `Task.WhenAll` 处理领到的文章，每篇用独立的 scope；
  - 每轮记录一条汇总日志（文章数，以及 Done、Failed、Retrying 的数量）；
  - 异常处理照 `FeedRefreshWorker` 的写法。

- [x] **Step 1: 写失败的测试**

用 `IImageDownloader` 和 `IImageStore` 的假实现测试。存储用 `LocalImageStore`（临时目录），下载器用 Moq。

```csharp
// ImageWorkSqliteTests（SqliteFixture）
[Fact] async Task Process_Success_StoresFileAndMarksDone()
// 存储里有 ImageKeys.For(sha256(bytes), "png")；记录是 Done；ImagesDueAt == null
[Fact] async Task Process_Transient_SchedulesRetry()
// Attempts == 1，Next == now + 1 分钟，ImagesDueAt == Next
[Fact] async Task Process_TenthTransientFailure_MarksFailed()
[Fact] async Task Process_ResaveDuringDownload_DoesNotApplyStaleResult()
// 假下载器在返回之前，用另一个上下文把文章正文换成新地址 → 旧地址的记录已删除，没有写入；新地址是 Pending
[Fact] async Task Process_ClearsLeaseEvenWithoutDueEntries()
[Fact] async Task Process_TrashedArticle_IsProcessed()
[Fact] async Task Process_DoesNotTouchRevisionProgressOrHighlights()
[Fact] async Task Backfill_StripsLegacyUrlsWithoutChangingRevision()
// 种子数据：正文是 LegacyRewrite(html)，Images=NULL，有进度，有一个 Ok 状态的高亮
// → 正文 == Normalize(html)；ContentRevision、进度、ProgressUpdatedAt、高亮的 AnchorStatus 都不变；Images 是 Pending
[Fact] async Task Backfill_RunsInBothModes()   // BackfillBatchAsync 不读取 Mode
[Fact] async Task Claim_SetsLeaseAndSkipsLeased()
// 领取后 ImagesDueAt == now + 10 分钟；用同一个 now 再领一次，返回空

// ImageWorkPostgresTests（"Postgres" 集合）
[Fact] async Task Claim_ConcurrentWorkers_DoNotClaimSameArticle()   // 两个上下文并发领取，结果没有交集
[Fact] async Task ExpiredLease_IsReclaimed()
```

- [x] **Step 2: 运行测试，确认失败**

运行：`dotnet test test/MiniCc.Api.Test --filter "FullyQualifiedName~ImageWork"`
预期：编译失败。

- [x] **Step 3: 按上面的约定实现**

- [x] **Step 4: 运行测试，确认通过**

运行：同 Step 2。预期：SQLite 和 PostgreSQL 两组都 PASS。

- [ ] **Step 5: 提交**

```bash
git add src/server/Core/ImageNs test/MiniCc.Api.Test/Infrastructure
git commit -m "feat(images): backfill legacy articles and download images in the background"
```

---

### Task 9: 图片端点

**Files:**
- Create: `src/server/Controllers/ImagesController.cs`
- Modify: `src/server/Core/ImageNs/Infrastructure/ImageServiceCollectionExtensions.cs`（用 `AddOptions<AuthorizationOptions>().Configure<IOptions<ImageOptions>>(...)` 注册授权策略）
- Test: `test/MiniCc.Api.Test/Infrastructure/ImageApiTests.cs`

**Interfaces:**
- Consumes：`IImageStore.OpenReadAsync`、`ImageKeys.TryParseName` / `For` / `ContentType`、`ImageOptions`
- Produces：
  - `static class ImagePolicies { public const string Read = "images:read"; }`
  - `GET /api/images/{name}`

**行为约定：**
- 授权策略：
  - `Delivery=Public` 时 `RequireAssertion(_ => true)`；
  - 否则 `AddAuthenticationSchemes(Cookie, ApiKey).RequireAuthenticatedUser()`，和其他控制器一样，未登录返回 401。
- 处理顺序：
  1. `Mode=ImageProxy` 时返回 404；
  2. `TryParseName` 失败时返回 404；
  3. `OpenReadAsync` 返回 null 时返回 404；
  4. 否则返回 `File(stream, ContentType(ext), lastModified: null, entityTag: "\"{hash}\"")`，并设置 Global Constraints 里的 `Cache-Control`、`X-Content-Type-Options`、`Content-Security-Policy` 响应头。

- [x] **Step 1: 写失败的测试**

用 SQLite 工厂，把 `Images:Storage:LocalPath` 设成临时目录，预先写入一个 PNG 和一个 SVG。

```csharp
[Fact] async Task Authenticated_NoCredentials_Returns401()
[Fact] async Task Authenticated_ApiKey_Returns200WithHeaders()
// Content-Type image/png；ETag "\"{hash}\""；Cache-Control "private, max-age=31536000, immutable"；nosniff；CSP 含 sandbox
[Fact] async Task Public_NoCredentials_Returns200_PublicCacheControl()
[Fact] async Task Svg_HasSandboxCsp()
public static TheoryData<string> MalformedNames => new()
{
    new string('A', 64) + ".png",     // 大写十六进制
    "..%2f..%2fetc%2fpasswd",         // 路径穿越
    new string('a', 64) + ".exe",     // 不支持的扩展名
    new string('a', 64),              // 没有扩展名
    new string('a', 63) + ".png",     // 长度不对
};
[Theory, MemberData(nameof(MalformedNames))]
async Task Get_MalformedNames_Return404(string name)   // 同时断言存储的 OpenReadAsync 没有被调用（用包装的存储计数）
[Fact] async Task Get_MissingObject_Returns404()
[Fact] async Task ImageProxyMode_Returns404()
```

- [x] **Step 2: 运行测试，确认失败**

运行：`dotnet test test/MiniCc.Api.Test --filter "FullyQualifiedName~ImageApiTests"`
预期：FAIL（返回 404，因为路由还不存在）。

- [x] **Step 3: 按上面的约定实现**

- [x] **Step 4: 运行测试，确认通过**

运行：同 Step 2。预期：全部 PASS。

- [ ] **Step 5: 提交**

```bash
git add src/server test/MiniCc.Api.Test/Infrastructure/ImageApiTests.cs
git commit -m "feat(images): serve stored images with auth-aware caching and safe headers"
```

---

### Task 10: 存储清理和缺失文件修复

**Files:**
- Create: `src/server/Core/ImageNs/Application/ImageCollector.cs`
- Modify: `src/server/Core/ArticleNs/Application/Services/TrashCleanup.cs`（`TrashPurgeWorker` 在回收站清理之后调用 `ImageCollector`，单独记录异常。模式判断由 `ImageCollector` 负责）
- Modify: `src/server/Core/ImageNs/Infrastructure/ImageServiceCollectionExtensions.cs`（`AddScoped<ImageCollector>()`）
- Test: `test/MiniCc.Api.Test/Infrastructure/ImageCollectorTests.cs`

**Interfaces:**
- Consumes：`IImageStore.ListAsync` / `GetLastModifiedAsync` / `DeleteAsync`、`ImageKeys.TryParseKey`、`Article.ResetMissingImages`、`IImageArticleRows.UpdateLockedAsync`
- Produces：
  - `sealed record CollectResult(bool Skipped, int Deleted, int Repaired)`
  - `class ImageCollector(MiniCcDbContext db, IImageArticleRows articleRows, IImageStore store, IOptions<ImageOptions> options, ILogger<ImageCollector> logger)`：
    - `static readonly TimeSpan Grace = TimeSpan.FromHours(24)`
    - `Task<CollectResult> CollectAsync(DateTimeOffset now, CancellationToken ct)`

**行为约定**（设计 3.9 的顺序）：
1. **跳过条件：** `Mode != Store`，或者存在 `Images == null` 的文章（`IgnoreQueryFilters`）时，返回 `Skipped`。
2. **标记：** 按 Id 分批（每批 200）读取所有文章的 `Images`（`IgnoreQueryFilters`），收集 `Done` 记录的 hash。
3. **列出：** 列出存储里的对象，只保留 `TryParseKey` 成功的。列出时出错就返回 `Skipped`，并记录日志。
4. **清除：** 哈希不在标记集合里、并且 `LastModified < now - Grace` 的对象，删除前再调用 `GetLastModifiedAsync`，仍然早于 `now - Grace` 才删除。
5. **修复：** 标记集合里有、但列表里没有的哈希，组成集合 `missing`。对引用了它们的文章，逐篇锁住（方式同任务 8），调用 `ResetMissingImages(missing, now)`，保存并提交。

- [x] **Step 1: 写失败的测试**

用 `LocalImageStore`（临时目录）和 SQLite，通过 `File.SetLastWriteTimeUtc` 控制文件的修改时间。

```csharp
[Fact] async Task SkipsWhenAnyArticleHasNullImages()
[Fact] async Task SkipsInImageProxyMode()
[Fact] async Task DeletesOldUnreferencedFiles()
[Fact] async Task KeepsFilesYoungerThanGrace()
[Fact] async Task KeepsFilesReferencedByTrashedArticles()
[Fact] async Task KeepsForeignKeys()                 // "notes.txt"、"ab/xyz.png"、目录名和哈希不一致的对象都不删
[Fact] async Task RechecksLastModifiedBeforeDelete()
// 测试用的存储包装：ListAsync 返回旧时间，GetLastModifiedAsync 返回新时间 → 不删除
[Fact] async Task RepairsDoneEntriesWithMissingFiles()
// Done 记录对应的文件不存在 → 记录变成 Pending(Attempts 0)，ImagesDueAt == now；Repaired == 1
[Fact] async Task PurgedArticleImages_AreDeletedOnNextRun()
// ArticlePurger 删除文章后，再运行（文件时间早于宽限期）→ 文件被删除
```

- [x] **Step 2: 运行测试，确认失败**

运行：`dotnet test test/MiniCc.Api.Test --filter "FullyQualifiedName~ImageCollectorTests"`
预期：编译失败。

- [x] **Step 3: 实现 `ImageCollector`，并接入 `TrashPurgeWorker`**

- [x] **Step 4: 运行测试，确认通过**

运行：`dotnet test test/MiniCc.Api.Test --filter "FullyQualifiedName~ImageCollectorTests|FullyQualifiedName~Trash"`
预期：全部 PASS。

- [ ] **Step 5: 提交**

```bash
git add src/server test/MiniCc.Api.Test/Infrastructure/ImageCollectorTests.cs
git commit -m "feat(images): mark-and-sweep stored images and repair missing files"
```

---

### Task 11: 前端保留 no-referrer

**Files:**
- Modify: `src/web/src/app/pages/article/[id]/components/Client.tsx:105`（`DOMPurify.sanitize(data.readableContent, { ADD_ATTR: ['referrerpolicy'] })`）、`:191`（封面 `<img>` 加 `referrerPolicy="no-referrer"`）
- Modify: `src/web/src/app/components/ArticleCard.tsx:109`（加 `referrerPolicy="no-referrer"`）
- Test: `src/web/src/app/pages/article/[id]/components/Client.test.tsx`、`src/web/src/app/components/ArticleCard.test.tsx`

- [x] **Step 1: 写失败的测试**

```tsx
// Client.test.tsx：沿用现有的 details 夹具，只改 readableContent
it('keeps referrerpolicy on article images after sanitizing', ...)
// readableContent: '<p>Body</p><img src="https://img.example/a.png" referrerpolicy="no-referrer">'
// → container.querySelector('img[src="https://img.example/a.png"]')?.getAttribute('referrerpolicy') === 'no-referrer'
it('renders link cover without referrer', ...)
// 用现有的 link 夹具，imageUrl 设成 'https://img.example/c.jpg' → .linkCover 的 referrerpolicy === 'no-referrer'

// ArticleCard.test.tsx
it('renders cover without referrer', ...)
// imageUrl 'https://img.example/c.jpg' → img 的 referrerpolicy === 'no-referrer'
```

- [x] **Step 2: 运行测试，确认失败**

运行：`pnpm test Client ArticleCard`
预期：新的 3 个测试 FAIL（属性为 null）。

- [x] **Step 3: 按上面的说明修改**

- [x] **Step 4: 运行测试和检查，确认通过**

运行：`pnpm test && cd src/web && npx tsc --noEmit && cd ../.. && pnpm lint`
预期：Vitest 全部 PASS；tsc 退出码 0；lint 只剩原有的 ArticleCard 原生 img 警告。

- [ ] **Step 5: 提交**

```bash
git add src/web/src
git commit -m "feat(web): load unresolved article images without a referrer"
```

---

### Task 12: 部署配置和文档

**Files:**
- Modify: `src/ng/conf.d/default.conf`：删掉第 11-14 行的 upstream 和第 31-35 行的 location；在 `server` 块里加上 `include /etc/nginx/conf.d/locations/*.conf;`
- Create: `src/ng/conf.d/locations/.gitkeep`
- Create: `src/ng/image-proxy.location.conf`
- Modify: `deploy/docker-compose.yaml`、`deploy/docker-compose.sqlite.yaml`
- Create: `deploy/docker-compose.image-proxy.yaml`
- Modify: `README.md`（第 41、54-60、78-79、97-116、156-157、249 行附近；新增"图片存储"和"从旧版本升级"两节）
- Modify: `CLAUDE.md`（Image Proxy、Image URLs、External Services、P5 里的 "signed image rewriting"；新增 "Image Storage" 一节）

**`image-proxy.location.conf` 的完整内容：**

```nginx
location /imageProxy {
    rewrite ^/imageProxy/(.*)$ /$1 break;
    proxy_pass http://image-proxy:8080;
}
```

**Compose 的修改：**
- **两个默认文件：**
  - 删掉 `image-proxy` 服务、`image-proxy-data` 卷、`ImageProxy__SignatureKey`，以及 lb 的 `depends_on` 里的 `image-proxy`；
  - PostgreSQL 版给 `minicc-api` 加上 `volumes: [api-data:/app/data]`，并在顶层声明 `api-data:` 卷。
- **`docker-compose.image-proxy.yaml`：**
  - `minicc-api.environment` 设置 `Images__Mode: ImageProxy` 和 `ImageProxy__SignatureKey: "${IMAGE_PROXY_SECRET:?Set IMAGE_PROXY_SECRET}"`；
  - `image-proxy` 服务照搬旧定义，`IMAGE_PROXY_SECRET` 同样用 `:?` 强制要求；
  - `lb` 增加 `depends_on: [image-proxy]`，并挂载 `../src/ng/image-proxy.location.conf:/etc/nginx/conf.d/locations/image-proxy.conf:ro`；
  - 声明 `image-proxy-data:` 卷。

**README 必须写到的内容**（设计 3.12）：
- 三种访问方式；
- 本地目录和 R2 的配置示例，使用环境变量；
- R2 的设置步骤：创建存储桶、创建对象读写令牌、Endpoint `https://<account_id>.r2.cloudflarestorage.com`、Region `auto`、可选的公开自定义域名；
- 备份时要另外备份图片目录；
- 换存储时用 rclone 同步；
- 切换到 image-proxy 模式的命令；
- 升级步骤 1–5，其中第 4 步要求保留 `image-proxy-data` 卷，第 5 步说明回退必须恢复备份；
- `Images__AllowPrivateNetworks` 的含义和风险；
- 最终的各项数值（30 秒、10 篇、4/2 并发、20MB、重试间隔、24 小时宽限期）。

- [x] **Step 1: 修改 nginx 和三个 Compose 文件**

- [x] **Step 2: 验证配置**

运行：
```bash
podman compose -f deploy/docker-compose.yaml config >/dev/null && \
podman compose -f deploy/docker-compose.sqlite.yaml config >/dev/null && \
podman compose -f deploy/docker-compose.yaml -f deploy/docker-compose.image-proxy.yaml config | grep -q 'image-proxy.conf' && \
IMAGE_PROXY_SECRET=x podman compose -f deploy/docker-compose.sqlite.yaml -f deploy/docker-compose.image-proxy.yaml config >/dev/null && echo OK
```
预期：输出 `OK`。注意：没有设置 `IMAGE_PROXY_SECRET` 时，第三条命令应该报错。验证时先 `unset IMAGE_PROXY_SECRET`，确认它确实报错，再设置变量运行。

运行：`podman run --rm -v "$PWD/src/ng/conf.d:/etc/nginx/conf.d:ro" nginx:alpine nginx -t`
预期：`syntax is ok`，`test is successful`。默认配置不再依赖 image-proxy 主机。

- [x] **Step 3: 更新 README 和 CLAUDE.md**

确认 `grep -n "IMAGE_PROXY_SECRET" README.md` 只出现在"切换到 image-proxy 模式"和升级两节里。

- [ ] **Step 4: 提交**

```bash
git add src/ng deploy README.md CLAUDE.md
git commit -m "feat(deploy): drop image-proxy from default stacks and document image storage"
```

---

### Task 13: 端到端测试和最终验收

**Files:**
- Create: `src/web/e2e/image-storage.spec.ts`
- Modify: `CLAUDE.md`（在 Frontend verification 段落里写明 E2E 需要的 `Images__AllowPrivateNetworks=true`、`E2E_IMAGE_URL`、`E2E_IMAGE_PORT`）

**E2E 约定：**
- 在 `beforeAll` 里用 `node:http` 起一个模拟服务器，监听 `0.0.0.0:${E2E_IMAGE_PORT ?? 5019}`。
- API 访问图片用的地址是 `${E2E_IMAGE_URL ?? 'http://127.0.0.1:5019'}`。API 跑在容器里时，由测试人员把它设成容器能访问到的地址。
- 请求带 `Referer` 头时返回 403；不带时返回一张内置的 1×1 PNG。
- 登录方式和 `reading-progress.spec.ts` 相同。每个 project 用不同的 `run` 值。

- [x] **Step 1: 写 `image-storage.spec.ts`**

```ts
test('stores article images and serves them after the source disappears', async ({ page }, testInfo) => {
  // 1. page.request.post('/api/articles/content', { data: { url: `https://e2e.invalid/${run}`, title: `Image ${run}`,
  //      originalContent: '<p>x</p>', readableContent: `<p>Image ${run}</p><img src="${imageUrl}/a.png?run=${run}">`,
  //      author: '', summary: '', imageUrl: '' } }) → 201，记下 id
  // 2. 打开 /pages/article/{id}：img 的 src 以 imageUrl 开头，referrerpolicy === 'no-referrer'，naturalWidth > 0
  // 3. expect.poll(GET /api/articles/{id} 的 readableContent，超时 90 秒)，直到包含 '/api/images/'
  // 4. 关闭模拟服务器，再刷新页面。img 可见，src 以 '/api/images/' 开头，naturalWidth > 0
  // 5. page.request.get(那个 src) → 200，content-type 'image/png'
});
test.afterAll(...)   // DELETE /api/articles/{id}（软删除），保持 65 篇基准文章不变
```

- [ ] **Step 2: 在隔离的测试部署上运行图片专项与全量 Playwright**

  部署时设置 `Images__AllowPrivateNetworks=true`。

  - [x] `E2E_BASE_URL=<隔离部署地址> E2E_IMAGE_URL=<API 能访问到的模拟服务器地址> pnpm test:e2e image-storage`。图片专项 2/2 通过，两个视口都验证了源站关闭后刷新仍能显示图片。
  - [ ] `E2E_BASE_URL=<隔离部署地址> pnpm test:e2e`。全量 Playwright 52/54 通过。订阅场景桌面端的取消订阅断言使用了改名前的标题，移动端在弹窗打开后按角色定位侧边栏超时。

- [x] **Step 3: 后端、前端全量验证**

运行：`dotnet test`
预期：全部 PASS，包括 PostgreSQL 和 MinIO 测试。

运行：`pnpm test && cd src/web && npx tsc --noEmit && npx next build --no-lint && cd ../.. && pnpm lint`
预期：退出码 0；lint 只剩原有的 ArticleCard 警告。

后端复核通过 624 项，含 PostgreSQL、MinIO 和生产配置的 HTTPS 上传请求测试。上一轮前端复核通过 Vitest 178 项、类型检查、构建和 lint。

- [ ] **Step 4: 部署验收（按设计第 4 节）**

隔离部署已验证 SQLite 和 PostgreSQL 的默认存储、image-proxy 覆盖配置，以及 3 篇旧文章的迁移和回填。图片来源使用模拟 HTTP 服务，升级使用预先生成的旧版 SQLite 备份。MinIO 实测已通过，R2 专用桶实测留待提供配置。

1. `podman compose -f deploy/docker-compose.sqlite.yaml up -d --build`，**不设置** `IMAGE_PROXY_SECRET`：服务正常启动，用扩展保存一篇带图的微信文章，30–60 秒后图片地址变成 `/api/images/`。PostgreSQL 版重复一遍。
2. 加上 `-f deploy/docker-compose.image-proxy.yaml` 和 `IMAGE_PROXY_SECRET`：图片地址变成 `/imageProxy/`。
3. 用一份升级前的数据库备份（正文带 `/imageProxy/` 地址）启动新版本：日志出现"已回填 X 篇，剩余 Y 篇"，直到剩余 0；随机抽查 3 篇文章，`ContentRevision` 和阅读进度都不变，图片逐步换成 `/api/images/`。
4. **R2 实测（需要用户提供凭据，并且必须使用专门的测试存储桶）：**
   - `Images__Storage__Provider=S3` 加上 R2 的配置：保存文章后，存储桶里出现 `{2位}/{hash}.{ext}` 对象，登录后能看到图片，退出登录后请求返回 401；
   - 再设置 `Images__Delivery=Public` 和 `Images__PublicBaseUrl=<测试桶的公开域名>`：图片地址变成公开域名，不登录也能加载。

- [ ] **Step 5: 提交**

```bash
git add src/web/e2e/image-storage.spec.ts CLAUDE.md
git commit -m "test(e2e): cover image storage end to end"
```
