# SQLite 数据库支持 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 后端用同一套代码同时支持 PostgreSQL 和 SQLite，部署时通过 `Database:Provider` 选择；SQLite 部署不需要数据库服务。

**Architecture:**
- **上下文：** 新增派生上下文 `SqliteMiniCcDbContext`，迁移单独放在 `Migrations/Sqlite/`。PostgreSQL 专属的模型配置收拢到基类的一个虚方法里，由 SQLite 版覆盖。
- **时间：** SQLite 上 `DateTimeOffset` 存成整数 UTC ticks。
- **加锁：** SQLite 事务一开始就执行 `BEGIN IMMEDIATE`，拿到整库写锁，代替 `FOR UPDATE` 和 advisory lock。两种数据库的差异集中在 `Shared/Data/DatabaseLocks.cs`、`Shared/Data/DatabaseErrors.cs` 和 `Core/FeedNs/Infrastructure/FeedLeases.cs`。
- **搜索：** SQLite 上改用四个字段的 `LIKE`。
- **部署：** 新增 `docker-compose.sqlite.yaml`，里面没有 db 服务，数据放在挂载到 `/app/data` 的命名卷里。

**Tech Stack:** ASP.NET Core（net10.0）、EF Core 9.0.7、Npgsql、Microsoft.EntityFrameworkCore.Sqlite 9.0.7、xUnit + FluentAssertions + Testcontainers、Podman compose、Playwright。

**Spec:** `docs/designs/2026-11-15-sqlite-support-design.md`（基于分支 `feature/tag-management`）

## Global Constraints

- **文件编码：** 以下文件是 UTF-16 LE 带 BOM：`Shared/Data/MiniCcDbContext.cs`、`Shared/Data/DbSeeder.cs`、`Core/DependencyInjection.cs`、`Core/ArticleNs/Infrastructure/EntityConfigurations/ArticleConfiguration.cs`。修改前后，`file <path>` 的输出必须相同；其他文件保持原有编码和换行（多数是 CRLF）。用 `grep` 搜索这些文件时，先用 `iconv -f UTF-16 -t UTF-8` 转码。
- **PostgreSQL 不变：**
  - `Migrations/` 根目录下不新增或修改任何文件；
  - Npgsql 下的 `MiniCcDbContext` 执行 `HasPendingModelChanges()` 返回 false；
  - PostgreSQL 分支执行的 SQL 文本与现在完全相同，包括 7 处 `… FOR UPDATE`、`SELECT pg_advisory_xact_lock(70453125)`、`SELECT pg_advisory_xact_lock(70453126)` 和两条租约 SQL。
- **配置：**
  - `Database:Provider` 取值 `postgres`（缺省或空字符串时）或 `sqlite`，不区分大小写；其他取值抛出 `InvalidOperationException`，消息为 `Database:Provider must be 'postgres' or 'sqlite'.`。
  - 连接串：postgres 依次取 `MiniCC_Db`、`ConnectionStrings:DefaultConnection`；sqlite 依次取 `MiniCC_Db`、`ConnectionStrings:Sqlite`，后者在 `appsettings.json` 中的默认值为 `Data Source=data/mini_cc.db`。
  - SQLite 的相对路径相对于内容根目录解析。
- **包：** 只新增 `Microsoft.EntityFrameworkCore.Sqlite` 9.0.7，其他 EF 包的版本不变。
- **SQLite 事务：** 只用默认的 `BeginTransactionAsync()`（Microsoft.Data.Sqlite 会执行 `BEGIN IMMEDIATE`），不得使用 deferred 事务。busy 超时保持默认的 30 秒。
- **SQLite 原生 SQL 参数：**
  - Microsoft.Data.Sqlite 把 `DateTimeOffset` 参数绑定成 TEXT，所以原生 SQL 中的时间值一律传 `long` 类型的 UTC ticks；
  - `Guid` 参数会绑定成大写 TEXT，与 EF 的存储格式一致，可以直接传。
- **容器：** 数据目录 `/app/data`，连接串 `Data Source=/app/data/mini_cc.db`，命名卷 `sqlite-data`。
- **测试命令：**
  - 后端全量：`dotnet test test/backend/MiniCc.Api.Test`。PostgreSQL 测试需要 Podman：

    ```bash
    export DOCKER_HOST="unix://$(podman machine inspect --format '{{.ConnectionInfo.PodmanSocket.Path}}')"
    export TESTCONTAINERS_HOST_OVERRIDE=localhost TESTCONTAINERS_RYUK_DISABLED=true
    ```

  - 只跑本计划新增的测试：`dotnet test test/backend/MiniCc.Api.Test --filter "FullyQualifiedName~Sqlite|FullyQualifiedName~Database"`。新增的测试类名都包含 `Sqlite`，或者以 `Database` 开头。
- **对外固定的文字：**
  - 数据库文件无法打开：`无法打开 SQLite 数据库文件：{完整路径}`；
  - 缺少 SQLite 连接串：`SQLite requires MiniCC_Db or ConnectionStrings:Sqlite.`

## Review Focus

1. **带时区的日期筛选和边界：** `savedFrom=2026-11-15T00:00:00+08:00` 与 `2026-11-14T16:00:00Z` 是同一时刻，`savedFrom` 包含这个时刻，`savedTo` 不包含。由 Task 5 的 `List_DateRangeUsesInstantsAcrossOffsets` 覆盖。
2. **进度版本号经 JSON 往返：** 另一个会话拿服务器返回的 `progressUpdatedAt` 原样作为 base 保存，应该成功，不能误判成 409。由 Task 6 的 `ArticleLifecycle_WorksOnSqlite` 覆盖。
3. **回收站保留期边界：** `DeletedAt` 正好等于截止时间时会被清理，比截止时间晚 1 tick 时保留。由 Task 4 的 `TrashCleanup_PurgesAtExactCutoff` 覆盖。
4. **搜索词中的反斜杠、`%`、`_`：** 都按字面匹配，例如 `C:\temp` 只命中标题里含 `C:\temp` 的文章。由 Task 5 的 `Search_TreatsWildcardsAndBackslashLiterallyAndIgnoresAsciiCase` 覆盖。
5. **内容根目录与工作目录不同：** SQLite 文件应该落在内容根目录下的 `data/`，而不是进程的当前目录。由 Task 3 的 `AddInfrastructureServices_Sqlite_RegistersSqliteContextUnderContentRoot` 覆盖。

---

## File Structure

| 文件 | 职责 |
|---|---|
| Modify `src/backend/MiniCc.Api/Dockerfile` | 改用 .NET 10 镜像和阿里云 Ubuntu 源（Task 1）；创建 `/app/data`（Task 7）。 |
| Modify `Shared/Data/MiniCcDbContext.cs`、`ArticleConfiguration.cs` | 中立的模型配置；PostgreSQL 专属配置收拢到 `ConfigureProviderModel`。 |
| Create `Shared/Data/SqliteMiniCcDbContext.cs` | SQLite 上下文和 `UtcTicksConverter`。 |
| Create `Shared/Data/SqliteDesignTimeDbContextFactory.cs` | 供 `dotnet ef` 使用的设计时工厂。 |
| Create `Migrations/Sqlite/*` | SQLite 迁移和快照（由工具生成）。 |
| Create `Shared/Data/DatabaseConfiguration.cs` | `DatabaseProvider`，读取数据库类型，解析 SQLite 路径。 |
| Modify `Core/DependencyInjection.cs`、`Program.cs`、`appsettings.json`、`.gitignore` | 按数据库类型注册上下文；传入内容根目录；记录启动日志。 |
| Modify `Shared/Data/DbSeeder.cs` | SQLite 启动初始化：创建目录、开启 WAL。 |
| Create `Shared/Data/DatabaseLocks.cs` | `FromSqlForUpdate` 和 `AcquireTransactionLockAsync`。 |
| Create `Core/FeedNs/Infrastructure/FeedLeases.cs` | 两种数据库的租约 SQL。 |
| Create `Shared/Data/DatabaseErrors.cs` | 识别两种数据库的唯一约束冲突。 |
| Modify `ArticleRepository.cs`、`ArticlePurger.cs`、`UnitOfWork.cs`、`FeedRefresher.cs`、`FeedImportService.cs`、`FeedService.cs` | 改用上述辅助方法。 |
| Modify `ArticleListFilter.cs`、`GetArticleListQueryHandler.cs` | SQLite 的 `LIKE` 搜索。 |
| Modify `ApiKeyService.cs`、`FeedService.cs:118` | 把查询里的当前时间提到查询之前计算。 |
| Modify `TagWriter.cs` | 改用 `DatabaseErrors`。 |
| Create `docker-compose.sqlite.yaml`；Modify `README.md`、`CLAUDE.md` | SQLite 部署和文档。 |
| Tests in `test/backend/MiniCc.Api.Test/Infrastructure/` | `DatabaseModelTests`、`DatabaseConfigurationTests`、`SqliteFixture`、`SqliteApi`、`SqliteStartupTests`、`SqliteConcurrencyTests`、`SqliteLibraryTests`、`SqliteApiTests`。 |

