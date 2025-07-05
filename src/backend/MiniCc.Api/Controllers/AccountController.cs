using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace MiniCc.Api.Controllers;


public class UserLoginCommand
{
    [Required]
    public string Username { get; set; } = "";
    [Required]
    public string Password { get; set; } = "";

    /// <summary>
    /// 记住我选项
    /// </summary>
    public bool RememberMe { get; set; } = true;
}


[ApiController]
[Route("api/[controller]/[action]")]
public class AccountController : ControllerBase
{
    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult> LoginAsync([FromForm] UserLoginCommand command)
    {
        var userName = Environment.GetEnvironmentVariable("MiniCC_UserName") ?? "demo";
        var pwd = Environment.GetEnvironmentVariable("MiniCC_Password") ?? "demo_password";

        if (command.Username != userName || command.Password != pwd)
        {
            ModelState.AddModelError("", "用户名或密码错误");

            return BadRequest();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, userName),
            new Claim(ClaimTypes.NameIdentifier, userName),
        };

        var claimsIdentity = new ClaimsIdentity(claims);
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
}
