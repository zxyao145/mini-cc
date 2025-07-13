using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ApiKeys.Domain.AggregatesModel;
using MiniCc.Api.Core.ApiKeys.Infrastructure.Repositories;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.Services;
using MiniCc.Api.Core.ArticleNs.Infrastructure.Repositories;
using MiniCc.Api.Core.ArticleNs.Infrastructure.Service;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Core.TagNs.Infrastructure.Repositories;
using MiniCc.Api.Core.UserNs.Domain.AggregatesModel;
using MiniCc.Api.Core.UserNs.Infrastructure.Repositories;
using MiniCc.Api.Shared.Data;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbConnectionString = Environment.GetEnvironmentVariable("MiniCC_Db")
            ?? configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<MiniCcDbContext>(options =>
            options.UseNpgsql(dbConnectionString));
        services.AddScoped<DbSeeder>();


        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IArticleRepository, ArticleRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IHighlightRepository, HighlightRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();

        services.AddScoped<IArticleDomainService, ArticleDomainService>();
        services.AddScoped<IContentExtractionService, ContentExtractionService>();

        return services;
    }
}