下文的路径如果没有写前缀，都相对于 `src/backend/MiniCc.Api/`。

---

### Task 1: Dockerfile 升级到 .NET 10

**Files:**
- Modify: `src/backend/MiniCc.Api/Dockerfile:4-17,28`

**Interfaces:**
- Produces：能构建的 API 镜像。Task 7 依赖它。

- [ ] **Step 1: 确认当前无法构建**

  Run: `podman build -f src/backend/MiniCc.Api/Dockerfile -t minicc-api:net10-check .`
  Expected: 失败，输出包含 `NETSDK1045`。

- [ ] **Step 2: 修改 Dockerfile**

  - 第 4 行改为 `FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base`；
  - 第 28 行改为 `FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build`；
  - 第 6–17 行的 Debian 源重写块，整体替换为：

  ```dockerfile
  # 使用阿里云 Ubuntu 源
  RUN sed -i -e 's|http://archive.ubuntu.com|http://mirrors.aliyun.com|g' \
             -e 's|http://security.ubuntu.com|http://mirrors.aliyun.com|g' \
             -e 's|http://ports.ubuntu.com|http://mirrors.aliyun.com|g' \
          /etc/apt/sources.list.d/ubuntu.sources && \
      apt-get update && apt-get install -y curl wget && apt-get clean
  ```

- [ ] **Step 3: 重新构建并检查镜像**

  Run: `podman build -f src/backend/MiniCc.Api/Dockerfile -t minicc-api:net10-check .`
  Expected: 成功。

  Run: `podman run --rm --entrypoint cat minicc-api:net10-check /etc/os-release | grep VERSION_ID`
  Expected: `VERSION_ID="24.04"`。

  Run: `podman run --rm --entrypoint dotnet minicc-api:net10-check --list-runtimes`
  Expected: 包含 `Microsoft.AspNetCore.App 10.0.`。

- [ ] **Step 4: 用默认（PostgreSQL）compose 做回归检查**

  Run:
  ```bash
  podman compose -f docker-compose.yaml up -d --build
  curl -s -o /dev/null -w '%{http_code}\n' -c "$TMPDIR/minicc.cookies" -d 'UserName=mini_cc&Password=mini_cc_password' http://localhost:5000/api/account/login
  curl -s -b "$TMPDIR/minicc.cookies" 'http://localhost:5000/api/articles?view=all&pageSize=1'
  ```
  Expected:
  - 登录返回 `200`；
  - 列表返回包含 `"total"` 的 JSON；
  - `podman logs minicc-api 2>&1 | grep -c "Unhandled exception"` 输出 `0`。

  如果本地数据库的账号不是默认账号，换成实际的账号密码。

- [ ] **Step 5: Commit**

  ```bash
  git add src/backend/MiniCc.Api/Dockerfile
  git commit -m "build(api): use .NET 10 container images"
  ```

---

### Task 2: SQLite 上下文、模型与迁移

**Files:**
- Modify: `src/backend/MiniCc.Api/MiniCc.Api.csproj`（新增包）
- Modify: `Shared/Data/MiniCcDbContext.cs`（UTF-16）
- Modify: `Core/ArticleNs/Infrastructure/EntityConfigurations/ArticleConfiguration.cs:3,20-24,75-80`（UTF-16）
- Create: `Shared/Data/SqliteMiniCcDbContext.cs`、`Shared/Data/SqliteDesignTimeDbContextFactory.cs`
- Create（生成）：`Migrations/Sqlite/<timestamp>_InitialSqlite.cs`、`.Designer.cs`、`SqliteMiniCcDbContextModelSnapshot.cs`
- Test: `test/backend/MiniCc.Api.Test/Infrastructure/DatabaseModelTests.cs`

**Interfaces:**
- Produces：
  - `MiniCcDbContext` 新增 `protected MiniCcDbContext(DbContextOptions options, IEncryptionService encryptionService)`，以及 `protected virtual void ConfigureProviderModel(ModelBuilder modelBuilder)`。
  - `public class SqliteMiniCcDbContext : MiniCcDbContext`，构造函数为 `(DbContextOptions<SqliteMiniCcDbContext> options, IEncryptionService encryptionService)`。
  - `public class UtcTicksConverter : ValueConverter<DateTimeOffset, long>`。
  - `public class SqliteDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SqliteMiniCcDbContext>`。

- [ ] **Step 1: 写 PostgreSQL 模型的保护测试**

  `DatabaseModelTests`（不属于任何 collection）。辅助方法 `Postgres()` 的写法与 `LibraryModelTests` 相同：`UseNpgsql("Host=localhost;Database=unused")`、`EnableServiceProviderCaching(false)`、`Mock.Of<IEncryptionService>()`。

  ```csharp
  [Fact] public void PostgresModel_MatchesExistingSnapshot()
  { using var db = Postgres(); db.Database.HasPendingModelChanges().Should().BeFalse(); }
  ```

  Run: `dotnet test test/backend/MiniCc.Api.Test --filter "FullyQualifiedName~DatabaseModelTests|FullyQualifiedName~LibraryModelTests"`
  Expected：通过。这个测试用来保护重构，现在就应该通过。

- [ ] **Step 2: 收拢 PostgreSQL 专属配置**

  `ArticleConfiguration.cs`：
  - 删除 `using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;`；
  - 第 20–23 行两个索引去掉 `.HasNullSortOrder(...)`，保留 `HasDatabaseName` 和 `IsDescending(true, true)`；
  - 第 24 行改为 `builder.HasIndex(a => a.NormalizedUrl);`；
  - 删除第 75–80 行（生效的 tsvector 配置），旁边被注释掉的旧代码不要动。

  `MiniCcDbContext.cs`：
  - 增加 protected 构造函数；
  - `OnModelCreating` 中删除 `HasPostgresExtension` 和 `.HasColumnType("jsonb")` 两行，在 `base.OnModelCreating` 之前调用 `ConfigureProviderModel(modelBuilder)`；
  - 新增下面这个方法，只搬移原来的配置值：

  ```csharp
  // PostgreSQL-only model details; SqliteMiniCcDbContext replaces them.
  protected virtual void ConfigureProviderModel(ModelBuilder modelBuilder)
  {
      modelBuilder.HasPostgresExtension("zhparser");
      modelBuilder.Entity<Article>(article =>
      {
          article.HasGeneratedTsVectorColumn(p => p.SearchVector, "mixed_zh_en", p => new { p.Title, p.Summary, p.ReadableContent, p.Author })
              .HasIndex(p => p.SearchVector).HasMethod("GIN");
          article.HasIndex(a => a.NormalizedUrl).HasMethod("hash");
          article.HasIndex(a => new { a.LastReadAt, a.Id }).HasNullSortOrder(NullSortOrder.NullsLast, NullSortOrder.Unspecified);
          article.HasIndex(a => new { a.PublishedAt, a.Id }).HasNullSortOrder(NullSortOrder.NullsLast, NullSortOrder.Unspecified);
      });
      modelBuilder.Entity<User>().Property(e => e.ReaderPreferences).HasColumnType("jsonb");
  }
  ```

  Run: 同 Step 1。
  Expected：`PostgresModel_MatchesExistingSnapshot` 和 `LibraryModelTests` 全部通过。

