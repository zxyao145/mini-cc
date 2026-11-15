using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniCc.Api.Controllers.ArticleNs;
using MiniCc.Api.Core.ApiKeys.Application.Services;
using MiniCc.Api.Infra;
using MiniCc.Api.Shared.Data;

namespace MiniCc.Api.Test.Infrastructure;

public static class SqliteApi
{
    public static WebApplicationFactory<ArticlesController> CreateFactory(SqliteFixture fixture) =>
        new WebApplicationFactory<ArticlesController>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ImageProxy:SignatureKey"] = "test-signing-key" }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<MiniCcDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MiniCcDbContext>>();
                services.RemoveAll<MiniCcDbContext>();
                services.AddDbContext<MiniCcDbContext, SqliteMiniCcDbContext>(options => options.UseSqlite(fixture.ConnectionString));

                services.RemoveAll<IEncryptionService>();
                services.AddSingleton(SqliteFixture.CreateEncryptionService());

                var keys = new Mock<IApiKeyService>();
                keys.Setup(k => k.ValidateAsync("test-api-key")).ReturnsAsync(true);
                services.RemoveAll<IApiKeyService>();
                services.AddSingleton(keys.Object);
            });
        });

    public static HttpClient AuthenticatedClient(WebApplicationFactory<ArticlesController> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
        return client;
    }
}
