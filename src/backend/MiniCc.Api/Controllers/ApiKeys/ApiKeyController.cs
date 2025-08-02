using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniCc.Api.Core.ApiKeys.Application.Commands;
using MiniCc.Api.Core.ApiKeys.Application.Services;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Controllers.ApiKeys;

[Authorize]
[Route("/api/[controller]/[action]")]
public class ApiKeyController : ApiControllerBase
{
    private readonly IApiKeyService _apiKeyService;

    public ApiKeyController(IApiKeyService apiKeyService)
    {
        _apiKeyService = apiKeyService;
    }

    [HttpGet]
    public async Task<ActionResult> List()
    {
        var uid = GetUserId()!;
        var keys = await _apiKeyService.List(uid.Value);
        return Ok(keys);
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateApiKeyRequest request)
    {
        var uid = GetUserId()!;
        request.UserId = uid.Value;

        var result = await _apiKeyService.CreateAsync(request);
        if (!result.Success)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        return Ok(result.ApiKey);
    }

    [HttpPut]
    public async Task<ActionResult> Update([FromBody] UpdateApiKeyRequest request)
    {
        var uid = GetUserId()!;
        request.UserId = uid.Value;

        var result = await _apiKeyService.UpdateAsync(request);
        if (!result.Success)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        return Ok(new { message = "Api Key 更新成功" });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var uid = GetUserId()!;

        var req = new DeleteApiKeyRequest
        {
            Id = id,
            UserId = uid.Value
        };

        var result = await _apiKeyService.DeleteAsync(req);
        if (!result.Success)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        return Ok(new { message = "Api Key 删除成功" });
    }
}