- [ ] **Step 3: 写 SQLite 模型测试**

  在 `DatabaseModelTests` 中增加辅助方法 `Sqlite(string dataSource = ":memory:")`，写法与 `Postgres()` 相同，改用 `UseSqlite($"Data Source={dataSource}")`。

  ```csharp
  [Fact] public void SqliteModel_MatchesSqliteSnapshot()
  { using var db = Sqlite(); db.Database.HasPendingModelChanges().Should().BeFalse(); }

  [Fact] public void SqliteModel_HasNoPostgresSpecifics()
  {
      using var db = Sqlite();
      var model = db.GetService<IDesignTimeModel>().Model;
      var article = model.FindEntityType(typeof(Article))!;
      article.FindProperty(nameof(Article.SearchVector)).Should().BeNull();
      model.FindEntityType(typeof(User))!.FindProperty(nameof(User.ReaderPreferences))!.GetColumnType().Should().Be("TEXT");
      article.GetIndexes().Select(i => i.GetDatabaseName()).Should().Contain(new[] { "IX_Articles_NormalizedUrl", "IX_Articles_LastReadAt", "IX_Articles_PublishedAt" });
      // 模型、实体、属性、索引上的所有注解名，都不以 "Npgsql:" 开头
      AnnotationNames(model).Should().NotContain(n => n.StartsWith("Npgsql:"));
  }

  [Fact] public async Task SqliteMigration_StoresTimesAsUtcTicks()
  {
      // 临时文件：Path.Combine(Path.GetTempPath(), $"minicc-model-{Guid.NewGuid():N}.db")
      // 1. MigrateAsync，然后保存一篇 Article.Create(...) 文章
      // 2. 用 ExecuteUpdateAsync 把 CreatedAt 设为 created：
      //    var created = new DateTimeOffset(2026, 11, 15, 8, 0, 0, TimeSpan.FromHours(8)).AddTicks(1234567);
      // 3. 换一个新的上下文读回：
      //    loaded.CreatedAt.Should().Be(created); loaded.CreatedAt.Offset.Should().Be(TimeSpan.Zero); loaded.CreatedAt.UtcTicks.Should().Be(created.UtcTicks);
      // 4. 用 ADO 命令执行 SELECT typeof("CreatedAt") FROM "Articles"，结果应为 "integer"
      // 5. db.Articles.Where(a => a.CreatedAt > created.AddTicks(-1)).OrderByDescending(a => a.CreatedAt).Select(a => a.Id).ToListAsync()，结果包含这篇文章
      // 结束时：SqliteConnection.ClearAllPools(); File.Delete(path);
  }
  ```

- [ ] **Step 4: 运行测试，确认失败**

  Run: `dotnet test test/backend/MiniCc.Api.Test --filter "FullyQualifiedName~DatabaseModelTests"`
  Expected: 编译失败，报 `SqliteMiniCcDbContext` 找不到（CS0246）。

- [ ] **Step 5: 加入包，实现 SQLite 上下文和设计时工厂**

  在 `MiniCc.Api.csproj` 的 Npgsql 包旁边加上 `<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.7" />`。


  `SqliteMiniCcDbContext.cs`：
  - 覆盖 `ConfigureConventions`，不调用基类，只配置以下两项：
    - `Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>()`；
    - `DefaultTypeMapping<DateTimeOffset>().HasConversion<UtcTicksConverter>()`。
  - 覆盖 `ConfigureProviderModel`：`modelBuilder.Entity<Article>().Ignore(a => a.SearchVector)`，以及把 `ReaderPreferences` 设为 `HasColumnType("TEXT")`。
  - `UtcTicksConverter` 的转换是 `v => v.UtcTicks` 和 `v => new DateTimeOffset(v, TimeSpan.Zero)`。

  `SqliteDesignTimeDbContextFactory.CreateDbContext`：
  - 返回一个使用 `UseSqlite("Data Source=design-time.db")` 的上下文；
  - 加密服务用 `new AesEncryptionService(...)`，配置与 `PostgresFixture.CreateContext` 中的内存 AES 配置相同（32 字节全零 Key、16 字节全零 IV）。

- [ ] **Step 6: 生成 SQLite 迁移**

  Run: `cd src/backend/MiniCc.Api && dotnet ef migrations add InitialSqlite --context SqliteMiniCcDbContext --output-dir Migrations/Sqlite`
  Expected：生成 3 个文件，命名空间为 `MiniCc.Api.Migrations.Sqlite`。工具版本比运行时新的警告可以忽略。

  Run: `grep -rlE "Npgsql|tsvector|jsonb|timestamp with time zone" src/backend/MiniCc.Api/Migrations/Sqlite`
  Expected：无输出。

  Run: `git status --short src/backend/MiniCc.Api/Migrations`
  Expected：只有 `?? src/backend/MiniCc.Api/Migrations/Sqlite/`。

- [ ] **Step 7: 运行测试，确认通过**

  Run: `dotnet test test/backend/MiniCc.Api.Test --filter "FullyQualifiedName~DatabaseModelTests|FullyQualifiedName~LibraryModelTests"`
  Expected：全部通过。

  Run: `file src/backend/MiniCc.Api/Shared/Data/MiniCcDbContext.cs src/backend/MiniCc.Api/Core/ArticleNs/Infrastructure/EntityConfigurations/ArticleConfiguration.cs`
  Expected：两个文件仍然是 `UTF-16, little-endian`。

- [ ] **Step 8: Commit**

  ```bash
  git add src/backend/MiniCc.Api test/backend/MiniCc.Api.Test/Infrastructure/DatabaseModelTests.cs
  git commit -m "feat(db): add SQLite context, model and migrations"
  ```

---

### Task 3: 选择数据库、SQLite 启动初始化与测试夹具

**Files:**
- Create: `Shared/Data/DatabaseConfiguration.cs`
- Modify: `Core/DependencyInjection.cs:22-30`（UTF-16）、`Program.cs:57,101-103`、`Shared/Data/DbSeeder.cs:19-22`（UTF-16，另加一个私有方法）、`appsettings.json:2-4`、仓库根目录的 `.gitignore`
- Test（Create）：`Infrastructure/DatabaseConfigurationTests.cs`、`Infrastructure/SqliteFixture.cs`、`Infrastructure/SqliteApi.cs`、`Infrastructure/SqliteStartupTests.cs`、`Infrastructure/SqliteApiTests.cs`

**Interfaces:**
- Consumes：Task 2 的 `SqliteMiniCcDbContext`。
- Produces：
  ```csharp
  namespace MiniCc.Api.Shared.Data;
  public enum DatabaseProvider { Postgres, Sqlite }
  public static class DatabaseConfiguration
  {
      public static DatabaseProvider GetProvider(IConfiguration configuration);
      public static string ResolveSqliteConnectionString(string connectionString, string contentRootPath);
  }
  // Core/DependencyInjection.cs
  public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration, string? contentRootPath = null);

  // 测试项目 MiniCc.Api.Test.Infrastructure
  [CollectionDefinition("Sqlite")] public class SqliteCollection : ICollectionFixture<SqliteFixture>;
  public sealed class SqliteFixture : IAsyncLifetime
  {
      public string ConnectionString { get; }   // $"Data Source={临时目录}/mini_cc.db"
      public SqliteMiniCcDbContext CreateContext(string? connectionString = null);
      public Task<Article> SeedArticleAsync(Action<Article>? configure = null);
  }
  public static class SqliteApi
  {
      public static WebApplicationFactory<ArticlesController> CreateFactory(SqliteFixture fixture);
      public static HttpClient AuthenticatedClient(WebApplicationFactory<ArticlesController> factory); // 请求头 X-Api-Key: test-api-key
  }
  [Collection("Sqlite")] public partial class SqliteApiTests(SqliteFixture fixture);
  ```

