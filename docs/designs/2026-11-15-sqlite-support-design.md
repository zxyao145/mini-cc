# SQLite 数据库支持设计

- 日期：2026-11-15
- 状态：已实现。[复核与验收记录](../superpowers/plans/2026-11-15-sqlite-support.md#复核与验收记录)
- 基于分支：`feature/tag-management`（依赖其中的 `TagWriter` 和标签 409 行为，`main` 上还没有）

---

## 1. 背景与目标

mini-cc 现在只能使用 PostgreSQL。PostgreSQL 必须单独安装并运行数据库服务，对个人自托管来说太重。

本次让后端**同一套代码同时支持 PostgreSQL 和 SQLite**，部署时通过配置二选一。选 SQLite 时不需要任何数据库服务：数据就是一个文件。

**成功标准：** 第 4 节的测试和验收全部通过。具体包括：
- PostgreSQL 部署的行为和数据库结构保持不变；
- SQLite 部署能用 `dotnet run` 或不含 db 容器的 docker compose 直接运行，所有现有功能都可用。

### 1.1 用户已定的事项

- 两种数据库**并存，部署时二选一**，不是用 SQLite 替换 PostgreSQL。
- SQLite 部署的形态：api、web、nginx、image-proxy 照旧由 docker compose 启动，只去掉 db 容器；数据库文件放在挂载卷里。
- SQLite 模式**不做中文分词**，搜索直接用 SQL `LIKE` 子串匹配。
- SQLite **只用于新部署**，不提供两种数据库之间的数据迁移。
- 使用场景是个人或家庭自托管：同时只有一两个人在用，写入并发很低。
- PostgreSQL 模式现有的行为保持不变，包括迁移、zhparser 中文搜索、订阅源并发刷新和 Testcontainers 测试。
- **Dockerfile 升级到 .NET 10 镜像，纳入本次范围**，作为计划的第一步单独提交（2026-11-15）。原因：提交 fa3f297 已经把项目改成 `net10.0`，但 Dockerfile 仍在用 .NET 9 镜像，所以两个 compose 文件目前都构建不出 API 镜像。

### 1.2 非目标

- PostgreSQL 与 SQLite 之间的数据导入、导出或迁移工具。
- SQLite 模式下的中文分词、FTS5 全文索引和相关度排序。
- 多个 API 实例共用同一个 SQLite 文件；把数据库文件放在 NFS、SMB 等网络文件系统上。
- 把默认数据库改成 SQLite。
- 运行时切换数据库。
- 在 SQLite 上用 CHECK 约束补齐字段长度限制（见 3.7）。
- 备份工具。文档只说明怎样手动备份。
- 修改前端或浏览器扩展。

### 1.3 反目标（即使能用，出现这些也算失败）

- PostgreSQL 部署因为这次改动需要新的迁移，或者表结构、索引、查询结果出现变化。3.8 中 API 镜像升级到 .NET 10 是有意为之，不算在内。
- SQLite 模式下，列表排序、游标分页或日期筛选结果错误，或者在内存里排序和过滤。例如把时间当字符串比较、加载整张表后再排序。
- SQLite 模式下出现并发问题：同一 URL 生成两篇文章、同一份过期进度被两个会话同时接受、已恢复的文章被清理任务删除，或者在正常的个人使用中（例如订阅源刷新的同时从扩展保存文章）报出 `database is locked`。
- SQLite 模式需要额外安装任何东西：原生扩展、`sqlite3` 命令行、单独的服务。
- `if (sqlite)` 分支散落在控制器和业务处理器里。两种数据库的差异应集中在数据层（见 3.5）。
- SQLite 的数据目录不存在或不可写时，API 能启动但运行到一半才报错，或者报错信息里看不出是哪个路径出了问题。

---

## 2. 现状：依赖 PostgreSQL 的地方（实现者需逐项处理）

后端只在 `Core/DependencyInjection.cs:26-30` 调用 `UseNpgsql`，没有任何 Npgsql 专属选项。连接串取环境变量 `MiniCC_Db`，没有就取 `ConnectionStrings:DefaultConnection`。

**注意编码：** `MiniCcDbContext.cs`、`DbSeeder.cs`、`Core/DependencyInjection.cs`、`ArticleConfiguration.cs`、`TagService.cs`、`TagRepository.cs`、`UserRepository.cs`、`ApiKeyRepository.cs`、`ApiKey.cs`、`UuidUtil.cs` 以及 2025 年的 5 个迁移文件是 **UTF-16 LE 带 BOM**。修改时保持原编码；用普通 `grep` 搜索会漏掉这些文件。

| 类别 | 位置（`src/backend/MiniCc.Api/` 下） | 现状 |
|---|---|---|
| 全文搜索 | `Article.cs:43-44`；`ArticleConfiguration.cs:75-80`；`ArticleListFilter.cs:19-24` | 领域实体上有 `NpgsqlTsVector SearchVector`。它是 `mixed_zh_en` 配置生成的列，覆盖 Title、Summary、ReadableContent、Author，并建了 GIN 索引。查询条件是 `Matches(PlainToTsQuery)`，或者对 Title 做 `ILike` 兜底；`\`、`%`、`_` 用反斜杠转义。只做筛选，不排序。`ReadableContent` 是净化后的 **HTML**，没有纯文本列。 |
| 搜索配置 | `DbSeeder.cs:23-79` | 启动时执行 `CREATE EXTENSION zhparser` 并创建 `mixed_zh_en` 配置，必须在迁移之前完成。 |
| 建库 | `DbSeeder.cs:107-125` | 用 `NpgsqlConnection` 查询 `pg_database`，不存在时执行 `CREATE DATABASE`。 |
| 行锁 | `ArticleRepository.cs:17-19`（`GetByIdForProgressUpdateAsync`）、`:41-50`（`GetByUrlAsync`，在原生 SQL 上再接 Include 和 `AsSplitQuery`）；`ArticlePurger.cs:16-17`；`FeedRefresher.cs:58`；`FeedImportService.cs:20`；`FeedService.cs:59`（按 `FeedUrlHash` 查询）、`:84` | 共 7 处 `SELECT … FOR UPDATE`，都在显式事务里执行。 |
| 应用锁 | `UnitOfWork.cs:41-42`（`LockArticleCreationAsync`，键 70453125）；`FeedService.cs:58`（键 70453126） | `pg_advisory_xact_lock`。前者的调用方是 `FeedImportService`、`FeedService`、`ArticlePurger`、`SaveLinkCommandHandler` 和 `SaveArticleContentCommandHandler`。 |
| 订阅源租约 | `FeedRefresher.cs:16`（`ClaimAsync`）、`:19-24`（`ClaimDueAsync`） | `ClaimAsync` 执行 `now() + interval '5 minutes'`。`ClaimDueAsync` 用 `FOR UPDATE SKIP LOCKED … LIMIT 4`，再 `RETURNING "Id"`。 |
| 时间类型 | `MiniCcDbContext.cs` 的 `ConfigureConventions` | 所有 `DateTimeOffset` 在保存前转成 UTC，存成 `timestamptz`。查询里大量对时间做比较和排序：列表排序与游标（`GetArticleListQueryHandler.cs:24-63`）、日期筛选（`ArticleListFilter.cs:31-32`）、回收站保留期（`TrashCleanup.cs:17`）、按 CreatedAt 排序（`FeedImportService.cs:56`、`ArticleRepository.cs:48`）、`GetHighlightsQueryHandler.cs:26` 和 `ApiKeyService.cs:35`。 |
| 微秒精度 | `UpdateProgressCommand.cs:79-82`；`SaveArticleContentCommandHandler.cs:88-93` | 进度版本号截断到 1 微秒，并用 `AddTicks(10)` 保证严格递增；客户端会把它原样回传。 |
| 查询里取当前时间 | `ApiKeyService.cs:35`（`ExpiredTime > DateTimeOffset.UtcNow`）；`FeedService.cs:118`（`ExecuteUpdate` 里 `SetProperty(DeletedAt, DateTimeOffset.UtcNow)`） | 由 EF 翻译成数据库端的当前时间。 |
| 列类型和索引 | `ArticleConfiguration.cs:20-24`；`MiniCcDbContext.cs:43,72` | `NormalizedUrl` 用 `HasMethod("hash")`，避开 B-tree 键长限制；`LastReadAt`/`PublishedAt` 索引用 `NULLS LAST`；`HasPostgresExtension("zhparser")`；`User.ReaderPreferences`（`string?`）是 `jsonb` 列，但没有任何 JSON 查询。 |
| 唯一冲突 | `TagWriter.cs:42-43` | 捕获 `PostgresException`（`UniqueViolation`，表 `Tags`，约束 `IX_Tags_Name`），返回已有标签的 ID。这是并发同名创建或改名时返回 409 的依据。 |
| 列表投影 | `ArticleListItemDto.cs:38` | `Feeds = a.FeedEntries.Select(e => new ArticleFeedDto(e.Feed.Id, e.Feed.Title)).Distinct().ToList()`。在 PostgreSQL 上可能被翻译成 LATERAL；SQLite 不支持 APPLY，可能无法翻译。 |
| 迁移 | `Migrations/` | 一套迁移（9 个）加一个快照，绑定 `MiniCcDbContext`，里面全是 PostgreSQL 类型和 Npgsql 注解。 |
| 测试 | `test/backend/MiniCc.Api.Test/` | Testcontainers 使用 `zhparser/zhparser:bookworm-16` 镜像，共有 9 个 "Postgres" 集合的测试类。很多测试用 `pg_stat_activity`、`pg_locks`、plpgsql 触发器、`TRUNCATE` 等 PostgreSQL 专属手段。`LibraryOptionsTests` 在没有连接串的情况下调用 `AddInfrastructureServices`，所以注册阶段不能因为连接串为空而失败。 |
| 部署 | `docker-compose.yaml`；`Dockerfile` | `db` 服务带健康检查，API `depends_on` 它。API 容器没有挂载卷，并以非 root 的 `$APP_UID` 运行。 |

可以直接复用的部分：所有主键都是 `Guid`；枚举都存成整数；没有 `decimal`、序列、并发令牌、数组列或 PostgreSQL 枚举；软删除查询过滤器、部分索引（`HasFilter`）、降序索引、`ExecuteUpdate/ExecuteDelete`、基本类型集合的 `Contains`，EF 9 的 SQLite 提供程序都支持；默认用户的种子数据和 URL 规范化回填是纯 C# 代码。

---

## 3. 方案

### 3.1 选择数据库

- 新增配置 `Database:Provider`，取值 `postgres`（缺省）或 `sqlite`，不区分大小写。环境变量写作 `Database__Provider`。其他取值让启动直接失败，错误信息写明允许的值。
- 连接串：
  - **postgres**：保持不变，先取 `MiniCC_Db`，再取 `ConnectionStrings:DefaultConnection`。
  - **sqlite**：先取 `MiniCC_Db`，再取新增的 `ConnectionStrings:Sqlite`。`appsettings.json` 中它的默认值是 `Data Source=data/mini_cc.db`。
  - 不能复用 `DefaultConnection`，因为它的默认值是 PostgreSQL 连接串。
- SQLite 的 `Data Source` 是相对路径时，相对于 API 的内容根目录解析：`dotnet run` 时是项目目录，容器里是 `/app`。启动时自动创建父目录。`src/backend/MiniCc.Api/data/` 加入 `.gitignore`。
- 注册方式：
  - postgres：保持现在的 `AddDbContext<MiniCcDbContext>(o => o.UseNpgsql(...))`。
  - sqlite：`AddDbContext<MiniCcDbContext, SqliteMiniCcDbContext>(o => o.UseSqlite(...))`。
  - 所有注入 `MiniCcDbContext` 的代码都不需要改动。
- 启动日志（Information 级别）记录当前使用的数据库；SQLite 模式同时记录数据库文件的绝对路径。

### 3.2 DbContext 与模型

- 新增 `SqliteMiniCcDbContext : MiniCcDbContext`，放在 `Shared/Data/`。基类增加一个 `protected` 构造函数，参数是非泛型的 `DbContextOptions` 和 `IEncryptionService`，供派生类调用。
- **PostgreSQL 专属的模型配置集中到一处**，作为基类的默认实现，SQLite 派生类覆盖它。涉及以下几项：
  - `HasPostgresExtension("zhparser")`；
  - `SearchVector` 的生成列和 GIN 索引；
  - `NormalizedUrl` 索引的 `hash` 方法；
  - `LastReadAt`/`PublishedAt` 索引的 `NULLS LAST`；
  - `ReaderPreferences` 的 `jsonb` 类型。
- SQLite 派生类的对应配置：
  - 忽略 `SearchVector`；
  - `NormalizedUrl` 用普通 B-tree 索引（SQLite 没有键长限制）；
  - `LastReadAt`/`PublishedAt` 索引保留原来的列和降序，但不设置空值排序；
  - `ReaderPreferences` 用 `TEXT`。
- **硬性约束：PostgreSQL 模型不能变。** 移动配置后，Npgsql 下的 `MiniCcDbContext` 与现有快照之间不能有待处理的模型变更，也不能生成新的 PostgreSQL 迁移。现有 `LibraryModelTests` 对 tsvector、`text` 和 `hash` 的断言要继续通过。注意：EF 9 发现模型有未生成迁移的变更时，`MigrateAsync` 会直接报错。
- **时间（SQLite）：** `DateTimeOffset` 存成 `INTEGER`，值是 UTC ticks。转换为 `v => v.UtcTicks` 和 `v => new DateTimeOffset(v, TimeSpan.Zero)`。
  - 这个转换要同时配置成属性约定和**默认类型映射**（`ConfigureConventions` 中的 `DefaultTypeMapping<DateTimeOffset>()`），这样原生 SQL 参数和 `SqlQuery` 也使用同样的表示。
  - 整数 ticks 按时间先后排序，EF 可以在 SQL 里翻译比较和排序。精度为 100 纳秒，满足 1 微秒版本号的要求。
  - PostgreSQL 继续使用现有的 UTC 转换。
  - 选这种存法的原因：EF 的 SQLite 提供程序不翻译 `DateTimeOffset` 的比较和排序，只支持相等比较；EF 自带的 `DateTimeOffsetToBinaryConverter` 会把精度截断到 0.1 毫秒，破坏进度版本号。
- **Guid（SQLite）：** 沿用 EF 的默认存法，即大写的标准文本格式。它的字符串顺序与 PostgreSQL `uuid` 的顺序一致，游标分页和 `TrashCleanup` 按 `Id` 分批的逻辑可以照用。不要改成 BLOB。
- 字符串在 SQLite 中都是 `TEXT`。`HasMaxLength` 在 SQLite 上不生效（见 3.7）。

### 3.3 迁移

- PostgreSQL：`Migrations/` 保持原样，不移动、不改名。
- SQLite：迁移放在 `Migrations/Sqlite/`，命名空间 `MiniCc.Api.Migrations.Sqlite`，绑定 `SqliteMiniCcDbContext`，有自己的快照。
  - 初始只有一个迁移，直接从当前模型生成。SQLite 只用于新部署，不需要重放 PostgreSQL 的历史迁移，也不需要 M1 中回填数据的 SQL。
  - EF 按迁移上的 `[DbContext]` 类型区分两套迁移，所以两套迁移可以放在同一个程序集里。
- 为 `SqliteMiniCcDbContext` 提供 `IDesignTimeDbContextFactory`，让 `dotnet ef` 不依赖当前的 `Database:Provider` 配置。加密服务用一个固定的设计时密钥即可，它不影响表结构。
- 以后修改模型时，要为两个上下文各生成一次迁移。有了两个上下文后，`dotnet ef` 必须指定 `--context`：

  ```bash
  dotnet ef migrations add <Name> --context MiniCcDbContext
  dotnet ef migrations add <Name> --context SqliteMiniCcDbContext --output-dir Migrations/Sqlite
  ```

  第 4 节有一个测试，检查两个上下文都没有待处理的模型变更，防止漏掉其中一套。迁移里如果用 `migrationBuilder.Sql` 写原生 SQL，要分别为两种数据库各写一份。SQLite 修改列时，EF 会自动重建表。

### 3.4 启动初始化（`DbSeeder`）

- PostgreSQL：流程不变。
- SQLite 的启动顺序：
  1. 确保数据库文件的父目录存在。目录无法创建或不可写时，启动失败，错误信息包含完整路径。
  2. 在事务之外执行 `PRAGMA journal_mode=WAL;`。这个设置会保存在数据库文件里，重复执行没有影响。
  3. `MigrateAsync()`。
  4. 运行现有的 URL 规范化回填和默认用户种子数据，代码与 PostgreSQL 共用。
- SQLite 模式跳过建库，也跳过 zhparser 和文本搜索配置。
- EF 9 在 SQLite 上用 `__EFMigrationsLock` 表防止迁移并发执行。如果进程在迁移过程中被杀掉，下次启动会一直等待这个锁。运维文档要写明处理方法：删除这张表里的行。

### 3.5 并发与加锁

**原理：** SQLite 同一时间只允许一个写事务。Microsoft.Data.Sqlite 的 `BeginTransaction()` 在不指定隔离级别时执行 `BEGIN IMMEDIATE`（已核对源码：Unspecified 会提升为 Serializable，非 deferred 模式下执行 `BEGIN IMMEDIATE`）。因此事务一开始就拿到写锁，直到提交才释放。这样，同一时间只有一个事务在“读取、检查、写入”，覆盖了现在 `FOR UPDATE` 加应用锁所提供的串行化保证，而且范围更大，覆盖整个数据库。

- **SQLite 上禁止使用 deferred 事务**（`BEGIN`）。WAL 模式下，deferred 事务先读后写时可能直接收到 `SQLITE_BUSY_SNAPSHOT`，而且无法重试。
- **WAL**：读不阻塞写，写也不阻塞读。订阅源导入期间，只读请求照常响应。
- **等待**：遇到 busy 或 locked 时，Microsoft.Data.Sqlite 会自动重试，直到命令超时，默认 30 秒。开始事务时同样会重试，超时由连接的 `DefaultTimeout` 控制。保留默认的 30 秒。

| PostgreSQL | SQLite |
|---|---|
| `pg_advisory_xact_lock(70453125)`（`IUnitOfWork.LockArticleCreationAsync`） | 什么也不做。所有调用方都已经在 `BEGIN IMMEDIATE` 事务里。 |
| `pg_advisory_xact_lock(70453126)`（`FeedService` 创建订阅源） | 什么也不做，理由同上。 |
| 7 处 `SELECT … FOR UPDATE` | 去掉 `FOR UPDATE` 的同一查询，过滤器、Include、`AsSplitQuery` 和跟踪方式保持不变。 |
| `ClaimDueAsync`：`FOR UPDATE SKIP LOCKED … LIMIT 4 … RETURNING` | 一条语句：`UPDATE "Feeds" SET … WHERE "Id" IN (SELECT "Id" … ORDER BY "NextFetchAt" LIMIT 4) RETURNING "Id"`。单条写语句本身是原子的，两个 worker 不会领到同一个订阅源。 |
| `ClaimAsync` 等处的 `now()`、`now() + interval '5 minutes'` | 改成由 C# 传入的当前时间和租约到期时间参数。 |

- **差异集中在数据层**：在 `Shared/Data/` 提供少量辅助方法，例如“加锁读取订阅源”“加锁读取文章”“获取事务级应用锁”，由现有调用点使用，业务服务里不出现数据库类型分支。
  - `IUnitOfWork.LockArticleCreationAsync`、`ArticleRepository.GetByIdForProgressUpdateAsync` 和 `GetByUrlAsync` 的签名保持不变。
  - PostgreSQL 分支执行的 SQL 与现在完全相同。
- 订阅源刷新保持最多 4 个并发。HTTP 抓取本来就在事务之外，所以多个抓取仍然可以同时进行，只有写库会排队。
- PostgreSQL 上，一条语句出错会让整个事务失效；SQLite 只回滚出错的那条语句。现有代码没有依赖在事务里捕获错误，不需要改。

### 3.6 搜索（SQLite）

- `q` 对 `Title`、`Summary`、`Author`、`ReadableContent` 分别做 `LIKE '%q%' ESCAPE '\'`，四个条件用 OR 连接。转义规则沿用现有代码。这四个字段就是 PostgreSQL 的 tsvector 和标题兜底合起来覆盖的范围。
- 大小写：英文字母不区分大小写，这是 SQLite 的默认行为；非 ASCII 字母区分大小写。不修改 `case_sensitive_like`。
- 只做筛选，不排序，与 PostgreSQL 相同。总数统计也应用同样的条件。
- 数据层根据当前数据库提供程序构造搜索条件。文章列表处理器通过同一个入口应用搜索和其他筛选，计数和分页查询共用筛选结果。
- **已知限制（写进文档）：**
  - `ReadableContent` 是 HTML。搜索 `div`、`class`、`href`、`img` 这类标签名或属性名，几乎会命中所有完整文章。
  - 正文里的 `&`、`<`、`>` 以实体形式存储，所以搜 `A&B` 搜不到正文里的“A&B”。
  - `LIKE '%…%'` 不能使用索引，每次搜索都要扫描全部正文，列表的数据查询和计数查询各扫一遍。个人规模的文库可以接受，本次不做优化。

### 3.7 其他差异

1. **标签唯一冲突：** `TagWriter` 的捕获条件增加 SQLite 的情况：`SqliteException` 的 `SqliteExtendedErrorCode == 2067`（`SQLITE_CONSTRAINT_UNIQUE`），并且消息中包含 `Tags.Name`。PostgreSQL 的判断保持不变。两种数据库下，并发同名创建都返回 409。
2. **查询里取当前时间：** 把 `ApiKeyService.cs:35` 和 `FeedService.cs:118` 中写在查询表达式里的 `DateTimeOffset.UtcNow`，提到查询之前的局部变量里，作为参数传入。其他查询表达式里如果也有，同样处理。
   - 原因：SQLite 上时间存成 ticks，由数据库端计算的“当前时间”无法与它比较。
   - 这个改动对两种数据库都生效。对 PostgreSQL 来说，只是从数据库时钟换成了应用时钟，语义相同。
3. **`ArticleListItemDto` 的 `Feeds` 投影：** 如果 SQLite 测试证明它因为需要 APPLY 而无法翻译，就改写成两种数据库都能翻译的形式，或者在物化之后再去重。两种数据库的结果必须相同，PostgreSQL 的列表测试继续通过。
4. **外键：** EF 的 SQLite 提供程序打开连接时会启用 `foreign_keys`。高亮、`ArticleTags`、`FeedTags` 的级联删除依赖这一点，要用测试确认。
5. **字段长度：** SQLite 不检查 `HasMaxLength`。接口层的校验器仍然是唯一的长度约束。超长数据在 PostgreSQL 上会报错，在 SQLite 上会被存下来。接受这个差异。
6. **字符串排序：** 标签按名称、订阅源按标题排序时，SQLite 按 Unicode 码点（BINARY）排序，PostgreSQL 按数据库区域设置排序。中文名称的顺序可能不同。接受这个差异。

### 3.8 部署

- **第一步（单独提交）：Dockerfile 升级到 .NET 10。**
  - 构建阶段改用 `mcr.microsoft.com/dotnet/sdk:10.0`，运行阶段改用 `aspnet:10.0`，与项目的 `net10.0` 一致。
  - .NET 10 镜像默认基于 Ubuntu 24.04，现有的阿里云 Debian bookworm apt 源写法不再适用。改成阿里云对应的 Ubuntu 源，继续安装 `curl` 和 `wget`。
  - 这一步同时影响 PostgreSQL 版镜像。完成后先确认默认 compose 能构建并正常启动，再继续 SQLite 的工作。
  - EF Core 相关包保持 9.0.7，本次不升级。
- 包：增加 `Microsoft.EntityFrameworkCore.Sqlite`，版本与 EF Core 一致（9.0.7）。它自带 Linux x64/arm64 和 macOS 的原生 SQLite 库，不需要额外安装。Npgsql 包保留。
- `Dockerfile`：在 `USER $APP_UID` 之前创建 `/app/data`，并把所有者设为 `$APP_UID`。空的命名卷第一次挂载时会继承镜像中这个目录的所有者，所以非 root 的应用用户可以写入。
- 新增独立的 `docker-compose.sqlite.yaml`：
  - 服务与 `docker-compose.yaml` 相同，只是没有 `db`。
  - API 设置 `Database__Provider: sqlite` 和 `MiniCC_Db: "Data Source=/app/data/mini_cc.db"`，`ImageProxy__SignatureKey` 与默认文件相同。
  - 挂载命名卷 `sqlite-data:/app/data`，去掉对 `db` 的 `depends_on`。
  - 启动命令：`podman compose -f docker-compose.sqlite.yaml up -d --build`。
- `docker-compose.yaml`、`docker-run.sh`、`docker-build.sh` 保持不变，默认仍然是 PostgreSQL。
- 两个 compose 文件使用相同的容器名，同一时间只能运行其中一套。切换前先 `down` 另一套。
- 数据库文件必须放在本地磁盘上（命名卷或本地目录），不能放在网络文件系统上：WAL 依赖共享内存和可靠的文件锁。
- 备份：先停止 API 再复制卷里的文件；或者在宿主机上用 `sqlite3 mini_cc.db ".backup <目标>"`。只写进文档。

### 3.9 文档

更新 `CLAUDE.md` 和 `README.md`：
- 数据库选择配置；
- SQLite 快速开始（`Database__Provider=sqlite dotnet run` 和 SQLite 版 compose 文件）；
- 两套迁移的工作流程；
- SQLite 测试不需要 Docker；
- 3.6、3.7 中的已知差异；
- 单实例限制、备份方法和迁移锁的处理方法。

把文中“PostgreSQL 是唯一数据库”的表述改成“二选一”。

---

## 4. 测试与验收

### 4.1 现有测试

- 后端 PostgreSQL 测试全部通过，数量不少于现在的 340 个。测试代码只允许为适应 3.7 第 2 项做最小改动。
- 前端 Vitest 测试数量不少于 96 个，全部通过，不改代码。
- 对 `Migrations/` 的改动只有新增的 `Migrations/Sqlite/`。

### 4.2 新增 SQLite 后端测试

这部分测试不依赖 Docker。`Sqlite` 测试集合共用临时目录里的数据库文件，可以从多个连接执行并发用例。集合夹具负责调用 `DbSeeder.InitAsync` 并在结束后清理目录。启动测试按场景另建临时路径。

- **启动和迁移：**
  - 目录为空时，启动会自动创建文件、应用迁移并生成默认用户；
  - 第二次启动不产生任何变化；
  - `journal_mode` 为 `wal`；
  - 两个上下文的 `HasPendingModelChanges()` 都返回 false。
- **配置：** 缺省时使用 postgres；`sqlite` 和 `SQLite` 都能识别；未知取值导致启动失败；相对路径能正确解析，目录会自动创建。
- **时间：**
  - 每一种排序（含回收站）在多页游标分页下的结果，包括空值排在最后；
  - `savedFrom`、`savedTo` 的边界；
  - 进度版本号截断到微秒后严格递增，客户端回传后能正确匹配；
  - 读回的值偏移量为 0，并且与写入的值相等；
  - 回收站保留期的截止时间；
  - API Key 过期判断。
- **搜索：**
  - 中文子串分别出现在标题、摘要、作者、正文中时都能搜到；
  - 搜 `Product%_Name` 能匹配字面值，不会匹配到 `ProductABName`；
  - 英文不区分大小写；
  - 与标签、状态筛选组合时，结果和总数都正确。
- **并发**（多个上下文同时操作同一个文件）：
  - 用 `BeginTransactionAsync` 开始的事务未提交时，另一个事务的开始会一直等到它提交，以此证明是 IMMEDIATE；
  - 同一 URL 被并发保存时只生成一篇文章；
  - 两个会话基于同一个过期版本保存进度时，只有一个成功，另一个收到 409；
  - 两个 worker 同时领取订阅源：领到的集合互不重叠，每次最多 4 个；租约过期的可以重新领取，已暂停的不会被领取；
  - 订阅源导入与扩展保存同一 URL 时只生成一篇文章；
  - 清理与恢复同时发生时，已恢复的文章不会被删除；
  - 订阅源刷新与 HTTP 写入同时进行，在默认超时内不出现 `database is locked`。
- **标签：** 并发同名创建时一个返回 201、一个返回 409；只有大小写不同的名称可以同时存在。
- **级联：** 永久删除文章时，它的高亮被一并删除，被其他文章共用的标签保留；删除标签时，对应的 `ArticleTags`、`FeedTags` 关联被删除。
- **阅读偏好：** 读写往返结果一致。
- **HTTP 冒烟**（WebApplicationFactory + SQLite）：
  - 登录；
  - 保存正文、保存链接；
  - 列表、搜索、筛选、详情；
  - 状态修改；
  - 进度保存、重置和 409 冲突；
  - 创建高亮、更新锚点；
  - 标签的增删改；
  - 批量操作（含移入回收站、恢复、永久删除）和清空回收站；
  - 订阅源的预览、订阅、刷新、取消订阅（沿用现有订阅源测试模拟 HTTP 的方式）；
  - 阅读偏好。

### 4.3 部署验收

- **SQLite 版 compose：**
  - 不存在 db 容器；
  - `http://localhost:5000` 可以登录；
  - 保存文章后执行 `down` 再 `up`，数据仍然存在；
  - 卷里的数据库文件属于应用用户。
- **手动运行：** 在干净的检出目录中执行 `Database__Provider=sqlite ImageProxy__SignatureKey=<任意密钥> dotnet run`，会生成 `data/mini_cc.db`，服务正常可用。
  - `ImageProxy:SignatureKey` 在启动时校验，并且没有默认值，所以快速开始文档的命令里要带上它。
  - 不要把默认密钥提交进仓库。
- **Playwright：** 针对 SQLite 版 compose 部署，运行自带测试数据的 spec：`reading-progress`、`reader-preferences`、`batch-trash`，都要通过。
  - 依赖 65 篇文章固定数据集的文库 spec，不要求在 SQLite 上运行。
  - `feeds` spec 也不要求运行：它需要的模拟订阅源服务器（`E2E_FEED_URL`）不在仓库里，而且容器里的 API 访问不到宿主机的 `127.0.0.1`。SQLite 上的订阅源功能由 4.2 的 HTTP 冒烟测试覆盖。
- **PostgreSQL 版 compose：** 用 .NET 10 镜像能构建并启动；已有 `pgdata` 卷中的数据照常可用；登录、文库列表和中文搜索与现在相同。

---

## 5. 留给实现者决定

- 3.5 中数据层辅助方法的具体形式和命名，以及原生 SQL 字符串在两种数据库之间怎样复用。
- 数据层搜索辅助方法的具体接口。
- SQLite 初始迁移的名称，以及设计时工厂的写法。
- 测试的文件组织方式，以及 SQLite 测试夹具的结构。
- 3.7 第 3 项是否需要改写，由测试结果决定；需要改写时具体怎么改。

---

## 6. 由 Claude 做出、请用户核对的决定

- 配置键使用 `Database:Provider`，取值 `postgres` 或 `sqlite`，缺省为 `postgres`。SQLite 的连接串依次取 `MiniCC_Db` 和 `ConnectionStrings:Sqlite`，默认值为 `Data Source=data/mini_cc.db`。
- 用派生上下文 `SqliteMiniCcDbContext` 加上单独的 `Migrations/Sqlite/` 维护两套迁移。不拆分出新项目，PostgreSQL 迁移不移动。
- SQLite 上 `DateTimeOffset` 存成 `INTEGER` 类型的 UTC ticks。
- SQLite 上用 `BEGIN IMMEDIATE` 加 WAL 代替行锁和应用锁，不另外模拟行锁；busy 超时保持 30 秒；订阅源刷新保持 4 个并发。
- `LIKE` 搜索范围是标题、摘要、作者和正文 HTML 四个字段，英文不区分大小写。
- 使用独立的 `docker-compose.sqlite.yaml`，默认部署仍然是 PostgreSQL；数据目录为容器内的 `/app/data`。
- 升级到 .NET 10 镜像后，apt 源继续使用阿里云，改成对应的 Ubuntu 源；EF Core 包不升级。
- 查询里的 `DateTimeOffset.UtcNow` 提到查询之前计算，这个改动对 PostgreSQL 也生效。
- 字段长度、字符串排序和非 ASCII 大小写方面的差异，作为已知限制接受，只写进文档，不额外处理。
- SQLite 上的 Playwright 验收不包括 `feeds` spec，也不为它重建模拟订阅源服务器；订阅源功能改由后端 HTTP 冒烟测试覆盖。
