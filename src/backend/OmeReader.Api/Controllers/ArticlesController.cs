using Microsoft.AspNetCore.Mvc;
using OmeReader.Api.Models;
using OmeReader.Api.Services;

namespace OmeReader.Api.Controllers;

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
    public async Task<ActionResult<IEnumerable<Article>>> GetArticles(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20, 
        [FromQuery] string? search = null)
    {
        try
        {
            var articles = await _articleService.GetArticlesAsync(page, pageSize, search);
            return Ok(articles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting articles");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Article>> GetArticle(int id)
    {
        try
        {
            var article = await _articleService.GetArticleByIdAsync(id);
            if (article == null)
            {
                return NotFound();
            }
            return Ok(article);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting article {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<Article>> SaveArticle([FromBody] SaveArticleRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Url) || !Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
            {
                return BadRequest("Invalid URL");
            }

            var article = await _articleService.SaveArticleAsync(request.Url);
            return CreatedAtAction(nameof(GetArticle), new { id = article.Id }, article);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving article from URL: {Url}", request.Url);
            return StatusCode(500, "Failed to save article");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Article>> UpdateArticle(int id, [FromBody] Article article)
    {
        try
        {
            var updatedArticle = await _articleService.UpdateArticleAsync(id, article);
            return Ok(updatedArticle);
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
    public async Task<IActionResult> DeleteArticle(int id)
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
    public async Task<ActionResult<Article>> ToggleFavorite(int id)
    {
        try
        {
            var article = await _articleService.ToggleFavoriteAsync(id);
            return Ok(article);
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
    public async Task<ActionResult<Article>> ToggleArchive(int id)
    {
        try
        {
            var article = await _articleService.ToggleArchiveAsync(id);
            return Ok(article);
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
    public async Task<ActionResult<Highlight>> AddHighlight(int id, [FromBody] Highlight highlight)
    {
        try
        {
            var newHighlight = await _articleService.AddHighlightAsync(id, highlight);
            return Ok(newHighlight);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding highlight to article {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("highlights/{highlightId}")]
    public async Task<IActionResult> DeleteHighlight(int highlightId)
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
    public async Task<ActionResult<Tag>> AddTag(int id, [FromBody] AddTagRequest request)
    {
        try
        {
            var tag = await _articleService.AddTagToArticleAsync(id, request.Name, request.Color);
            return Ok(tag);
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
    public async Task<IActionResult> RemoveTag(int id, int tagId)
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