- [ ] **Step 1: 写 `DatabaseConfigurationTests`**

  - `GetProvider_DefaultsToPostgres`（Theory：`null`、`""`）→ `DatabaseProvider.Postgres`。
  - `GetProvider_IsCaseInsensitive`（Theory：`sqlite`、`SQLite` → Sqlite；`postgres`、`Postgres` → Postgres）。
  - `GetProvider_RejectsUnknownValue`（取值 `mysql`）→ `Throw<InvalidOperationException>().WithMessage("Database:Provider must be 'postgres' or 'sqlite'.")`。
  - `ResolveSqliteConnectionString_UsesContentRootForRelativePaths`：
    - `root = Path.Combine(Path.GetTempPath(), "minicc-root")`；
    - 用 `SqliteConnectionStringBuilder(...).DataSource` 解析结果后断言：
      - `Data Source=data/mini_cc.db` → `Path.GetFullPath(Path.Combine(root, "data", "mini_cc.db"))`；
      - `Data Source=/var/lib/mini_cc.db` → 保持不变；
      - `Data Source=:memory:` → `:memory:`。
  - `AddInfrastructureServices_Sqlite_RegistersSqliteContextUnderContentRoot`：
    - 配置：`Database:Provider=sqlite`、`ConnectionStrings:Sqlite=Data Source=data/x.db`、`ImageProxy:SignatureKey=test`；
    - `contentRootPath` 是一个临时目录，并且与当前工作目录不同；
    - 先 `AddSingleton(Mock.Of<IEncryptionService>())` 再调用 `AddInfrastructureServices`；
    - 在一个 scope 中解析 `MiniCcDbContext`：`BeOfType<SqliteMiniCcDbContext>()`，`Database.GetConnectionString()` 包含 `Path.GetFullPath(Path.Combine(root, "data", "x.db"))`。
  - `AddInfrastructureServices_PrefersMiniCcDbSetting`：配置中 `MiniCC_Db=Data Source=/tmp/minicc-a.db`，数据库类型为 sqlite → 连接串包含 `/tmp/minicc-a.db`。
  - `AddInfrastructureServices_Sqlite_RequiresConnectionString`：数据库类型为 sqlite，并且没有任何连接串 → `Throw<InvalidOperationException>().WithMessage("SQLite requires MiniCC_Db or ConnectionStrings:Sqlite.")`。

- [ ] **Step 2: 写 `SqliteFixture`、`SqliteApi`、`SqliteStartupTests`，以及 `SqliteApiTests` 的启动用例**

  `SqliteFixture`：
  - 临时目录：`Path.Combine(Path.GetTempPath(), "minicc-sqlite-" + Guid.NewGuid().ToString("N"))`；
  - `InitializeAsync`：用 `CreateContext()` 执行 `new DbSeeder(db).InitAsync()`。目录由 DbSeeder 创建；
  - `DisposeAsync`：`SqliteConnection.ClearAllPools()`，然后递归删除临时目录；
  - `CreateContext`、`SeedArticleAsync` 的写法与 `PostgresFixture` 相同，只是改用 `DbContextOptionsBuilder<SqliteMiniCcDbContext>().UseSqlite(...)`。

  `SqliteApi.CreateFactory` 照抄 `LibraryApiTests.CreateFactory` 的环境、内存配置和 `IApiKeyService` 模拟，只把数据库替换部分改为：
  - 先 `RemoveAll` 掉 `DbContextOptions<MiniCcDbContext>`、`IDbContextOptionsConfiguration<MiniCcDbContext>` 和 `MiniCcDbContext`；
  - 再 `AddDbContext<MiniCcDbContext, SqliteMiniCcDbContext>(o => o.UseSqlite(fixture.ConnectionString))`。

  `SqliteStartupTests`（不属于任何 collection，每个用例使用自己的临时目录）：
  - `InitAsync_CreatesDatabaseInMissingDirectoryOnce`：
    - 数据源为 `<temp>/nested/mini_cc.db`，目录事先不存在；
    - 用两个新的上下文各执行一次 `InitAsync`；
    - 文件存在；`Users` 只有一行，`UserName == "mini_cc"`；
    - 用 ADO 执行 `PRAGMA journal_mode`，返回 `"wal"`。
  - `InitAsync_UnwritableDirectory_ReportsFullPath`：
    - 把父目录设为 `UnixFileMode.UserRead | UnixFileMode.UserExecute`；数据源为 `<parent>/data/mini_cc.db`；
    - `ThrowAsync<InvalidOperationException>()`，消息包含该完整路径；
    - 在 finally 中恢复权限并删除目录。

  `SqliteApiTests.Api_StartsOnSqlite`：`GET /api/tags` 返回 200。这同时证明 `Program` 的启动初始化能在 SQLite 上执行。

- [ ] **Step 3: 运行测试，确认失败**

  Run: `dotnet test test/backend/MiniCc.Api.Test --filter "FullyQualifiedName~DatabaseConfigurationTests|FullyQualifiedName~SqliteStartupTests|FullyQualifiedName~SqliteApiTests"`
  Expected：编译失败，报 `DatabaseConfiguration` 找不到。

- [ ] **Step 4: 实现 `DatabaseConfiguration` 和注册逻辑**

  `ResolveSqliteConnectionString`：
  - 用 `SqliteConnectionStringBuilder` 读取 `DataSource`；
  - 不是 `:memory:`、不是绝对路径时，改成 `Path.GetFullPath(Path.Combine(contentRootPath, dataSource))`；
  - 返回 `builder.ToString()`。

  `AddInfrastructureServices`：
  - `MiniCC_Db` 改为从 `configuration["MiniCC_Db"]` 读取。`WebApplication` 的配置包含环境变量，所以部署行为不变，测试也可以通过内存配置传入。
  - postgres 分支保持现在的 `AddDbContext<MiniCcDbContext>(o => o.UseNpgsql(cs))`。
  - sqlite 分支：
    - 连接串依次取 `MiniCC_Db`、`ConnectionStrings:Sqlite`，都没有时抛出全局约束中的固定消息；
    - 用 `contentRootPath ?? Directory.GetCurrentDirectory()` 解析；
    - 注册 `AddDbContext<MiniCcDbContext, SqliteMiniCcDbContext>(o => o.UseSqlite(resolved))`。

  其他文件：
  - `Program.cs:57`：传入 `builder.Environment.ContentRootPath`。
  - `Program.cs` 在 `InitAsync` 之前用 `app.Logger` 记录 Information 日志：
    - 两种数据库都记录 `Database provider: {Provider}`，值为 `postgres` 或 `sqlite`；
    - SQLite 另外记录 `SQLite database: {Path}`，值为 `GetDbConnection().DataSource`。
  - `appsettings.json`：在 `ConnectionStrings` 下加 `"Sqlite": "Data Source=data/mini_cc.db"`。
  - `.gitignore`：追加 `src/backend/MiniCc.Api/data/`。

- [ ] **Step 5: 实现 SQLite 启动初始化**

  `DbSeeder.InitAsync`：
  - `_context.Database.IsSqlite()` 时，调用新的 `PrepareSqliteAsync()`；
  - 否则执行原来的 `EnsureDatabaseExistsAsync` 和文本搜索 SQL，代码原样保留；
  - 之后的迁移、URL 回填、默认用户逻辑两种数据库共用。

  `PrepareSqliteAsync`：
  - 完整路径 = `Path.GetFullPath(new SqliteConnectionStringBuilder(conn).DataSource)`；
  - 创建父目录，然后 `ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;")`；
  - 捕获 `IOException`、`UnauthorizedAccessException`、`SqliteException`，重新抛出 `InvalidOperationException($"无法打开 SQLite 数据库文件：{path}", ex)`。

- [ ] **Step 6: 运行测试，确认通过**

  Run: 同 Step 3。
  Expected：全部通过。

  Run: `dotnet test test/backend/MiniCc.Api.Test --filter "FullyQualifiedName~LibraryOptionsTests"`
  Expected：通过（没有配置时仍然默认使用 postgres）。

  Run: `file src/backend/MiniCc.Api/Core/DependencyInjection.cs src/backend/MiniCc.Api/Shared/Data/DbSeeder.cs`
  Expected：仍然是 UTF-16。

- [ ] **Step 7: Commit**

  ```bash
  git add .gitignore src/backend/MiniCc.Api test/backend/MiniCc.Api.Test/Infrastructure
  git commit -m "feat(db): select database provider and initialize SQLite"
  ```

---

### Task 4: 事务与加锁

**Files:**
- Create: `Shared/Data/DatabaseLocks.cs`、`Core/FeedNs/Infrastructure/FeedLeases.cs`
- Modify: `Shared/Data/Repositories/UnitOfWork.cs:40-42`、`Core/ArticleNs/Infrastructure/Repositories/ArticleRepository.cs:16-19,41`、`Core/ArticleNs/Application/Services/ArticlePurger.cs:16`、`Core/FeedNs/Application/FeedRefresher.cs:13-24,58`、`Core/FeedNs/Application/FeedImportService.cs:20`、`Core/FeedNs/Application/FeedService.cs:58-59,84`
- Test: `Infrastructure/SqliteConcurrencyTests.cs`

