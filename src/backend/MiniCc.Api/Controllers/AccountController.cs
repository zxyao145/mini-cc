using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniCc.Api.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace MiniCc.Api.Controllers;



public class UserInfo
{
    public string UserName { get; set; } = "";
    public bool IsAuthenticated { get; set; }
}


[ApiController]
[Route("api/[controller]/[action]")]
public class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult> LoginAsync([FromForm] UserLoginCommand command)
    {
        var userName = Environment.GetEnvironmentVariable("MiniCC_UserName") ?? "demo";
        var pwd = Environment.GetEnvironmentVariable("MiniCC_Password") ?? "demo_password";

        var loginResult = await _accountService.LoginAsync(command);
        if (loginResult == LoginResult.Fail )
        {
            ModelState.AddModelError("", "用户名或密码错误");
            return BadRequest(ModelState);
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, userName),
            new Claim(ClaimTypes.NameIdentifier, userName),
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = command.RememberMe,
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity), authProperties);

        // Simulate a successful login
        Response.StatusCode = 200; // OK
        return Ok();
    }


    [Authorize]
    [HttpPost]
    public async Task<ActionResult> LoginOut()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok();
    }

    // 获取当前用户信息
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Current()
    {
        var userInfo = new UserInfo
        {
            UserName = User.Identity?.Name ?? "",
            IsAuthenticated = User.Identity?.IsAuthenticated ?? false
        };

        return Ok(userInfo);
    }

}
