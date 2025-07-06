using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniCc.Api.Services;

namespace MiniCc.Api.Controllers;



[Authorize]
[ApiController]
[Route("api/[controller]/[action]")]
public class AccessKeyController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly IAccessKeyService _accessKeyService;

    public AccessKeyController(IAccountService accountService, IAccessKeyService accessKeyService)
    {
        _accountService = accountService;
        _accessKeyService = accessKeyService;
    }

    [HttpGet]
    public async Task<ActionResult> List()
    {
        var userName = User.Identity?.Name ?? "";
        var user = await _accountService.FindByUserName(userName);
        if(user == null)
        {
            return Unauthorized("用户未找到");
        }

        var keys = await _accessKeyService.List(user.Id);
        return Ok(keys);
    }
}