**Interfaces:**
- Consumes：Task 3 的 `SqliteFixture`、`SqliteApi`。
- Produces：
  ```csharp
  namespace MiniCc.Api.Shared.Data;
  public static class DatabaseLocks
  {
      public const long ArticleCreationLockKey = 70453125;
      public const long FeedIdentityLockKey = 70453126;
      // PostgreSQL appends " FOR UPDATE"; SQLite relies on the caller's BEGIN IMMEDIATE transaction.
      public static IQueryable<T> FromSqlForUpdate<T>(this DbSet<T> set, FormattableString sql) where T : class;
      // PostgreSQL: SELECT pg_advisory_xact_lock(<key>); SQLite: no-op inside the caller's BEGIN IMMEDIATE transaction.
      public static Task AcquireTransactionLockAsync(this DatabaseFacade database, long key, CancellationToken cancellationToken);
  }
  namespace MiniCc.Api.Core.FeedNs.Infrastructure;
  public static class FeedLeases
  {
      public static Task<int> ClaimAsync(MiniCcDbContext db, Guid id, Guid token, CancellationToken cancellationToken); // 返回受影响的行数
      public static Task<List<Guid>> ClaimDueAsync(MiniCcDbContext db, Guid token, CancellationToken cancellationToken);
  }
  ```

- [ ] **Step 1: 写 `SqliteConcurrencyTests`（`[Collection("Sqlite")]`）**

  - `BeginTransaction_IsImmediate_SecondWriterWaitsForCommit`（这个测试检验的是库的现有行为，写好就会通过）：
    - `first` 开启事务；
    - `second` 使用 `fixture.ConnectionString + ";Default Timeout=1"`，调用 `BeginTransactionAsync` → `ThrowAsync<SqliteException>()`，`SqliteErrorCode == 5`；
    - `first` 提交后，`second` 可以成功开启事务。
  - `LockedLookups_LoadRowsOnSqlite`：
    - `SeedArticleAsync()`；在 `new UnitOfWork(db)` 的事务中依次调用：
      - `LockArticleCreationAsync()`；
      - `Articles.GetByIdForProgressUpdateAsync(id)`，返回这篇文章；
      - `Articles.GetByUrlAsync(article.NormalizedUrl)`，返回这篇文章；
    - 然后提交。
  - `ConcurrentExtensionSaves_SameUrl_CreateOneArticle`：
    - 4 个并发 `POST /api/articles/content`，`url` 相同，标题和正文各不相同；
    - 全部返回成功；
    - `Articles.IgnoreQueryFilters().CountAsync(a => a.NormalizedUrl == UrlNormalizer.Normalize(url)) == 1`。
  - `ConcurrentProgressSaves_SameStaleBase_OneConflict`：
    - 2 个并发 `PUT /api/articles/{id}/progress`；请求体照抄 `LibraryApiTests` 中进度测试的写法，只改 `sessionId`（`session-a`、`session-b`），`baseUpdatedAt = null`；
    - 排序后的状态码为 `[200, 409]`。
  - `FeedLeases_ClaimDue_TwoWorkersClaimDisjointBatches`：
    - 先用 `ExecuteUpdateAsync` 把库中所有订阅源的 `NextFetchAt` 设为一天后；
    - 种入 6 个到期的订阅源（`NextFetchAt = UtcNow.AddMinutes(-1)`），外加 1 个到期但已暂停的、1 个到期但 `LeaseUntil = UtcNow.AddMinutes(5)` 的；
    - 两个上下文并发调用 `ClaimDueAsync`：每批不超过 4 个，合并后没有重复，并且正好等于那 6 个；
    - 再把被租出的那个订阅源的 `LeaseUntil` 设为一分钟前，再次 `ClaimDueAsync` 只返回它。
  - `Lease_IsAtomic_AndStaleOwnerCannotClearReclaimedLease`：
    - 移植 `FeedRefreshPostgresTests` 中的同名测试，连同私有的 `Factory` 和 `Refresher` 辅助代码一起复制，把 `fixture` 换成 `SqliteFixture`。
  - `PurgeRacingRestore_NeverDeletesRestoredArticle`：重复 5 次：
    - 种入一篇已在回收站的文章；
    - 并发执行 `POST /api/articles/batch { ids:[id], action:"purge" }` 和 `POST /api/articles/{id}/restore`；
    - 如果恢复返回 200，文章必须仍然存在，并且批量结果的 `failed` 中有这个 id，`reason == "notInTrash"`；否则文章已经不存在。
  - `TrashCleanup_PurgesAtExactCutoff`：
    - 种入一篇在回收站的文章，用 `ExecuteUpdate` 把 `DeletedAt` 设为 `T = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(1234567)`。选一个很早的时间，这样清理不会影响共享库中其他测试放入回收站的文章；
    - `new TrashCleanup(db, new ArticlePurger(db, uow), NullLogger<TrashCleanup>.Instance).PurgeAsync(T.AddTicks(-1), default)` 之后，文章仍然存在；
    - `PurgeAsync(T, default)` 之后，文章已被删除。
  - `FeedRefreshDuringExtensionSaves_CompletesWithoutBusyErrors`：
    - 用 `Refresher` 辅助代码创建一个 handler，返回一份包含 20 个 item 的 RSS，每个 item 的 guid 和 link 都不同；
    - 认领订阅源后，`RefreshAsync` 与 10 个 HTTP 保存请求并发执行。其中 9 个保存的 URL 与 RSS 无关，1 个保存的 URL 与第一个 RSS item 的 link 相同；
    - 刷新结果为 `Status == "ok"`，`Imported + Skipped == 20`；10 个保存都成功；
    - 与那个共同 URL 对应的文章只有 1 篇（`NormalizedUrl` 计数为 1）。

- [ ] **Step 2: 运行测试，确认失败**

  Run: `dotnet test test/backend/MiniCc.Api.Test --filter "FullyQualifiedName~SqliteConcurrencyTests"`
  Expected：除了 `BeginTransaction_IsImmediate…` 之外都失败，原因是 SQLite 报 `pg_advisory_xact_lock`、`now` 或 `FOR` 附近的语法错误。

- [ ] **Step 3: 实现 `DatabaseLocks`**

  - 用 `set.GetService<ICurrentDbContext>().Context.Database.IsSqlite()` 判断数据库类型。
  - PostgreSQL：`set.FromSql(FormattableStringFactory.Create(sql.Format + " FOR UPDATE", sql.GetArguments()))`，以及 `database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(" + key + ")", ct)`。
  - SQLite：`set.FromSql(sql)` 和 `Task.CompletedTask`。

