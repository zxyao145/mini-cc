using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniCc.Api.Core.UserNs.Application.Commands;
using MiniCc.Api.Core.UserNs.Application.DTOs;
using MiniCc.Api.Infra;
using MiniCc.Api.Shared;
using System.Security.Claims;

namespace MiniCc.Api.Controllers.UserNs;

[Authorize]
[Route("/api/[controller]")]
public class AccountController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly ISignInService _signInService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IMediator mediator, ILogger<AccountController> logger, ISignInService signInService)
    {
        _mediator = mediator;
        _logger = logger;
        _signInService = signInService;
    }

    // 获取当前用户信息
    [AllowAnonymous]
    [HttpGet("current")]
    public IActionResult Current()
    {
        var userInfo = new UserInfo
        {
            UserName = User.Identity?.Name ?? "",
            IsAuthenticated = User.Identity?.IsAuthenticated ?? false
        };

        return Ok(userInfo);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult> LoginAsync([FromForm] UserLoginCommand command)
    {
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            _logger.LogError("Error updatingLoginAsync: {Error}", result.Error);
            return BadRequest(result.Error);
        }

        if (result.Value != LoginResult.Success)
        {
            ModelState.AddModelError("", "用户名或密码错误");
            return BadRequest(ModelState);
        }
        return Ok();
    }

    [HttpPost("logout")]
    public async Task<ActionResult> Logout()
    {
        await _signInService.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok();
    }

    // 更新用户名
    [HttpPut("username")]
    public async Task<ActionResult> UpdateUserName([FromBody] UpdateUserNameCommand command)
    {
        var userName = User.Identity?.Name ?? "";
        command.OldUserName = userName;

        var isPersistent = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.IsPersistent)?.Value;
        if (string.IsNullOrWhiteSpace(isPersistent))
        {
            command.IsPersistent = false;
        }
        else
        {
            command.IsPersistent = bool.Parse(isPersistent);
        }
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(new { message = result.Error });
        }

        return Ok(new { message = "用户名更新成功" });
    }

    // 更新密码
    [HttpPut("password")]
    public async Task<ActionResult<UserDto>> UpdatePassword([FromBody] UpdatePasswordCommand request)
    {
        var userName = User.Identity?.Name ?? "";
        request.UserName = userName;
        var result = await _mediator.Send(request);
        if (result.IsFailure)
        {
            _logger.LogError("Error updating password: {Error}", result.Error);
            return BadRequest(result.Error);
        }

        return Ok(new { message = "密码更新成功" });
    }
}