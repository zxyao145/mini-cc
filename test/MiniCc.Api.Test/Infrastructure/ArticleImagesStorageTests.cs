using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;

namespace MiniCc.Api.Test.Infrastructure;

[Collection("Sqlite")]
public class SqliteArticleImagesStorageTests(SqliteFixture fixture)
{
    private const string Url = "https://img.test/a.png";

    [Fact]
    public async Task Sqlite_RoundTripsImagesAndDetectsInPlaceChanges()
    {
        var now = DateTimeOffset.UtcNow;
        var seeded = await fixture.SeedArticleAsync(article => article.SyncImages([Url], resetFailed: false, now));
        var done = new ArticleImage(ArticleImageState.Done, Hash: new string('a', 64), Ext: "png");

        await using (var db = fixture.CreateContext())
        {
            var json = await db.Database.SqlQuery<string>(
                $"""SELECT "Images" AS "Value" FROM "Articles" WHERE "Id" = {seeded.Id}""").SingleAsync();
            json.Should().Contain("\"next\":");
            var article = await db.Articles.SingleAsync(a => a.Id == seeded.Id);
            article.Images.Should().BeEquivalentTo(seeded.Images);
            article.ImagesDueAt.Should().Be(now);
            article.Images![Url] = done;
            await db.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext();
        var reloaded = await read.Articles.SingleAsync(a => a.Id == seeded.Id);
        reloaded.Images![Url].Should().Be(done);
    }

    [Fact]
    public async Task MigratedLegacyRow_HasNullImages()
    {
        var seeded = await fixture.SeedArticleAsync();
        await using var db = fixture.CreateContext();
        await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Articles" SET "Images" = NULL WHERE "Id" = {seeded.Id}""");

        (await db.Articles.SingleAsync(a => a.Id == seeded.Id)).Images.Should().BeNull();
    }
}

[Collection("Postgres")]
public class PostgresArticleImagesStorageTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Postgres_RoundTripsImagesAsJsonb()
    {
        const string url = "https://img.test/b.png";
        var article = await fixture.SeedArticleAsync(a => a.SyncImages([url], resetFailed: false, DateTimeOffset.UtcNow));
        await using var db = fixture.CreateContext();
        var loaded = await db.Articles.SingleAsync(a => a.Id == article.Id);

        loaded.Images.Should().BeEquivalentTo(article.Images);
        var type = await db.Database.SqlQuery<string>(
            $"""SELECT pg_typeof("Images")::text AS "Value" FROM "Articles" WHERE "Id" = {article.Id}""").SingleAsync();
        type.Should().Be("jsonb");
    }
}