- [ ] **Step 4: 实现 `FeedLeases`，并替换所有调用点**

  PostgreSQL 分支：把 `FeedRefresher.cs:16` 和 `:19-24` 的 SQL 原样搬过来。

  SQLite 分支：先计算 `var now = DateTimeOffset.UtcNow; long nowTicks = now.UtcTicks, untilTicks = now.AddMinutes(5).UtcTicks;`，然后执行：

  ```sql
  UPDATE "Feeds" SET "LeaseUntil" = {untilTicks}, "LeaseToken" = {token} WHERE "Id" = {id} AND "Status" = 0 AND ("LeaseUntil" IS NULL OR "LeaseUntil" < {nowTicks})
  ```
  ```sql
  UPDATE "Feeds" SET "LeaseUntil" = {untilTicks}, "LeaseToken" = {token}
  WHERE "Id" IN (SELECT "Id" FROM "Feeds" WHERE "Status" = 0 AND "NextFetchAt" <= {nowTicks}
    AND ("LeaseUntil" IS NULL OR "LeaseUntil" < {nowTicks}) ORDER BY "NextFetchAt" LIMIT 4)
  RETURNING "Id" AS "Value"
  ```

  调用点替换。表中的 SQL 都是去掉 `FOR UPDATE` 之后的原 SQL：

  | 位置 | 改为 |
  |---|---|
  | `ArticleRepository.cs:18` | `_context.Articles.FromSqlForUpdate($"SELECT * FROM \"Articles\" WHERE \"Id\" = {id} AND \"DeletedAt\" IS NULL")`。第 16 行注释改为：事务在提交前持有该行锁（PostgreSQL）或数据库写锁（SQLite） |
  | `ArticleRepository.cs:41` | `FromSqlForUpdate($"SELECT * FROM \"Articles\" WHERE \"NormalizedUrl\" = {url} ORDER BY \"CreatedAt\", \"Id\" LIMIT 1")`，后面的链式调用保持不变 |
  | `ArticlePurger.cs:16` | `db.Articles.FromSqlForUpdate($"SELECT * FROM \"Articles\" WHERE \"Id\" = {id}")` |
  | `FeedRefresher.cs:58`、`FeedImportService.cs:20`、`FeedService.cs:84` | `db.Feeds.FromSqlForUpdate($"SELECT * FROM \"Feeds\" WHERE \"Id\" = {…}")` |
  | `FeedService.cs:59` | `db.Feeds.FromSqlForUpdate($"SELECT * FROM \"Feeds\" WHERE \"FeedUrlHash\" = {hash}")` |
  | `FeedService.cs:58` | `await db.Database.AcquireTransactionLockAsync(DatabaseLocks.FeedIdentityLockKey, ct);` |
  | `UnitOfWork.cs:41-42` | `_context.Database.AcquireTransactionLockAsync(DatabaseLocks.ArticleCreationLockKey, cancellationToken)` |
  | `FeedRefresher.ClaimAsync` | `await FeedLeases.ClaimAsync(db, id, token, cancellationToken) == 1 ? token : null` |
  | `FeedRefresher.ClaimDueAsync` | `FeedLeases.ClaimDueAsync(db, token, cancellationToken)` |

- [ ] **Step 5: 运行测试，确认通过**

  Run: `dotnet test test/backend/MiniCc.Api.Test --filter "FullyQualifiedName~SqliteConcurrencyTests"`
  Expected：全部通过。

  Run: `grep -rn "FOR UPDATE\|pg_advisory\|now()" src/backend/MiniCc.Api --include='*.cs' | grep -v Migrations`
  Expected：只在 `DatabaseLocks.cs` 和 `FeedLeases.cs` 中出现。

- [ ] **Step 6: 用 PostgreSQL 回归**

  Run: `dotnet test test/backend/MiniCc.Api.Test --filter "FullyQualifiedName~Postgres|FullyQualifiedName~LibraryApiTests"`（需要先设置全局约束中的 Podman 环境变量）
  Expected：全部通过。

- [ ] **Step 7: Commit**

  ```bash
  git add src/backend/MiniCc.Api test/backend/MiniCc.Api.Test/Infrastructure/SqliteConcurrencyTests.cs
  git commit -m "feat(db): replace row and advisory locks on SQLite"
  ```

---

### Task 5: 搜索、查询时钟、标签冲突与列表投影

**Files:**
- Modify: `Core/ArticleNs/Application/Queries/ArticleListFilter.cs:8,19-24`、`GetArticleListQueryHandler.cs:20`
- Modify: `Core/ApiKeys/Application/Services/ApiKeyService.cs:32-36`、`Core/FeedNs/Application/FeedService.cs:118`
- Create: `Shared/Data/DatabaseErrors.cs`；Modify: `Core/TagNs/Infrastructure/TagWriter.cs:6,42-43`
- Modify（只在 Step 5 的条件成立时）：`Core/ArticleNs/Application/DTOs/ArticleListItemDto.cs`
- Test: `Infrastructure/SqliteLibraryTests.cs`

**Interfaces:**
- Consumes：Task 3 的 `SqliteFixture`、`SqliteApi`。
- Produces：
  ```csharp
  public static IQueryable<Article> Apply(IQueryable<Article> query, ArticleListRequest request, bool useLikeSearch = false); // ArticleListFilter
  namespace MiniCc.Api.Shared.Data;
  public static class DatabaseErrors
  {
      // PostgreSQL: PostgresException UniqueViolation with TableName == table and ConstraintName == postgresConstraint.
      // SQLite: SqliteException extended code 2067 whose message contains "{table}.{sqliteColumn}".
      public static bool IsUniqueViolation(DbUpdateException exception, string table, string postgresConstraint, string sqliteColumn);
  }
  ```

- [ ] **Step 1: 写 `SqliteLibraryTests`（`[Collection("Sqlite")]`）**

  每个用例新建一个唯一标签，种入的文章都加上它，查询时带上 `tags={tagId}`，与共享库中的其他数据隔离。文章用 `Article.Create(Url.Create(...), title, author, Content.Create(original, readable), summary)` 创建；时间字段用 `ExecuteUpdateAsync` 设置。

  - `Search_MatchesTitleSummaryAuthorAndBodySubstrings`：
    - `token = "检索" + Guid.NewGuid().ToString("N")[..6]`；
    - 4 篇文章分别只在标题、摘要、作者、正文（`<p>前文{token}后文</p>`）中包含 token，另有 1 篇不含；
    - `q=token` 时 `total == 4`，id 集合正好是这 4 篇；
    - `q=token[1..5]`（从中间截取的一段）时结果相同。
  - `Search_TreatsWildcardsAndBackslashLiterallyAndIgnoresAsciiCase`：
    - 标题分别为 `Product%_Name`、`ProductABName`、`C:\temp\file`；
    - `q=Product%_Name` 和 `q=product%_name` 都只返回第一篇；
    - `q=C:\temp` 只返回第三篇。
  - `List_PagesEverySortAcrossNullsAndPages`：
    - 5 篇文章，`CreatedAt` 各不相同；其中 3 篇有 `LastReadAt`，2 篇有 `PublishedAt`；
    - 对 `saved_desc`、`saved_asc`、`read_desc`、`published_desc` 分别以 `pageSize=2` 沿 `nextCursor` 翻完所有页；
    - 拼起来的 id 序列，等于在 C# 中按 `GetArticleListQueryHandler` 的规则（键为 null 的排最后，再按键降序、Id 降序；`saved_asc` 按 CreatedAt、Id 升序）算出的顺序；
    - 再把其中 2 篇移入回收站，`DeletedAt` 各不相同，`view=trash` 按 `DeletedAt` 降序。
  - `List_DateRangeUsesInstantsAcrossOffsets`：
    - 一篇文章 `CreatedAt = 2026-11-14T16:00:00Z`；
    - `savedFrom=2026-11-15T00:00:00+08:00` 包含它；
    - `savedTo=2026-11-15T00:00:00+08:00` 不包含它；
    - `savedTo=2026-11-15T00:00:00.0000001+08:00` 包含它；
    - 查询字符串里的 `+` 要编码成 `%2B`。
  - `List_ProjectsEachFeedOnce`：
    - 一篇文章有来自订阅源 A 的 2 个条目（key 不同）和来自订阅源 B 的 1 个条目；
    - 列表项的 `feeds` 长度为 2，标题是 A 和 B。
  - `ApiKeyValidation_HonorsExpiryOnSqlite`：
    - 为默认用户种入两个 `ApiKey`，一个 `ExpiredTime = UtcNow.AddMinutes(-1)`，一个 `UtcNow.AddDays(1)`；
    - `new ApiKeyService(db, NullLogger<ApiKeyService>.Instance).ValidateAsync(key)` 分别返回 false 和 true。
  - `FeedTrashArticles_TrashesEligibleArticles`：
    - 直接种入一个 `Feed`、一篇文章，以及一个 `FeedEntry.Create(feed.Id, key, url, hash)`，它的 `ArticleId` 指向这篇文章；
    - `POST /api/feeds/{id}/trash-articles` 返回 `{ trashed: 1, skipped: 0 }`；
    - 文章的 `DeletedAt` 在一分钟以内。
  - `TagNameConflict_IsRecognizedOnSqlite`：
    - 先保存一个名为 `dup-{guid}` 的标签，再在另一个上下文中保存同名标签，捕获 `DbUpdateException`；
    - `DatabaseErrors.IsUniqueViolation(ex, "Tags", "IX_Tags_Name", "Name")` 为 true；
    - `IsUniqueViolation(ex, "Users", "IX_Users_UserName", "UserName")` 为 false。
  - `ConcurrentTagCreates_SameName_ReturnCreatedAndConflict`：
    - 2 个并发 `POST /api/tags`，名称相同，排序后的状态码为 `[201, 409]`；
    - 先后创建 `Case-{x}` 和 `case-{x}`，都返回 201。

