using Microsoft.Extensions.Options;
using MiniCc.Api.Core.ImageNs.Application;
using MiniCc.Api.Core.ImageNs.Domain;

namespace MiniCc.Api.Core.ImageNs.Infrastructure;

public sealed class ImageOptionsValidator(string contentRootPath) : IValidateOptions<ImageOptions>
{
    public ValidateOptionsResult Validate(string? name, ImageOptions options)
    {
        var errors = new List<string>();
        if (!Enum.IsDefined(options.Mode)) errors.Add("Images:Mode must be Store or ImageProxy.");
        if (!Enum.IsDefined(options.Delivery)) errors.Add("Images:Delivery must be Authenticated or Public.");
        if (!Enum.IsDefined(options.Storage.Provider)) errors.Add("Images:Storage:Provider must be Local or S3.");

        if (!string.IsNullOrEmpty(options.PublicBaseUrl) &&
            (options.Delivery != ImageDelivery.Public || options.Storage.Provider != ImageStorageProvider.S3 ||
             !ImageHtml.TryGetHttpUri(options.PublicBaseUrl, out _)))
        {
            errors.Add("Images:PublicBaseUrl must be an absolute HTTP(S) URL and requires Public delivery with S3 storage.");
        }

        if (options.Mode == ImageMode.Store)
        {
            if (options.Storage.Provider == ImageStorageProvider.S3)
            {
                var s3 = options.Storage.S3;
                Require(s3.ServiceUrl, nameof(s3.ServiceUrl));
                Require(s3.Bucket, nameof(s3.Bucket));
                Require(s3.AccessKeyId, nameof(s3.AccessKeyId));
                Require(s3.SecretAccessKey, nameof(s3.SecretAccessKey));
            }
            else if (options.Storage.Provider == ImageStorageProvider.Local && errors.Count == 0)
            {
                var path = Path.Combine(contentRootPath, options.Storage.LocalPath);
                try
                {
                    path = Path.GetFullPath(path);
                    Directory.CreateDirectory(path);
                    using var probe = new FileStream(Path.Combine(path, $".{Guid.NewGuid():N}.tmp"),
                        FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.DeleteOnClose);
                    probe.WriteByte(0);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    errors.Add($"Images:Storage:LocalPath '{path}' is not writable. {ex.Message}");
                }
            }
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);

        void Require(string value, string setting)
        {
            if (string.IsNullOrWhiteSpace(value))
                errors.Add($"Images:Storage:S3:{setting} is required for S3 image storage.");
        }
    }
}
