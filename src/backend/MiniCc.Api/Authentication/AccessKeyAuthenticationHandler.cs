using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Data;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace MiniCc.Api.Authentication;

public class AccessKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "AccessKey";
    public string HeaderName { get; set; } = "X-Access-Key";
    public string QueryParameterName { get; set; } = "access_key";
}

public class AccessKeyAuthenticationHandler : AuthenticationHandler<AccessKeyAuthenticationSchemeOptions>
{
    private readonly ILogger<AccessKeyAuthenticationHandler> _logger;

    public AccessKeyAuthenticationHandler(IOptionsMonitor<AccessKeyAuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        _logger = logger.CreateLogger<AccessKeyAuthenticationHandler>();
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 从Header或Query参数获取AccessKey
        var accessKey = GetAccessKeyFromRequest();

        if (string.IsNullOrEmpty(accessKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Get the expected access key from environment variable or configuration
        var expectedAccessKey = Environment.GetEnvironmentVariable("MiniCC_AccessKey");

        if (string.IsNullOrWhiteSpace(expectedAccessKey))
        {
            _logger.LogError("expected AccessKey not set");
            return Task.FromResult(AuthenticateResult.Fail("Invalid access key"));
        }

        if (accessKey != expectedAccessKey)
        {
            _logger.LogWarning("Invalid access key provided: {AccessKey}", accessKey);
            return Task.FromResult(AuthenticateResult.Fail("Invalid access key"));
        }

        // Create claims for the authenticated user
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "AccessKeyUser"),
            new Claim(ClaimTypes.NameIdentifier, "AccessKeyUser"),
            new Claim("AuthenticationType", "AccessKey"),
            new Claim("AccessKey", accessKey)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        _logger.LogInformation("Access key authentication successful");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }



    private string? GetAccessKeyFromRequest()
    {
        // 1. 先从Header中获取
        if (Request.Headers.TryGetValue(Options.HeaderName, out var headerValue))
        {
            return headerValue.FirstOrDefault();
        }

        // 2. 从Query参数中获取
        if (Request.Query.TryGetValue(Options.QueryParameterName, out var queryValue))
        {
            return queryValue.FirstOrDefault();
        }

        // 3. 从Authorization Header中获取 (Bearer格式)
        if (Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var authValue = authHeader.FirstOrDefault();
            if (!string.IsNullOrEmpty(authValue) && authValue.StartsWith("ak "))
            {
                return authValue.Substring(7); // 移除 "Bearer " 前缀
            }
        }

        return null;
    }
}