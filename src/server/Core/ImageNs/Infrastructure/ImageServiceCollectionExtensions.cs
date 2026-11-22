using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using MiniCc.Api.Authentication;
using MiniCc.Api.Core.ImageNs.Application;
using MiniCc.Api.Core.ImageNs.Infrastructure.Download;
using MiniCc.Api.Core.ImageNs.Infrastructure.Persistence;
using MiniCc.Api.Core.ImageNs.Infrastructure.Storage;
using MiniCc.Api.Core.ImageNs.Infrastructure.Worker;
using MiniCc.Api.Infra;

namespace MiniCc.Api.Core.ImageNs.Infrastructure;

public static class ImageServiceCollectionExtensions
{
    public static IServiceCollection AddImageServices(this IServiceCollection services, IConfiguration configuration,
        string contentRootPath)
    {
        services.AddOptions<ImageOptions>().Bind(configuration.GetSection("Images")).ValidateOnStart();
        services.AddSingleton<IValidateOptions<ImageOptions>>(new ImageOptionsValidator(contentRootPath));

        services.AddOptions<ImageProxyOptions>()
            .Bind(configuration.GetSection("ImageProxy"))
            .Validate<IOptions<ImageOptions>>((proxy, images) =>
                images.Value.Mode != ImageMode.ImageProxy || !string.IsNullOrWhiteSpace(proxy.SignatureKey),
                "ImageProxy:SignatureKey is required when Images:Mode is ImageProxy.")
            .ValidateOnStart();
        services.AddSingleton<ImageProxyService>();
        services.AddSingleton<LegacyImageUrls>();
        services.AddSingleton<ImageUrlResolver>();

        services.AddSingleton<IAmazonS3>(provider =>
        {
            var s3 = provider.GetRequiredService<IOptions<ImageOptions>>().Value.Storage.S3;
            return new AmazonS3Client(new BasicAWSCredentials(s3.AccessKeyId, s3.SecretAccessKey), new AmazonS3Config
            {
                ServiceURL = s3.ServiceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = s3.Region,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            });
        });

        services.AddSingleton<IImageStore>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<ImageOptions>>().Value;
            if (options.Mode == ImageMode.Store && options.Storage.Provider == ImageStorageProvider.S3)
                return new S3ImageStore(provider.GetRequiredService<IAmazonS3>(), options.Storage.S3.Bucket);

            var path = options.Mode == ImageMode.Store ? options.Storage.LocalPath : ImageStorageOptions.DefaultLocalPath;
            return new LocalImageStore(Path.GetFullPath(path, contentRootPath));
        });

        services.AddHttpClient("images", client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(provider => ImageDownloader.CreateHandler(
                provider.GetRequiredService<IOptions<ImageOptions>>().Value.AllowPrivateNetworks));
        services.AddSingleton<IImageDownloader, ImageDownloader>();
        services.AddScoped<IImageArticleRows, ImageArticleRows>();
        services.AddScoped<ImageWorkService>();
        services.AddScoped<ImageCollector>();
        services.AddHostedService<ImageDownloadWorker>();

        services.AddOptions<AuthorizationOptions>().Configure<IOptions<ImageOptions>>((authorization, images) =>
            authorization.AddPolicy(ImagePolicies.Read, policy =>
            {
                var options = images.Value;
                if (options.Mode == ImageMode.ImageProxy || options.Delivery == ImageDelivery.Public)
                    policy.RequireAssertion(_ => true);
                else
                    policy.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme,
                        ApiKeyAuthenticationSchemeOptions.DefaultScheme).RequireAuthenticatedUser();
            }));

        return services;
    }
}
