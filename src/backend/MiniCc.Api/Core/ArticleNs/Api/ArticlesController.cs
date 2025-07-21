using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniCc.Api.Authentication;
using MiniCc.Api.Core.ArticleNs.Application.Commands;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Application.Queries;
using MiniCc.Api.Core.TagNs.Application.DTOs;
using MiniCc.Api.Shared;
using System.Text.Json.Serialization;

namespace MiniCc.Api.Core.ArticleNs.Api;

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

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ArticleDto>>> GetArticles(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var query = new GetArticlesQuery(page, pageSize, search);
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            _logger.LogError("Error getting articles: {Error}", result.Error);
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpGet("light")]
    public async Task<ActionResult<IEnumerable<ArticleLightDto>>> GetArticlesLight(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var query = new GetArticlesLightQuery(page, pageSize, search);
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            _logger.LogError("Error getting articles light: {Error}", result.Error);
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id}")]
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

    [HttpPost]
    public async Task<ActionResult<ArticleDto>> SaveArticle([FromBody] SaveArticleRequest request)
    {
        var command = new SaveArticleCommand(request.Url);
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            _logger.LogError("Error saving article: {Error}", result.Error);
            return BadRequest(result.Error);
        }

        return CreatedAtAction(nameof(GetArticle), new { id = result.Value.Id }, result.Value);
    }

    [DisableRequestSizeLimit]
    [HttpPost("content")]
    public async Task<ActionResult<ArticleDto>> SaveArticleContent([FromBody] SaveArticleContentRequest request)
    {
        var command = new SaveArticleContentCommand(
            request.Url,
            request.OriginalContent,
            request.Title,
            request.Author,
            request.Summary,
            request.ImageUrl);

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

    [HttpPost("{id}/favorite")]
    public async Task<ActionResult<ArticleDto>> ToggleFavorite(Guid id)
    {
        var command = new ToggleFavoriteCommand(id);
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            if (result.Error.Contains("not found"))
                return NotFound(result.Error);

            _logger.LogError("Error toggling favorite for article {Id}: {Error}", id, result.Error);
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id}/archive")]
    public async Task<ActionResult<ArticleDto>> ToggleArchive(Guid id)
    {
        var command = new ToggleArchiveCommand(id);
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            if (result.Error.Contains("not found"))
                return NotFound(result.Error);

            _logger.LogError("Error toggling archive for article {Id}: {Error}", id, result.Error);
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id}/read")]
    public async Task<ActionResult<ArticleDto>> MarkAsRead(Guid id)
    {
        var command = new ToggleArchiveCommand(id);
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            if (result.Error.Contains("not found"))
                return NotFound(result.Error);

            _logger.LogError("Error marking article as read {Id}: {Error}", id, result.Error);
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

public class SaveArticleRequest
{
    public string Url { get; set; } = string.Empty;
}

public class SaveArticleContentRequest
{
    public string Url { get; set; } = string.Empty;
    public string OriginalContent { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
}

public class UpdateArticleRequest
{
    [JsonIgnore]
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public bool? IsFavorite { get; set; }
    public bool? IsArchived { get; set; }
}

public class AddTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
}
