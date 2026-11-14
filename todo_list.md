# TODO：文库管理、阅读体验与 RSS 订阅

详细设计见 `docs/superpowers/plans/2026-11-07-library-reading-rss.md`。按阶段顺序执行；P3 与 P4 可并行。每个阶段遵循：先写失败测试 → 实现 → 跑通 `dotnet test` / 前端类型检查与 lint → 验收 → 更新 `CLAUDE.md`。

---

## P0：统一文章状态模型

### 测试基础设施
- [x] 在 `test/backend/MiniCc.Api.Test` 引入 Testcontainers.PostgreSql，使用项目 db 镜像（带 zhparser + `mixed_zh_en` 配置）
- [x] 编写共享 `PostgresFixture`：启动容器、执行迁移、提供种子数据工具

### 领域与数据
- [x] `Article` 新增字段：`ReadingStatus`、`LastReadAt`、`ProgressBlockIndex`、`ProgressBlockRatio`、`ProgressTextOffset`、`ProgressPercent`、`ProgressUpdatedAt`、`ProgressSessionId`、`DeletedAt`、`PublishedAt`、`NormalizedUrl`、`ContentKind`、`SavedFromExtensionAt`
- [x] 新增 `ReadingStatus`、`ContentKind` 枚举
- [x] 新增 `UrlNormalizer`（去 fragment、`utm_*` 等追踪参数，host 小写，统一尾斜杠）+ 单测
- [x] 用幂等方法替换 toggle：`SetFavorite`、`SetArchived`、`MarkRead`、`MarkUnread`、`ResetProgress`、`UpdateProgress`、`MoveToTrash`、`Restore`
- [x] `ArticleTests`：状态相互独立；归档不改阅读状态；标记未读保留位置；从头阅读不改状态；删除/恢复后状态不变
- [x] `ArticleConfiguration`：加 `HasQueryFilter(a => a.DeletedAt == null)`；`SearchVector` 加入 `Summary`；补齐索引
- [x] 迁移 `LibraryStateModel`：回填 `ReadingStatus`（有 `ReadAt` 即 `Read`）、`NormalizedUrl`、`SavedFromExtensionAt = CreatedAt`
- [x] 迁移回填测试（Testcontainers）

### 应用层与 API
- [x] `UpdateArticleStateCommand` + handler + validator；新增 `PATCH /api/articles/{id}/state`
- [x] `DeleteArticleCommandHandler` 改为软删除
- [x] `RestoreArticleCommand` + `POST /api/articles/{id}/restore`
- [x] `SaveArticleContentCommandHandler` 改为按 `NormalizedUrl` 匹配；命中回收站文章时自动恢复并保留笔记和状态 + 测试
- [x] 修复 `POST /api/articles/{id}/read` 误调 `ToggleArchiveCommand` 的 bug（随后整体删除 toggle 端点）
- [x] 确认 `src/web-extension` 不调用 `favorite`/`archive`/`read` 端点
- [x] `ArticleDto` / 前端 `types/index.ts` 补充新字段

### 配置
- [x] 新增 `LibraryOptions`（`TrashRetentionDays=30`、`UndoWindowSeconds=10`），绑定 appsettings 的 `Library` 节，启动时校验范围（1–365 / 3–60）
- [x] `appsettings.json` 加入 `Library` 节；`docker-compose.yaml` 视需要补环境变量示例
- [x] `GET /api/settings/library` 返回 `{ trashRetentionDays, undoWindowSeconds }`

### 前端迁移
- [x] `lib/api.ts`：用 `updateState(id, patch)` 替换 `toggleFavorite`/`toggleArchive`
- [x] `ArticleCard`、阅读页改用新接口
- [x] 删除旧 toggle 端点、Command、handler 及其测试

---

## P1：文库视图、筛选与分页

