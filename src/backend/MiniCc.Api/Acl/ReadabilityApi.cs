using Microsoft.Extensions.Logging;
using MiniCc.Api.Core.ArticleNs.Infrastructure.Service;
using System.Text.Json.Serialization;

namespace MiniCc.Api.Acl;

public class ReadabilityContent
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("byline")]
    public string? Byline { get; set; }

    [JsonPropertyName("dir")]
    public string? Dir { get; set; }

    [JsonPropertyName("lang")]
    public string? Lang { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("textContent")]
    public string? TextContent { get; set; }

    [JsonPropertyName("length")]
    public int Length { get; set; }

    [JsonPropertyName("excerpt")]
    public string? Excerpt { get; set; }

    [JsonPropertyName("siteName")]
    public string? SiteName { get; set; }
}

public class ReadabilityApi : IReadabilityApi
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ReadabilityApi> _logger;

    public ReadabilityApi(HttpClient httpClient, ILogger<ReadabilityApi> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ReadabilityResult> ParseAsync(string url, string content)
    {
        var requestBody = new
        {
            url,
            content,
            isNewsletter = false
        };

        var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
        var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/extract")
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

        try
        {
            var response = await _httpClient.SendAsync(httpRequestMessage);
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            var result = System.Text.Json.JsonSerializer.Deserialize<ReadabilityContent>(responseContent);

            if (result == null)
            {
                _logger.LogWarning("Deserialized result is null for URL: {Url}", url);
                throw new Exception("json Deserialize failed");
            }

            _logger.LogInformation("Successfully parsed readability content for URL: {Url}", url);

            return new ReadabilityResult
            {
                Content = result.Content
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing readability content.");
            throw;
        }
    }
}