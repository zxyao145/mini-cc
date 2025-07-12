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
public class HighlightsController : ControllerBase
{
    private readonly IHighlightService _highlightService;
    private readonly ILogger<HighlightsController> _logger;

    public HighlightsController(IHighlightService highlightService, ILogger<HighlightsController> logger)
    {
        _highlightService = highlightService;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有高亮列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<HighlightDto>>> GetHighlights()
    {
        try
        {
            var highlights = await _highlightService.GetHighlightsAsync();
            return Ok(highlights.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting highlights");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 根据高亮ID获取高亮详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<HighlightDto>> GetHighlight(Guid id)
    {
        try
        {
            var highlight = await _highlightService.GetHighlightByIdAsync(id);
            if (highlight == null)
            {
                return NotFound();
            }
            return Ok(highlight.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting highlight {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 更新高亮
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<HighlightDto>> UpdateHighlight(Guid id, [FromBody] HighlightUpdateRequest updateRequest)
    {
        try
        {
            var highlight = await _highlightService.UpdateHighlightAsync(id, updateRequest);
            if (highlight == null)
            {
                return NotFound();
            }
            return Ok(highlight.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating highlight {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 删除高亮
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteHighlight(Guid id)
    {
        try
        {
            var success = await _highlightService.DeleteHighlightAsync(id);
            if (!success)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting highlight {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}