# 图片持久化存储设计

- 日期：2026-11-21
- 状态：草稿，待用户审阅
- 基于提交：`33b0380`（分支 `docs/readme-restructure`）

---

## 1. 背景与目标

现在文章里的图片全靠 image-proxy（[willnorris/imageproxy](https://github.com/willnorris/imageproxy) 0.13.0）显示。API 在保存文章时，把每个图片地址改写成带 HMAC 签名的 `/imageProxy/{宽}x{高},s{签名}/{原图地址}`，再存进数据库。阅读时浏览器请求这个地址，nginx 把请求转给 image-proxy，由它去原站取图并缓存。这样能绕过防盗链，但有三个问题：

1. 自部署要多跑一个容器，还要配置一个两边必须一致的密钥 `IMAGE_PROXY_SECRET`。
2. 签名已经写进了数据库里的正文，中途更换密钥，旧文章的图片就会全部失效。
3. image-proxy 只是缓存，不是存档。原站删图以后，缓存一过期，图片就没了。

本次让 **API 自己下载并永久保存图片**，默认部署不再需要 image-proxy。image-proxy 保留为可选模式，在 appsettings 里切换。

**成功标准：** 第 4 节的测试和验收全部通过。具体包括：

- 默认的两套 Compose（PostgreSQL、SQLite）只启动 api、web、lb（以及 db），不需要 image-proxy 容器，也不需要 `IMAGE_PROXY_SECRET`。
- 新保存的文章和 RSS 文章，图片下载完成后从 mini-cc 自己的存储返回；原站删图以后照样显示。
- 升级后，旧文章的图片会自动补下载；升级过程不改变任何文章的 `contentRevision`、阅读进度和高亮状态。
- 切换到 image-proxy 模式后，读者看到的图片地址和现在完全一样。

### 1.1 用户已定的事项

- 目标：完全取代 image-proxy，简化服务，图片持久化保存。
- 在 appsettings 中配置是否使用 image-proxy。**启用 image-proxy 时不使用本功能**：不下载、不存图，行为和现在一样。
- 图片存在**本地目录**或**对象存储（OSS）**中，用配置二选一。不存进数据库。用户使用的对象存储是 **Cloudflare R2**。
- 图片访问支持两种方式，用配置二选一：
  - **登录后访问**：由 mini-cc 校验登录，再返回图片。
  - **公开访问**：不需要登录。
- 下载结果记录在 Articles 表**新增的列**里，不另建映射表。
- 用户的备份方式是定期复制 SQLite 文件、对 PostgreSQL 做 dump。用户已知道这些备份不包含本地目录里的图片，需要另外备份（见 3.12）。

以下是我的决定，用户在讨论中没有提出反对：

- 由**服务端**下载图片。RSS 文章不经过浏览器扩展，只有服务端下载才能覆盖所有来源。
- 对象存储按 **S3 兼容协议**接入。R2、MinIO、阿里云 OSS、腾讯云 COS 都能用同一套代码。
- 正文里存**原图地址**，在**返回文章时**再按当前配置替换成实际的图片地址（见 3.4）。

### 1.2 非目标

- 浏览器扩展下载或上传图片（比如需要登录才能看的图片）。
- R2 临时签名地址；在 Cloudflare 上用 WAF 规则或 Worker 做鉴权。
- 在本地目录和对象存储之间搬运已有图片。换存储时用 rclone 等工具手动同步，文档里写明步骤。
- 缩略图、按尺寸缩放、格式转换。只保存原图。image-proxy 按 HTML 宽高缩放的功能，在本地保存模式下不再提供。
- `OriginalContent`（原始 HTML）里的图片。它不直接展示，保持原样。
- 视频、音频、iframe，以及 CSS `background-image` 里的图片。现在的 image-proxy 也不处理这些。
- 图片下载状态的界面、手动重新下载按钮、存储用量统计和配额。用户想重试时，用扩展重新保存文章即可（见 3.3）。
- 把图片存进数据库。

### 1.3 反目标（即使能用，出现这些也算失败）

- 升级或回填让内容没变的文章 `contentRevision` 加一、阅读进度被重置，或者高亮变成 Pending。
- 升级后第一次用扩展重新保存旧文章时，因为图片地址的写法不同，被误判为正文变了（见 3.3 的字节一致性要求）。
- 保存文章或导入 RSS 时同步等待图片下载，导致保存变慢。
- 登录后访问模式下，不登录也能拿到图片。
- 服务端去请求内网、回环或云元数据地址上的图片（除非显式允许，见 3.6）。
- 保存下来的 SVG 能在 mini-cc 的域名下执行脚本。
- 清理任务删掉了仍被任何文章（包括回收站里的文章，以及还没回填的旧文章）引用的图片。
- 清理任务删掉了对象存储里不属于 mini-cc 的文件。
- ImageProxy 模式下，浏览器收到的图片地址应与升级前一致，允许以下兼容性变化。
  - 订阅文章的封面在原先直接返回原图的响应中也按列表规则签名，包括详情、保存、元数据更新、状态更新、恢复和旧标签详情。
  - 新导入的 RSS 或 Atom 条目从正文提取封面时，先解码 HTML 实体再签名。带 `&amp;` 等实体的封面地址会使用解码后的 URL，因此列表签名也会变化。
  - 回填采用历史代理配置的旧地址后，ImageProxy 模式按当前配置重新签名。回填期间按 3.10 处理旧签名地址。
- 默认部署仍然要求 image-proxy 容器或 `IMAGE_PROXY_SECRET`。

---

## 2. 现状：和图片地址有关的代码（实现者需逐项处理）

**注意编码：** `Core/DependencyInjection.cs`、`Shared/Config/MappingProfile.cs`、`Shared/Data/DbSeeder.cs`、`MiniCcDbContext.cs`、`TagService.cs` 等文件是 **UTF-16 LE 带 BOM**。修改时保持原编码；用普通 `grep` 会搜不到它们的内容（可以用 `grep -a` 再去掉 `\0`）。

| 位置（`src/server/` 下） | 现在做什么 |
|---|---|
| `Infra/ImageProxyService.cs:19-34`（`GetUrl`） | 只处理 http/https 绝对地址：先转成 `Uri.AbsoluteUri`，再签名 `"{url}#{宽}x{高}"`，返回 `{ProxyUrl}/{宽}x{高},s{签名}/{url}`。已经带代理前缀的地址原样返回。 |
| `Infra/ImageProxyService.cs:36-56`（`RewriteImages`） | 用 HtmlAgilityPack 遍历 `img`、`source`：`src` 用元素的 `width`/`height` 签名，`srcset` 的每个候选都用 `0x0` 签名；去掉 `crossorigin`；写回时做 HTML 编码；最后输出 `DocumentNode.OuterHtml`。 |
| `Core/ArticleNs/Domain/Services/ArticleDomainService.cs:19,21,26` | 扩展保存全文和链接时，正文调用 `RewriteImages`，封面调用 `GetUrl`，**写入时签名**后存库。 |
| `Core/ArticleNs/Application/Commands/SaveArticleContentCommandHandler.cs:42-51` | 重新保存时，用改写后的正文和库里的正文**逐字比较**（第 44 行），决定要不要调用 `ReplaceContent`（会让 `contentRevision` 加一、重置进度、高亮变 Pending）。同时刷新或补全封面。 |
| `Core/ArticleNs/Application/Commands/SaveLinkCommandHandler.cs:24` | 新建链接，封面经过领域服务签名。 |
| `Core/FeedNs/Infrastructure/FeedParser.cs:74-79` | RSS 正文调用 `RewriteImages`；封面取 `media:content` 或正文第一张图，**不签名**，原样保存。`ContentHash` 由 `OriginalContent` 计算（第 19 行），与图片改写无关。 |
| `Core/FeedNs/Application/FeedImportService.cs:60` | 用解析结果直接 `Content.Create` 并创建 Article。 |
| `Core/ArticleNs/Application/Queries/GetArticleListQueryHandler.cs:46` | 列表返回时对封面调用 `GetUrl`（读取时签名，已签名的跳过）。 |
| `UpdateArticleStateCommandHandler.cs:21`、`RestoreArticleCommandHandler.cs:28` | 用 Mapster 映射 `ArticleListItemDto`，封面**原样**返回。所以 RSS 文章的封面在这里是未签名的原图地址。 |
| `GetArticleByIdQueryHandler.cs:34`、`UpdateArticleCommandHandler.cs:49`、`SaveLinkCommandHandler.cs:33`、`SaveArticleContentCommandHandler` | 返回 `ArticleDto`，正文和封面原样输出。RSS 文章的详情封面也是未签名的。 |
| `Core/TagNs/Application/Services/TagService.cs:72` | 旧的标签详情接口返回文章封面，原样输出。 |
| `Core/ArticleNs/Application/DTOs/ArticleListItemDto.cs:30,43` | 两个列表投影都直接投影 `ImageUrl = a.ImageUrl`。 |
| `Core/DependencyInjection.cs:66-70` | 注册 `ImageProxyService`（单例），绑定 `ImageProxy` 配置节，**启动时强制要求** `SignatureKey`。 |
| `Shared/Data/DbSeeder.cs:20-31` | 启动时先执行迁移，再用哨兵值（`NormalizedUrl == ""`）回填旧数据。本次的回填沿用这个思路，但放到后台执行（见 3.10）。 |
| `Core/FeedNs/Infrastructure/FeedLeases.cs` | 订阅源租约：PostgreSQL 用 `FOR UPDATE SKIP LOCKED … RETURNING`，SQLite 用一条 `UPDATE … RETURNING`。图片下载任务的领取沿用这个写法。 |
| `Core/FeedNs/Infrastructure/FeedFetcher.cs:17,34`；`DependencyInjection.cs:56-57` | 20 秒总期限（包括读取响应体）、10MB 上限、最多 5 次重定向。图片下载参照这个写法。**项目里目前没有任何内网地址拦截。** |
| `Shared/Data/MiniCcDbContext.cs:93`；`SqliteMiniCcDbContext.cs:22` | JSON 列的先例：`User.ReaderPreferences` 在 PostgreSQL 上是 `jsonb`，在 SQLite 上是 `TEXT`。 |

前端和部署：

| 位置 | 现在做什么 |
|---|---|
| `src/web/src/app/pages/article/[id]/components/Client.tsx:105` | 用 DOMPurify 默认配置净化正文。 |
| `src/web/src/app/components/ArticleCard.tsx:109`；`Client.tsx:191` | 列表卡片封面、链接详情封面，都是普通 `<img>`。 |
| `src/ng/conf.d/default.conf:11-14,32-35` | `image_proxy_upstream` 和 `location /imageProxy`。nginx 启动时会解析上游主机名，**删掉 image-proxy 容器而不删这段配置，nginx 会启动失败**。 |
| `src/ng/Dockerfile` | 把 `conf.d` 整个复制进 `nginx:alpine` 镜像。 |
| `deploy/docker-compose.yaml:34,52-65,78,82`；`deploy/docker-compose.sqlite.yaml:17,34-47,60,64` | image-proxy 服务、`IMAGE_PROXY_SECRET`、`image-proxy-data` 卷、lb 对它的依赖。PostgreSQL 版的 API 没有挂载卷。 |
| `src/server/Dockerfile:13` | 已经创建 `/app/data` 并交给非 root 的 `$APP_UID`。API 的工作目录是 `/app`。 |
| `README.md:41,54-60,78-79,97-116,156-157,249,347` | 部署说明、端口、数据卷、环境变量表都提到 image-proxy 和签名密钥。 |
| `CLAUDE.md` | "Image Proxy"、"Image URLs"、P5 的"signed image rewriting"等段落。 |

测试里有十几处直接 `new ImageProxyService(...)`（`ImageProxyServiceTests`、`ArticleDomainServiceTests`、`SaveArticleContentCommandHandlerTests`、`SaveLinkCommandHandlerTests`、`FeedParserTests`、`FeedFetcherTests`、`FeedRefreshPostgresTests`、`SqliteConcurrencyTests`、`ArticleList*PostgresTests` 等），还有几处配置 `ImageProxy:SignatureKey`。断言 `/imageProxy/` 前缀的测试（如 `SaveArticleContentCommandHandlerTests.cs:144`、`LinkApiTests.cs:145`、`ArticleDomainServiceTests.cs:46`）需要按新的写入格式修改。

---

## 3. 方案

### 3.1 配置

```jsonc
"Images": {
  "Mode": "Store",                 // Store（默认）| ImageProxy
  "Delivery": "Authenticated",     // Authenticated（默认）| Public
  "ApiUrl": "/api/images",         // 浏览器访问 API 图片端点用的地址
  "PublicBaseUrl": "",             // 仅 S3 + Public：图片直链前缀，如 https://img.example.com
  "AllowPrivateNetworks": false,   // 允许下载内网地址上的图片
  "Storage": {
    "Provider": "Local",           // Local（默认）| S3
    "LocalPath": "data/images",    // 相对 API 内容根目录，和 SQLite 默认路径一致
    "S3": {
      "ServiceUrl": "",            // R2：https://<account_id>.r2.cloudflarestorage.com
      "Bucket": "",
      "Region": "auto",
      "AccessKeyId": "",
      "SecretAccessKey": ""
    }
  }
},
"ImageProxy": { "ProxyUrl": "/imageProxy", "SignatureKey": "" }   // 仅 Mode=ImageProxy 时使用
```

- 环境变量照常覆盖，例如 `Images__Mode=ImageProxy`、`Images__Storage__S3__SecretAccessKey=…`。密钥类配置只通过环境变量传入，不写进仓库里的 appsettings。
- 枚举值不区分大小写，和 `Database:Provider` 一致。
- **启动时校验**（`ValidateOnStart`，报错信息写明配置项名称）：
  - `Mode=ImageProxy` 时，`ImageProxy:SignatureKey` 必填。现在的无条件校验改成只在这个模式下生效。
  - `Mode=Store` 且 `Provider=S3` 时，`ServiceUrl`、`Bucket`、`AccessKeyId`、`SecretAccessKey` 必填。
  - `PublicBaseUrl` 不为空时，必须是 http/https 绝对地址，而且只能和 `Delivery=Public`、`Provider=S3` 一起用；其他组合视为配置矛盾，启动报错。
  - `Provider=Local` 时，启动时创建 `LocalPath` 目录并检查可写；失败就报错退出，报错信息里带完整路径。
- `ApiUrl` 的作用和现在的 `ImageProxy:ProxyUrl` 一样：经过 nginx 时用默认的相对地址；手动本地开发时（网页在 4000 端口，API 在 5001 端口），要设成 `http://localhost:5001/api/images`。
- `AllowPrivateNetworks` 是我加的：端到端测试的模拟图片服务器跑在本机，有人也会收藏内网站点的文章。默认关闭。

图片地址的生成规则：

| 模式 | 已下载的图片 | 未下载或下载失败的图片 |
|---|---|---|
| Store + Authenticated | `{ApiUrl}/{hash}.{ext}`，需要登录 | 原图地址，并加 `referrerpolicy="no-referrer"` |
| Store + Public，没有 `PublicBaseUrl` | `{ApiUrl}/{hash}.{ext}`，不需要登录 | 同上 |
| Store + Public + `PublicBaseUrl`（仅 S3） | `{PublicBaseUrl}/{hash前2位}/{hash}.{ext}` | 同上 |
| ImageProxy | 和现在完全一样的签名地址 | — |

`hash` 是图片字节的 SHA-256（64 位小写十六进制），`ext` 是识别出的类型（见 3.6）。

### 3.2 数据模型

Articles 表新增两列，PostgreSQL 迁移命名为 `M5_ImageStorage`，SQLite 迁移同名：

- **`Images`**：可为空的 JSON（PostgreSQL `jsonb`，SQLite `TEXT`）。键是规范化后的原图地址（见 3.3），值是下载结果：

  ```json
  {
    "https://mmbiz.qpic.cn/a.png": { "state": "done", "hash": "9f2c…", "ext": "png" },
    "https://example.com/b.jpg":   { "state": "pending", "attempts": 2, "next": "2026-11-21T08:00:00Z" },
    "https://example.com/c.gif":   { "state": "failed", "reason": "notFound" }
  }
  ```

  - `NULL` 表示**旧数据，还没回填**（见 3.10）。迁移后所有现有行都是 `NULL`。
  - 新保存的文章一律写入非空值；没有图片的文章写 `{}`。
  - 只有 API 自己读写这一列，不在 SQL 里查询 JSON 内容。
- **`ImagesDueAt`**：可为空的时间，建部分索引（`WHERE "ImagesDueAt" IS NOT NULL`）。表示这篇文章最早什么时候有图片需要下载或重试；没有待处理的图片时为 `NULL`。它同时充当领取租约（见 3.5），不再另加租约列。

C# 端映射成一个"地址 → 结果"的字典，用值转换器序列化成 JSON。实现时要配置 ValueComparer，否则 EF 检测不到字典内部的变化。

### 3.3 写入：地址规范化和映射同步

**写入时不再签名，只做规范化。** 把 `RewriteImages` 改成规范化版本（下文叫 `NormalizeImages`），遍历和输出方式完全不变：

- 遍历 `img`、`source` 的 `src` 和 `srcset`；
- http/https 绝对地址写成 `Uri.AbsoluteUri`，**不加代理前缀**；其他地址（`data:`、相对地址）原样保留；
- 去掉 `crossorigin`；
- 写回时做 HTML 编码，`srcset` 用 `", "` 连接，最后输出 `DocumentNode.OuterHtml`。

封面地址同样规范化成 `AbsoluteUri`。RSS 封面也按这个规则处理，这样所有来源的封面写法统一，现在 RSS 封面"详情未签名、列表已签名"的不一致也随之消失。

**字节一致性要求（必须有测试）：** 对同一份输入 HTML `x`：

1. `NormalizeImages(x)` 必须和"旧的 `RewriteImages(x)` 去掉代理前缀"的结果**逐字相同**。否则升级后第一次重新保存旧文章时，`SaveArticleContentCommandHandler.cs:44` 的比较会误判正文变了（见 1.3）。
2. image-proxy 模式下，对 `NormalizeImages(x)` 做返回时改写（3.4）的结果，必须和旧的 `RewriteImages(x)` **逐字相同**。

**回填之前就被重新保存的旧文章：** 如果库里文章的 `Images` 还是 `NULL`，`SaveArticleContentCommandHandler.cs:44` 比较之前，先用 3.10 的方法剥掉库里正文的旧代理前缀，再和新正文比较。

**`<source>` 元素：** 规范化和 image-proxy 模式下的改写，和现在一样处理所有 `source`（包括 `<video>`、`<audio>` 里的），以满足字节一致性要求。但收集要下载的地址时，只收集 `img` 和父元素是 `<picture>` 的 `source`。

**映射同步：** 每个修改正文或封面的写入路径，都要在同一次 `SaveChanges` 里同步 `Images` 和 `ImagesDueAt`。涉及的路径有：

- `SaveArticleContentCommandHandler`：新建、替换正文、刷新封面、补全封面；
- `SaveLinkCommandHandler`：新建；
- `FeedImportService`：新建；
- 旧数据回填（3.10）。

同步规则：

1. 从正文中收集地址（`img`/`source` 的 `src`、`srcset` 里的每个候选），再加上封面，只要 http/https 绝对地址，规范化以后去重。
2. 已有记录的地址保留原记录。例外：**用户通过扩展主动保存**（全文或链接）时，`failed` 的记录重置为 `pending`、`attempts` 清零。这就是"手动重试"的方式。RSS 导入和回填不重置。
3. 新出现的地址记为 `pending`，`attempts = 0`，`next = 现在`。
4. 不再出现的地址删除记录。
5. `ImagesDueAt` 取所有 `pending` 记录里最早的 `next`；没有就设为 `NULL`。

image-proxy 模式下**同样维护映射**，只是不下载。这样切回本地保存模式时，下载任务可以直接接手，不需要重新扫描所有文章。

### 3.4 读取：返回时替换图片地址

新增一个解析服务（下文叫 `ImageUrlResolver`，单例，依赖 `Images` 和 `ImageProxy` 配置），提供两个方法：

- 替换正文 HTML 里的图片地址；
- 替换单个封面地址。

两种模式的处理：

- **Store：**
  - 映射里状态为 `done` 的，按 3.1 的表格生成地址；
  - 其他地址（`pending`、`failed`、映射里没有，或者 `Images` 为 `NULL`）保留原图地址。只要元素里有任何一个地址没被替换，就给对应的 `img` 元素加上 `referrerpolicy="no-referrer"`。很多站点（例如微信公众号）允许空 Referer，所以图片还没下载完时，大多数情况下也能正常显示。
  - `Images` 为 `NULL` 的旧文章，正文里可能还是旧的代理地址：先按 3.10 的规则剥掉前缀，再按上面的规则处理。
- **ImageProxy：** 用现在 `GetUrl` 的签名逻辑处理（`src` 带元素宽高，`srcset` 和封面用 `0x0`），结果必须满足 3.3 的字节一致性要求第 2 条。

以下所有返回正文或封面的地方，都必须经过这个服务：

- `ArticleDto`：文章详情、扩展保存全文、保存链接、更新元数据的响应；
- `ArticleListItemDto`：文章列表（包括标签下的文章列表，它复用同一个列表查询）、状态更新、恢复文章的响应；
- `TagService.cs:72` 的旧标签详情接口。

列表投影需要把 `Images` 一起查出来，才能替换封面。`Images` 不能出现在 JSON 响应里，可以用不序列化的内部属性，也可以另写一个投影，由实现者决定。每次只查 20–50 行，可以接受。

正文返回时每次都要解析一遍 HTML，单篇文章耗时在毫秒级，对个人使用没有影响。

### 3.5 下载任务

新增后台服务（下文叫 `ImageDownloadWorker`），每 30 秒运行一次：

1. **领取：** 找出 `ImagesDueAt <= 现在` 的文章（包括回收站里的），最多 10 篇，把它们的 `ImagesDueAt` 原子地改成"现在 + 10 分钟"作为租约。PostgreSQL 用 `FOR UPDATE SKIP LOCKED … RETURNING`，SQLite 用 `UPDATE … RETURNING`，写法照 `FeedLeases.cs`。进程崩溃时租约会过期，文章会被重新领取。
2. **下载：** 对每篇文章里 `next <= 现在` 的 `pending` 地址逐个下载。整个进程同时最多 4 个下载请求，同一个域名同时最多 2 个。升级回填时会一下子下载整个图书馆的图片，限制每个域名的并发，可以减少被对方限流。请求细节见 3.6。
3. **保存文件：** 计算 SHA-256，按 `{hash前2位}/{hash}.{ext}` 写入存储。内容寻址，同一张图只存一份。如果文件已经存在，要刷新它的修改时间（Local 更新文件的修改时间，S3 直接重新上传覆盖），避免清理任务（3.9）把它当成没人用的旧文件删掉。
4. **写回结果：** 在事务里锁住文章（PostgreSQL 行锁，SQLite 的 `BEGIN IMMEDIATE`，和现有的 `GetByIdForProgressUpdateAsync` 一样），重新读取 `Images`：
   - 某个地址的记录还在，并且仍是 `pending`，才写入结果。这样文章在下载期间被重新保存时，不会写入已经过时的结果。
   - 按 3.3 第 5 条重新计算 `ImagesDueAt`。
   - **不改** `contentRevision`、进度，也不改其他字段。

重复下载、重复写入都没有副作用：文件按内容寻址，写回时也会检查记录状态。所以不需要像订阅源那样使用租约令牌。

**失败处理：**

| 情况 | 处理 |
|---|---|
| 404、410；除 408、429 以外的其他 4xx；类型不支持；超过大小上限；地址被拦截；地址无效 | 直接记为 `failed`，`reason` 写明原因 |
| 第一次请求得到 401 或 403 | 带上 `Referer: <文章地址>` 再试一次；仍然失败就记为 `failed` |
| 超时、网络错误、408、429、5xx | `attempts + 1`，依次在 1 分钟、10 分钟、1 小时、6 小时、24 小时后重试，之后每 24 小时重试一次；第 10 次失败后（大约一周）记为 `failed`。响应带 `Retry-After` 时，下次重试不早于它指定的时间 |

重试持续大约一周，是为了让升级回填时被临时限流的图片还有机会下载成功。用户想手动重试时，可以用扩展重新保存文章，会把 `failed` 重置为 `pending`（3.3）。但 RSS 文章用扩展重新保存时，正文会被网页内容替换，`contentRevision` 会加一，所以这一期 RSS 文章**没有干净的手动重试方式**。手动重试的按钮仍然不在本期范围内（1.2）。

只有 `Mode=Store` 时执行下载。image-proxy 模式下，这个后台服务只做旧数据回填（3.10）。每轮结束后记一条结构化日志，写明处理了多少篇文章、多少张图，各有多少成功和失败。

### 3.6 安全

**内网地址拦截：** 下载用的命名 HttpClient `images` 使用 `SocketsHttpHandler`，并设置 `ConnectCallback`：

- 先解析 DNS，拒绝以下地址：回环、私有网段（`10/8`、`172.16/12`、`192.168/16`）、链路本地（`169.254/16`，包括云元数据地址）、CGNAT（`100.64/10`）、`0.0.0.0/8`、组播、IPv6 的 `::1`、`fc00::/7`、`fe80::/10`，以及映射到以上 IPv4 地址的 IPv6 地址；
- 然后**直接连接校验过的 IP**，避免 DNS 在校验之后被换成别的地址（DNS 重绑定）；
- 每次重定向都会重新连接，所以重定向到内网地址也会被拦住；
- `AllowPrivateNetworks=true` 时跳过这项检查。

图片下载使用 `UseProxy=false`，跳过系统代理和 `HTTP_PROXY`、`HTTPS_PROXY`。API 需要能直连图片来源。仅允许代理出站的部署无法在 Store 模式下下载图片。

**请求：**

- 总期限 30 秒，包括读取响应体；
- 最多 5 次重定向；
- 大小上限 20MB，读取时边读边计数，超过就中止；
- User-Agent 用和 image-proxy 相同的 Chrome 字符串（`src/image-proxy/start_imageproxy.sh:3`）；
- `Accept: image/avif,image/webp,image/apng,image/*,*/*;q=0.8`；
- 第一次请求不带 Referer。

**类型识别：** 只看文件头几个字节判断类型，不信任对方返回的 Content-Type。只接受以下类型，其他都记为 `unsupportedType`：

- JPEG（`jpg`）、PNG（`png`）、GIF（`gif`）、WebP（`webp`）、AVIF（`avif`）；
- SVG（`svg`）：去掉 BOM 和空白后以 `<svg` 开头，或者是 XML 声明且文档里有 `<svg` 元素。

**SVG 和所有图片响应：** API 返回图片时一律带上：

- `X-Content-Type-Options: nosniff`
- `Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'; sandbox`

这样，即使有人直接在浏览器里打开一张 SVG，它也不能在 mini-cc 的域名下执行脚本。这两个响应头对普通位图没有影响。

### 3.7 存储

`IImageStore` 支持写入（带 Content-Type）、打开读取（对象存在时返回可读流）、获取最后修改时间、列出对象（含键和最后修改时间）及删除。

**Local：**

- 文件路径是 `{LocalPath}/{hash前2位}/{hash}.{ext}`；
- 写入时先在同一目录下写临时文件，再改名，保证不会出现写了一半的文件；目标文件已经存在时，丢弃临时文件；
- 通过 `IImageStore.OpenReadAsync` 打开文件流，由图片端点调用 `File(stream)`，与 S3 共用鉴权和响应头逻辑。

**S3：**

- 使用 `AWSSDK.S3`，版本写进 `Directory.Packages.props`；
- 配置 `ServiceURL`、`ForcePathStyle = true`，`AuthenticationRegion` 用 `Region`（R2 填 `auto`）；
- 对象键是 `{hash前2位}/{hash}.{ext}`，写入时设置 Content-Type；
- 图片已经完整读进内存（不超过 20MB），直接用一次 PutObject 上传，不用分片上传；
- S3 客户端将 `RequestChecksumCalculation` 和 `ResponseChecksumValidation` 设为 `WHEN_REQUIRED`，`PutObject` 设置 `UseChunkEncoding = false`。HTTPS 端点还关闭负载签名和默认校验，以兼容 R2。MinIO 对象操作和生产配置生成的 HTTPS 上传请求均有测试。R2 专用测试桶实测待第 4 节验收。

`Delivery=Public` 并且配置了 `PublicBaseUrl` 时，图片由用户绑定的自定义域名（或 r2.dev）直接提供，存储桶的公开访问由用户在 Cloudflare 上自己配置。README 要写明这些步骤。

### 3.8 图片端点

`GET /api/images/{name}`：

- `name` 必须匹配 `^[0-9a-f]{64}\.(jpg|png|gif|webp|avif|svg)$`，否则返回 404；对象不存在也返回 404。
- `Delivery=Authenticated` 时要求登录，认证方案和其他控制器一样（Cookie 和 AccessKey）。没登录返回 401。浏览器加载同源 `<img>` 时会自动带上 Cookie。
- `Delivery=Public` 时允许匿名访问。
- 响应头：
  - `Content-Type` 由扩展名决定；
  - `ETag: "{hash}"`；
  - 登录后访问时 `Cache-Control: private, max-age=31536000, immutable`，公开访问时 `Cache-Control: public, max-age=31536000, immutable`；
  - 3.6 中的两个安全响应头。
- `Mode=ImageProxy` 时，这个端点一律返回 404。
- 不校验这张图属于哪篇文章：mini-cc 的文章不分用户，能登录就能看到所有文章。

nginx 已经把 `/api/` 转发给 API，所以不需要修改 nginx 配置。

### 3.9 清理

在 `TrashPurgeWorker` 每天清理回收站之后执行，满足以下全部条件才运行：

- `Mode=Store`；
- 没有 `Images IS NULL` 的文章（包括回收站里的），也就是说旧数据回填已经完成；
- 列出存储对象时没有出错。

做法是"标记 + 清除"，按以下顺序：

1. **标记：** 分批读取所有文章（`IgnoreQueryFilters`，包括回收站里的）的 `Images`，收集所有 `done` 记录里的 `hash`。
2. **列出：** 列出存储里的全部对象，**只处理键名完全匹配 `^[0-9a-f]{2}/[0-9a-f]{64}\.(jpg|png|gif|webp|avif|svg)$` 的对象**。
3. **清除：** 对哈希不在标记集合里、并且最后修改时间早于 24 小时之前的对象，**删除前再查一次它的修改时间**（Local 读文件信息，S3 用 HEAD），仍然早于 24 小时之前才删除。这样，清理进行期间被下载任务刷新过时间的文件不会被误删（见 3.5 第 3 步）。
4. **修复：** 标记集合里有、但存储里没有的哈希，把引用它的记录改回 `pending`（`attempts = 0`），并更新这些文章的 `ImagesDueAt`。这种情况出现在用户恢复的数据库备份比图片目录的备份新，或者换存储时没有同步完整的时候。原站还在的图片会被重新下载。修复时同样要锁住文章（方式同 3.5 第 4 步）。

24 小时的宽限期是为了避开"文件已经上传、但结果还没写回数据库"的时间窗口。即使极少数情况下仍然误删了一个文件，第二天的修复步骤也会让它重新下载。永久删除文章（`ArticlePurger`）的流程不用改：文章删掉以后，它引用的图片会在下一次清理时被删除。

### 3.10 旧数据回填（升级）

所有 `Images IS NULL` 的文章都要回填，**两种模式下都执行**。由 `ImageDownloadWorker` 在每轮开始时处理，每批 50 篇，处理完才去下载：

1. 锁住文章（方式同 3.5）。
2. **剥掉旧的代理前缀：** 用 HtmlAgilityPack 解析正文，在 `img`/`source` 的 `src`、`srcset` 里识别 `{前缀}/{数字}x{数字},s{签名}/{http或https绝对地址}`，替换成后面的原图地址。
   - 识别当前配置的 `ImageProxy:ProxyUrl` 和默认值 `/imageProxy`。对于使用其他 HTTP(S) 绝对前缀的旧地址，要求签名部分为 43 个 Base64URL 字符，末段为有效的 HTTP(S) 绝对 URL。
   - 签名部分按格式识别，每次只剥最外层，保留来源 URL 自带的代理层。
   - 封面仅在旧记录已有 `SavedFromExtensionAt` 时按同一规则处理。从未由扩展保存的订阅封面保留来源地址。扩展重新保存时先回填，再设置扩展保存标记。
   - 输出格式和 `NormalizeImages` 一致，满足 3.3 的字节一致性要求第 1 条。
3. 按 3.3 的规则建立映射，设置 `ImagesDueAt`。
4. 用一个**专门的领域方法**写回正文和封面：不调用 `ReplaceContent`，不改 `contentRevision`、进度、`ProgressUpdatedAt` 和高亮。
5. 保存。每处理完一批记一条日志："已回填 X 篇，剩余 Y 篇"。

回填完成后，用户在 Store 模式下就能看到旧文章的图片逐步换成本地保存的副本。回填期间，Store 模式按上述规则临时剥离代理前缀，正文使用 `no-referrer`。ImageProxy 模式保留按同一规则识别的签名地址。订阅封面按正常来源地址签名，保留来源自身的代理层。

### 3.11 前端

- `Client.tsx:105`：保证 DOMPurify 净化后保留 `referrerpolicy` 属性。实现时先确认它是否在 DOMPurify 默认的属性白名单里；不在的话，用 `ADD_ATTR: ['referrerpolicy']`。要有 Vitest 测试覆盖。
- `ArticleCard.tsx:109`、`Client.tsx:191`：封面 `<img>` 一律加 `referrerPolicy="no-referrer"`。对 mini-cc 自己的地址没有影响，Cookie 照常发送。
- 前端不需要知道当前是哪种模式，也不需要知道图片下载到了哪一步。

### 3.12 部署和文档

**nginx：**

- 从 `default.conf` 删掉 `image_proxy_upstream` 和 `location /imageProxy`；
- 在 `server` 块里加上 `include /etc/nginx/conf.d/locations/*.conf;`。这个目录在 `conf.d` 的子目录里，nginx 主配置不会把它当成顶层配置自动加载；目录为空时，通配符 include 也不会报错。
- 新增 `src/ng/conf.d/locations/.gitkeep`，以及一个**不在** `locations/` 目录里的 `src/ng/image-proxy.location.conf`，内容是 `location /imageProxy { rewrite …; proxy_pass http://image-proxy:8080; }`。

**Compose：**

- 两个默认文件都删掉 image-proxy 服务、`image-proxy-data` 卷、`ImageProxy__SignatureKey` 环境变量，以及 lb 对 image-proxy 的依赖；
- PostgreSQL 版给 minicc-api 加上 `api-data:/app/data` 卷。镜像里的 `/app/data` 已经归 `$APP_UID` 所有，命名卷第一次创建时会继承这个属主；
- SQLite 版不需要改，图片会落在已有的 `sqlite-data` 卷里（`/app/data/images`）；
- 新增 `deploy/docker-compose.image-proxy.yaml` 覆盖文件：
  - 加回 image-proxy 服务和卷；
  - 给 API 设置 `Images__Mode: ImageProxy` 和 `ImageProxy__SignatureKey`；
  - 把 `image-proxy.location.conf` 只读挂载到 lb 的 `/etc/nginx/conf.d/locations/image-proxy.conf`；
  - 用法：`podman compose -f deploy/docker-compose.yaml -f deploy/docker-compose.image-proxy.yaml up -d`，SQLite 版同理。
- `src/image-proxy/` 保留。

**README：**

- 更新架构说明、部署步骤（不再需要 `IMAGE_PROXY_SECRET`）、端口（不再有 5050）、数据卷、环境变量表；
- 新增"图片存储"一节：三种访问方式；本地目录和 R2 的配置示例；R2 的设置步骤（创建存储桶、创建对象读写令牌、Endpoint、可选的公开自定义域名）；
- 说明备份：复制 SQLite 文件或做 PostgreSQL dump 时，**还要备份图片目录**（Compose 部署下就是 `/app/data/images` 所在的卷）；用 R2 时，图片的持久性交给 R2；
- 说明换存储的方法：用 rclone 同步，再改配置；
- 说明如何切换到 image-proxy 模式；
- 新增"从旧版本升级"一节：
  1. 先备份；
  2. 拉取新版本并重新构建；
  3. 启动时自动执行迁移，后台开始回填和下载，可以在日志里看进度；
  4. 先保留 `image-proxy-data` 卷，不要删除。删除它是不可恢复的，原站已经删掉、只在缓存里留着的图片会随之丢失（见第 6 节）；
  5. 回退到旧版本时，必须恢复升级前的备份：回填已经把正文里的签名地址改回了原图地址，旧版本不认识这种写法。

**CLAUDE.md：** 同步更新 Image Proxy、Image URLs、External Services 以及 P5 里关于"signed image rewriting"的描述。

---

## 4. 测试与验收

**单元测试：**

- `NormalizeImages`：`src`、`srcset`（包括带逗号的地址、只有描述符的候选）、`source`、`data:` 和相对地址原样保留、去掉 `crossorigin`、HTML 实体。
- 3.3 的两条字节一致性要求：用现有测试里的 HTML 夹具，再加上微信、知乎、带 `&` 查询参数、IDN 域名的样本。
- RSS 从正文图片回退生成封面时，将查询参数中的命名和数字 HTML 实体解码成原始 URL。
- 映射同步：保留、新增、删除、扩展保存时把 failed 重置为 pending、RSS 和回填不重置、`ImagesDueAt` 的计算。
- `ImageUrlResolver`：3.1 表格里的每一种组合、`no-referrer` 属性、`Images` 为 `NULL` 时的旧代理地址、image-proxy 模式的签名结果。
- 旧代理前缀剥离：默认前缀、自定义前缀、绝对地址形式的前缀（`http://localhost:5050`）、正文文字里出现 `/imageProxy/` 字样时不受影响。
- 地址拦截：3.6 列出的每类 IPv4、IPv6 网段，以及映射到 IPv4 的 IPv6 地址；`AllowPrivateNetworks` 的开关效果。
- 类型识别：各种文件头、SVG 的几种写法、伪装成图片的 HTML。
- 失败分类和重试间隔。
- 配置校验：3.1 列出的每种非法组合都会在启动时报错，错误信息里带配置项名称。

**集成测试（SQLite 用临时文件，PostgreSQL 用现有的 Testcontainers）：**

- 保存文章后，下载任务把图片写入存储，详情接口返回 `/api/images/…`。HTTP 请求通过注入的 HttpMessageHandler 模拟。
- 下载期间文章被重新保存：过时的结果不会写回，新地址会被下载。
- 租约过期后文章被重新领取；两个下载任务同时运行时不会重复写坏数据（PostgreSQL）。
- 回填：带代理地址的旧文章被剥掉前缀、建立映射，`contentRevision`、进度、`ProgressUpdatedAt`、高亮状态都不变；回填之后用扩展重新保存相同内容，不会让 `contentRevision` 加一。
- 清理：仍有 `Images IS NULL` 的文章时不运行；回收站文章引用的图片不删；24 小时内的新文件不删；删除前再次检查修改时间；键名不匹配的对象不删；永久删除文章后，它引用的图片被删除；记录为 `done` 但文件缺失时，记录被改回 `pending`。
- 下载：文件已存在时刷新修改时间；同一域名的并发限制；`Retry-After`；完整的重试间隔和第 10 次失败后记为 `failed`。
- 回填之前就被重新保存的旧文章：内容相同时不会让 `contentRevision` 加一。
- 本地存储用临时目录测试，S3 用 MinIO 覆盖写入、读取、列出和删除。HTTPS 测试通过生产服务注册解析 S3 客户端和存储实现，仅替换 HTTP 传输，验证路径式地址、校验和配置、`UNSIGNED-PAYLOAD`、原始图片字节和正确的 Content-Type。
- 真实 HTTP：
  - 登录后访问模式下，不登录访问图片端点返回 401，登录后返回 200；
  - 公开访问模式下不登录返回 200；
  - 名称格式不对、对象不存在都返回 404；
  - 检查缓存头、`nosniff` 和 CSP 头；
  - image-proxy 模式下端点返回 404，列表和详情返回签名地址；
  - 启动时配置校验失败的情况。

**前端：** Vitest 覆盖正文净化后保留 `referrerpolicy`、两处封面带 `referrerPolicy`。

**端到端（Playwright，1280×800 和 375×812）：** 新增 `e2e/image-storage.spec.ts`。测试部署设置 `Images__AllowPrivateNetworks=true`，用本机的模拟服务器提供图片，模拟服务器只在请求不带 Referer 时返回图片。覆盖以下流程：

1. 保存一篇带图文章，在下载完成前打开，图片通过 `no-referrer` 正常显示；
2. 下载完成后再打开，图片地址变成 `/api/images/…`；
3. 关闭模拟服务器后刷新文章，确认 `/api/images/…` 的图片仍能在页面显示；
4. 测试结束后清理自己创建的文章，保持 65 篇基准文章不变。

**部署验收：**

- 两套默认 Compose 不设置 `IMAGE_PROXY_SECRET` 也能启动，图片正常显示；
- 加上 image-proxy 覆盖文件后，图片走 `/imageProxy`；
- 用一份带旧代理地址的数据库升级，观察回填日志，旧文章的图片逐步换成本地副本；
- **R2 实测**：用户提供凭据，分别验证登录后访问和公开域名访问两种方式。**必须使用专门的测试存储桶**，不能用正式环境的存储桶：测试数据库里没有正式环境的文章，清理任务会把正式环境里超过 24 小时的图片全部删掉。

完整的后端、前端测试，TypeScript 检查和 lint 都要通过，标准和以往阶段一致。

---

## 5. 留给实现者决定的事

- 类名、文件位置、DTO 里内部属性的写法。上文的 `NormalizeImages`、`ImageUrlResolver`、`ImageDownloadWorker`、`IImageStore` 只是代称。
- JSON 字段名的具体写法（上文的示例只是示意），以及失败原因代码的清单。
- 下载任务是否在保存后立即唤醒，不必等满 30 秒。不唤醒也满足要求。
- 3.5 和 3.6 里的数值（30 秒周期、10 篇、4 个并发、30 秒期限、20MB、重试间隔），可以在同一数量级内调整，但要在 README 或 CLAUDE.md 里写明最终取值。
- 回填和下载放在同一个后台服务里还是拆开。

---

## 6. 待用户确认

1. **升级时，是否从 image-proxy 的缓存里救回旧图？** 原站已经删掉的图片，image-proxy 的缓存里可能还留着。
   - 本文档目前的做法是只从原站重新下载，并在升级说明里要求先保留 `image-proxy-data` 卷，以后还可以补救。
   - 如果要救，不能通过 image-proxy 服务去取。它的缓存过期以后会先向原站复查，原站返回 404 时会直接删掉这份缓存。可行的做法是写一个一次性的导入步骤，直接读取 `image-proxy-data` 卷里的缓存文件。这部分工作量更大，而且依赖 imageproxy 0.13 的缓存文件格式。
2. **同一个存储桶（或本地目录）会不会被多个 mini-cc 共用？** 例如正式环境和测试环境用同一个 R2 存储桶。清理任务会删除本实例数据库没有引用的文件，共用时会互相删掉对方的图片。
   - 本文档目前假设不共用，没有对象键前缀配置。
   - 如果会共用，需要新增 `Images:Storage:S3:Prefix`（以及本地目录各用各的路径），清理任务只处理自己前缀下的文件。
