using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.FeedNs.Domain;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel.ValueObjects;

namespace MiniCc.Api.Test.Infrastructure;

public partial class LibraryApiTests
{
    [Fact]
    public async Task CreateTag_ReturnsCreatedTrimmedTagAndListsIt()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var suffix = Guid.NewGuid().ToString("N");
        var name = $"阅读 {suffix}";

        var response = await client.PostAsJsonAsync("/api/tags", new { name = $"  {name}  ", color = "#ef4444" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tag = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = tag.GetProperty("id").GetGuid();
        id.Should().NotBeEmpty();
        response.Headers.Location!.AbsolutePath.Should().Be($"/api/tags/{id}");
        tag.GetProperty("name").GetString().Should().Be(name);
        tag.GetProperty("color").GetString().Should().Be("#ef4444");
        tag.GetProperty("createdAt").GetDateTimeOffset().Should().NotBe(default);
        tag.GetProperty("articleCount").GetInt32().Should().Be(0);

        var listed = await client.GetFromJsonAsync<JsonElement>($"/api/tags?search={suffix}");
        listed.GetArrayLength().Should().Be(1);
        listed[0].GetProperty("id").GetGuid().Should().Be(id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateTag_DefaultsColorWhenOmittedOrBlank(bool sendBlank)
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var name = $"Default {Guid.NewGuid():N}";
        var response = sendBlank
            ? await client.PostAsJsonAsync("/api/tags", new { name, color = "  " })
            : await client.PostAsJsonAsync("/api/tags", new { name });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tag = await response.Content.ReadFromJsonAsync<JsonElement>();
        tag.GetProperty("color").GetString().Should().Be("#3B82F6");
    }

    [Fact]
    public async Task CreateTag_AcceptsHundredCharactersAfterTrim()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var name = new string('字', 68) + Guid.NewGuid().ToString("N");

        var response = await client.PostAsJsonAsync("/api/tags", new { name = $"  {name}  " });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tag = await response.Content.ReadFromJsonAsync<JsonElement>();
        tag.GetProperty("name").GetString().Should().Be(name);
    }

    [Fact]
    public async Task CreateTag_DuplicateNameReturnsConflictWithExistingIdButCaseVariantSucceeds()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var suffix = Guid.NewGuid().ToString("N");
        var name = $"Dup {suffix}";

        var first = await client.PostAsJsonAsync("/api/tags", new { name });
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await first.Content.ReadFromJsonAsync<JsonElement>();

        var duplicate = await client.PostAsJsonAsync("/api/tags", new { name = $"  {name}  " });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var conflict = await duplicate.Content.ReadFromJsonAsync<JsonElement>();
        conflict.GetProperty("existingTagId").GetGuid().Should().Be(created.GetProperty("id").GetGuid());

        var variant = await client.PostAsJsonAsync("/api/tags", new { name = $"dup {suffix}" });
        variant.StatusCode.Should().Be(HttpStatusCode.Created);
        await using var db = fixture.CreateContext();
        (await db.Tags.CountAsync(t => t.Name == name)).Should().Be(1);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("overlong", null)]
    [InlineData("ok", "red")]
    [InlineData("ok", "#12345")]
    [InlineData("ok", "#GGGGGG")]
    [InlineData("ok", "#123456\n")]
    public async Task CreateTag_InvalidInputReturnsBadRequest(string? name, string? color)
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var suffix = Guid.NewGuid().ToString("N");
        name = name switch
        {
            "ok" => $"Valid {suffix}",
            "overlong" => new string('x', 69) + suffix,
            _ => name
        };

        var response = await client.PostAsJsonAsync("/api/tags", new { name, color });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateTag_RequiresAuthentication()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var name = $"Unauthenticated {Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync("/api/tags", new { name });

