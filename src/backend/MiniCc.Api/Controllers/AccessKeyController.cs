using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniCc.Api.Services;
using System.ComponentModel.DataAnnotations;

namespace MiniCc.Api.Controllers;

public class CreateAccessKeyRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "名称长度必须在1-50字符之间")]
    public string Name { get; set; } = "";
    
    public DateTimeOffset? ExpiredTime { get; set; }
}

public class UpdateAccessKeyRequest
{
    [Required]
    public Guid Id { get; set; }
    
    [Required]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "名称长度必须在1-50字符之间")]
    public string Name { get; set; } = "";
    
    public DateTimeOffset? ExpiredTime { get; set; }
    
    public bool Disabled { get; set; }
}

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

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateAccessKeyRequest request)
    {
        var userName = User.Identity?.Name ?? "";
        var user = await _accountService.FindByUserName(userName);
        if (user == null)
        {
            return Unauthorized("用户未找到");
        }

        var result = await _accessKeyService.CreateAsync(user.Id, request.Name, request.ExpiredTime);
        if (!result.Success)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        return Ok(result.AccessKey);
    }

    [HttpPut]
    public async Task<ActionResult> Update([FromBody] UpdateAccessKeyRequest request)
    {
        var userName = User.Identity?.Name ?? "";
        var user = await _accountService.FindByUserName(userName);
        if (user == null)
        {
            return Unauthorized("用户未找到");
        }

        var result = await _accessKeyService.UpdateAsync(user.Id, request.Id, request.Name, request.ExpiredTime, request.Disabled);
        if (!result.Success)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        return Ok(new { message = "Access Key 更新成功" });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var userName = User.Identity?.Name ?? "";
        var user = await _accountService.FindByUserName(userName);
        if (user == null)
        {
            return Unauthorized("用户未找到");
        }

        var result = await _accessKeyService.DeleteAsync(user.Id, id);
        if (!result.Success)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        return Ok(new { message = "Access Key 删除成功" });
    }
}
