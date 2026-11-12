# 文库管理、阅读体验与 RSS 订阅设计

- 日期：2026-11-07
- 上游：`docs/superpowers/plans/2026-11-07-library-reading-rss.md`（概要计划）、`todo_list.md`（任务清单）
- 状态：待评审

---

## 1. 背景与目标

MiniCC 是个人自托管的稍后读工具：Chrome 插件抽取网页正文，网站负责阅读和管理。随着文章增多，现在只有“首页最新 20 篇 + 搜索”的文库已经不够用。本设计加入五项能力，它们共用同一套文章状态：

1. 文库视图、筛选与分页：文章变多后能快速找到内容。
2. 阅读状态与断点续读：知道哪些没读、读到哪里、哪些已经读完。
3. 阅读器设置：长文章更易读，偏好在各处持续生效。
4. 批量管理与删除撤销：减少重复操作，误删可以找回。
5. RSS / Atom 订阅：内容自动进入文库，沿用现有的阅读、高亮和归档流程。

**成功标准：** 第 13 节的验收场景全部通过。

### 1.1 非目标（首版不做）

- 多用户隔离（已确认按单用户设计，见 2.1）。
- 自动判断“读完”。
- “选择所有符合筛选条件的文章”（选择范围只限当前已加载）。
- 从原网站自动抓取全文（订阅只保存源站提供的正文或摘要）。
- OPML 导入导出、订阅文件夹、搜索查询语法（如 `is:unread`）、保存的搜索。
- 离线阅读、推送通知、原生 App。
- 订阅条目更新后的内容对比和合并。

### 1.2 反目标（即使功能能用，出现这些也算失败）

- 修改阅读样式改写了已保存正文，或导致高亮错位。
- 删除、恢复、刷新订阅或插件重新保存后，用户的标签、高亮、笔记、阅读状态丢失。
- 进度没有保存成功，界面却显示已同步；或者旧页面把其他设备的新进度覆盖了。
- 批量“加入收藏”逐篇反转了状态。
- 界面让用户误以为勾选了整个文库。
- 重复刷新订阅产生重复文章，或者被删除的条目又被自动导入。
- 一个订阅源出错，阻塞了其他源或整个 API。

---

## 2. 约束与现状

### 2.1 已确认的决策

| # | 决策 |
|---|---|
| D1 | **单用户。** `Article` 不加 `UserId`；阅读偏好存在 `User` 上。 |
| D2 | 插件保存的 URL 命中回收站中的文章时，**自动恢复原文章**，保留标签、高亮、笔记、阅读位置、收藏和归档状态，不新建。 |
| D3 | 订阅条目判定为“仅摘要”的阈值：纯文本少于 500 字符（以及 6.3 中的其他条件）。 |
| D4 | 订阅地址**不限制内网和回环地址**，只要求 http/https。 |
| D5 | 撤销窗口和回收站保留期**只做服务端配置**，在 appsettings 的 `Library` 节中设置，默认 10 秒 / 30 天。 |
| D6 | 实施顺序：P0 状态模型 → P1 文库 → P2 续读 → P3 阅读器设置 / P4 批量与回收站（可并行）→ P5 RSS。 |

### 2.2 技术约束

- 后端沿用现有分层：`Core/{Domain}/{Application,Domain,Infrastructure}`，MediatR 命令/查询、FluentValidation、Mapster 映射，`Result<T>` 返回，`IUnitOfWork` 提交，EF Core 9 + Npgsql。
- 数据库是 PostgreSQL 16 + zhparser，全文检索配置为 `mixed_zh_en`。
- 前端是 Next.js 15 App Router、React 19、SCSS modules、lucide 图标、axios。不新增 UI 组件库。
- 页面滚动容器是 `window`（`ClientLayout` 的 `main` 不设 overflow），滚动恢复和进度计算都基于 `window`。
- 当前前端只有浅色主题（`globals.scss` 中 `color-scheme: light`）。阅读器主题需要自行定义颜色变量。
- 图片继续通过 `ImageProxyService.RewriteImages` / `GetUrl` 签名。
- 单个 API 实例部署（docker compose）。后台任务在 API 进程内以 `BackgroundService` 运行，但领取任务的写法要保证多实例下也安全。

### 2.3 现状问题（本设计会一并修正）

| 位置 | 问题 |
|---|---|
| `ArticlesController.MarkAsRead` | 实际调用了 `ToggleArchiveCommand`。 |
| `favorite` / `archive` 端点 | 是切换（toggle），不幂等，批量操作无法表达“全部设为收藏”。 |
| `DeleteArticleCommandHandler` | 硬删除，高亮级联删除。 |
| `ArticleRepository.GetAllAsync` | 使用 `SearchVector.Matches(search)`，查询端用的是数据库默认检索配置，而不是 `mixed_zh_en`，中文检索不可靠；列表加载了全文和高亮；不返回总数。 |
| `SearchVector` | 生成列不含 `Summary`。 |
| `SaveArticleContentCommandHandler` | URL 精确匹配；已存在的文章直接返回，不做任何更新。 |
| `TagService` / 高亮查询 | 直接查询 `Articles` / `Highlights`。引入软删除后，必须靠全局过滤器排除回收站中的文章（见 3.3）。 |
| 领域事件 | `AddDomainEvent` 只有记录，没有分发。本设计不依赖领域事件。 |

---

## 3. 统一文章状态模型（P0）

### 3.1 四个相互独立的维度

| 维度 | 字段 | 取值 |
|---|---|---|
| 阅读状态 | `ReadingStatus` | `Unread=0`、`Reading=1`、`Read=2` |
| 收藏 | `IsFavorite` | bool |
| 归档 | `IsArchived` | bool |
| 删除 | `DeletedAt` | `null` 表示未删除；有值表示在回收站 |

任意组合都合法。例如“已读 + 收藏 + 归档 + 已删除”在恢复后仍是“已读 + 收藏 + 归档”。

### 3.2 `Article` 字段变更

新增字段（迁移 `M1_LibraryStateModel`）：

| 字段 | 类型 / 约束 | 含义 |
|---|---|---|
| `ReadingStatus` | int，非空，默认 0 | 阅读状态 |
| `ReadAt` | 已有 | 语义改为“标记读完的时间”；`MarkUnread` 时清空 |
| `LastReadAt` | timestamptz? | 最近一次被接受的阅读进度保存时间 |
| `ProgressTextOffset` | int? | 锚点块起始位置的全局文本偏移（见 5.1） |
| `ProgressBlockIndex` | int? | 锚点块序号（用于校验和兜底） |
| `ProgressBlockRatio` | double?，0–1 | 锚点在块内的位置比例 |
| `ProgressPercent` | double?，0–100 | 用于展示的进度 |
| `ProgressUpdatedAt` | timestamptz? | 服务器写入进度的时间（并发控制） |
| `ProgressSessionId` | varchar(64)? | 最后写入进度的页面会话 |
| `DeletedAt` | timestamptz? | 软删除时间 |
| `PublishedAt` | timestamptz? | 发布时间（插件来源可能为空） |
| `NormalizedUrl` | text，非空，默认 '' | 规范化 URL，用于去重（见 3.6）；转义或补根路径后可能变长，原 URL 输入仍限 2000 字符。 |
| `ContentKind` | int，非空，默认 0 | `Full=0`、`SummaryOnly=1` |
| `SavedFromExtensionAt` | timestamptz? | 通过插件保存的时间；非空表示来源包含“浏览器收藏” |

- `CreatedAt` 保持不变，含义是“进入文库的时间”，界面上称为**保存时间**。
- `SearchVector` 生成列改为 `Title, Summary, ReadableContent, Author`（配置仍为 `mixed_zh_en`）。迁移会删除并重建该列和 GIN 索引。

索引：

- `IX_Articles_DeletedAt_IsArchived_CreatedAt_Id`：`(DeletedAt, IsArchived, CreatedAt DESC, Id DESC)`
- `IX_Articles_LastReadAt`：`(LastReadAt DESC NULLS LAST, Id DESC)`
- `IX_Articles_PublishedAt`：`(PublishedAt DESC NULLS LAST, Id DESC)`
- `IX_Articles_NormalizedUrl`：非唯一。历史数据可能有重复；匹配时取 `CreatedAt` 最早的一条。
- `IX_Articles_DeletedAt`：部分索引，`WHERE "DeletedAt" IS NOT NULL`，服务回收站视图和清理任务。

