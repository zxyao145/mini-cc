using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MiniCc.Api.Authentication;
using MiniCc.Api.Shared.Config;

namespace MiniCc.Api.Controllers;

[Authorize(AuthenticationSchemes =
    $"{CookieAuthenticationDefaults.AuthenticationScheme}, {ApiKeyAuthenticationSchemeOptions.DefaultScheme}")]
[ApiController]
[Route("api/settings")]
public class SettingsController(IOptions<LibraryOptions> options) : ControllerBase
{
    [HttpGet("library")]
    public ActionResult<LibraryOptions> GetLibrary() => Ok(options.Value);
}
