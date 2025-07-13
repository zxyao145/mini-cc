using ContentHandler.WebSite;
using Microsoft.Extensions.DependencyInjection;

namespace ContentHandler;

public static class ServiceExtension
{
    public static IServiceCollection AddContentHandlers(this IServiceCollection services)
    {
        services.AddScoped<ContentHandlerBase, DefaultHandler>();
        services.AddScoped<IContentFetchService, ContentFetchService>();
        return services;
    }
}