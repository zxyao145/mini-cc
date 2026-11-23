using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Shared.Data;

namespace MiniCc.Api.Authentication;

internal static class CookieSession
{
    public const string VersionClaim = "minicc:session-version";

    // Password changes generate a new salted hash, which also changes the session version.
    public static string GetVersion(string passwordHash) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)));

    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var version = principal?.FindFirstValue(VersionClaim);
        if (!string.IsNullOrEmpty(version) &&
            Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<MiniCcDbContext>();
            var passwordHash = await db.Users.Where(user => user.Id == userId)
                .Select(user => user.Password)
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

            if (passwordHash is not null && version == GetVersion(passwordHash))
                return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }
}
