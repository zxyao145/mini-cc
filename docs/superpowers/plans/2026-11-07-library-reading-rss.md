# 文库管理、阅读体验与 RSS 订阅实施计划

**目标：** 在个人自托管、网页端为主的前提下，为 MiniCC 增加统一的文章状态模型、文库视图/筛选/分页、阅读状态与断点续读、阅读器设置、批量管理与回收站、RSS/Atom 订阅。

**实施顺序：** P0 状态模型 → P1 文库筛选分页 → P2 阅读状态与续读 → P3 阅读器设置 → P4 批量与回收站 → P5 RSS。P0 是后面所有阶段的前置；P3、P4 之间无依赖，可并行。

**约束：** 保留现有文章、标签、高亮数据（迁移需回填）；修改阅读样式不得改写已保存正文、不得影响高亮偏移；沿用 Clean Architecture + MediatR + EF Core 的现有分层；图片继续经 `ImageProxyService` 签名。

---

## 现状与差距（基于代码）

| 位置 | 现状 | 对需求的影响 |
|---|---|---|
| `Article.cs` | 只有 `ReadAt`、`IsArchived`、`IsFavorite`；无阅读进度、无软删除、无来源、无发布日期 | 需要扩展状态模型 |
| `ArticleConfiguration.cs` | Highlights `OnDelete(Cascade)`；`DeleteArticleCommandHandler` 硬删除 | 无法撤销/回收站，改为软删除 |
| `ArticlesController` | `favorite`/`archive` 是 **toggle**；`POST {id}/read` 实际调用了 `ToggleArchiveCommand`（bug） | 批量操作需要幂等的显式动作，顺带修复 |
| `ArticleRepository.GetAllAsync` | 只支持 `search`，`Include` 全量正文和高亮，不返回总数 | 列表接口需重写：轻量 DTO + 总数 + 游标 |
| `SearchVector` | 生成列只含 `Title, ReadableContent, Author` | 需求要求搜索摘要，需加入 `Summary` |
| `SaveArticleContentCommandHandler` | URL 精确匹配，已存在就直接返回，不更新 | RSS 仅摘要文章需要能被插件补全文；URL 需规范化去重 |
| `Article` 无 `UserId` | 实际为单用户文库 | 已确认按单用户实现，不做多用户隔离；阅读偏好存 `User` 上 |
| `HighlightManager.tsx` | 高亮按 `.readable-content` 内文本节点的**全局字符偏移**定位 | 阅读位置可复用同一坐标系，天然不受字号/宽度影响 |
| 前端 | `page.tsx` 固定取第一页 20 篇，无筛选/分页/滚动恢复；无前端测试框架 | P1 重写文库页 |

**术语提醒：** 需求里“收藏”同时指“浏览器收藏（保存）”和“收藏视图（星标）”。界面上建议区分：`CreatedAt` 叫 **保存时间**，`IsFavorite` 叫 **收藏**。下文“保存日期”筛选对应 `CreatedAt`。

---

## P0：统一文章状态模型（后端基础）

### 数据模型变更（一次迁移）

`Article` 新增：

| 字段 | 类型 | 说明 |
|---|---|---|
| `ReadingStatus` | enum `Unread=0, Reading=1, Read=2` | 回填：`ReadAt != null` → `Read`，否则 `Unread` |
| `ReadAt` | 保留 | 语义改为“标记读完时间”，标记未读时清空 |
| `LastReadAt` | `DateTimeOffset?` | 最近一次产生有效阅读位置的时间，用于“最近阅读”排序和“继续阅读” |
| `ProgressBlockIndex` / `ProgressBlockRatio` / `ProgressTextOffset` / `ProgressPercent` | `int?` / `double?` / `int?` / `double?` | 断点位置（P2 详述） |
| `ProgressUpdatedAt` / `ProgressSessionId` | `DateTimeOffset?` / `string?` | 服务器时间戳，用于防止旧页面覆盖 |
| `DeletedAt` | `DateTimeOffset?` | 软删除；配置 `HasQueryFilter(a => a.DeletedAt == null)`，回收站查询用 `IgnoreQueryFilters()` |
| `PublishedAt` | `DateTimeOffset?` | 发布日期，与 `CreatedAt`（保存时间）分开 |
| `NormalizedUrl` | `string` | 去掉 fragment、`utm_*` 等追踪参数、host 小写、尾斜杠统一；建索引；迁移中回填 |
| `ContentKind` | enum `Full=0, SummaryOnly=1` | RSS 仅摘要时使用 |
| `SavedFromExtensionAt` | `DateTimeOffset?` | 来源筛选“浏览器收藏”；回填为 `CreatedAt` |