- [ ] **Step 2: 运行测试，确认失败**

  Run: `dotnet test test/backend/MiniCc.Api.Test --filter "FullyQualifiedName~SqliteLibraryTests"`
  Expected：
  - 搜索测试失败，报 `PlainToTsQuery`、`ILike` 或 `SearchVector` 无法翻译；
  - `ApiKeyValidation…` 和 `FeedTrashArticles…` 失败，报 `DateTimeOffset.UtcNow` 无法翻译；
  - `TagNameConflict…` 编译失败（`DatabaseErrors` 不存在）。

- [ ] **Step 3: 实现搜索和查询时钟**

  - `ArticleListFilter.Apply` 增加 `useLikeSearch` 参数：
    - 为 true 时：`EF.Functions.Like(a.Title, pattern, "\\") || Like(a.Summary, …) || Like(a.Author, …) || Like(a.ReadableContent, …)`，`pattern` 沿用现有的转义代码；
    - 为 false 时：保留原来的表达式，一个字符都不改。
  - `GetArticleListQueryHandler.cs:20` 传入 `db.Database.IsSqlite()`。
  - `ApiKeyService`：在 `AnyAsync` 之前写 `var now = DateTimeOffset.UtcNow;`，条件改为 `x.ExpiredTime > now`。
  - `FeedService.cs:118`：同样先取 `var now = DateTimeOffset.UtcNow;`，再 `SetProperty(a => a.DeletedAt, now)`。

- [ ] **Step 4: 实现 `DatabaseErrors`，并修改 `TagWriter`**

  - `TagWriter.cs:42-43` 的 `when` 条件改为 `DatabaseErrors.IsUniqueViolation(exception, "Tags", TagConfiguration.NameIndex, "Name")`；
  - 删除不再使用的 `using Npgsql;`。

- [ ] **Step 5: 运行测试；如果投影无法翻译，按下面的方法改写**

  Run: 同 Step 2。

  如果 `List_ProjectsEachFeedOnce` 或其他列表用例报 `InvalidOperationException`，并且消息提到 APPLY：
  - 在 `ArticleListItemDto` 中新增 `ListProjection`：与 `Projection` 相同，只是 `Feeds` 去掉 `.Distinct()`；
  - `GetArticleListQueryHandler` 改用 `ListProjection`，物化之后执行 `foreach (var item in items) item.Feeds = item.Feeds.Distinct().ToList();`；
  - Mapster 使用的 `Projection` 保持不变。

  Expected：全部通过。

- [ ] **Step 6: 用 PostgreSQL 回归**

  Run: `dotnet test test/backend/MiniCc.Api.Test --filter "FullyQualifiedName~ArticleList|FullyQualifiedName~LibraryApiTests|FullyQualifiedName~Tag"`
  Expected：全部通过（包括 PostgreSQL 的中文搜索、`%_` 字面匹配和标签 409 测试）。

- [ ] **Step 7: Commit**

  ```bash
  git add src/backend/MiniCc.Api test/backend/MiniCc.Api.Test/Infrastructure/SqliteLibraryTests.cs
  git commit -m "feat(db): add SQLite search, clock and conflict handling"
  ```

---

### Task 6: SQLite 上的 HTTP 冒烟测试

**Files:**
- Modify: `test/backend/MiniCc.Api.Test/Infrastructure/SqliteApiTests.cs`（Task 3 创建）
- Modify: `test/backend/MiniCc.Api.Test/Infrastructure/FeedApiTests.cs:13`（把 `private class FeedHttpHandler` 改成 `internal class`，供 SQLite 测试复用）
- Modify：测试暴露出的问题所在的生产代码。

**Interfaces:**
- Consumes：`SqliteApi`、`SqliteFixture`、`LibraryApiTests.FeedHttpHandler`。

各请求的格式，照抄 `LibraryApiTests`、`FeedApiTests`、`LinkApiTests`、`TagApiTests`、`ReaderPreferencesPostgresTests` 中对应的写法。

- [ ] **Step 1: 写测试**

  - `ArticleLifecycle_WorksOnSqlite`：
    - 保存正文（201）；保存链接（201，`contentKind == "link"`）；
    - 获取详情；列表按 `status=unread` 和 `source=extension` 筛选；
    - `PATCH state`：收藏、归档、已读；
    - 进度：
      - 会话 A 保存，拿到 `progressUpdatedAt`；
      - 会话 B 以这个字符串作为 `baseUpdatedAt` 保存，返回 **200**；
      - 会话 C 以 `null` 作为 base 保存，返回 **409**；
      - 重置进度，返回 200；
    - 创建高亮，返回 201；
    - 用不同的正文重新保存这篇文章：高亮变为 Pending；`PUT /api/highlights/{id}/anchor` 写入新的偏移量，带上新的 `contentRevision`，返回成功（写法照抄 `FeedApiTests` 中更新锚点的部分）；
    - 移入回收站（204），再恢复（200）；
    - 批量 `addTags`、`removeTags`；
    - 给这篇文章和另一篇文章加上同一个标签，然后对这篇文章执行批量 `trash` 和 `purge`：
      - 它的高亮行全部被删除（`Highlights.IgnoreQueryFilters()` 计数为 0）；
      - 共用的标签和另一篇文章上的关联都还在；
    - `DELETE /api/trash` 返回 `{ purged }`。
  - `TagLifecycle_WorksOnSqlite`：
    - 标签：创建、修改名称和颜色、`GET /api/tags?search=`；
    - 文章标签：`POST` 和 `DELETE /api/articles/{id}/tags`；
    - 把同一个标签同时加到一篇文章和一个订阅源的默认标签上，然后删除这个标签：文章的 `tags` 为空，订阅源的标签列表为空，文章和订阅源本身都还在。
  - `FeedLifecycle_WorksOnSqlite`（工厂上叠加 `FeedHttpHandler`，写法与 `FeedApiTests` 相同）：
    - 预览；订阅（201，`imported == 1`）；`GET /api/feeds`；
    - `PATCH` 标题、标签、暂停；
    - 暂停时手动刷新返回 409，恢复后刷新返回 200；
    - `GET /api/articles?view=feeds&feedId=`；
    - 取消订阅。
  - `ReaderPreferences_RoundTripOnSqlite`：照抄 `ReaderPreferencesPostgresTests` 中读写往返的用例，换成 SQLite 夹具。

- [ ] **Step 2: 运行测试**

  Run: `dotnet test test/backend/MiniCc.Api.Test --filter "FullyQualifiedName~SqliteApiTests"`
  Expected：全部通过。

  如果有失败：
  - 原因是 SQLite 翻译不了某个查询时，按设计第 3.5–3.7 节的原则，在数据层修复。修复后的查询要让两种数据库得到相同的结果，并重跑 Task 4 Step 6 的 PostgreSQL 回归。
  - 每处修复都写进 commit message。

- [ ] **Step 3: Commit**

  ```bash
  git add test/backend/MiniCc.Api.Test/Infrastructure src/backend/MiniCc.Api
  git commit -m "test(db): cover the HTTP lifecycle on SQLite"
  ```

---

### Task 7: SQLite 部署、文档与验收

**Files:**
- Modify: `src/backend/MiniCc.Api/Dockerfile`：在 base 阶段的 `USER $APP_UID` 之前加一行 `RUN mkdir -p /app/data && chown $APP_UID /app/data`
- Create: `docker-compose.sqlite.yaml`
- Modify: `README.md`、`CLAUDE.md`

**Interfaces:**
- Consumes：Task 1–6 的全部成果。

- [ ] **Step 1: 写 `docker-compose.sqlite.yaml`**

  内容与 `docker-compose.yaml` 相同，只有以下差异：
  - 没有 `db` 服务，没有 `pgdata` 卷；
  - `minicc-api` 的 `environment` 为：

    ```yaml
    Database__Provider: sqlite
    MiniCC_Db: "Data Source=/app/data/mini_cc.db"
    ImageProxy__SignatureKey: "${IMAGE_PROXY_SECRET:-my-secret-key-123!@#}"
    ```

  - `minicc-api` 增加 `volumes: - sqlite-data:/app/data`，去掉 `depends_on: db`；
  - 顶层 `volumes` 为 `sqlite-data:` 和 `image-proxy-data:`。

