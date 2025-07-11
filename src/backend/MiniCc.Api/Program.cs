using ContentHandler;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Authentication;
using MiniCc.Api.Configurations;
using MiniCc.Api.Data;
using MiniCc.Api.Services;
using Scalar.AspNetCore;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);
// 开启服务验证
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;      // 应用启动时验证
    options.ValidateScopes = true;       // 验证 Scoped 生命周期使用是否合法（防止 Singleton 注入 Scoped）
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // 处理循环引用
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        // 写入缩进格式化（可选，用于调试）
        options.JsonSerializerOptions.WriteIndented = false;
    });
builder.Services.AddOpenApi();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/pages/login";           // 登录页面路径
                options.LogoutPath = "/pages/logout";         // 登出路径
                options.AccessDeniedPath = "/pages/access-denied"; // 访问被拒绝页面
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);  // Cookie过期时间
                options.SlidingExpiration = true;               // 启用滑动过期
                // options.Cookie.Name = "AuthCookie";             // Cookie名称
                options.Cookie.HttpOnly = true;                 // 仅HTTP访问
                // if http；if in docker
                options.Cookie.SecurePolicy = CookieSecurePolicy.None; // 安全策略
                options.Cookie.SameSite = SameSiteMode.Lax;   // 跨域必须
                // if https；if in local debug
                //options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // 安全策略
                //options.Cookie.SameSite = SameSiteMode.None;   // 跨域必须
            })
            .AddScheme<AccessKeyAuthenticationSchemeOptions, AccessKeyAuthenticationHandler>(
                AccessKeyAuthenticationSchemeOptions.DefaultScheme, options => { });


builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();
var dbConnectionString = Environment.GetEnvironmentVariable("MiniCC_Db");
if (string.IsNullOrWhiteSpace(dbConnectionString))
{
    dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
}

Console.WriteLine($"dbConnectionString:{dbConnectionString}");

builder.Services.AddDbContext<MiniCcContext>(options =>
    options.UseNpgsql(dbConnectionString)
    );
builder.Services.AddScoped<IAccessKeyService, AccessKeyService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IArticleService, ArticleService>();
builder.Services.AddScoped<DbSeeder>();
builder.Services.AddHttpContextAccessor();


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

// 配置 Mapster
MapsterConfig.ConfigureMapster();


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


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();

// 注册 Mapster 映射
MapsterConfig.RegisterMappings();

// 启用认证和授权
app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();


using var scope = app.Services.CreateScope();
var dbSeeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
//await dbSeeder.InitAsync();

app.Run();


