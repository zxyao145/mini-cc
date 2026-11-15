using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Infra;
using MiniCc.Api.Shared.Data;

namespace MiniCc.Api.Test.Infrastructure;

[CollectionDefinition("Sqlite")]
public class SqliteCollection : ICollectionFixture<SqliteFixture>;

public sealed class SqliteFixture : IAsyncLifetime
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"minicc-sqlite-{Guid.NewGuid():N}");

    public string ConnectionString => $"Data Source={Path.Combine(directory, "mini_cc.db")}";

    public SqliteMiniCcDbContext CreateContext(string? connectionString = null) => new(
        new DbContextOptionsBuilder<SqliteMiniCcDbContext>().UseSqlite(connectionString ?? ConnectionString).Options,
        CreateEncryptionService());

    internal static IEncryptionService CreateEncryptionService() =>
        new AesEncryptionService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AesKeys:0:Id"] = "test",
            ["AesKeys:0:Key"] = Convert.ToBase64String(new byte[32]),
            ["AesKeys:0:IV"] = Convert.ToBase64String(new byte[16])
        }).Build());

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await new DbSeeder(db).InitAsync();
    }

    public async Task<Article> SeedArticleAsync(Action<Article>? configure = null)
    {
        var article = Article.Create(Url.Create($"https://example.com/{Guid.NewGuid()}"), "Title", "",
            Content.Create("original", "readable"));
        configure?.Invoke(article);
        await using var db = CreateContext();
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        return article;
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
        return Task.CompletedTask;
    }
}