        response.IsSuccessStatusCode.Should().BeFalse();
        response.StatusCode.Should().Be((await client.GetAsync("/api/tags")).StatusCode);
        await using var db = fixture.CreateContext();
        (await db.Tags.AnyAsync(t => t.Name == name)).Should().BeFalse();
    }

    [Fact]
    public async Task CreateTag_ConcurrentSameNameCreatesExactlyOne()
    {
        using var factory = CreateFactory();
        using var firstClient = AuthenticatedClient(factory);
        using var secondClient = AuthenticatedClient(factory);
        var name = $"Concurrent {Guid.NewGuid():N}";

        var responses = await SendPastNameLookupAsync(
            () => firstClient.PostAsJsonAsync("/api/tags", new { name }),
            () => secondClient.PostAsJsonAsync("/api/tags", new { name }));
        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.Conflict]);
        var created = await responses.Single(r => r.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<JsonElement>();
        var conflict = await responses.Single(r => r.StatusCode == HttpStatusCode.Conflict).Content.ReadFromJsonAsync<JsonElement>();
        conflict.GetProperty("existingTagId").GetGuid().Should().Be(created.GetProperty("id").GetGuid());
        await using var db = fixture.CreateContext();
        (await db.Tags.CountAsync(t => t.Name == name)).Should().Be(1);
    }

    [Fact]
    public async Task UpdateTag_RenamesTrimmedRecolorsAndKeepsArticles()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var tag = await SeedTagAsync("Before", "#3b82f6", tagged: true);
        var name = $"After-{Guid.NewGuid():N}";

        var response = await client.PutAsJsonAsync($"/api/tags/{tag.Id}", new { name = $"  {name}  ", color = "#10b981" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var listed = await client.GetFromJsonAsync<JsonElement>($"/api/tags?search={name}");
        listed.GetArrayLength().Should().Be(1);
        listed[0].GetProperty("id").GetGuid().Should().Be(tag.Id);
        listed[0].GetProperty("name").GetString().Should().Be(name);
        listed[0].GetProperty("color").GetString().Should().Be("#10b981");
        listed[0].GetProperty("articleCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task UpdateTag_KeepingTheNameChangesOnlyTheColor()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var tag = await SeedTagAsync("Same", "#3b82f6");

        var response = await client.PutAsJsonAsync($"/api/tags/{tag.Id}", new { name = tag.Name, color = "#ef4444" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using var db = fixture.CreateContext();
        var saved = await db.Tags.SingleAsync(t => t.Id == tag.Id);
        saved.Name.Should().Be(tag.Name);
        saved.Color.Value.Should().Be("#ef4444");
    }

    [Fact]
    public async Task UpdateTag_DuplicateNameReturnsConflictButCaseVariantSucceeds()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var taken = await SeedTagAsync("Taken", "#3b82f6");
        var tag = await SeedTagAsync("Renamed", "#ef4444");

        var conflict = await client.PutAsJsonAsync($"/api/tags/{tag.Id}", new { name = taken.Name, color = "#10b981" });

        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await conflict.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("existingTagId").GetGuid().Should().Be(taken.Id);
        await using (var db = fixture.CreateContext())
        {
            var unchanged = await db.Tags.SingleAsync(t => t.Id == tag.Id);
            unchanged.Name.Should().Be(tag.Name);
            unchanged.Color.Value.Should().Be("#ef4444");
        }

        var caseVariant = await client.PutAsJsonAsync($"/api/tags/{tag.Id}", new { name = taken.Name.ToUpperInvariant(), color = "#ef4444" });
        caseVariant.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData(null, "#10b981")]
    [InlineData("   ", "#10b981")]
    [InlineData("overlong", "#10b981")]
    [InlineData("ok", null)]
    [InlineData("ok", "")]
    [InlineData("ok", "red")]
    [InlineData("ok", "#GGGGGG")]
    public async Task UpdateTag_InvalidInputReturnsBadRequestAndKeepsTag(string? name, string? color)
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var tag = await SeedTagAsync("Invalid", "#ef4444");
        name = name switch
        {
            "ok" => $"Valid-{Guid.NewGuid():N}",
            "overlong" => new string('x', 101),
            _ => name
        };

        var response = await client.PutAsJsonAsync($"/api/tags/{tag.Id}", new { name, color });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
        await using var db = fixture.CreateContext();
        var unchanged = await db.Tags.SingleAsync(t => t.Id == tag.Id);
        unchanged.Name.Should().Be(tag.Name);
        unchanged.Color.Value.Should().Be("#ef4444");
    }

    [Fact]
    public async Task UpdateTag_UnknownTagReturnsNotFound()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);

        var response = await client.PutAsJsonAsync($"/api/tags/{Guid.NewGuid()}", new { name = $"Missing-{Guid.NewGuid():N}", color = "#10b981" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateTag_RequiresAuthentication()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var tag = await SeedTagAsync("Unauthenticated", "#ef4444");

        var response = await client.PutAsJsonAsync($"/api/tags/{tag.Id}", new { name = $"Changed-{Guid.NewGuid():N}", color = "#10b981" });

        response.IsSuccessStatusCode.Should().BeFalse();
        await using var db = fixture.CreateContext();
        var unchanged = await db.Tags.SingleAsync(t => t.Id == tag.Id);
        unchanged.Name.Should().Be(tag.Name);
        unchanged.Color.Value.Should().Be("#ef4444");
    }

    [Fact]
    public async Task UpdateTag_ConcurrentRenamesToTheSameNameRenameExactlyOne()
    {
        using var factory = CreateFactory();
        using var firstClient = AuthenticatedClient(factory);
        using var secondClient = AuthenticatedClient(factory);
        var first = await SeedTagAsync("First", "#3b82f6");
        var second = await SeedTagAsync("Second", "#3b82f6");
        var name = $"Concurrent-rename-{Guid.NewGuid():N}";

        var responses = await SendPastNameLookupAsync(
            () => firstClient.PutAsJsonAsync($"/api/tags/{first.Id}", new { name, color = "#3b82f6" }),
            () => secondClient.PutAsJsonAsync($"/api/tags/{second.Id}", new { name, color = "#3b82f6" }));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.NoContent, HttpStatusCode.Conflict]);
        var conflict = await responses.Single(r => r.StatusCode == HttpStatusCode.Conflict).Content.ReadFromJsonAsync<JsonElement>();
        await using var db = fixture.CreateContext();
        var renamed = await db.Tags.SingleAsync(t => t.Name == name);
        conflict.GetProperty("existingTagId").GetGuid().Should().Be(renamed.Id);
        (await db.Tags.CountAsync(t => t.Id == first.Id || t.Id == second.Id)).Should().Be(2);
    }

    private async Task<Tag> SeedTagAsync(string prefix, string color, bool tagged = false)
    {
        var tag = Tag.Create($"{prefix}-{Guid.NewGuid():N}", TagColor.Create(color));
        await using var seed = fixture.CreateContext();
        if (tagged)
        {
            var article = Article.Create(Url.Create($"https://example.com/{Guid.NewGuid():N}"), "Tagged", "", Content.Create("original", "readable"));
            article.AddTag(tag);
            seed.Articles.Add(article);
        }
        else
        {
            seed.Tags.Add(tag);
        }

        await seed.SaveChangesAsync();
        return tag;
    }

    // Holds a SHARE lock on "Tags" until both requests are waiting to write, so both pass the name lookup first.
    private async Task<HttpResponseMessage[]> SendPastNameLookupAsync(
        Func<Task<HttpResponseMessage>> first, Func<Task<HttpResponseMessage>> second)
    {
        await using var gate = fixture.CreateContext();
        await using var transaction = await gate.Database.BeginTransactionAsync();
        await gate.Database.ExecuteSqlRawAsync("""LOCK TABLE "Tags" IN SHARE MODE""");
        var pending = Task.WhenAll(first(), second());
        var waiting = 0;
        try
        {
            await using var monitor = fixture.CreateContext();
            for (var attempt = 0; attempt < 100 && waiting != 2; attempt++)
            {
                waiting = await monitor.Database.SqlQueryRaw<int>("""
                    SELECT COUNT(*)::int AS "Value" FROM pg_locks
                    WHERE relation = '"Tags"'::regclass AND mode = 'RowExclusiveLock' AND NOT granted
                    """).SingleAsync();
                if (waiting != 2) await Task.Delay(20);
            }
        }
        finally
        {
            await transaction.CommitAsync();
        }

        var responses = await pending;
        waiting.Should().Be(2, "both writes must have passed the initial duplicate lookup");
        return responses;
    }

    [Fact]
    public async Task RemoveTagFromArticle_RemovesTheAssociationAndKeepsTheTag()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var tag = Tag.Create($"Remove {Guid.NewGuid():N}", TagColor.Create("#ef4444"));
        var article = await fixture.SeedArticleAsync(a => a.AddTag(tag));

        using var response = await client.DeleteAsync($"/api/articles/{article.Id}/tags/{tag.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var db = fixture.CreateContext();
        var saved = await db.Articles.Include(a => a.Tags).SingleAsync(a => a.Id == article.Id);
        saved.Tags.Should().BeEmpty();
        (await db.Tags.AnyAsync(t => t.Id == tag.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteTag_RemovesActiveTrashedAndFeedAssociationsOnly()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var deletedTag = Tag.Create($"Delete {Guid.NewGuid():N}", TagColor.Create("#ef4444"));
        var keptTag = Tag.Create($"Keep {Guid.NewGuid():N}", TagColor.Create("#3b82f6"));
        var active = Article.Create(Url.Create($"https://example.com/{Guid.NewGuid():N}"), "Active", "", Content.Create("original", "readable"));
        active.AddTag(deletedTag);
        active.AddTag(keptTag);
        active.SetFavorite(true);
        var trashed = Article.Create(Url.Create($"https://example.com/{Guid.NewGuid():N}"), "Trashed", "", Content.Create("original", "readable"));
        trashed.AddTag(deletedTag);
        trashed.MoveToTrash(DateTimeOffset.UtcNow);
        var feed = Feed.Create($"https://example.com/{Guid.NewGuid():N}/feed", "Tagged feed", null, FeedFormat.Rss);
        feed.Tags.Add(deletedTag);
        await using (var seed = fixture.CreateContext())
        {
            seed.Articles.AddRange(active, trashed);
            seed.Feeds.Add(feed);
            await seed.SaveChangesAsync();
        }

        (await client.DeleteAsync($"/api/tags/{deletedTag.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var db = fixture.CreateContext();
        (await db.Tags.AnyAsync(t => t.Id == deletedTag.Id)).Should().BeFalse();
        (await db.Tags.AnyAsync(t => t.Id == keptTag.Id)).Should().BeTrue();
        var activeAfter = await db.Articles.IgnoreQueryFilters().Include(a => a.Tags).SingleAsync(a => a.Id == active.Id);
        activeAfter.Tags.Select(t => t.Id).Should().Equal(keptTag.Id);
        activeAfter.ReadableContent.Should().Be(active.ReadableContent);
        activeAfter.IsFavorite.Should().BeTrue();
        activeAfter.DeletedAt.Should().BeNull();
        var trashedAfter = await db.Articles.IgnoreQueryFilters().Include(a => a.Tags).SingleAsync(a => a.Id == trashed.Id);
        trashedAfter.DeletedAt.Should().NotBeNull();
        trashedAfter.ReadableContent.Should().Be(trashed.ReadableContent);
        trashedAfter.Tags.Should().BeEmpty();
        var feedAfter = await db.Feeds.Include(f => f.Tags).SingleAsync(f => f.Id == feed.Id);
        feedAfter.Tags.Should().BeEmpty();
    }
}