**数据回填：**

- 迁移 SQL：`ReadingStatus = CASE WHEN "ReadAt" IS NOT NULL THEN 2 ELSE 0 END`；`SavedFromExtensionAt = "CreatedAt"`。
- `NormalizedUrl` 的规范化逻辑在 C# 中，所以由 `DbSeeder.InitAsync` 在启动时回填：查出 `NormalizedUrl = ''` 的文章，计算后保存。该操作幂等。

### 3.3 软删除与查询过滤

- `ArticleConfiguration`：`HasQueryFilter(a => a.DeletedAt == null)`。
- `HighlightConfiguration`：`HasQueryFilter(h => h.Article.DeletedAt == null)`。这样高亮页面不会出现回收站文章的高亮。
- 标签文章数 `t.Articles.Count()` 经由导航属性查询，EF 会对 `Article` 应用过滤器，自动排除回收站文章。需要写测试确认。
- 必须看到回收站文章的地方，显式调用 `IgnoreQueryFilters()`：回收站视图、恢复、永久删除、插件保存时按 URL 匹配、订阅导入时按 URL 匹配、清理任务。
- 级联删除配置保持不变，只在永久删除时触发。删除标签时 `ArticleTags` 由数据库级联删除，也会删掉与回收站文章的关联，这是可以接受的行为。

### 3.4 领域方法

用以下幂等方法替换 `ToggleFavorite` / `ToggleArchive` / `MarkAsRead`。所有方法都不修改其他维度：

| 方法 | 行为 |
|---|---|
| `SetFavorite(bool)` | 设置收藏 |
| `SetArchived(bool)` | 设置归档；**不改阅读状态** |
| `MarkRead(now)` | `ReadingStatus = Read`；如果 `ReadAt` 为空则设为 `now`；**不归档**；保留位置 |
| `MarkUnread()` | `ReadingStatus = Unread`，`ReadAt = null`；**保留位置** |
| `ApplyProgress(position, sessionId, now)` | 写入位置，`ProgressUpdatedAt = now`、`LastReadAt = now`；如果当前是 `Unread` 则变为 `Reading`；`Read` 保持不变 |
| `ResetProgress(sessionId?, now)` | 清空位置的 4 个字段，`ProgressUpdatedAt = now`，`ProgressSessionId = sessionId`（插件补全文时为 null）；**不改状态** |
| `MoveToTrash(now)` | 未删除时设置 `DeletedAt = now`；已删除时不变 |
| `Restore()` | `DeletedAt = null` |
| `ReplaceContent(content, kind)` | 用于插件补全文（见 6.6） |

`ArticleTests` 需要覆盖上表每一行，以及“设置各种组合 → 删除 → 恢复后完全一致”。

### 3.5 状态接口

```
PATCH /api/articles/{id}/state
请求  { "isFavorite"?: bool, "isArchived"?: bool, "readingStatus"?: "unread" | "read" }
响应  200 ArticleListItemDto；404；400（请求体为空或取值非法）
```

- `readingStatus` 只接受 `unread` / `read`。`reading` 只能由进度保存产生。
- 回收站中的文章返回 404。前端只能在回收站视图里恢复或永久删除。
- 枚举在 JSON 中统一使用小写字符串：在 `AddJsonOptions` 里注册 `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`。
- 删除 `POST {id}/favorite`、`{id}/archive`、`{id}/read` 及对应的 Command/Handler。插件（`src/web-extension/src/background.ts`）只调用 `/api/articles/content`，不受影响。
- `DELETE /api/articles/{id}` 改为移入回收站，返回 204。
- 新增 `POST /api/articles/{id}/restore`，返回 200 和 DTO。

### 3.6 URL 规范化（`UrlNormalizer`）

1. 解析为绝对 URL；scheme 和 host 转为小写；去掉默认端口（80 / 443）。
2. 去掉 fragment。
3. 去掉以下查询参数：`utm_*`、`fbclid`、`gclid`、`mc_cid`、`mc_eid`、`spm`、`from`、`share_source`，名称不区分大小写；其余参数保持原有顺序。
4. 路径不是 `/` 时去掉末尾的 `/`。
5. 不处理 `www.` 前缀，也不改写 http/https。

单测覆盖以上每条规则，以及解析失败时原样返回原 URL。

### 3.7 配置

```json
"Library": { "TrashRetentionDays": 30, "UndoWindowSeconds": 10 }
```

- `LibraryOptions` 通过 `AddOptions<LibraryOptions>().Bind(...).Validate(...).ValidateOnStart()` 绑定，写法与 `ImageProxyOptions` 一致。校验范围：保留期 1–365 天，撤销窗口 3–60 秒。
- 可以用环境变量 `Library__TrashRetentionDays` 等覆盖。
- `GET /api/settings/library` 返回 `{ "trashRetentionDays": 30, "undoWindowSeconds": 10 }`，需要登录。

---

## 4. 文库视图、筛选与分页（P1）

### 4.1 视图

| view | 条件 | 说明 |
|---|---|---|
| `inbox`（默认） | `!IsArchived` | 包含未读、阅读中、已读 |
| `all` | — | 包含已归档 |
| `favorites` | `IsFavorite` | 包含已归档 |
| `archive` | `IsArchived` | |
| `feeds` | 至少有一个 `FeedEntry` 指向该文章 | P5 之前不显示该入口，接口返回 400 |
| `trash` | `DeletedAt != null` | 使用 `IgnoreQueryFilters()`，固定按 `DeletedAt DESC, Id DESC` 排序，忽略 `sort` 参数 |

除 `trash` 外，所有视图都隐含“未删除”（由全局过滤器保证）。

### 4.2 列表接口

```
GET /api/articles
  view      = inbox | all | favorites | archive | feeds | trash
  q         = string，≤ 200 字符
  status    = unread,reading,read（逗号分隔，取并集；为空表示不限）
  tags      = guid,guid（AND，最多 10 个）
  source    = extension | feed
  feedId    = guid（隐含 source=feed）
  savedFrom = ISO 8601（含）
  savedTo   = ISO 8601（不含）
  sort      = saved_desc（默认）| saved_asc | read_desc | published_desc
  cursor    = string（不透明）
  pageSize  = 1–50，默认 20
响应 200
{
  "items": ArticleListItemDto[],
  "total": 126,
  "nextCursor": "eyJ..." | null
}
```

**谓词组合：** 视图条件 AND 各个筛选条件。

- **关键词：** `a.SearchVector.Matches(EF.Functions.PlainToTsQuery("mixed_zh_en", q)) || EF.Functions.ILike(a.Title, "%" + escaped(q) + "%")`。标题子串兜底，用于弥补分词遗漏，例如短词或产品名。
- **阅读状态：** `statuses.Contains(a.ReadingStatus)`。
- **标签：** 对每个 tagId 追加一个 `Where(a => a.Tags.Any(t => t.Id == id))`。
- **来源：**
  - `extension` → `SavedFromExtensionAt != null`。
  - `feed` → `FeedEntries.Any(e => e.ArticleId == a.Id)`，指定 `feedId` 时再加 `e.FeedId == feedId`。
- **保存日期：** `CreatedAt >= savedFrom && CreatedAt < savedTo`。“最近一周”等预设由前端按本地时区换算成具体时间点。

**总数与分页：** 先用同一个 `IQueryable` 执行 `CountAsync` 得到 `total`，再按排序和游标取 `pageSize + 1` 条，判断是否还有下一页。两条 SQL 在同一个请求内执行。

**游标（keyset）：**

- 内容为 base64url 编码的 JSON：`{ "s": sort, "v": "<排序键 ISO 时间或 null>", "id": "<guid>" }`。
- `s` 与本次请求的 `sort` 不一致时返回 400。
- `saved_desc`：`ORDER BY CreatedAt DESC, Id DESC`，条件为 `CreatedAt < v OR (CreatedAt = v AND Id < id)`。`saved_asc` 方向相反。
- `read_desc` / `published_desc`：排序键可能为空，空值排在最后。`ORDER BY (key IS NULL), key DESC, Id DESC`。
  - 游标 `v` 非空时，条件为 `(key IS NOT NULL AND (key < v OR (key = v AND Id < id))) OR key IS NULL`。
  - 游标 `v` 为空时，条件为 `key IS NULL AND Id < id`。
