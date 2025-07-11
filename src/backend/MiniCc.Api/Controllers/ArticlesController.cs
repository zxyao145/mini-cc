using ContentHandler;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniCc.Api.Authentication;
using MiniCc.Api.Models;
using MiniCc.Api.Models.Dtos;
using MiniCc.Api.Services;
using MiniCc.Api.Extensions;

namespace MiniCc.Api.Controllers;

//[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
[Authorize(AuthenticationSchemes = 
    $"{CookieAuthenticationDefaults.AuthenticationScheme}, {AccessKeyAuthenticationSchemeOptions.DefaultScheme}")]
[ApiController]
[Route("api/[controller]")]
public class ArticlesController : ControllerBase
{
    private readonly IArticleService _articleService;
    private readonly ILogger<ArticlesController> _logger;

    public ArticlesController(IArticleService articleService, ILogger<ArticlesController> logger)
    {
        _articleService = articleService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ArticleDto>>> GetArticles(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20, 
        [FromQuery] string? search = null)
    {
        try
        {
            var articles = await _articleService.GetArticlesAsync(page, pageSize, search);
            return Ok(articles.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting articles");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ArticleDto>> GetArticle(Guid id)
    {
        try
        {
            var article = await _articleService.GetArticleByIdAsync(id);
            if (article == null)
            {
                return NotFound();
            }
            return Ok(article.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting article {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<ArticleDto>> SaveArticle([FromBody] SaveArticleRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Url) || !Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
            {
                return BadRequest("Invalid URL");
            }

            var article = await _articleService.SaveArticleAsync(request.Url);
            return CreatedAtAction(nameof(GetArticle), new { id = article.Id }, article.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving article from URL: {Url}", request.Url);
            return StatusCode(500, "Failed to save article");
        }
    }


    [Route("content")]
    [HttpPost]
    public async Task<ActionResult<ArticleDto>> SaveArticleContent([FromBody] ContentFetchResult request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Url) || !Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
            {
                return BadRequest("Invalid URL");
            }

            var article = await _articleService.SaveContentAsync(request);
            return CreatedAtAction(nameof(GetArticle), new { id = article.Id }, article.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving article from URL: {Url}", request.Url);
            return StatusCode(500, "Failed to save article");
        }
    }


    [HttpPut("{id}")]
    public async Task<ActionResult<ArticleDto>> UpdateArticle(Guid id, [FromBody] Article article)
    {
        try
        {
            var updatedArticle = await _articleService.UpdateArticleAsync(id, article);
            return Ok(updatedArticle.ToDto());
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating article {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteArticle(Guid id)
    {
        try
        {
            await _articleService.DeleteArticleAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting article {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("{id}/favorite")]
    public async Task<ActionResult<ArticleDto>> ToggleFavorite(Guid id)
    {
        try
        {
            var article = await _articleService.ToggleFavoriteAsync(id);
            return Ok(article.ToDto());
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling favorite for article {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("{id}/archive")]
    public async Task<ActionResult<ArticleDto>> ToggleArchive(Guid id)
    {
        try
        {
            var article = await _articleService.ToggleArchiveAsync(id);
            return Ok(article.ToDto());
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling archive for article {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("{id}/highlights")]
    public async Task<ActionResult<HighlightDto>> AddHighlight(Guid id, [FromBody] Highlight highlight)
    {
        try
        {
            var newHighlight = await _articleService.AddHighlightAsync(id, highlight);
            return Ok(newHighlight.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding highlight to article {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("highlights/{highlightId}")]
    public async Task<IActionResult> DeleteHighlight(Guid highlightId)
    {
        try
        {
            await _articleService.DeleteHighlightAsync(highlightId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting highlight {Id}", highlightId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("{id}/tags")]
    public async Task<ActionResult<TagDto>> AddTag(Guid id, [FromBody] AddTagRequest request)
    {
        try
        {
            var tag = await _articleService.AddTagToArticleAsync(id, request.Name, request.Color);
            return Ok(tag.ToDto());
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding tag to article {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id}/tags/{tagId}")]
    public async Task<IActionResult> RemoveTag(Guid id, Guid tagId)
    {
        try
        {
            await _articleService.RemoveTagFromArticleAsync(id, tagId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing tag from article {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 获取文章列表 - 轻量版本（不包含内容和高亮）
    /// </summary>
    [HttpGet("light")]
    public async Task<ActionResult<IEnumerable<ArticleDto>>> GetArticlesLight(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20, 
        [FromQuery] string? search = null)
    {
        try
        {
            var articles = await _articleService.GetArticlesAsync(page, pageSize, search);
            return Ok(articles.ToLightDto()); // 使用轻量级映射
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting articles");
            return StatusCode(500, "Internal server error");
        }
    }
}

public class SaveArticleRequest
{
    public string Url { get; set; } = string.Empty;
}

public class AddTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
}