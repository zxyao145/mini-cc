using System;
using System.Reflection.Metadata;
using System.Text.Json.Serialization;

namespace MiniCc.Api.Services;

//如果好用，请收藏地址，帮忙分享。
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


public interface IReadabilityApi
{
    public Task<ReadabilityContent> ParseAsync(
         string url,
         string htmlContent,
         bool isNewsletter = false);
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



    public async Task<ReadabilityContent> ParseAsync(string url, string htmlContent, bool isNewsletter = false)
    {
        var requestBody = new
        {
            url = url,
            content = htmlContent,
            isNewsletter = isNewsletter
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
            if(result == null)
            {
                _logger.LogWarning("Deserialized result is null for URL: {Url}", url);
                throw new Exception("json Deserialize failed");
            }
            else
            {
                _logger.LogInformation("Successfully parsed readability content for URL: {Url}", url);
            }
            return result!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing readability content.");
            throw;
        }
    }
}