- `trash`：键为 `DeletedAt`，规则同 `saved_desc`。

选择游标而不是页码偏移，是为了在收件箱里归档或删除文章后，“加载更多”不会漏掉条目。

**`ArticleListItemDto`：**

```ts
{
  id, url, title, author, summary, imageUrl,
  createdAt, publishedAt?, readAt?, lastReadAt?, deletedAt?,
  readingStatus: "unread" | "reading" | "read",
  progressPercent?: number,
  isFavorite, isArchived,
  contentKind: "full" | "summaryOnly",
  savedFromExtension: boolean,
  feeds: { id, title }[],          // P5 前为空数组
  tags: TagDto[],
  highlightCount: number
}
```

查询使用投影（`Select`），不加载 `ReadableContent` 和 `OriginalContent`。

**代码组织：**

- 新建 `GetArticleListQuery(ArticleListRequest)`，返回 `Result<ArticleListPage>`。
- 新建 `ArticleListFilter.Apply(IQueryable<Article>, ArticleListRequest)`，是纯函数，便于单测。
- 新建 `ArticleCursor.Encode/Decode`。
- 删除 `GetArticlesQuery`、`GetArticlesLightQuery` 和 `/light` 端点。
- `GET /api/tags/{id}/articles` 改为内部调用 `GetArticleListQuery(view=all, tags=[id])`，返回同样的分页结构。前端标签详情页同步调整。

### 4.3 前端：路由与状态

- 文库只有一个路由 `/`，所有条件放在 query 里：`/?view=inbox&status=unread&tags=a,b&q=缓存&sort=saved_desc&source=feed&feedId=…&savedFrom=…&savedTo=…&range=week`。
  - `range` 只给前端用（`week | month | custom`），用来回显预设；发给后端时只传 `savedFrom` / `savedTo`。
  - 默认值不写进 URL，比如 `view=inbox`、`sort=saved_desc`。
- `lib/library-query.ts` 提供 `parseLibraryQuery(URLSearchParams)`、`serializeLibraryQuery(q)`、`toApiParams(q)`、`isFiltered(q)`，并配 Vitest 单测。
- 搜索框输入防抖 300ms，用 `router.replace` 更新 URL，不产生历史记录；切换视图和改筛选用 `router.push`。
- `page.tsx` 中使用 `useSearchParams` 的组件需要包一层 `<Suspense>`（Next 15 的要求）。

**`useArticleList(query)`：**

- query 的序列化结果变化时，清空列表，取消在途请求（`AbortController`），请求第一页。
- `loadMore()` 带上 `nextCursor` 追加；按 `id` 去重。
- 本地修改：
  - 状态改变后不再满足当前视图时（例如在收件箱里归档），从列表移除，`total - 1`。
  - 仍满足时原地更新。
  - 列表在本地判断是否满足视图条件，用 `matchesView(item, query)`。这个判断只用于本地移除，以服务端结果为准。
- 返回 `{ items, total, loading, loadingMore, error, hasMore, loadMore, updateItem, removeItems }`。

**滚动恢复：**

- 模块级 `Map<queryKey, { items, total, nextCursor, scrollY, savedAt }>`，最多保留 5 个键；离开文库时写入。同时把当前键写入 `sessionStorage`，刷新页面也能恢复。
- 回到同一 queryKey 时：先用缓存渲染，在 `requestAnimationFrame` 后执行 `window.scrollTo(0, scrollY)`，然后静默重新请求第一页；第一页有序 ID 和总数未变、且没有已知的成员或排序键变更时，只合并第一页的元数据。成员变化（包括总数变化）或已有本地成员/排序键变更时，沿服务端游标重新获取已加载区间，并使用与该区间匹配的尾部游标；保留缓存界面和滚动，不把文章缺席第一页直接当作删除。
- 缓存超过 10 分钟则直接重新加载。
- Next 的路由自动滚动用 `scroll={false}` 关闭，避免冲突。

### 4.4 前端：界面

**侧边栏**（`Sidebar.tsx`）中把 “Library” 替换为一组视图入口：

收件箱（Inbox）/ 全部文章 / 收藏 / 归档 / 订阅（P5）/ 回收站

激活状态按 `view` 参数判断。“Highlights”“Tags”保留。

**工具栏 `LibraryToolbar`：**

- 搜索框。
- “阅读状态”多选下拉。
- “标签”多选下拉，内置搜索，数据来自 `GET /api/tags`。
- “来源”单选：全部 / 浏览器收藏 / RSS；选 RSS 时出现订阅源选择。
- “保存日期”：不限 / 最近一周 / 最近一个月 / 自定义；自定义使用两个原生 `<input type="date">`。
- “排序”：最近保存 / 最早保存 / 最近阅读；仅在订阅视图下增加“最新发布”。
- 卡片/列表视图切换：保留现有功能。

**其他：**

- **计数行：** “共 126 篇，已加载 40 篇”；没有下一页时显示“共 40 篇”。
- **加载更多：** 列表底部按钮，加载中禁用并显示 spinner；请求失败时显示“加载失败，重试”。
- **空结果：**
  - 有筛选条件时：用 chip 列出当前条件，例如“未读 ×”“标签：.NET ×”“搜索：缓存 ×”，并提供“清除筛选”按钮（保留当前视图）。
  - 无筛选条件时：显示该视图专属的空状态文案，例如收件箱“没有待处理的文章”、回收站“回收站是空的”。
- **继续阅读条**（只在收件箱且无筛选时显示）：请求 `status=reading&sort=read_desc&pageSize=5&view=all`，横向排列标题和进度；为空时不显示。
- **卡片 `ArticleCard` 新增内容：**
  - 状态标识：未读圆点 / “已读 42%” / “已读 · 11月7日”。
  - 来源图标。
  - “仅摘要”标签。
  - 快捷操作：收藏、归档 / 移回收件箱、标记已读 / 未读、删除。

---

## 5. 阅读状态与断点续读（P2）

### 5.1 位置坐标

**全局文本偏移：** 对 DOMPurify 处理后的 `.readable-content`，用 `TreeWalker(SHOW_TEXT)` 依次累加所有文本节点的长度。这与 `HighlightManager` 现有的 `calculateTextOffset` 是同一个坐标系，两者都迁到 `lib/text-offsets.ts` 共用。

**块：**

- 匹配 `p, h1, h2, h3, h4, h5, h6, li, pre, blockquote, figure, table, dt, dd` 的元素。
- `pre`、`table`、`figure` 视为原子块，忽略其内部的匹配元素。
- 其他元素只取最内层：不包含其他匹配元素的那一个。
- 不在任何块内的非空白文本节点：同一父元素下**连续**的这类文本节点（中间夹着的行内元素也算在内），合并成一个**匿名块**，用 `Range` 表示，位置通过 `range.getBoundingClientRect()` 读取。这样块之间不会嵌套或重叠，`startOffset` 按文档顺序严格递增。（我的判断）

**块信息：** `{ node: Element | Range, index, startOffset, textLength }`，按文档顺序排列，`startOffset` 严格递增。由 `useReaderBlocks(contentRef, deps)` 在正文和高亮渲染完成后计算。只有正文变化时重新计算；尺寸变化只重新读取位置，不重算偏移。

**锚点：**

- 锚线位于视口顶部往下 20%（`window.innerHeight * 0.2`）。
- 锚点块是第一个底边位于锚线下方的块。
- `blockRatio = clamp((锚线 - 块顶) / 块高, 0, 1)`。
- 记录 `{ textOffset: startOffset, blockIndex: index, blockRatio, percent }`。

**百分比：**

- `percent = (startOffset + blockRatio * textLength) / totalTextLength * 100`。
- 最后一个块的底边进入视口时记为 100。
- 百分比按文本计算，与设备和字号无关；卡片上的“已读 42%”和阅读页进度条使用同一个值。

**恢复：**

- 在块列表中二分查找 `startOffset <= textOffset` 的最后一个块。如果 `textOffset` 为空，按 `blockIndex` 查找；找不到时取最后一个块。
- 滚动到 `块顶 + blockRatio * 块高 - 锚线` 的位置，使用 `behavior: "instant"`。
- 恢复后的 2 秒内，正文中每有一张图片加载完成，就按同一锚点再校正一次。用户主动滚动后停止校正。

