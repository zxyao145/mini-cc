using ContentHandler.WebSite;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
