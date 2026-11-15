using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniCc.Api.Authentication;
using MiniCc.Api.Core.ArticleNs.Application.Services;

namespace MiniCc.Api.Controllers;

[ApiController]
[Route("api/trash")]
[Authorize(AuthenticationSchemes = $"{CookieAuthenticationDefaults.AuthenticationScheme}, {ApiKeyAuthenticationSchemeOptions.DefaultScheme}")]
public class TrashController(TrashCleanup cleanup) : ControllerBase
{
    [HttpDelete]
    public async Task<IActionResult> EmptyTrash(CancellationToken cancellationToken) => Ok(new { purged = await cleanup.PurgeAsync(null, cancellationToken) });
}