**不变式（写进测试）：** 阅读页对正文做的所有 DOM 加工都不能改变文本节点序列的拼接结果。这些加工包括：

- 高亮：现有 `HighlightManager` 用 `<span data-highlight-id>` 包裹，并追加一个空的笔记图标 span（不含文本）。
- 标题 `id`、表格外层滚动容器、图片懒加载属性。

块划分和偏移计算要跳过高亮附加的图标元素（它们不含文本，不影响偏移，但不能被当成块）。用 Vitest + happy-dom 断言加工前后 `textContent` 一致。

### 5.2 状态流转

```
          首次有效进度                       标记已读 / 接受文末提示
 Unread ───────────────▶ Reading ──────────────────────────────▶ Read
   ▲                                                                │
   └──────────────────────── 标记未读（保留位置） ◀──────────────────┘
 Unread 也可以直接标记已读；Read 状态下继续保存位置，但状态不变。
 从头阅读：只清空位置，任何状态下都不改状态。
```

**有效进度**同时满足三个条件：

1. 页面可见。
2. 用户发生过主动滚动（wheel / touch / 键盘 / 拖动滚动条，程序滚动不算）。
3. 锚点的 `percent ≥ 2` 或 `blockIndex ≥ 2`，并且在该位置附近停留了至少 3 秒。

只有满足这三个条件，客户端才会开始保存进度。因此只打开文章、只看标题，不会把文章变成“阅读中”。

**文末提示：** 最后一个块进入视口、状态不是 `Read`、本次访问没有关闭过提示时，底部出现提示条：“读完了？[标记已读] [×]”。

### 5.3 进度接口与并发

```
PUT /api/articles/{id}/progress
请求  { textOffset, blockIndex, blockRatio, percent, sessionId, baseUpdatedAt | null }
响应  200 { progressUpdatedAt, readingStatus, lastReadAt, contentKind }
     409 { current: { textOffset, blockIndex, blockRatio, percent, progressUpdatedAt } }
     404（不存在或在回收站）  400（取值越界）

POST /api/articles/{id}/progress/reset
请求  { sessionId }
响应  200 { progressUpdatedAt, readingStatus, lastReadAt, contentKind }
```

保存与重置的 `sessionId` 上限均为 64 字符，与现有存储契约一致。成功响应新增 `contentKind`，取自更新进度时锁定的同一文章行。摘要阅读页必须在接纳 reset 返回的新版本/会话基准前核对该正文类型；若已补全或不能确认仍是摘要，暂停旧坐标并提示刷新。仅预先 GET 详情无法关闭 GET 与 reset 事务之间发生补全文的竞争窗口。列表/状态元数据不得改变当前已载入正文对应的类型；状态响应发现补全文时立即暂停旧正文进度。

重置时把发起页面的 `sessionId` 写入，并返回新的 `progressUpdatedAt`；客户端用它更新 `baseUpdatedAt`。这样发起重置的页面下一次保存不会被自己的 409 挡住，而其他页面拿着旧的 `baseUpdatedAt` 保存时仍会收到 409。（我的判断）

服务端判断冲突的规则：

```
冲突 = 已有 ProgressUpdatedAt
       且 ProgressSessionId 不是本次的 sessionId
       且 (baseUpdatedAt 为空 或 baseUpdatedAt < ProgressUpdatedAt)
```

- 冲突时返回 409；否则调用 `ApplyProgress`。
- 时间戳都由服务器生成，不受各设备时钟偏差影响。

客户端（`useReadingProgress`）：

- `sessionId = crypto.randomUUID()`，每次打开阅读页生成一次。
- `baseUpdatedAt` 初始取文章详情里的 `progressUpdatedAt`，每次保存成功后更新为返回值。
- 保存时机：
  - 位置变化后节流，最多每 5 秒一次。
  - `visibilitychange → hidden`、`pagehide`、组件卸载（路由离开）时立即保存，使用 `fetch(url, { method: "PUT", keepalive: true, credentials: "include" })`。
  - 不用 `sendBeacon`，因为开发环境是跨域的 JSON 请求。
- 页面重新可见时，先 `GET /api/articles/{id}` 拉取进度：
  - 如果 `progressUpdatedAt` 比本地已知的新，且不是本会话写的，在顶部提示“已在其他设备读到 63%  [跳转] [留在这里]”。
  - “跳转”：恢复到服务器位置，更新 `baseUpdatedAt`。
  - “留在这里”：把 `baseUpdatedAt` 设为服务器值，下次保存会覆盖。
- 收到 409 时显示同样的提示。
- 失败处理：
  - 网络错误或 5xx 时按 2s / 5s / 15s / 30s 退避重试，并把待保存的位置写入 `sessionStorage`（键 `progress:{id}`），下次打开时优先尝试提交。
  - 工具栏的同步状态有三种：已同步（不显示）/ 保存中 / “进度暂未同步”。只有收到 2xx 后才算已同步。

### 5.4 阅读页操作

- **工具栏：** 返回、标记已读 / 标记未读、从头阅读、收藏、归档 / 移回收件箱、删除、阅读设置、目录，以及同步状态。
- **列表卡片：** 标记已读 / 未读。
- **返回按钮：** 如果是从文库进入的（`document.referrer` 同源，或者 `history.length > 1` 且来源记在 sessionStorage 中），调用 `router.back()`，配合 4.3 的滚动恢复；否则跳转到 `/`。
- **文章详情接口：** `GET /api/articles/{id}` 的 `ArticleDto` 增加以下字段：`readingStatus`、`progress { textOffset, blockIndex, blockRatio, percent, updatedAt } | null`、`lastReadAt`、`publishedAt`、`contentKind`、`feeds`、`savedFromExtension`。回收站中的文章返回 404。

---

## 6. RSS / Atom 订阅（P5）

### 6.1 数据模型（迁移 `M3_Feeds`）

**`Feed`（聚合根）**

| 字段 | 类型 | 说明 |
|---|---|---|
| `Id` | guid v7 | |
| `FeedUrl` | text | 用户确认后的订阅地址，经 `UrlNormalizer` 规范化；完整值保留 |
| `FeedUrlHash` | varchar(64)，唯一 | C# 计算的 SHA-256 查找键，命中后核对完整 `FeedUrl` |
| `SiteUrl` | text? | |
| `OriginalTitle` | varchar(500) | 源站标题 |
| `Title` | varchar(500) | 用户命名 |
| `Format` | int | `Rss=0`、`Atom=1` |
| `Status` | int | `Active=0`、`Paused=1`、`Unsubscribed=2` |
| `ETag` / `LastModified` | varchar? | 条件请求 |
| `LastFetchedAt` / `LastSuccessAt` | timestamptz? | |
| `LastError` | varchar(1000)? | 最近一次失败的原因，成功后清空 |
| `ConsecutiveFailures` | int | |
| `MinIntervalMinutes` | int | 由 `ttl` / `sy:updatePeriod` 推算，最小 30 |
| `NextFetchAt` | timestamptz | 带索引 |
| `LeaseUntil` | timestamptz? | 正在拉取的租约 |
| `LeaseToken` | guid? | 本次认领的所有权令牌，提交前在行锁内核对，过期 worker 不可覆盖新租约 |
| `CreatedAt` | timestamptz | |

- 默认标签使用多对多关联 `FeedTags(FeedId, TagId)`，删除标签时级联删除关联。
- **健康状态**由字段推导，不另外存储：`ConsecutiveFailures > 0` 表示“更新失败”，否则为“正常”。

**`FeedEntry`**

| 字段 | 类型 | 说明 |
|---|---|---|
| `Id` | guid v7 | |
| `FeedId` | guid，FK，级联删除 | |
| `EntryKey` | text | 完整条目标识 |
| `EntryKeyHash` | varchar(64) | 唯一索引 `(FeedId, EntryKeyHash)`，命中后核对完整 `EntryKey` |
| `EntryUrl` | text? | |
| `ArticleId` | guid?，FK，删除文章时置空 | |
| `ContentHash` | varchar(64) | 正文的 SHA-256 |
| `FirstSeenAt` / `LastSeenAt` | timestamptz | |
| `Dismissed` | bool | 为 true 时不再导入。两种情况会置为 true：关联文章被永久删除；首次导入时超出 20 篇的条目 |