### 后端
- [x] `ArticleListFilter`（view/q/status/tags/source/feedId/savedFrom/savedTo）谓词构造器 + 单测
- [x] 游标编解码 `(排序键, isNull, Id)`，支持 `saved_desc`、`saved_asc`、`read_desc`、`published_desc`
- [x] `ArticleListItemDto`（不含正文和高亮正文，含 `highlightCount`、来源摘要）+ 映射
- [x] `GetArticleListQuery` + handler：同一 `IQueryable` 先 `Count()` 再按游标取 `pageSize + 1`
- [x] `GET /api/articles` 返回 `{ items, total, nextCursor }`
- [x] 删除 `GetArticlesQuery`、`GetArticlesLightQuery`、`/light` 端点
- [x] `TagsController` 标签文章列表复用 `GetArticleListQuery`
- [x] Testcontainers 集成测试：收件箱 + 未读 + 标签 + 关键词组合；标签 AND；游标翻页无重复/遗漏；翻页途中删除一篇不漏项；65 篇种子翻完等于 `total`

### 前端
- [x] `lib/library-query.ts`：筛选条件 ↔ URL query 序列化 + 单测（引入 Vitest）
- [x] `Sidebar`：收件箱 / 全部文章 / 收藏 / 归档 / 订阅（P5 前隐藏）/ 回收站
- [x] `LibraryToolbar`：搜索（防抖 300ms）、阅读状态、标签多选、来源、保存日期（预设 + 自定义）、排序
- [x] `useArticleList` hook：条件变化重置并从第一页加载，`AbortController` 丢弃过期响应，加载更多
- [x] “共 N 篇，已加载 M 篇”计数
- [x] 列表缓存 + 滚动恢复（模块缓存 + `sessionStorage`，以 query 为键）
- [x] 空结果：条件 chip（可单独移除）+ “清除筛选”
- [x] 收件箱顶部“继续阅读”横条
- [x] 界面文案：`CreatedAt` 称“保存时间”，`IsFavorite` 称“收藏”

### 验收
- [x] 65 篇种子逐页加载到 65/65
- [x] 收藏、归档、标签、搜索组合下，总数与分页内容一致
- [x] 进入文章再返回，筛选条件与滚动位置保留

---

## P2：阅读状态与断点续读

### 后端
- [x] `UpdateProgressCommand` + `PUT /api/articles/{id}/progress`：`baseUpdatedAt` 早于服务端记录且会话不同时返回 409 和服务器进度
- [x] 首次有效进度时 `Unread → Reading`，并更新 `LastReadAt`；`Read` 状态下只更新位置
- [x] `POST /api/articles/{id}/progress/reset`（从头阅读）
- [x] handler 测试：正常保存、409 冲突、同会话连续保存、已读文章保存不改状态

### 前端
- [x] `lib/reading-position.ts`：块枚举、记录位置、按 `textOffset` / `blockIndex` 恢复 + 单测
- [x] `HighlightManager` 的偏移计算迁到共用工具
- [x] 测试守卫：高亮、标题 id、表格容器等 DOM 加工前后文本节点 `textContent` 序列不变
- [x] `useReadingProgress` hook：5 秒节流保存；`visibilitychange`/`pagehide`/路由离开时用 `keepalive` 补存；失败退避重试 + `sessionStorage` 暂存
- [x] 回到可见时拉取服务器进度；发现更新时提示“已在其他设备读到 xx%，跳转？”
- [x] 工具栏同步状态：“进度暂未同步”
- [x] 打开文章恢复位置（图片加载后再校正一次）
- [x] 文末“读完了？标记已读”提示条
- [x] 阅读页和列表卡片：标记已读/未读；阅读页：从头阅读
- [x] 卡片展示：未读标识 / “已读 42%” / 已读标识与读完时间

### 验收（Playwright）
- [x] 桌面读到某段 → 关闭 → 重开回到附近
- [x] 375px 视口和改字号后定位到同一段
- [x] 两个标签页交替阅读，旧页不覆盖新进度
- [x] 标记已读、归档、重新标记未读分别生效

---

## P3：阅读器设置、目录与进度条