- 删除不再级联：软删除时正文、标签关联、高亮、阅读位置、收藏/归档状态全部原样保留，恢复只需清空 `DeletedAt`。
- `SearchVector` 生成列改为 `Title, Summary, ReadableContent, Author`（迁移会重建列和 GIN 索引）。
- 索引：`(DeletedAt, IsArchived, CreatedAt DESC, Id)`、`(DeletedAt, LastReadAt DESC)`、`(DeletedAt, PublishedAt DESC)`、`NormalizedUrl`。

### 领域方法（替换 toggle）

`Article` 上增加幂等方法：`SetFavorite(bool)`、`SetArchived(bool)`、`MarkRead()`、`MarkUnread()`（保留位置）、`ResetProgress()`、`UpdateProgress(...)`、`MoveToTrash()`、`Restore()`。约定：

- `MarkRead` 不改归档；`SetArchived` 不改阅读状态。
- `MarkUnread` 清空 `ReadAt`，状态回到 `Unread`，**不清除**阅读位置。
- `ResetProgress`（从头阅读）只清位置，不改状态。

### API

- `PATCH /api/articles/{id}/state`：`{ isFavorite?, isArchived?, readingStatus? }`，只修改传入的字段。
- 修复 `POST /api/articles/{id}/read` 的 bug；前端迁移到 PATCH 后删除 `favorite` / `archive` / `read` 三个 toggle 端点及其 Command（先确认插件没有调用它们）。
- `DELETE /api/articles/{id}` 改为移入回收站。

### 测试

- `ArticleTests`：各状态方法相互独立（已读+收藏+归档组合；归档不改阅读状态；标记未读保留位置；删除/恢复后状态不变）。
- 迁移回填测试：已有 `ReadAt` 的文章变成 `Read`。

---

## P1：文库视图、筛选与分页

### 视图定义（服务端唯一实现）

| view | 谓词（均隐含未删除，回收站除外） |
|---|---|
| `inbox` | `!IsArchived` |
| `all` | — |
| `favorites` | `IsFavorite` |
| `archive` | `IsArchived` |
| `feeds` | 存在 `FeedEntry` 关联（P5 前隐藏该视图） |
| `trash` | `DeletedAt != null` |

### 列表接口

`GET /api/articles`，参数：

```
view=inbox|all|favorites|archive|feeds|trash
q=关键词                     # SearchVector.Matches
status=unread,reading,read   # 多选取并集
tags=id1,id2                 # 默认 AND：每个 tagId 一个 EXISTS
source=extension|feed  feedId=...
savedFrom=  savedTo=         # 保存日期；“最近一周/一个月”由前端换算
sort=saved_desc|saved_asc|read_desc|published_desc
cursor=...  pageSize=20
```

返回 `{ items: ArticleListItemDto[], total: number, nextCursor: string | null }`。

- 所有条件先拼成同一个 `IQueryable`，`total = Count()`，再分页，保证总数和分页内容一致。
- **分页使用游标（keyset），不用 offset。** 在收件箱里归档或删除一篇后，offset 分页会跳过一篇；游标 `(排序键, Id)` 不受影响。`read_desc` / `published_desc` 的空值排在最后，游标里带上 “是否为空” 标记。
- `ArticleListItemDto` 不含 `ReadableContent` 和高亮正文。字段包括 `readingStatus`、`progressPercent`、`readAt`、`createdAt`、`publishedAt`、`contentKind`、`tags`、`highlightCount`、来源摘要（`savedFromExtension`、`feeds: [{id,title}]`）。
- 新建 `GetArticleListQuery` + `ArticleListFilter`（可单测的谓词构造器），替换 `GetArticlesQuery` / `GetArticlesLightQuery`；`TagsController` 的标签文章列表也改为复用它。

### 前端