**经真实 PostgreSQL 验证的内部存储修正：** 419 字符的 Unicode HTTP 地址经标准 `Uri.AbsoluteUri` 序列化后即可超过 2000 字符；1800 字符的不重复 CJK 条目标识也会超过 B-tree 键宽。因此完整 URL/条目标识使用 `text`，唯一性由持久化 SHA-256 查找列提供，哈希命中后必须核对完整值，碰撞报错，不截断或混并。`Articles.Url` 与 `Articles.ImageUrl` 同样使用 `text` 以保存订阅中解析生成的完整文章/封面地址；已有文章 URL 与封面地址写入 API 仍保留 2000 字符输入上限。新订阅接口按 D4 接受 http/https 地址，允许 preview 返回的规范化地址直接用于创建。

`Highlight` 新增 `AnchorStatus`：`Ok=0`、`Pending=1`、`Orphaned=2`（见 6.6）。

### 6.2 获取与解析

- **`FeedFetcher`：** 使用 `IHttpClientFactory` 命名客户端 `feeds`。
  - 超时 20 秒；`User-Agent: MiniCC/1.0 (+self-hosted feed reader)`。
  - 读取响应流时超过 10MB 即中止；20 秒期限包含响应体读取。
  - 在转换为字符串之前按 BOM → 显式 HTTP charset → XML 声明/默认编码的优先级解码；无 BOM/HTTP charset 时由安全配置的 `XmlReader.Create(Stream)` 识别 XML 字节编码。
  - 自动跟随重定向，最多 5 次。
  - 只允许 http/https，不做内网限制（D4）。
  - 有 `ETag` / `LastModified` 时发送 `If-None-Match` / `If-Modified-Since`。
- **自动发现：** 响应的 Content-Type 是 HTML，或者 XML 解析失败而内容像 HTML 时，用 HtmlAgilityPack 查找 `<link rel="alternate" type="application/rss+xml|application/atom+xml">`，取第一个，以页面地址为基准解析成绝对地址，然后再请求一次。最多发现一层。
- **`FeedParser`：** 使用 `System.ServiceModel.Syndication` 的 `SyndicationFeed.Load(XmlReader)`。读取 XML 时设置 `DtdProcessing = Ignore`，防止 XXE。
  - 格式：根元素是 `rss` 为 `Rss`，是 `feed` 为 `Atom`。
  - `EntryKey` 依次取：Atom `id` / RSS `guid` → 条目链接 → `sha256(title + "|" + publishDate)`。
  - 条目 URL 依次取：`link`（Atom 取 `rel=alternate`）→ `isPermaLink` 不为 false 的 RSS `guid`。都没有时**跳过该条目**，并计入本次刷新的跳过数。
  - 正文 HTML 依次取：RSS 的 `content:encoded`（从 `ElementExtensions` 读取）/ Atom `content` → RSS `description` / Atom `summary`。
  - 发布时间依次取：`PublishDate` → `LastUpdatedTime` → 空。解析失败也为空，不抛异常。
  - 作者：条目作者 → 订阅源标题。
  - 封面图依次取：`media:thumbnail` / `media:content` → 图片类型的 `enclosure` → 正文中第一张 `img`。
  - 遇到 RSS 1.0 (RDF) 等不支持的格式时，返回错误“不支持的订阅格式”。

### 6.3 内容清洗与仅摘要判定

**清洗：** 使用 `HtmlSanitizer`（Ganss.Xss）。

- 在默认白名单基础上允许 `figure, figcaption, picture, source`，以及 `pre` / `code` 上的 `class` 属性（保留代码语言标记）。
- 订阅/站点/条目/链接先按继承的 `xml:base`（以传输地址为文档基准）解析。正文与嵌套 XHTML 的链接/图片同样继承各层 `xml:base`；空值继承父级，不重置基准。正文没有显式基址时，`BaseUri` 回退到条目 URL。相对地址转为绝对地址后仍经过 HtmlSanitizer 与图片代理。
- 清洗后交给 `ImageProxyService.RewriteImages`。
- 订阅内容不经过插件的 DOMPurify，所以服务端必须清洗；前端渲染前仍会再做一次 DOMPurify。

**仅摘要判定（D3）：** 满足任一条件即为 `SummaryOnly`：

- 条目只有 `description` / `summary`，没有 `content:encoded` / Atom `content`，并且纯文本少于 500 字符。
- 选中的正文纯文本少于 500 字符。
- 纯文本以 `…`、`...`、`Read more`、`阅读全文`、`[…]` 结尾（去除空白后判断）。

**字段映射：**

- `ReadableContent`：清洗后的 HTML。仅摘要时就是清洗后的摘要。
- `Summary`：`description` 的纯文本，没有时取正文纯文本，截断到 300 字符。
- `OriginalContent`：未清洗的原始条目正文字符串。
- `Length`：纯文本长度。
- `PublishedAt`：发布时间。
- `CreatedAt`：导入时间。
- `SavedFromExtensionAt`：空。

### 6.4 导入规则（`FeedImportService.ImportAsync(feed, entries, isInitial)`）

对每个条目，按以下顺序判断：

1. **已存在 `(FeedId, EntryKey)`：**
   - 更新 `LastSeenAt`。
   - `Dismissed` 为 true，或关联文章在回收站中：跳过。
   - `ContentHash` 不同：只更新哈希，**不修改文章**。
2. **新条目：** 按 `NormalizedUrl` 查找文章，使用 `IgnoreQueryFilters()`，多条时取最早的一条。
   - 找到：新建 `FeedEntry` 指向它，**不修改文章**。如果该文章在回收站，保持在回收站，不恢复。
   - 未找到：创建 `Article` 和 `FeedEntry`，并附加订阅源当前的默认标签。
3. **首次导入**（创建订阅、或重新订阅后的第一次）：条目按发布时间倒序排列（无发布时间的保持源中顺序），只导入前 20 个新条目。其余新条目也写入 `FeedEntry`，但设为 `ArticleId = null, Dismissed = true`，这样后续刷新时会按规则 1 跳过。否则第二次刷新就会把它们全部导入，20 篇的上限也就失效了。
4. **非首次导入：** 导入所有新条目，不设上限。

- 每个订阅源的一次导入在一个数据库事务里完成。
- 唯一索引冲突时（并发情况）跳过该条目。

**同一文章、多个订阅源：** 多个 `FeedEntry` 指向同一个 `Article`，阅读状态、标签和高亮自然共享。

**被删除的条目不会被重新导入：**

- 在回收站期间：规则 1 跳过；其他订阅源的同一 URL 由规则 2 关联到这篇在回收站中的文章，也不会新建。
- 永久删除后：清理任务和永久删除操作在删除文章之前，把所有指向它的 `FeedEntry` 设为 `Dismissed = true, ArticleId = null`，此后规则 1 会跳过它。

### 6.5 后台刷新

**`FeedRefreshWorker : BackgroundService`** 每 60 秒执行一次：

1. 用一条 SQL 原子地领取任务，最多 4 个：

   ```sql
   UPDATE "Feeds" SET "LeaseUntil" = now() + interval '5 minutes'
   WHERE "Id" IN (
     SELECT "Id" FROM "Feeds"
     WHERE "Status" = 0 AND "NextFetchAt" <= now()
       AND ("LeaseUntil" IS NULL OR "LeaseUntil" < now())
     ORDER BY "NextFetchAt" LIMIT 4
     FOR UPDATE SKIP LOCKED)
   RETURNING "Id";
   ```

2. 每个订阅源在独立的 DI scope 中，用 `Task.WhenAll` 并发执行 `FeedRefresher.RefreshAsync(feedId)`，每个都单独 try/catch，一个源失败不影响其他源。

**`FeedRefresher.RefreshAsync` 的结果处理：**

| 结果 | 处理 |
|---|---|
| 200 | 解析并导入，更新 ETag / LastModified；`LastSuccessAt = now`，`ConsecutiveFailures = 0`，清空 `LastError`；`NextFetchAt = now + max(30, MinIntervalMinutes) 分钟` |
| 304 | 与 200 相同，只是不导入 |
| 429 / 503 | 按失败处理；如果有 `Retry-After`，`NextFetchAt` 取它和退避时间中较晚的那个 |
| 其他失败（网络、超时、4xx/5xx、解析错误） | `ConsecutiveFailures += 1`，`LastError` 记录人能看懂的原因；`NextFetchAt = now + min(30 × 2^(failures-1), 1440) 分钟` |

