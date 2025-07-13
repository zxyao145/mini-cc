using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace MiniCc.Api.Infra;

public class SignInUser
{
    public string NameIdentifier { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsPersistent { get; set; } 
}

public interface ISignInService
{
    Task SignInAsync(SignInUser user);
    Task SignOutAsync(string scheme = CookieAuthenticationDefaults.AuthenticationScheme);
}

public class SignInService : ISignInService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SignInService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task SignInAsync(SignInUser user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.NameIdentifier, user.NameIdentifier),
            new Claim(ClaimTypes.IsPersistent,  user.IsPersistent + ""),
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = user.IsPersistent,
        };

        await _httpContextAccessor.HttpContext!
            .SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );
    }

    public async Task SignOutAsync(string scheme = CookieAuthenticationDefaults.AuthenticationScheme)
    {
        await _httpContextAccessor.HttpContext!.SignOutAsync(scheme);
    }
}