### 后端
- [x] `User.ReaderPreferences`（jsonb）+ 迁移
- [x] `GET/PUT /api/account/reader-preferences` + 范围校验 + 测试

### 前端
- [x] `useReaderPreferences`：`localStorage` 首帧应用，服务端为准，500ms 防抖提交
- [x] 阅读器 CSS 变量：字号 16–28、行距 1.5–2.2、宽度 窄/标准/宽、系统/衬线字体；代码块保持等宽字体
- [x] 主题：跟随系统 / 浅色 / 深色 / 护眼底色（`data-reader-theme`）
- [x] 图片自适应、`pre` 横向滚动、表格滚动容器
- [x] 设置面板：桌面下拉、手机底部抽屉、恢复默认
- [x] 修改设置时保持当前段落（复用 `reading-position`）
- [x] 目录：取正文最高两级标题，只补 `id`，`IntersectionObserver` 标出当前章节，无标题不渲染；桌面右侧栏，手机抽屉
- [x] 进度条：只按 `.readable-content` 区间计算

### 验收
- [x] 中文长文、英文文章、带代码和表格的技术文章、375px 窄屏
- [x] 设置即时生效，刷新和换设备后保留
- [x] 改字号前后高亮位置不变、当前段落不跳走

---

## P4：批量管理与回收站

### 后端
- [x] `BatchArticleCommand` + `POST /api/articles/batch`：逐篇执行、逐篇捕获，返回 `{ succeeded, failed }`；`ids` 上限 200
- [x] 支持动作：markRead、markUnread、favorite、unfavorite、archive、unarchive、addTags、removeTags、trash、restore、purge（仅限回收站中的文章）
- [x] `DELETE /api/trash` 清空回收站
- [x] `TrashPurgeWorker`（每天一次，读取 `Library:TrashRetentionDays`）
- [x] 测试：明确动作不反转；部分失败；删除不删共用标签；purge 拒绝未删除文章；清理 worker 只删过期项

### 前端
- [x] 选择模式：桌面复选框、手机“选择”按钮，“已选 N 篇（范围：当前已加载的 M 篇）”
- [x] 批量操作栏 + 标签选择
- [x] 部分失败提示“18 篇已归档，2 篇失败 · 重试”，失败项保持选中
- [x] 撤销 toast（时长取自 `/api/settings/library`），单篇删除共用
- [x] 回收站视图：删除时间、剩余天数、恢复、永久删除、清空回收站（二次确认）

### 验收
- [x] 不同状态 + 高亮笔记 + 阅读位置的文章批量删除后，分别通过撤销和回收站恢复，全部还原，共用标签仍在

---

## P5：RSS / Atom 订阅

### 数据与解析
- [x] `Feed`、`FeedEntry` 实体 + 配置 + 迁移（完整键 text + 唯一索引 `(FeedId, EntryKeyHash)`，命中核对完整键）
- [x] 引入 `System.ServiceModel.Syndication`、`HtmlSanitizer`
- [x] `FeedParser`：RSS 2.0 / Atom；`content:encoded`；`EntryKey` 回退链；发布日期；仅摘要判定（< 500 字符 / 只有 description / “…/Read more” 结尾）
- [x] 内容清洗：HtmlSanitizer → 解析相对地址 → `ImageProxyService.RewriteImages`
- [x] 解析器单测（XML fixture）：RSS、Atom、仅摘要、guid 不是 URL、缺少 guid、相对图片地址
- [x] 订阅地址自动发现（`<link rel="alternate">`）