处理完成后清空 `LeaseUntil`，`LastFetchedAt = now`。

**手动刷新** `POST /api/feeds/{id}/refresh`：

- 用同样的租约条件领取这一个源（不检查 `NextFetchAt`）。
- 领取失败返回 409“正在刷新”。
- 成功则同步执行，返回 `{ imported, skipped, status, lastError }`。

**回收站清理 `TrashPurgeWorker`：** 启动后延迟 1 分钟首次执行，之后每 24 小时执行一次。每次分批（每批 100 篇）永久删除 `DeletedAt < now - TrashRetentionDays` 的文章，删除前按 6.4 将相关 `FeedEntry` 设为 `Dismissed`。

### 6.6 插件保存的处理（修改 `SaveArticleContentCommandHandler`）

1. 用 `IgnoreQueryFilters()` 按 `NormalizedUrl` 查找文章，多条时取最早的。
2. 未找到：创建文章，`SavedFromExtensionAt = now`。这是现有行为。
3. 找到：
   - 在回收站中时先 `Restore()`（D2）。
   - `SavedFromExtensionAt ??= now`。
   - 如果 `ContentKind == SummaryOnly`：调用 `ReplaceContent`，即替换 `ReadableContent`、`OriginalContent`、`Length`，`ContentKind = Full`；同时调用 `ResetProgress`，并把该文章所有高亮设为 `AnchorStatus = Pending`。标签、收藏、归档、阅读状态、笔记都不变。
   - 如果 `ContentKind == Full`：不修改正文。
4. 返回 `ArticleDto`。

恢复后的文章保留删除前的归档状态，不会强制回到收件箱。（这是我的判断：与“恢复时保留状态”的总体规则保持一致。）

**高亮重新定位（在客户端完成，保证与渲染使用同一坐标系）：**

- 阅读页加载后，对每个 `Pending` 高亮，在当前正文文本中查找 `Text` 的所有出现位置，选取离原 `StartOffset` 最近的一个。
- 找到时调用 `PUT /api/highlights/{id}/anchor { startOffset, endOffset }`，状态变为 `Ok`；找不到时调用 `PUT /api/highlights/{id}/anchor { orphaned: true }`，状态变为 `Orphaned`。
- `Pending` 和 `Orphaned` 的高亮不在正文中渲染。高亮列表和高亮页面里，`Orphaned` 高亮显示“原文位置已失效”，笔记照常显示。

### 6.7 订阅接口

| 方法 | 路径 | 请求 | 响应 |
|---|---|---|---|
| POST | `/api/feeds/preview` | `{ url }` | 200 `{ feedUrl, title, siteUrl, format, items: [{ title, url, publishedAt, contentKind }] (≤5), existingFeedId? }`；422 `{ error }`，例如无法访问、不是订阅地址、格式不支持 |
| POST | `/api/feeds` | `{ feedUrl, title, tagIds[] }` | 201 `{ feed: FeedDto, imported }`。`feedUrl` 已存在：如果是 `Unsubscribed`，重新激活并执行首次导入；否则返回 409 `{ existingFeedId }` |
| GET | `/api/feeds` | — | `FeedDto[]`，不含 `Unsubscribed`，按标题排序 |
| PATCH | `/api/feeds/{id}` | `{ title?, tagIds?, paused? }` | 200 `FeedDto`。`paused: false` 时 `NextFetchAt = now` |
| POST | `/api/feeds/{id}/refresh` | — | 见 6.5 |
| DELETE | `/api/feeds/{id}` | — | 204，`Status = Unsubscribed`，保留文章和条目 |
| POST | `/api/feeds/{id}/trash-articles` | — | 200 `{ trashed }`：把只来自该订阅源、且不是插件保存的文章移入回收站 |

```ts
FeedDto = {
  id, title, originalTitle, feedUrl, siteUrl, format,
  status: "active" | "paused",
  health: "ok" | "failing",
  lastSuccessAt?, lastFetchedAt?, lastError?,
  unreadCount, articleCount,
  tags: TagDto[]
}
```

`unreadCount` 统计指向该源的未删除、`ReadingStatus = Unread` 的不同文章数。

`trash-articles` 只处理“只来自该订阅源”的文章（我的判断）：如果一篇文章同时来自插件或其他订阅源，删除它会影响其他来源，所以跳过，并在响应中返回跳过数 `{ trashed, skipped }`。

### 6.8 前端

- **侧边栏“订阅”：** 可展开，显示各订阅源标题和未读数；失败的源显示警示图标。点击跳转到 `/?view=feeds&feedId=…`。数据来自 `FeedsContext`：启动时加载，订阅相关操作后刷新，此外每 5 分钟刷新一次。
- **`/pages/feeds` 管理页：**
  - 列表：名称、未读数、最近成功更新时间、状态（正常 / 已暂停 / 更新失败 + 原因）。
  - 每行操作：立即刷新、编辑（名称、默认标签）、暂停 / 恢复、取消订阅（确认框，说明“已导入的文章会保留”）、删除已导入文章（确认框，说明跳过规则）。
- **添加订阅对话框，分两步：**
  1. 输入地址 → 调用 preview，显示名称、网站和最近 5 条。
  2. 修改名称、选择默认标签 → 确认 → 显示“已导入 N 篇”。
- **阅读页：**
  - 来源信息：“来自 {订阅源名称} · 发布于 {日期}”，多个源时用逗号分隔。
  - `SummaryOnly` 时，正文上方显示横幅：“仅摘要 · 订阅源只提供了摘要。[访问原文] 用浏览器插件保存原文即可补全全文，标签和笔记会保留。”

---

## 7. 阅读器设置、目录与进度条（P3）

### 7.1 偏好

迁移 `M2_ReaderPreferences`：`User.ReaderPreferences` 为 jsonb，可空。为空时使用默认值。

```ts
ReaderPreferences = {
  fontSize: 16..28,                // 默认 18，步长 1
  lineHeight: 1.5..2.2,            // 默认 1.8，步长 0.1
  width: "narrow" | "standard" | "wide",      // 默认 standard
  fontFamily: "system" | "serif",             // 默认 system
  theme: "system" | "light" | "dark" | "sepia" // 默认 system
}
```

- **接口：** `GET /api/account/reader-preferences` 返回完整对象（缺省字段用默认值补齐）；`PUT` 接收完整对象，校验失败返回 400。
- **前端 `useReaderPreferences`（放在 `ReaderPreferencesContext`）：**
  - 首帧读取 `localStorage["reader-preferences"]`，没有则用默认值。
  - 挂载后请求服务器，以服务器为准，覆盖本地。
  - 修改时立即生效，同时写入 localStorage，500ms 防抖后 PUT。
  - PUT 失败时保留本地值，在设置面板里显示“偏好未同步，[重试]”。

### 7.2 渲染

阅读器根元素 `.reader` 使用以下 CSS 变量：

| 变量 | 取值 |
|---|---|
| `--reader-font-size` | `{fontSize}px` |
| `--reader-line-height` | `{lineHeight}` |
| `--reader-max-width` | 窄 640px / 标准 760px / 宽 920px；窄屏下统一为 `100%`，左右各留 16px |
| `--reader-font` | system：`system-ui, -apple-system, "PingFang SC", "Microsoft YaHei", sans-serif`；serif：`"Source Han Serif SC", "Noto Serif SC", Georgia, serif` |
| `--reader-mono` | 固定为等宽字体栈，不受设置影响 |

- **主题：** 在 `.reader` 上设置 `data-reader-theme="light|dark|sepia"`，`system` 根据 `prefers-color-scheme` 决定。每个主题定义 `--reader-bg`、`--reader-fg`、`--reader-muted`、`--reader-link`、`--reader-code-bg`、`--reader-border`、`--reader-mark-bg`。主题作用于阅读页的标题区、正文、高亮列表，以及阅读器的浮层（设置面板、目录）；侧边栏不受影响。（我的判断：现在全站只有浅色主题，只给正文换色会显得割裂。）
- **内容适配：**
  - `img, video { max-width: 100%; height: auto }`。
  - `pre { overflow-x: auto; font-family: var(--reader-mono) }`，行内 `code` 同样使用等宽字体。
  - 表格由客户端包一层 `<div class="table-scroll">`，设置 `overflow-x: auto`。只增加元素，不改变文本（5.1 的不变式）。
  - 长 URL / 长单词：`overflow-wrap: anywhere`。