- **筛选状态以 URL query 为唯一来源**（`/?view=inbox&status=unread&tags=...&q=...&sort=...`），侧边栏链接就是预设好的 query。这样返回列表、刷新、分享链接都能保留条件。
- 侧边栏：收件箱 / 全部文章 / 收藏 / 归档 / 订阅（P5 展开订阅源）/ 回收站，替换现在单一的 “Library” 入口。
- 顶部工具栏：搜索框（防抖 300ms）、阅读状态多选、标签多选、来源、保存日期（预设+自定义区间）、排序；`published_desc` 只在订阅视图出现。条件变化时清空列表，从第一页重新请求，并用 `AbortController` 丢弃过期响应。
- “加载更多”按钮 + 文案“共 126 篇，已加载 40 篇”。
- 滚动恢复：以 query string 为键，把 `{ items, nextCursor, total, scrollY }` 存在模块级缓存（并写一份到 `sessionStorage`）。从阅读页返回时先用缓存渲染、恢复 `scrollY`，再在后台刷新第一页。
- 无结果：以 chip 列出当前条件（可单独移除），并提供“清除筛选”。
- 收件箱顶部放“继续阅读”横条：即 `status=reading&sort=read_desc&pageSize=5`。

### 测试与验收

- 后端：筛选组合（收件箱 + 未读 + 标签 + 关键词）、标签 AND、游标连续翻页无重复/无遗漏、翻页过程中删除一篇不漏项。全文检索依赖 PostgreSQL + zhparser，EF InMemory 跑不了，所以引入 **Testcontainers** 并使用项目自带的 db 镜像。
- 验收：种子 65 篇，逐页加载到 65/65；组合收藏、归档、标签、搜索后，`total` 等于翻完所有页拿到的条数。

---

## P2：阅读状态与断点续读

### 位置模型

复用高亮的坐标系：`.readable-content` 内文本节点的全局字符偏移。它只和正文内容有关，与字号、宽度、设备、图片加载都无关。

- **记录：** 取视口顶部往下约 20% 处的块级元素（`p, h1–h6, li, pre, blockquote, figure, table`），保存它的 `blockIndex`、块内滚动比例 `blockRatio`、块起点的 `textOffset`，以及用于展示的 `percent`。
- **恢复：** 先按 `textOffset` 找到块（`blockIndex` 作为校验和兜底），`scrollIntoView` 后再按 `blockRatio` 微调。图片尚未加载时，等 `load` 后再校正一次（最多约 2 秒）。
- **硬约束（写成工具函数测试）：** 阅读页对正文做的任何 DOM 加工（高亮 `<mark>`、标题 id、表格滚动容器）都不得改变文本节点序列的 `textContent`。

工具函数放在 `src/frontend/src/lib/reading-position.ts`，`HighlightManager` 的偏移计算也迁到这里共用。

### 状态流转

- 新文章 → `Unread`。
- 首次产生有效位置（滚动越过首屏且停留超过约 3 秒）→ `Reading`，并更新 `LastReadAt`。
- `Read` 只在两种情况下产生：用户手动标记，或接受文末提示（最后一个块进入视口时，底部出现“读完了？标记已读”）。已读文章继续保存位置，但状态不变。
- 列表卡片和阅读页工具栏都提供“标记已读 / 标记未读”和“从头阅读”。

### 保存与并发

- `PUT /api/articles/{id}/progress`：`{ blockIndex, blockRatio, textOffset, percent, sessionId, baseUpdatedAt }`。
- 前端：位置变化后最多每 5 秒保存一次；`visibilitychange → hidden`、`pagehide`、路由离开时用 `fetch(..., { keepalive: true })` 补存。不用 `sendBeacon`，因为开发环境 4000→5001 是跨域 JSON 请求，需要 preflight。
- **防止旧页面覆盖：** 服务端只比较自己的时间戳（不受设备时钟偏差影响）。如果 `baseUpdatedAt` 早于当前 `ProgressUpdatedAt`，且 `ProgressSessionId` 不是本会话，返回 `409` 和服务器上的进度。页面在 `visibilitychange → visible` 时主动拉取一次进度；发现更新的记录时，提示“已在其他设备读到 xx%，跳转？”。后台标签页不做定时保存。
- 失败时在工具栏显示“进度暂未同步”，指数退避重试，未同步的位置暂存在 `sessionStorage`。只有收到 2xx 后才显示已同步。

