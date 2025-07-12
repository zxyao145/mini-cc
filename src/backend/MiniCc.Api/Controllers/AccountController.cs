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

public class UpdateUserRequest
{
    [Required]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "用户名长度必须在2-20字符之间")]
    public string UserName { get; set; } = "";
}

public class UpdatePasswordRequest
{
    [Required]
    [StringLength(20, MinimumLength = 6, ErrorMessage = "密码长度必须在6-20字符之间")]
    public string CurrentPassword { get; set; } = "";
    
    [Required]
    [StringLength(20, MinimumLength = 6, ErrorMessage = "密码长度必须在6-20字符之间")]
    public string NewPassword { get; set; } = "";
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
        var loginResult = await _accountService.LoginAsync(command);
        if (loginResult != LoginResult.Success)
        {
            ModelState.AddModelError("", "用户名或密码错误");
            return BadRequest(ModelState);
        }
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

    // 更新用户名
    [Authorize]
    [HttpPut]
    public async Task<ActionResult> UpdateUserName([FromBody] UpdateUserRequest request)
    {
        var userName = User.Identity?.Name ?? "";
        var result = await _accountService.UpdateUserNameAsync(userName, request.UserName);
        
        if (!result.Success)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        return Ok(new { message = "用户名更新成功" });
    }

    // 更新密码
    [Authorize]
    [HttpPut]
    public async Task<ActionResult> UpdatePassword([FromBody] UpdatePasswordRequest request)
    {
        var userName = User.Identity?.Name ?? "";
        var result = await _accountService.UpdatePasswordAsync(userName, request.CurrentPassword, request.NewPassword);
        
        if (!result.Success)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        return Ok(new { message = "密码更新成功" });
    }

}