- **切换设置时保持位置：** 修改前记录当前锚点（5.1），应用新变量，等两帧（`requestAnimationFrame` 两次）后按锚点恢复。这次恢复属于程序滚动，不触发进度保存。

### 7.3 设置面板

- **桌面（≥ 768px）：** 点击工具栏按钮，在按钮下方弹出约 320px 宽的面板；点击外部或按 Esc 关闭。
- **手机：** 底部抽屉，最大高度 70vh，带遮罩。
- **控件：**
  - 字号：A- / 数值 / A+。
  - 行距：滑块。
  - 宽度：三段选择。
  - 字体：两段选择。
  - 主题：四个色块。
  - “恢复默认”按钮。
- **无障碍：** 面板 `role="dialog"`，打开时聚焦第一个控件，关闭后焦点回到触发按钮。

### 7.4 目录

- 从正文收集 `h1`–`h4`，取**实际出现的最高两级**。例如只有 h2 / h3 就用 h2 / h3；只有 h3 / h4 就用 h3 / h4。目录项少于 2 个时不显示目录。
- 为没有 `id` 的标题补上 `id="toc-{n}"`，只加属性。
- 用 `IntersectionObserver`（`rootMargin: "-20% 0px -70% 0px"`）判断当前章节并高亮对应目录项。点击目录项时用 `scrollIntoView` 跳转，并更新 URL hash（`history.replaceState`，不产生历史记录）。
- **布局：** 视口 ≥ 1200px 时，目录在正文右侧 sticky 显示；更窄时放进工具栏“目录”按钮打开的抽屉，点击后自动关闭。

### 7.5 进度条

- 固定在视口顶部，高 3px，宽度等于 5.1 计算出的 `percent`。
- `percent` 只按 `.readable-content` 内的文本计算，自然排除了页头、标签面板和高亮列表。
- 加上 `role="progressbar"` 和 `aria-valuenow`。

---

## 8. 批量管理与回收站（P4）

### 8.1 批量接口

```
POST /api/articles/batch
请求 {
  "ids": ["guid", ...],              // 1–200，去重
  "action": "markRead" | "markUnread" | "favorite" | "unfavorite" |
            "archive" | "unarchive" | "addTags" | "removeTags" |
            "trash" | "restore" | "purge",
  "tagIds": ["guid", ...]            // 仅 addTags / removeTags 需要，1–20
}
响应 200 {
  "succeeded": ["guid", ...],
  "failed": [{ "id": "guid", "reason": "notFound" | "notInTrash" | "inTrash" | "error" }]
}
```

- **逐篇执行：** 加载文章（`restore` / `purge` 使用 `IgnoreQueryFilters`）→ 调用领域方法 → `SaveChangesAsync`。某一篇抛出异常时，执行 `ChangeTracker.Clear()`，记为失败，继续处理下一篇。
- **各动作的前提条件：**
  - `restore` / `purge` 要求文章在回收站，否则返回 `notInTrash`。
  - 其他动作要求文章不在回收站，否则返回 `inTrash`。
- **标签：** `tagIds` 中有不存在的标签时，整个请求返回 400，不执行任何操作。`addTags` 已有的标签跳过，`removeTags` 没有的标签跳过。
- **永久删除：** `purge` 先把相关 `FeedEntry` 设为 `Dismissed`，再删除文章，高亮级联删除，`Tag` 不受影响。
- **清空回收站：** `DELETE /api/trash` 返回 `{ purged }`，分批执行，规则同 `purge`。
- **单篇接口复用领域方法：** 单篇 `DELETE` / `restore` 与批量接口调用同一套领域方法。

### 8.2 前端选择模式

- **进入方式：**
  - 桌面：鼠标悬停卡片时左上角出现复选框，勾选即进入选择模式。
  - 手机：工具栏右侧的“选择”按钮。
- **选择模式下：**
  - 顶部固定显示操作栏：“已选 5 篇 · 仅限当前已加载的 40 篇 [全选已加载] [取消]”。
  - 操作按钮：标记已读、标记未读、收藏、取消收藏、归档、移回收件箱、添加标签、移除标签、删除。
  - 在回收站视图中，操作只有恢复和永久删除。
- **切换视图或修改筛选时：** 自动退出选择模式并清空选择。
- **执行后：**
  - 全部成功：toast“已归档 5 篇”，退出选择模式。
  - 部分失败：toast“18 篇已归档，2 篇失败 [重试]”；成功的文章按 4.3 的本地修改规则更新或移除，失败的文章保持选中，可以直接重试。
- **执行中：** 操作栏禁用，并显示进度。

### 8.3 撤销与 Toast

- 全局 `ToastProvider` 放在 `ClientLayout` 中，同一时间最多显示 3 条，使用 `aria-live="polite"`。
- **删除（单篇或批量）：** 成功后显示“已删除 5 篇文章 [撤销]”，持续 `undoWindowSeconds` 秒。点击撤销时，用这次成功删除的 ID 调用 `batch restore`，然后把这些文章插回当前列表（按当前排序插入；不在已加载范围内的不插入，只把 `total` 加回去）。
- **阅读页删除：** 删除后跳转回文库，撤销 toast 在文库页显示。
- `undoWindowSeconds` 在应用启动时从 `/api/settings/library` 获取，请求失败时使用 10。

### 8.4 回收站视图

- 卡片显示删除时间和“{n} 天后永久删除”，`n = trashRetentionDays - 已删除天数`，最小为 0。
- 每张卡片的操作：恢复、永久删除（确认框）。
- 顶部按钮“清空回收站”，确认框写明“将永久删除 {total} 篇文章，无法恢复”。

---

## 9. 接口总表

| 方法 | 路径 | 阶段 | 变化 |
|---|---|---|---|
| GET | `/api/articles` | P1 | 重写：视图、筛选、游标、总数 |
| GET | `/api/articles/{id}` | P0/P2 | 新增字段；回收站中的文章返回 404 |
| POST | `/api/articles/content` | P0/P5 | 规范化 URL 匹配、自动恢复、补全文 |
| PUT | `/api/articles/{id}` | — | 不变 |
| PATCH | `/api/articles/{id}/state` | P0 | 新增 |
| DELETE | `/api/articles/{id}` | P0 | 改为软删除 |
| POST | `/api/articles/{id}/restore` | P0 | 新增 |
| PUT | `/api/articles/{id}/progress` | P2 | 新增 |
| POST | `/api/articles/{id}/progress/reset` | P2 | 新增 |
| POST | `/api/articles/batch` | P4 | 新增 |
| DELETE | `/api/trash` | P4 | 新增 |
| GET | `/api/settings/library` | P0 | 新增 |
| GET/PUT | `/api/account/reader-preferences` | P3 | 新增 |
| PUT | `/api/highlights/{id}/anchor` | P5 | 新增 |
| GET | `/api/tags/{id}/articles` | P1 | 改为分页结构 |
| `*` | `/api/feeds…` | P5 | 新增（见 6.7） |
| POST | `/api/articles/{id}/favorite`、`/archive`、`/read` | P0 | 删除 |
| GET | `/api/articles/light` | P1 | 删除 |

**错误约定：** 沿用现有写法，`NotFound` / `BadRequest` 返回错误字符串；409 / 422 返回 JSON 对象，结构如上文各节所述。

---

## 10. 前端结构

