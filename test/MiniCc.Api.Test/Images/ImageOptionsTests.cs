using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MiniCc.Api.Core;
using MiniCc.Api.Core.ImageNs.Application;

namespace MiniCc.Api.Test.Images;

public sealed class ImageOptionsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"minicc-images-options-{Guid.NewGuid():N}");

    [Fact]
    public void Defaults_AreStoreAuthenticatedLocal()
    {
        using var services = Services();
        services.GetRequiredService<IStartupValidator>().Validate();
        var value = services.GetRequiredService<IOptions<ImageOptions>>().Value;

        value.Mode.Should().Be(ImageMode.Store);
        value.Delivery.Should().Be(ImageDelivery.Authenticated);
        value.ApiUrl.Should().Be("/api/images");
        value.AllowPrivateNetworks.Should().BeFalse();
        value.Storage.Provider.Should().Be(ImageStorageProvider.Local);
        value.Storage.LocalPath.Should().Be("data/images");
        value.Storage.S3.Region.Should().Be("auto");
        Directory.Exists(Path.Combine(root, "data/images")).Should().BeTrue();
        Directory.EnumerateFiles(Path.Combine(root, "data/images")).Should().BeEmpty();
    }

    [Fact]
    public void ImageProxyMode_RequiresSignatureKey()
    {
        using var services = Services(new() { ["Images:Mode"] = "ImageProxy" });
        services.Invoking(s => s.GetRequiredService<IStartupValidator>().Validate())
            .Should().Throw<OptionsValidationException>().WithMessage("*ImageProxy:SignatureKey*");
    }

    [Fact]
    public void EnumValues_AreCaseInsensitive()
    {
        using var services = Services(new()
        {
            ["Images:Mode"] = "imageproxy", ["Images:Delivery"] = "PUBLIC",
            ["Images:Storage:Provider"] = "s3", ["ImageProxy:SignatureKey"] = "test",
        });
        services.GetRequiredService<IStartupValidator>().Validate();
        var value = services.GetRequiredService<IOptions<ImageOptions>>().Value;
        value.Mode.Should().Be(ImageMode.ImageProxy);
        value.Delivery.Should().Be(ImageDelivery.Public);
        value.Storage.Provider.Should().Be(ImageStorageProvider.S3);
    }

    [Theory]
    [InlineData("ServiceUrl")]
    [InlineData("Bucket")]
    [InlineData("AccessKeyId")]
    [InlineData("SecretAccessKey")]
    public void S3Provider_RequiresSetting(string missing)
    {
        var settings = S3Settings();
        settings.Remove("Images:Storage:S3:" + missing);
        using var services = Services(settings);
        services.Invoking(s => s.GetRequiredService<IStartupValidator>().Validate())
            .Should().Throw<OptionsValidationException>().WithMessage($"*Images:Storage:S3:{missing}*");
    }

    [Theory]
    [InlineData("Authenticated", "S3", "https://img.example.com")]
    [InlineData("Public", "Local", "https://img.example.com")]
    [InlineData("Public", "S3", "img.example.com")]
    [InlineData("Public", "S3", "ftp://img.example.com")]
    public void PublicBaseUrl_RejectsInvalidCombinations(string delivery, string provider, string url)
    {
        var settings = S3Settings();
        settings["Images:Delivery"] = delivery;
        settings["Images:Storage:Provider"] = provider;
        settings["Images:PublicBaseUrl"] = url;
        using var services = Services(settings);
        services.Invoking(s => s.GetRequiredService<IStartupValidator>().Validate())
            .Should().Throw<OptionsValidationException>().WithMessage("*Images:PublicBaseUrl*");
    }

    [Fact]
    public void LocalStore_UnwritableDirectory_FailsWithFullPath()
    {
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "file");
        File.WriteAllText(file, "");
        using var services = Services(new() { ["Images:Storage:LocalPath"] = "file" });

        services.Invoking(s => s.GetRequiredService<IStartupValidator>().Validate())
            .Should().Throw<OptionsValidationException>().WithMessage($"*Images:Storage:LocalPath*{file}*");
    }

    private ServiceProvider Services(Dictionary<string, string?>? values = null) =>
        new ServiceCollection()
            .AddInfrastructureServices(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), root)
            .BuildServiceProvider();

    private static Dictionary<string, string?> S3Settings() => new()
    {
        ["Images:Storage:Provider"] = "S3",
        ["Images:Storage:S3:ServiceUrl"] = "https://account.r2.cloudflarestorage.com",
        ["Images:Storage:S3:Bucket"] = "test",
        ["Images:Storage:S3:AccessKeyId"] = "test",
        ["Images:Storage:S3:SecretAccessKey"] = "test",
    };

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