### 导入与拉取
- [x] `FeedImportService`：按条目去重；按 `NormalizedUrl` 关联已有文章（含回收站，不恢复）；更新不覆盖正文；首次只导入 20 条；附加默认标签
- [x] `TrashPurgeWorker` 永久删除时将相关 `FeedEntry` 置为 `Dismissed`
- [x] 插件补全文：命中 `SummaryOnly` 时替换正文，改为 `Full`，高亮按文本重新锚定，找不到的标记 `AnchorStatus.Orphaned`，重置阅读位置
- [x] `Highlight.AnchorStatus`（Ok/Pending/Orphaned）字段 + 迁移；高亮列表展示孤立高亮
- [x] `FeedRefreshWorker`：每分钟扫描，4 个并发，独立 scope，原子认领；30 分钟默认间隔，尊重 `ttl` / `sy:updatePeriod`；ETag / If-Modified-Since / 304；429/503 `Retry-After`；失败指数退避（最长 24 小时）
- [x] HttpClient：20 秒超时、10MB 上限、User-Agent；只允许 http/https，不限制内网
- [x] 导入服务测试：重复刷新不新增、删除后不重导、两个源同一 URL 只生成一篇、条目更新不覆盖
- [x] 拉取测试（HTTP stub + TCP 停滞响应体）：304、500、20 秒响应体超时、慢/失败源不阻塞其他源

### API
- [x] `POST /api/feeds/preview`
- [x] `POST /api/feeds`（同步导入最近 20 条）
- [x] `GET /api/feeds`（未读数、最近成功时间、状态、错误）
- [x] `PATCH /api/feeds/{id}`（改名、默认标签、暂停/恢复）
- [x] `POST /api/feeds/{id}/refresh`
- [x] `DELETE /api/feeds/{id}`（取消订阅，保留文章）
- [x] `POST /api/feeds/{id}/trash-articles`
- [x] 列表筛选接入 `view=feeds`、`source=feed`、`feedId`、`published_desc`

### 前端
- [x] 侧边栏“订阅”展开订阅源与未读数
- [x] `/pages/feeds` 管理页：两步添加（地址 → 预览确认名称和标签）、刷新、改名、改标签、暂停/恢复、取消订阅、错误与重试
- [x] 阅读页来源信息“来自 XXX · 发布于 …”
- [x] 仅摘要横幅 + 访问原文 + 插件补全文提示

### 验收
- [x] RSS 与 Atom 各一个源正常导入
- [x] 重复刷新不产生重复文章
- [x] 摘要与全文有明确区别
- [x] 暂停、取消订阅（保留文章）符合预期
- [x] 删除过的条目不被自动恢复
- [x] 源站不可用时仍可阅读已保存内容
- [x] 插件补全文后标签和笔记仍在

---

P5 阶段验收基线：后端真实 PostgreSQL 327/327、前端单测 89/89、真实 HTTP 生命周期及 EF 模型对齐通过；桌面 1280×800 / 移动 375×812 全套 E2E 42/42（原有 38 + P5 新增 4）通过，原有 65 篇活动文章保留。订阅导航边界、来源信息和重定位/孤立高亮已做截图复核。阶段复审与整体 diff 复审均已通过；最终修正后的 340/96/46 项验证见下方收尾记录。

## 收尾
- [x] 更新 `CLAUDE.md`：API 列表、实体、`Library` 配置节、后台 worker
- [x] 后端实际 .NET 9 / PostgreSQL `dotnet test` 340/340；前端 Vitest 96/96；`tsc --noEmit` 通过
- [x] 范围内 `eslint src e2e --ignore-pattern src/app/pages/account/page.tsx` 通过（仅已有 ArticleCard 图片警告）
- [x] 最终复审修正后的隔离生产构建 `next build --no-lint` 复验通过
- [x] 最终复审修正后的桌面 / 移动完整 E2E 46/46 通过（2.3 分钟），原有 65 篇活动文章完整
- [x] 最终运行时真实 HTTP 生命周期、Latin1/XML base 地址及去重、sessionId 64/65 边界和 EF 模型对齐复验通过

全量 lint 仍有 8 个基线错误，均位于未修改的 `src/frontend/src/app/pages/account/page.tsx`；未宣称全量 lint 通过，也未为此改动全局 lint 配置或无关代码。

- [x] 审阅整体 diff：规格与代码质量复审通过，全部必修问题已关闭