### 验收

桌面读到某段后关闭并重新打开，能回到附近；用手机视口或改字号后重开，能定位到同一段；在两个标签页交替阅读，旧标签页不覆盖新进度；标记已读、归档、重新标记未读分别生效，互不影响。

---

## P3：阅读器设置、目录与进度条

### 偏好存储

- `User` 新增 `ReaderPreferences`（jsonb）：`{ fontSize: 18, lineHeight: 1.8, width: "standard", fontFamily: "system", theme: "system" }`。服务端校验取值范围（16–28、1.5–2.2、枚举值）。
- `GET/PUT /api/account/reader-preferences`。
- 前端：`useReaderPreferences` hook。同时写一份到 `localStorage`，首帧直接应用，避免闪烁；服务器值为准，登录或聚焦时同步。修改时立即生效，防抖约 500ms 后提交。

### 渲染

- 只作用于阅读器容器的 CSS 变量：`--reader-font-size`、`--reader-line-height`、`--reader-max-width`（窄 640 / 标准 760 / 宽 920）、`--reader-font`。`pre, code` 固定使用等宽字体变量。主题 `light / dark / sepia / system` 通过容器上的 `data-reader-theme` 实现。
- `img { max-width: 100% }`；`pre` 横向滚动；表格包一层 `overflow-x: auto` 容器（只加元素，不改文本）。
- **切换样式时保持当前段落：** 修改前调用 P2 的“记录”，下一帧布局完成后调用“恢复”。
- 设置面板：桌面端是工具栏下拉小面板，手机端（<640px）是底部抽屉；包含“恢复默认”。

### 目录与进度条

- 目录：从正文提取 h2/h3。Readability 输出的层级不稳定，所以实际取正文中出现的**最高两级**标题。只在 DOM 上补 `id` 属性。用 `IntersectionObserver` 标出当前章节；没有标题时不渲染目录。桌面端放右侧栏，手机端放在工具栏抽屉里。
- 进度条：只按 `.readable-content` 元素自身的滚动区间计算，不包括页头、标签面板和高亮列表。

### 验收

用中文长文、英文文章、带代码和表格的技术文章、375px 窄屏各验证一遍；所有设置即时生效，刷新和换设备登录后保留；改字号前后高亮位置不变、当前段落不跳走。

---

## P4：批量管理与回收站

### API

`POST /api/articles/batch`：

```json
{ "ids": ["..."], "action": "markRead|markUnread|favorite|unfavorite|archive|unarchive|addTags|removeTags|trash|restore|purge", "tagIds": ["..."] }
```

返回 `{ succeeded: string[], failed: [{ id, reason }] }`。

- 每篇单独执行、单独捕获异常，所以部分失败不会回滚成功项。`ids` 上限 200。
- 所有动作都是明确的目标状态，复用 P0 的幂等领域方法，不做逐篇反转。
- `addTags` / `removeTags` 只修改指定标签；任何删除操作都不删除 `Tag` 本身。
- `purge`（永久删除）只接受已在回收站中的文章。另提供 `DELETE /api/trash` 清空回收站。

### 回收站自动清理

`TrashPurgeWorker`（`BackgroundService`，每天运行一次）硬删除 `DeletedAt` 超过保留期的文章，高亮级联删除，保留标签。

### 配置（appsettings）

两项都只做服务端配置，不做用户设置。用 `LibraryOptions` 通过 `IOptions` 绑定，与现有 `ImageProxy` 配置节的写法一致；也可以用环境变量 `Library__TrashRetentionDays` 覆盖：

```json
"Library": {
    "TrashRetentionDays": 30,
    "UndoWindowSeconds": 10
}
```

- `TrashRetentionDays`：`TrashPurgeWorker` 和回收站“剩余天数”都使用这个值。
- `UndoWindowSeconds`：撤销提示在前端显示，所以新增 `GET /api/settings/library` 返回 `{ trashRetentionDays, undoWindowSeconds }`，前端启动时读取一次。
- 启动时校验取值范围：保留期 1–365 天，撤销窗口 3–60 秒。