- [ ] **Step 2: 启动 SQLite 部署并验收**

  Run:
  ```bash
  podman compose -f docker-compose.yaml down
  podman compose -f docker-compose.sqlite.yaml up -d --build
  podman ps --format '{{.Names}}'
  curl -s -o /dev/null -w '%{http_code}\n' -c "$TMPDIR/minicc.cookies" -d 'UserName=mini_cc&Password=mini_cc_password' http://localhost:5000/api/account/login
  curl -s -o /dev/null -w '%{http_code}\n' -b "$TMPDIR/minicc.cookies" -H 'Content-Type: application/json' \
    -d '{"url":"https://example.com/sqlite-acceptance","title":"SQLite 验收","originalContent":"<p>原文</p>","readableContent":"<p>验收正文</p>"}' \
    http://localhost:5000/api/articles/content
  curl -s -b "$TMPDIR/minicc.cookies" 'http://localhost:5000/api/articles?view=all&q=%E9%AA%8C%E6%94%B6'
  podman exec minicc-api ls -ln /app/data
  ```
  Expected：
  - 容器列表中没有 `db`；
  - 登录返回 `200`，保存返回 `201`；
  - 搜索结果 `"total":1`；
  - `/app/data/mini_cc.db` 的所有者 uid 为 `1654`。

- [ ] **Step 3: 确认重启后数据仍在**

  Run: `podman compose -f docker-compose.sqlite.yaml down && podman compose -f docker-compose.sqlite.yaml up -d`，然后重新登录，重复 Step 2 中的搜索请求。
  Expected：`"total":1`。

- [ ] **Step 4: 运行 Playwright**

  Run: `cd src/frontend && E2E_BASE_URL=http://localhost:5000 E2E_USERNAME=mini_cc E2E_PASSWORD=mini_cc_password pnpm test:e2e e2e/reading-progress.spec.ts e2e/reader-preferences.spec.ts e2e/batch-trash.spec.ts`
  Expected：全部通过（两种视口）。

- [ ] **Step 5: 切回 PostgreSQL 部署做回归**

  Run: `podman compose -f docker-compose.sqlite.yaml down && podman compose -f docker-compose.yaml up -d`，然后登录，并请求 `/api/articles?view=all&q=<库中已有文章标题里的一个中文词>`。
  Expected：登录返回 200；中文搜索能返回那篇文章；`podman logs minicc-api` 中没有未处理的异常。

- [ ] **Step 6: 不用 Docker 手动运行**

  Run: `cd src/backend/MiniCc.Api && Database__Provider=sqlite ImageProxy__SignatureKey=dev dotnet run --urls http://localhost:5099`（后台运行），然后检查 `ls data/mini_cc.db`，并用 `curl -d 'UserName=mini_cc&Password=mini_cc_password' http://localhost:5099/api/account/login` 登录。
  Expected：文件存在，登录返回 200。最后停止进程，删除 `data/`。

- [ ] **Step 7: 更新文档**

  `README.md`：
  - Tech Stack 中改为“PostgreSQL 或 SQLite”；
  - Getting Started 增加 SQLite 方式：使用 `docker-compose.sqlite.yaml`，或执行 `Database__Provider=sqlite ImageProxy__SignatureKey=<key> dotnet run`；
  - Development 中的 `dotnet ef` 命令加上 `--context`，并列出两条 `migrations add` 命令（见设计第 3.3 节）。

  `CLAUDE.md`：
  - Quick Start、Manual Setup 和 `dotnet ef` 命令改为带 `--context` 的写法；
  - Architecture Overview 中的 Database 改为二选一；
  - Environment Setup 中写明 `Database:Provider` 和连接串的读取顺序；
  - Testing 中说明 SQLite 测试不需要 Docker，并写出筛选命令；
  - 新增 “SQLite Support” 一节，写设计第 3.5–3.8 节的要点：
    - IMMEDIATE + WAL；
    - `LIKE` 搜索及其已知限制；
    - 排序、长度和大小写方面的差异；
    - 单实例、只能用本地磁盘；
    - 备份方法；
    - `__EFMigrationsLock` 的处理方法；
    - 两个 compose 文件不能同时运行。

- [ ] **Step 8: 全量验证**

  Run: `dotnet test test/backend/MiniCc.Api.Test`（先设置 Podman 环境变量）
  Expected：全部通过，数量不少于 340 加上本计划新增的测试数。

  Run: `cd src/frontend && pnpm test`
  Expected：不少于 96 个，全部通过。

  Run: `git status --short`
  Expected：只有本任务涉及的文件。

- [ ] **Step 9: Commit**

  ```bash
  git add src/backend/MiniCc.Api/Dockerfile docker-compose.sqlite.yaml README.md CLAUDE.md
  git commit -m "feat(deploy): add SQLite compose deployment and docs"
  ```

## 复核与验收记录

2026-11-15 在 `feature/sqlite-support` 复验。分支基于 `cc22ab0`，API 镜像构建自 `c7e1397`，Playwright 用例版本为 `a810b4f`。本机 5000 端口被 macOS 控制中心占用，验收通过临时 Compose 覆盖文件将网关映射到 `localhost:15000`。

| 检查 | 实际结果 |
|---|---|
| 后端全量 `dotnet test test/backend/MiniCc.Api.Test` | Podman Testcontainers 环境下 456/456 通过。PostgreSQL 回归用例验证了解除标签关联和清理先提交时的恢复响应。 |
| `FullyQualifiedName~Sqlite\|FullyQualifiedName~Database` | 43/43 通过。已有数据库目录设为不可写后，启动失败，错误包含数据库完整路径。 |
| EF 迁移 | 两个上下文的 `migrations has-pending-model-changes` 均通过。PostgreSQL 迁移目录的变更只有新增的 `Migrations/Sqlite/` 子目录。 |
| Vitest `pnpm test` | 176/176 通过。 |
| SQLite Compose | `minicc-api`、`minicc-web`、`image-proxy` 和 `lb` 均运行。登录 200，保存 201，`/app/data/mini_cc.db` 的 UID/GID 为 `1654:1654`。执行 `down` 再 `up` 后，搜索返回相同文章 ID。 |
| SQLite Playwright | `reading-progress`、`reader-preferences` 和 `batch-trash` 使用仓库默认配置，在桌面和移动视口共 30/30 通过。 |
| PostgreSQL Compose | 在启用 SQLite 功能前写入验收文章。切换到新 API 镜像后，使用同一个 `pgdata` 卷登录返回 200。中文搜索命中原文章 ID。 |
| 手动 SQLite 启动 | 从 `git archive` 解包目录运行 `dotnet run`，以 `Database__Provider=sqlite` 选择 SQLite。生成项目目录下的 `data/mini_cc.db`，登录返回 200。 |

本次构建的 API 镜像为 `localhost/minicc-api:sqlite-review`，镜像 ID 为 `9584c246990bc7c012a6693a84a190bd60429ec1c5e5a9ad84e9b0adf3021cdf`，包含 ASP.NET Core 10.0.15。两套 Compose 的 API 冒烟均使用该镜像完成。可在本次使用的 Podman 连接中核对镜像和源码标签。

```bash
podman --connection podman-machine-default-root image inspect localhost/minicc-api:sqlite-review \
  --format '{{.ID}} {{index .Labels "org.opencontainers.image.revision"}}'
```

复核中将标签解除、恢复竞态、脚本权限与换行及 C# 14 状态筛选修复放进独立提交。两个测试提交分别按 .NET 10 的实际 HTTP 401 更新认证断言，以及等待桌面目录抽屉完成入场后再检查位置。高亮创建冒烟使用现有的 HTTP 200 契约。

EF Core SQLite 9.0.7 的传递依赖 `SQLitePCLRaw.lib.e_sqlite3` 2.1.10 仍触发 `NU1903`。[安全公告](https://github.com/advisories/GHSA-2m69-gcr7-jv3q)
