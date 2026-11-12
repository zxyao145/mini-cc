using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniCc.Api.Authentication;
using MiniCc.Api.Controllers.ArticleNs.Requests;
using MiniCc.Api.Core.ArticleNs.Application.Commands;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Application.Queries;
using MiniCc.Api.Core.TagNs.Application.DTOs;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Controllers.ArticleNs;

[Authorize(AuthenticationSchemes =
    $"{CookieAuthenticationDefaults.AuthenticationScheme}, {ApiKeyAuthenticationSchemeOptions.DefaultScheme}")]
[ApiController]
[Route("api/[controller]")]
public class ArticlesController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ArticlesController> _logger;

    public ArticlesController(IMediator mediator, ILogger<ArticlesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("batch")]
    public async Task<ActionResult<BatchArticleResult>> Batch([FromBody] BatchArticleCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
        }
        catch (FluentValidation.ValidationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet]
    public async Task<ActionResult<ArticleListPage>> GetArticles([FromQuery] ArticleListRequest request)
    {
        var result = await _mediator.Send(new GetArticleListQuery(request));
        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ArticleDto>> GetArticle(Guid id)
    {
        var query = new GetArticleByIdQuery(id);
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            if (result.Error.Contains("not found"))
                return NotFound(result.Error);

            _logger.LogError("Error getting article {Id}: {Error}", id, result.Error);
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [DisableRequestSizeLimit]
    [HttpPost("content")]
    public async Task<ActionResult<ArticleDto>> SaveArticleContent([FromBody] SaveArticleContentCommand command)
    {
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            _logger.LogError("Error saving article content: {Error}", result.Error);
            return BadRequest(result.Error);
        }

        return CreatedAtAction(nameof(GetArticle), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ArticleDto>> UpdateArticle(Guid id, [FromBody] UpdateArticleCommand command)
    {
        command.Id = id;
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            if (result.Error.Contains("not found"))
                return NotFound(result.Error);

            _logger.LogError("Error updating article {Id}: {Error}", id, result.Error);
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPatch("{id}/state")]
    public async Task<ActionResult<ArticleListItemDto>> UpdateState(Guid id, [FromBody] UpdateArticleStateCommand command)
    {
        command.ArticleId = id;
        try
        {
            var result = await _mediator.Send(command);
            if (result.IsFailure)
            {
                if (result.Error.Contains("not found")) return NotFound(result.Error);
                return BadRequest(result.Error);
            }
            return Ok(result.Value);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}/progress")]
    public async Task<ActionResult<ProgressSaveDto>> UpdateProgress(Guid id, [FromBody] UpdateProgressCommand command)
    {
        command.ArticleId = id;
        return await SaveProgress(command);
    }

    [HttpPost("{id}/progress/reset")]
    public async Task<ActionResult<ProgressSaveDto>> ResetProgress(Guid id, [FromBody] ResetProgressRequest request) =>
        await SaveProgress(new UpdateProgressCommand { ArticleId = id, SessionId = request.SessionId, Reset = true });

    private async Task<ActionResult<ProgressSaveDto>> SaveProgress(UpdateProgressCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);
            if (result.IsFailure) return NotFound(result.Error);
            if (result.Value.Current != null) return Conflict(new { current = result.Value.Current });
            return Ok(result.Value);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/restore")]
    public async Task<ActionResult<ArticleListItemDto>> RestoreArticle(Guid id)
    {
        var result = await _mediator.Send(new RestoreArticleCommand(id));
        if (result.IsFailure)
        {
            if (result.Error.Contains("not found")) return NotFound(result.Error);
            return BadRequest(result.Error);
        }
        return Ok(result.Value);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteArticle(Guid id)
    {
        var command = new DeleteArticleCommand(id);
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            if (result.Error.Contains("not found"))
                return NotFound(result.Error);

            _logger.LogError("Error deleting article {Id}: {Error}", id, result.Error);
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpPost("{id}/tags")]
    public async Task<ActionResult<TagDto>> AddTag(Guid id, [FromBody] AddTagRequest request)
    {
        var command = new AddTagToArticleCommand(id, request.Name, request.Color);
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            if (result.Error.Contains("not found"))
                return NotFound(result.Error);

            _logger.LogError("Error adding tag to article {Id}: {Error}", id, result.Error);
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpDelete("{id}/tags/{tagId}")]
    public async Task<ActionResult> RemoveTag(Guid id, Guid tagId)
    {
        var command = new RemoveTagFromArticleCommand(id, tagId);
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            if (result.Error.Contains("not found"))
                return NotFound(result.Error);

            _logger.LogError("Error removing tag from article {Id}: {Error}", id, result.Error);
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpPost("{id}/highlights")]
    public async Task<ActionResult<HighlightDto>> AddHighlight(Guid id, [FromBody] AddHighlightCommand command)
    {
        command.ArticleId = id;
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            if (result.Error.Contains("not found"))
                return NotFound(result.Error);

            _logger.LogError("Error adding highlight to article {Id}: {Error}", id, result.Error);
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }
}