using System.Diagnostics;
using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.Queries;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniCc.Api.Authentication;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.TagNs.Application.Commands;
using MiniCc.Api.Core.TagNs.Application.DTOs;
using MiniCc.Api.Core.TagNs.Application.Services;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Controllers.TagNs;

[Authorize(AuthenticationSchemes =
    $"{CookieAuthenticationDefaults.AuthenticationScheme}, {ApiKeyAuthenticationSchemeOptions.DefaultScheme}")]
public class TagsController : ApiControllerBase
{
    private readonly ITagService _tagService;
    private readonly IMediator _mediator;
    private readonly ILogger<TagsController> _logger;

    public TagsController(ITagService tagService, ILogger<TagsController> logger, IMediator mediator)
    {
        _tagService = tagService;
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有标签列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TagWithArticleCountDto>>> GetTags(
        [FromQuery] string? search = null)
    {
        try
        {
            var tags = await _tagService.GetTagsAsync(search);
            return Ok(tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tags");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 新建标签
    /// </summary>
    [HttpPost]
    [ProducesResponseType<TagWithArticleCountDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<CreateTagError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<CreateTagConflict>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TagWithArticleCountDto>> CreateTag(
        CreateTagCommand request, [FromServices] LinkGenerator links, CancellationToken cancellationToken)
    {
        var validation = new CreateTagCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return BadRequest(new CreateTagError(validation.ToString()));
        }

        var outcome = await _tagService.TryCreateTagAsync(request, cancellationToken);
        return outcome switch
        {
            CreateTagOutcome.Created(var tag) => Created(
                links.GetUriByAction(HttpContext, nameof(GetTag), values: new { id = tag.Id },
                    options: new LinkOptions { LowercaseUrls = true }), tag),
            CreateTagOutcome.Conflict(var existingTagId) => Conflict(new CreateTagConflict(existingTagId)),
            _ => throw new UnreachableException()
        };
    }

    public record CreateTagError(string Error);
    public record CreateTagConflict(Guid ExistingTagId);

    /// <summary>
    /// 根据标签ID获取标签详情及其关联的文章
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TagWithArticlesDto>> GetTag(Guid id)
    {
        try
        {
            var tag = await _tagService.GetTagByIdAsync(id);
            if (tag == null)
            {
                return NotFound();
            }
            return Ok(tag);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tag {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 获取标签关联的文章列表
    /// </summary>
    [HttpGet("{id}/articles")]
    public async Task<ActionResult<ArticleListPage>> GetTagArticles(Guid id, [FromQuery] ArticleListRequest request)
    {
        request.View = "all";
        request.Tags = id.ToString();
        var result = await _mediator.Send(new GetArticleListQuery(request));
        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }

    /// <summary>
    /// 修改标签名称和颜色
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTag(Guid id, UpdateTagCommand request, CancellationToken cancellationToken)
    {
        var validation = new UpdateTagCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return BadRequest(new { error = validation.ToString() });
        }

        return await _tagService.UpdateTagAsync(id, request, cancellationToken) switch
        {
            UpdateTagOutcome.Updated => NoContent(),
            UpdateTagOutcome.NotFound => NotFound(),
            UpdateTagOutcome.Conflict(var existingTagId) => Conflict(new { existingTagId }),
            _ => throw new UnreachableException()
        };
    }

    /// <summary>
    /// 删除标签
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTag(Guid id)
    {
        try
        {
            await _tagService.DeleteTagAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tag {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}