```
src/frontend/src/
├── lib/
│   ├── api.ts                    # articleApi 扩展；新增 feedApi、settingsApi、preferencesApi
│   ├── library-query.ts          # 筛选条件 ↔ URL
│   ├── list-cache.ts             # 文库列表缓存与滚动位置
│   ├── text-offsets.ts           # 全局文本偏移（高亮与进度共用）
│   └── reading-position.ts       # 块、锚点、百分比、恢复
├── contexts/
│   ├── ToastContext.tsx
│   ├── LibrarySettingsContext.tsx  # undoWindowSeconds、trashRetentionDays
│   ├── ReaderPreferencesContext.tsx
│   └── FeedsContext.tsx
├── app/
│   ├── page.tsx                  # 文库页（Suspense + LibraryView）
│   ├── components/
│   │   ├── library/LibraryToolbar.tsx, FilterChips.tsx, LoadMore.tsx,
│   │   │   ContinueReadingStrip.tsx, SelectionBar.tsx, EmptyState.tsx
│   │   ├── ArticleList.tsx       # 增加选择模式
│   │   └── ArticleCard.tsx       # 状态、来源、快捷操作、复选框
│   ├── hooks/useArticleList.ts, useLibraryQuery.ts
│   ├── pages/article/[id]/components/
│   │   ├── Client.tsx            # 组装阅读器
│   │   ├── ReaderToolbar.tsx, ReaderSettingsPanel.tsx, TableOfContents.tsx,
│   │   │   ReadingProgressBar.tsx, EndOfArticlePrompt.tsx,
│   │   │   SummaryOnlyBanner.tsx, SourceLine.tsx, ProgressConflictBanner.tsx
│   │   ├── hooks/useReaderBlocks.ts, useReadingProgress.ts
│   │   └── HighlightManager.tsx  # 改用 text-offsets；处理 Pending 高亮
│   └── pages/feeds/page.tsx + components/AddFeedDialog.tsx, FeedRow.tsx
```

组件命名和拆分粒度由实施者决定，上面只是建议。

---

## 11. 测试策略

**后端：**

- **单元测试（现有 xUnit + Moq + FluentAssertions）：**
  - `Article` 的所有领域方法。
  - `UrlNormalizer`。
  - `ArticleCursor`。
  - `FeedParser`：使用 `test/.../Fixtures/feeds/*.xml`，覆盖 RSS 2.0、Atom、仅摘要、guid 不是 URL、缺少 guid 和 link、相对图片地址、DTD 注入。
  - 仅摘要判定。
  - `FeedRefresher` 的退避计算。
- **集成测试（新增，Testcontainers.PostgreSql）：**
  - 镜像使用 compose 中 db 服务的镜像（带 zhparser 和 `mixed_zh_en`）；如果 CI 环境无法构建该镜像，就在 `postgres:16` 上执行 `init-db-text-search.sql`，需要镜像里有 zhparser。具体做法由实施者决定。
  - 每个测试类共享一个容器；每个测试前清空所有表。
  - 覆盖内容：
    - 列表筛选组合；65 篇翻页的完整性；翻页中途删除不漏项。
    - 中文检索。
    - 全局过滤器对标签文章数和高亮列表生效。
    - 进度保存的 409 规则。
    - 批量部分失败。
    - 永久删除后 `FeedEntry` 变为 `Dismissed`。
    - 导入规则（6.4 全部分支）。
    - 插件补全文。
  - 订阅拉取：用 `WireMock.Net` 模拟 200、304、429（带 Retry-After）、500、超时、HTML 自动发现。
- **API 层：** 沿用现有 `ArticlesControllerTests` 的写法，覆盖新端点的状态码。

**前端：**

- 新增 Vitest + happy-dom：`library-query`、`text-offsets`、`reading-position`（块划分、偏移、百分比），以及 5.1 的 DOM 加工不变式。
- 新增 Playwright（`src/frontend/e2e`），依赖本地运行的完整服务：第 13 节的验收场景，分别在 1280×800 和 375×812 两个视口下运行。

---

## 12. 迁移与发布

| 迁移 | 阶段 | 内容 |
|---|---|---|
| `M1_LibraryStateModel` | P0 | 3.2 的字段、索引、回填 SQL；`SearchVector` 重建 |
| `M2_ReaderPreferences` | P3 | `Users.ReaderPreferences jsonb` |
| `M3_Feeds` | P5 | `Feeds`、`FeedTags`、`FeedEntries`、`Highlights.AnchorStatus` |

- 每个阶段完成后都可以单独发布，前后端一起部署。旧的 toggle 端点在前端完成迁移的同一次发布中删除。
- `SearchVector` 重建会扫描全表；在个人数据规模（几千篇）下耗时可以接受，不需要额外安排。
- 每个阶段完成后，同步更新 `CLAUDE.md` 的接口列表、实体说明、配置和后台任务。

---

## 13. 验收场景

1. **文库：** 准备 65 篇文章，在“全部文章”中加载 4 次，达到 65/65，没有重复。设置不同的收藏、归档、标签后，在收件箱中组合“未读 + .NET 标签 + 搜索‘缓存’”：总数等于翻完所有页拿到的条数，且每条都满足全部条件。
2. **返回列表：** 在收件箱筛选后加载到第 2 页并滚动到中部，打开一篇文章再返回：筛选条件、已加载的条数、滚动位置都保留。
3. **空结果：** 没有结果时显示当前条件和“清除筛选”，点击后恢复该视图的全部结果。
4. **续读：**
   - 桌面读到某段，关闭后重新打开，回到这一段附近。
   - 用 375px 视口打开，或把字号调到 24，仍定位到同一段。
   - 两个标签页交替阅读，后台标签页不覆盖新进度，并出现提示。
5. **状态独立：** 标记已读后仍在收件箱；归档不改变已读状态；重新标记未读后，打开文章仍能回到原来的位置；从头阅读后位置回到开头，状态不变。
6. **进度未同步：** 断网阅读，显示“进度暂未同步”；恢复网络后自动同步，提示消失。
7. **阅读器设置：** 用中文长文、英文文章、带代码和表格的技术文章、375px 窄屏分别验证：每项设置即时生效；代码块保持等宽字体；表格和长代码行不撑破页面；刷新、打开另一篇文章、换浏览器登录后设置仍然保留；调整设置前后，高亮位置不变，当前段落不跳走。
8. **目录：** 有标题的文章显示目录，点击可以跳转，滚动时当前章节高亮；没有标题的文章不显示目录。
9. **批量与恢复：** 给 5 篇文章设置不同的状态，加上高亮笔记和阅读位置后批量删除。点击“撤销”后完全还原；再次删除，在回收站中恢复，同样完全还原；它们共用的标签仍然存在。批量“加入收藏”混合状态的文章后，全部变为收藏。
10. **部分失败：** 选中 20 篇，其中 2 篇在另一个标签页里已经删除，执行归档后提示“18 篇已归档，2 篇失败”，这 2 篇保持选中。
11. **订阅：**
    - RSS 和 Atom 各一个源都能导入，且每个最多导入 20 篇。
    - 反复手动刷新不产生重复文章。
    - 仅摘要的文章显示横幅；用插件保存原文后变为全文，标签和笔记还在。
    - 暂停后不再拉取；取消订阅后文章保留。
    - 删除一篇订阅文章后刷新，不会重新出现；永久删除后刷新，同样不会出现。
    - 让源站返回 500：管理页显示失败原因和上次成功时间，已保存的文章仍然可以阅读，其他源照常更新。
12. **插件保存回收站中的文章：** 删除一篇有笔记的文章，再用插件保存同一个 URL：文章被恢复，笔记、标签、状态都保留。

---

## 14. 留给实施者的决定

- 视觉细节：配色、间距、图标选择、动效，与现有设计系统保持一致即可。
- 组件和文件的具体拆分（第 10 节只是建议）。
- 日期展示格式和相对时间文案。
- `HtmlSanitizer` 白名单中 6.3 未列出的标签和属性。
- Testcontainers 使用的镜像获取方式（11 节）。
- 订阅预览中最近 5 条显示哪些字段。
- 批量请求在前端是否分块（ID 超过 200 时）。目前选择范围只限已加载的文章，正常不会超过上限，可以简单禁止。

## 15. 我做出的判断（供审阅）

以下几点没有在需求或对话中明确，是我按整体原则做的决定：

1. 恢复的文章保留删除前的归档状态，即使是通过插件重新保存触发的恢复（6.6）。
2. 阅读器主题作用于整个阅读页，而不只是正文段落（7.2）。
3. “删除已导入文章”跳过同时来自插件或其他订阅源的文章（6.7）。
4. 关键词搜索额外使用标题子串匹配兜底（4.2）。
5. 进度百分比按文本位置计算，卡片和进度条使用同一个值（5.1）。
6. 首次导入时，超出 20 篇的条目记为 `Dismissed`，以后不会再导入；只有之后新发布的条目才会进入文库（6.4）。
7. 高亮重新定位在阅读页打开时由客户端完成，而不是服务端（6.6）。
8. “从头阅读”的请求带上 `sessionId`，并返回新的 `progressUpdatedAt`（5.3）。
9. 不在任何块内的连续文本合并成匿名块（5.1）。