### 前端

- 选择模式：桌面端卡片悬停显示复选框；手机端在工具栏放“选择”按钮。进入后显示“已选 N 篇（范围：当前已加载的 M 篇）”和操作栏。
- 结果提示：“18 篇已归档，2 篇失败 · 重试”，失败项保持选中。
- 删除后显示 toast “已删除 5 篇文章 · 撤销”，撤销即调用 `restore`。显示时长取自服务端配置（默认 10 秒）。阅读页单篇删除也使用这个 toast。
- 回收站视图：显示删除时间和剩余天数，支持单篇/多选恢复和永久删除；“清空回收站”需要二次确认。

### 验收

几篇文章分别设置不同的收藏/归档/阅读状态，并添加高亮笔记和阅读位置。批量删除后，分别通过“撤销”和回收站恢复，检查正文、标签、高亮、笔记、位置、状态全部还原；共用标签仍在。

---

## P5：RSS / Atom 订阅

### 数据模型

- `Feed`：`Id, FeedUrl, SiteUrl, OriginalTitle, Title, Format(Rss/Atom), DefaultTagIds, Status(Active/Paused/Unsubscribed), LastFetchedAt, LastSuccessAt, LastError, ConsecutiveFailures, ETag, LastModified, NextFetchAt, MinIntervalMinutes, CreatedAt`。
  - 取消订阅只把状态置为 `Unsubscribed`，不删除行，这样已导入文章的来源名称仍可显示。
- `FeedEntry`：`FeedId, EntryKey, EntryUrl, ArticleId?, ContentHash, FirstSeenAt, LastSeenAt, Dismissed`；完整 `EntryKey` 使用 text；唯一索引 `(FeedId, EntryKeyHash)`，SHA-256 命中后核对完整键（详见设计 6.1 的存储边界修正）。
  - `EntryKey` 依次取 Atom `id`、RSS `guid`、链接，最后退回 `hash(title + published)`。
- 多个订阅源的条目可以指向同一个 `Article`，阅读状态自然共享。

### 解析与清洗

- 使用 `System.ServiceModel.Syndication`（官方包，支持 RSS 2.0 / Atom 1.0）。RSS 的 `content:encoded` 需要从 `ElementExtensions` 读取。
- 正文优先级：`content:encoded` / Atom `content` > `description` / `summary`。
- 判定仅摘要：只有 description，或纯文本少于约 500 字符，或以“…/Read more”结尾 → `SummaryOnly`。
- **服务端清洗：** 订阅内容不经过插件的 DOMPurify，所以引入 `HtmlSanitizer`（Ganss.Xss），按 `xml:base` / 条目链接解析相对地址，再交给 `ImageProxyService.RewriteImages`。
- 发布日期写入 `PublishedAt`，导入时间写入 `CreatedAt`。

### 导入规则（`FeedImportService`）

对每个条目：

1. 已有 `(FeedId, EntryKey)`：更新 `LastSeenAt`。如果 `Dismissed`，或关联文章在回收站中，跳过。内容哈希变化时只记录，**不覆盖**正文、高亮和笔记。
2. 新条目：按 `NormalizedUrl` 查找已有文章（包括回收站中的）。找到就只建立关联；找到的是回收站文章时不恢复。
3. 都没有：新建 `Article`，并附加订阅的默认标签。
4. 首次订阅时只导入最新的 20 条；之后每次刷新导入所有新条目。

回收站清理永久删除文章时，把相关 `FeedEntry` 置为 `Dismissed = true, ArticleId = null`，保证删除过的条目不会被重新导入。

**插件保存回收站中的 URL：** 视为用户明确想要这篇文章，自动恢复原文章（清空 `DeletedAt`），保留标签、高亮、笔记和状态，不新建文章。这条在 P0 软删除上线时一并实现，测试覆盖“删除 → 插件再次保存 → 恢复且笔记仍在”。

**插件补全文：** `SaveArticleContentCommandHandler` 改为按 `NormalizedUrl` 匹配。命中 `SummaryOnly` 文章时替换正文，改为 `Full`，保留标签、状态和笔记。正文变了，高亮偏移会失效，所以按高亮的 `Text` 在新正文中搜索最接近原偏移的位置，重新锚定；服务端先标记 `AnchorStatus.Pending`，客户端重新定位并确认；找不到的高亮标记为 `AnchorStatus.Orphaned`，只在高亮列表中显示。阅读位置重置。命中 `Full` 文章时保持现有行为，不覆盖。

