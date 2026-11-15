using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MiniCc.Api.Core.ApiKeys.Application.Services;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace MiniCc.Api.Authentication;

public class ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public string HeaderName { get; set; } = "X-Api-Key";
    public string QueryParameterName { get; set; } = "api_key";
}

public class ApiAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>
{
    private readonly ILogger<ApiAuthenticationHandler> _logger;
    private readonly IApiKeyService _apiKeyService;

    public ApiAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder,
        IApiKeyService apiKeyService)
        : base(options, logger, encoder)
    {
        _logger = logger.CreateLogger<ApiAuthenticationHandler>();
        _apiKeyService = apiKeyService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // ��Header��Query������ȡApiKey
        var apiKey = GetApiKeyFromRequest();

        if (string.IsNullOrEmpty(apiKey))
        {
            return AuthenticateResult.NoResult();
        }

        var isValid = await _apiKeyService.ValidateAsync(apiKey);
        if (!isValid)
        {
            _logger.LogWarning("Invalid api key provided: {ApiKey}", apiKey);
            return AuthenticateResult.Fail("Invalid api key");
        }

        // Create claims for the authenticated user
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "ApiKeyUser"),
            new Claim(ClaimTypes.NameIdentifier, "ApiKeyUser"),
            new Claim("AuthenticationType", ApiKeyAuthenticationSchemeOptions.DefaultScheme),
            new Claim("ApiKey", apiKey)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        _logger.LogInformation("Api key authentication successful");
        return AuthenticateResult.Success(ticket);
    }

    private string? GetApiKeyFromRequest()
    {
        // 1. �ȴ�Header�л�ȡ
        if (Request.Headers.TryGetValue(Options.HeaderName, out var headerValue))
        {
            return headerValue.FirstOrDefault();
        }

        // 2. ��Query�����л�ȡ
        if (Request.Query.TryGetValue(Options.QueryParameterName, out var queryValue))
        {
            return queryValue.FirstOrDefault();
        }

        // 3. ��Authorization Header�л�ȡ (Bearer��ʽ)
        if (Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var authValue = authHeader.FirstOrDefault();
            if (!string.IsNullOrEmpty(authValue) && authValue.StartsWith("ak "))
            {
                return authValue.Substring(7); // �Ƴ� "Bearer " ǰ׺
            }
        }

        return null;
    }
}