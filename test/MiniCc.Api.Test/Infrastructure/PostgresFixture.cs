using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Infra;
using MiniCc.Api.Shared.Data;
using Testcontainers.PostgreSql;

namespace MiniCc.Api.Test.Infrastructure;

[CollectionDefinition("Postgres")]
public class PostgresCollection : ICollectionFixture<PostgresFixture>;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("zhparser/zhparser:bookworm-16")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public MiniCcDbContext CreateContext(string? connectionString = null) => new(
        new DbContextOptionsBuilder<MiniCcDbContext>().UseNpgsql(connectionString ?? ConnectionString).Options,
        new AesEncryptionService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AesKeys:0:Id"] = "test",
            ["AesKeys:0:Key"] = Convert.ToBase64String(new byte[32]),
            ["AesKeys:0:IV"] = Convert.ToBase64String(new byte[16])
        }).Build()));

    public async Task InitializeAsync()
    {
        using var startup = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await _container.StartAsync(startup.Token);
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

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
