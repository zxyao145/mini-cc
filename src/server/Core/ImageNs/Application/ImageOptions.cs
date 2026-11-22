namespace MiniCc.Api.Core.ImageNs.Application;

public enum ImageMode { Store, ImageProxy }

public enum ImageDelivery { Authenticated, Public }

public enum ImageStorageProvider { Local, S3 }

public class ImageOptions
{
    public ImageMode Mode { get; set; } = ImageMode.Store;
    public ImageDelivery Delivery { get; set; } = ImageDelivery.Authenticated;
    public string ApiUrl { get; set; } = "/api/images";
    public string PublicBaseUrl { get; set; } = "";
    public bool AllowPrivateNetworks { get; set; }
    public ImageStorageOptions Storage { get; set; } = new();
}

public class ImageStorageOptions
{
    internal const string DefaultLocalPath = "data/images";

    public ImageStorageProvider Provider { get; set; } = ImageStorageProvider.Local;
    public string LocalPath { get; set; } = DefaultLocalPath;
    public S3StorageOptions S3 { get; set; } = new();
}

public class S3StorageOptions
{
    public string ServiceUrl { get; set; } = "";
    public string Bucket { get; set; } = "";
    public string Region { get; set; } = "auto";
    public string AccessKeyId { get; set; } = "";
    public string SecretAccessKey { get; set; } = "";
}
