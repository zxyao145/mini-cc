using ContentHandler;
using Microsoft.AspNetCore.Authentication.Cookies;
using MiniCc.Api.Acl;
using MiniCc.Api.Authentication;
using MiniCc.Api.Componments;
using MiniCc.Api.Core;
using MiniCc.Api.Core.ApiKeys.Application.Services;
using MiniCc.Api.Core.TagNs.Application.Services;
using MiniCc.Api.Infra;
using MiniCc.Api.Shared.Data;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);


#region serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Services.AddSerilog();
#endregion

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = false;
    });

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/pages/login";
        options.LogoutPath = "/pages/logout";
        options.AccessDeniedPath = "/pages/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
        options.Cookie.SameSite = SameSiteMode.Lax;
    })
    .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiAuthenticationHandler>(
        ApiKeyAuthenticationSchemeOptions.DefaultScheme, options => { });

// Add Clean Architecture layers
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddHttpContextAccessor();
// Legacy services for authentication
builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<ISignInService, SignInService>();

builder.Services.AddMapping()
    .AddValidators()
    .AddMediator();

builder.Services.AddHttpClient("default", (httpClient) =>
{
    httpClient.DefaultRequestHeaders
        .Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.4472.124 Safari/537.36");
});

var readabilityApi = Environment.GetEnvironmentVariable("MiniCC_ReadabilityApi");
if (string.IsNullOrWhiteSpace(readabilityApi))
{
    readabilityApi = "http://127.0.0.1:5002";
}
builder.Services.AddHttpClient<IReadabilityApi, ReadabilityApi>((httpClient) =>
{
    httpClient.BaseAddress = new Uri(readabilityApi);
});

builder.Services.AddContentHandlers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", builder =>
    {
        builder.WithOrigins("http://localhost:3000", "https://localhost:3000", "http://localhost:5000")
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});

builder.Services.AddOptions<ReadabilityApiOptions>()
    .Bind(builder.Configuration.GetSection("ReadabilityApi"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.UseCors("AllowFrontend");

// 启用认证和授权
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using var scope = app.Services.CreateScope();
var dbSeeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
await dbSeeder.InitAsync();

app.Run();