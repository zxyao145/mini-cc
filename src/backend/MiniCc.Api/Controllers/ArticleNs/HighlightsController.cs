using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniCc.Api.Authentication;
using MiniCc.Api.Core.ArticleNs.Application.Commands;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Application.Queries;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Controllers.ArticleNs;

[Authorize(AuthenticationSchemes =
    $"{CookieAuthenticationDefaults.AuthenticationScheme}, {ApiKeyAuthenticationSchemeOptions.DefaultScheme}")]
public class HighlightsController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<HighlightsController> _logger;

    public HighlightsController(IMediator mediator, ILogger<HighlightsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public record AnchorRequest(int? StartOffset, int? EndOffset, bool Orphaned = false, int? ContentRevision = null);
    [HttpPut("{id:guid}/anchor")]
    public async Task<IActionResult> Anchor(Guid id, AnchorRequest request, CancellationToken ct)
    {
        var command = new UpdateHighlightAnchor(id, request.StartOffset, request.EndOffset, request.Orphaned, request.ContentRevision);
        var validation = new UpdateHighlightAnchorValidator().Validate(command);
        if (!validation.IsValid) return BadRequest(new { error = validation.ToString() });
        var result = await _mediator.Send(command, ct);
        if (result.IsFailure) return NotFound(new { error = result.Error });
        if (result.Value.ConflictingRevision is int revision)
            return Conflict(new { current = new { contentRevision = revision } });
        return Ok(result.Value.Highlight);
    }

    /// <summary>
    /// 获取所有高亮列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<HighlightDto>>> GetHighlights()
    {
        var result = await _mediator.Send(new GetHighlightsQuery());
        if (result.IsFailure)
        {
            _logger.LogError("Error getting highlights: {Error}", result.Error);
            return StatusCode(500, result.Error);
        }
        return Ok(result.Value);
    }

    /// <summary>
    /// 根据高亮ID获取高亮详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<HighlightDto>> GetHighlight(Guid id)
    {
        var result = await _mediator.Send(new GetHighlightByIdQuery(id));
        if (result.IsFailure)
        {
            if (result.Error.Contains("not found"))
            {
                return NotFound(result.Error);
            }
            _logger.LogError("Error getting highlight {Id}: {Error}", id, result.Error);
            return StatusCode(500, result.Error);
        }
        return Ok(result.Value);
    }

    /// <summary>
    /// 更新高亮
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<HighlightDto>> UpdateHighlight(Guid id, [FromBody] UpdateHighlightCommand updateRequest)
    {
        updateRequest.Id = id;
        var result = await _mediator.Send(updateRequest);
        if (result.IsFailure)
        {
            if (result.Error.Contains("not found"))
            {
                return NotFound(result.Error);
            }
            _logger.LogError("Error updating highlight {Id}: {Error}", id, result.Error);
            return StatusCode(500, result.Error);
        }
        return Ok(result.Value);
    }

    /// <summary>
    /// 删除高亮
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteHighlight(Guid id)
    {
        var result = await _mediator.Send(new DeleteHighlightCommand(id));
        if (result.IsFailure)
        {
            if (result.Error.Contains("not found"))
            {
                return NotFound(result.Error);
            }
            _logger.LogError("Error deleting highlight {Id}: {Error}", id, result.Error);
            return StatusCode(500, result.Error);
        }
        return NoContent();
    }
}