### 后台拉取（`FeedRefreshWorker : BackgroundService`）

- 每分钟扫描一次 `Status=Active && NextFetchAt <= now` 的订阅源。最多 4 个并发，每个源使用独立的 DI scope 和 try/catch，一个源失败不影响其他源。
- 使用 `IHttpClientFactory` 命名客户端：超时 20 秒，响应上限 10MB，明确的 User-Agent，条件请求带 `ETag` / `If-Modified-Since`，处理 304；遇到 429/503 遵守 `Retry-After`。
- 默认间隔 30 分钟，同时尊重 RSS `<ttl>` / `sy:updatePeriod`。失败后指数退避，最长 24 小时，并记录 `LastError`。
- 认领订阅源时用 `UPDATE ... SET NextFetchAt = ... WHERE NextFetchAt <= now` 原子抢占，以后多实例部署也不会重复拉取。
- 地址限制：只允许 http/https。已确认**不限制内网和回环地址**，自托管用户可以直接订阅局域网服务。

### API

```
POST   /api/feeds/preview        { url } → { title, siteUrl, format, items[5] }
                                 # 粘贴网页地址时，解析 <link rel="alternate"> 自动发现订阅地址
POST   /api/feeds                { url, title, tagIds } → 创建订阅，同步导入最近 20 条
GET    /api/feeds                # 名称、未读数、最近成功更新时间、状态、错误
PATCH  /api/feeds/{id}           { title?, tagIds?, paused? }
POST   /api/feeds/{id}/refresh
DELETE /api/feeds/{id}           # 取消订阅，保留已导入文章
POST   /api/feeds/{id}/trash-articles   # 独立动作：把该源导入的文章移入回收站
```

### 前端

- 侧边栏“订阅”可展开，列出各订阅源和未读数，点击即 `view=feeds&feedId=`。
- `/pages/feeds` 管理页，分两步添加订阅：输入地址 → 预览确认名称和标签。每个源可以立即刷新、改名、改默认标签、暂停/恢复、取消订阅；失败的源显示原因、上次成功时间和重试按钮。
- 阅读页显示来源信息：“来自 XXX · 发布于 …”。仅摘要文章显示“仅摘要”横幅、“访问原文”按钮，并提示可用插件保存全文。

### 测试与验收

- 用 XML fixture 写解析器单测：RSS 2.0、Atom、仅摘要、guid 不是 URL、缺少 guid、相对图片地址。
- 导入服务测试：重复刷新不新增文章、删除后不重导、两个源指向同一 URL 只生成一篇、条目更新不覆盖正文。
- 用 Testcontainers + 本地 HTTP stub 模拟 304、500、超时，确认其他源照常刷新，已保存文章照常可读。
- 端到端：RSS 和 Atom 各一个真实源，覆盖暂停、取消订阅（文章保留）、插件补全文后笔记仍在。

---

## 横切事项

- **前端测试：** 目前没有。加入 Vitest，测试 `reading-position`、筛选与 URL 的序列化等纯函数；用 Playwright 跑上面各阶段的验收场景（桌面视口和 375px 视口）。
- **文档：** 每个阶段结束后更新 `CLAUDE.md` 的 API 列表和实体说明。
- **性能：** 列表 DTO 不含正文；65 篇以上的数据量用游标分页 + 索引足够，暂不需要缓存层。

## 已确认的决策（2026-11-07）

1. **单用户：** 不做多用户隔离，文章不加 `UserId`。
2. **插件保存回收站中的 URL：** 自动恢复原文章，保留笔记和状态，不新建。
3. **仅摘要判定阈值：** 纯文本少于约 500 字符，或只有 description，或以“…/Read more”结尾。
4. **内网订阅地址：** 不限制，只要求 http/https。
5. **撤销窗口和保留期：** 只做服务端配置，在 appsettings 的 `Library` 节中设置（默认 10 秒 / 30 天），前端通过 `GET /api/settings/library` 读取。
