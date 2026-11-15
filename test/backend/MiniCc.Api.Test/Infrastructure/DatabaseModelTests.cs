using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.UserNs.Domain.AggregatesModel;
using MiniCc.Api.Infra;
using MiniCc.Api.Shared.Data;

namespace MiniCc.Api.Test.Infrastructure;

public class DatabaseModelTests
{
    [Fact]
    public void PostgresModel_MatchesExistingSnapshot()
    {
        using var db = new MiniCcDbContext(new DbContextOptionsBuilder<MiniCcDbContext>()
            .UseNpgsql("Host=localhost;Database=unused").EnableServiceProviderCaching(false).Options,
            Mock.Of<IEncryptionService>());

        db.Database.HasPendingModelChanges().Should().BeFalse();
    }

    [Fact]
    public void SqliteModel_MatchesSqliteSnapshot()
    {
        using var db = Sqlite();
        db.Database.HasPendingModelChanges().Should().BeFalse();
    }

    [Fact]
    public void SqliteModel_HasNoPostgresSpecifics()
    {
        using var db = Sqlite();
        var model = db.GetService<IDesignTimeModel>().Model;
        var article = model.FindEntityType(typeof(Article))!;
        article.FindProperty(nameof(Article.SearchVector)).Should().BeNull();
        model.FindEntityType(typeof(User))!.FindProperty(nameof(User.ReaderPreferences))!.GetColumnType().Should().Be("TEXT");
        article.GetIndexes().Select(i => i.GetDatabaseName()).Should().Contain(
            ["IX_Articles_NormalizedUrl", "IX_Articles_LastReadAt", "IX_Articles_PublishedAt"]);

        var entities = model.GetEntityTypes().ToArray();
        IEnumerable<IReadOnlyAnnotatable> annotatables = [model, .. entities,
            .. entities.SelectMany(e => e.GetProperties()), .. entities.SelectMany(e => e.GetIndexes())];
        annotatables.SelectMany(a => a.GetAnnotations()).Select(a => a.Name)
            .Should().NotContain(name => name.StartsWith("Npgsql:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SqliteMigration_StoresTimesAsUtcTicks()
    {
        var path = Path.Combine(Path.GetTempPath(), $"minicc-model-{Guid.NewGuid():N}.db");
        var created = new DateTimeOffset(2026, 11, 15, 8, 0, 0, TimeSpan.FromHours(8)).AddTicks(1234567);
        var article = Article.Create(Url.Create("https://example.com/model"), "Title", "", Content.Create("original", "readable"));
        try
        {
            await using (var db = Sqlite(path))
            {
                await db.Database.MigrateAsync();
                db.Articles.Add(article);
                await db.SaveChangesAsync();
                await db.Articles.Where(a => a.Id == article.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.CreatedAt, created));
            }

            await using var read = Sqlite(path);
            var loaded = await read.Articles.SingleAsync();
            loaded.CreatedAt.Offset.Should().Be(TimeSpan.Zero);
            loaded.CreatedAt.UtcTicks.Should().Be(created.UtcTicks);
            await read.Database.OpenConnectionAsync();
            await using var query = read.Database.GetDbConnection().CreateCommand();
            query.CommandText = """SELECT typeof("CreatedAt") FROM "Articles" """;
            (await query.ExecuteScalarAsync()).Should().Be("integer");

            var lowerBound = created.AddTicks(-1);
            var ids = await read.Articles.Where(a => a.CreatedAt > lowerBound)
                .OrderByDescending(a => a.CreatedAt).Select(a => a.Id).ToListAsync();
            ids.Should().Equal(article.Id);
        }
        finally
        {
            using var connection = new SqliteConnection($"Data Source={path}");
            SqliteConnection.ClearPool(connection);
            File.Delete(path);
        }
    }

    private static SqliteMiniCcDbContext Sqlite(string dataSource = ":memory:") => new(
        new DbContextOptionsBuilder<SqliteMiniCcDbContext>()
            .UseSqlite($"Data Source={dataSource}").EnableServiceProviderCaching(false).Options,
        Mock.Of<IEncryptionService>());
}
