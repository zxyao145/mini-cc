using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MiniCc.Api.Shared;

[ApiController]
[Route("/api/[controller]")]
public class ApiControllerBase : ControllerBase
{
    protected Guid? GetUserId()
    {
        if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
        {
            return null;
        }
        var id = User.FindFirst(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }
        return Guid.Parse(id);
    }
}
