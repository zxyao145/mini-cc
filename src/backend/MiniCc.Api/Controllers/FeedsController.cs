using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniCc.Api.Authentication;
using MiniCc.Api.Core.FeedNs.Application;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Controllers;

[Authorize(AuthenticationSchemes = $"{CookieAuthenticationDefaults.AuthenticationScheme}, {ApiKeyAuthenticationSchemeOptions.DefaultScheme}")]
public class FeedsController(IMediator mediator) : ApiControllerBase
{
    [HttpPost("preview")]
    public async Task<IActionResult> Preview(PreviewFeed request, CancellationToken ct)
    { var result = await mediator.Send(request, ct); return result.IsSuccess ? Ok(result.Value) : UnprocessableEntity(new { error = result.Error }); }
    [HttpPost]
    public async Task<IActionResult> Create(CreateFeed request, CancellationToken ct)
    {
        var validation = new CreateFeedValidator().Validate(request);
        if (!validation.IsValid) return BadRequest(new { error = validation.ToString() });
        var result = await mediator.Send(request, ct);
        if (result.IsFailure) return UnprocessableEntity(new { error = result.Error });
        if (result.Value.ExistingFeedId.HasValue) return Conflict(new { existingFeedId = result.Value.ExistingFeedId.Value });
        return StatusCode(201, new { feed = result.Value.Feed, imported = result.Value.Imported });
    }
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) { var result = await mediator.Send(new ListFeeds(), ct); return Ok(result.Value); }
    public record EditRequest(string? Title, Guid[]? TagIds, bool? Paused);
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, EditRequest request, CancellationToken ct)
    {
        var command = new EditFeed(id, request.Title, request.TagIds, request.Paused);
        var validation = new EditFeedValidator().Validate(command);
        if (!validation.IsValid) return BadRequest(new { error = validation.ToString() });
        var result = await mediator.Send(command, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return result.Value == null ? NotFound() : Ok(result.Value);
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    { var result = await mediator.Send(new UnsubscribeFeed(id), ct); return result.Value ? NoContent() : NotFound(); }
    [HttpPost("{id:guid}/refresh")]
    public async Task<IActionResult> Refresh(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new RefreshFeed(id), ct);
        if (result.IsFailure) return UnprocessableEntity(new { error = result.Error });
        return result.Value == null ? Conflict(new { error = "正在刷新或订阅已暂停" }) : Ok(result.Value);
    }
    [HttpPost("{id:guid}/trash-articles")]
    public async Task<IActionResult> Trash(Guid id, CancellationToken ct)
    { var result = await mediator.Send(new TrashFeedArticles(id), ct); return result.Value == null ? NotFound() : Ok(result.Value); }
}
