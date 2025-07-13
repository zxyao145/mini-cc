using FluentValidation;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using MiniCc.Api.Shared.Behaviors;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MiniCc.Api.Componments;

public static class DependencyInjection
{
    public static IServiceCollection AddMapping(this IServiceCollection services, Action<TypeAdapterConfig>? options = null)
    {
        var config = new TypeAdapterConfig();
        config.Scan(Assembly.GetExecutingAssembly());

        options?.Invoke(config);
        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();
        return services;
    }




    public static IServiceCollection AddValidators(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        return services;
    }


    public static IServiceCollection AddMediator(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });
        return services;
    }
}
