using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using MiniCc.Api.Core.ImageNs.Application;
using MiniCc.Api.Core.ImageNs.Domain;
using MiniCc.Api.Core.ImageNs.Infrastructure;

namespace MiniCc.Api.Controllers;

[ApiController]
[Route("api/images")]
[Authorize(Policy = ImagePolicies.Read)]
public sealed class ImagesController(IImageStore store, IOptions<ImageOptions> options) : ControllerBase
{
    public const string ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";

    [HttpGet("{name}")]
    public async Task<IActionResult> Get(string name, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (settings.Mode == ImageMode.ImageProxy || !ImageKeys.TryParseName(name, out var hash, out var ext))
            return NotFound();

        var stream = await store.OpenReadAsync(ImageKeys.For(hash, ext), cancellationToken);
        if (stream is null)
            return NotFound();

        Response.Headers[HeaderNames.CacheControl] = settings.Delivery == ImageDelivery.Public
            ? "public, max-age=31536000, immutable"
            : "private, max-age=31536000, immutable";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = ContentSecurityPolicy;

        return File(stream, ImageKeys.ContentType(ext), lastModified: null,
            entityTag: new EntityTagHeaderValue($"\"{hash}\""));
    }
}
