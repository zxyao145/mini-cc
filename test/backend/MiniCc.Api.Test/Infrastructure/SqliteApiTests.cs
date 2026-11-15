using System.Net;

namespace MiniCc.Api.Test.Infrastructure;

[Collection("Sqlite")]
public class SqliteApiTests(SqliteFixture fixture)
{
    [Fact]
    public async Task Api_StartsOnSqlite()
    {
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);

        using var response = await client.GetAsync("/api/tags");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
