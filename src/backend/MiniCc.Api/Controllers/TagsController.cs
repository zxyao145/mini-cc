using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniCc.Api.Authentication;
using MiniCc.Api.Models.Dtos;
using MiniCc.Api.Services;
using MiniCc.Api.Extensions;

namespace MiniCc.Api.Controllers;

[Authorize(AuthenticationSchemes = 
    $"{CookieAuthenticationDefaults.AuthenticationScheme}, {AccessKeyAuthenticationSchemeOptions.DefaultScheme}")]
[ApiController]
[Route("api/[controller]")]
public class TagsController : ControllerBase
{
    private readonly ITagService _tagService;
    private readonly ILogger<TagsController> _logger;

    public TagsController(ITagService tagService, ILogger<TagsController> logger)
    {
        _tagService = tagService;
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
    public async Task<ActionResult<IEnumerable<ArticleDto>>> GetTagArticles(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var articles = await _tagService.GetTagArticlesAsync(id, page, pageSize);
            return Ok(articles.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting articles for tag {Id}", id);
            return StatusCode(500, "Internal server error");
        }
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