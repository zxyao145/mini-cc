using ContentHandler;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OmeReader.Api.Data;
using OmeReader.Api.Services;
using Scalar.AspNetCore;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {

                options.LoginPath = "/Account/Login";           // 登录页面路径
                options.LogoutPath = "/Account/Logout";         // 登出路径
                options.AccessDeniedPath = "/Account/AccessDenied"; // 访问被拒绝页面
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);  // Cookie过期时间
                options.SlidingExpiration = true;               // 启用滑动过期
                // options.Cookie.Name = "AuthCookie";             // Cookie名称
                options.Cookie.HttpOnly = true;                 // 仅HTTP访问
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // 安全策略

            });

var dbConnectionString = Environment.GetEnvironmentVariable("MiniCC_Db");
if (string.IsNullOrWhiteSpace(dbConnectionString))
{
    dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
}

builder.Services.AddDbContext<OmeReaderContext>(options =>
    options.UseNpgsql(dbConnectionString)
    );

builder.Services.AddScoped<IArticleService, ArticleService>();
builder.Services.AddHttpClient("default", (httpClient) =>
{
    httpClient.DefaultRequestHeaders
    .Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.4472.124 Safari/537.36");

});
builder.Services.AddHttpClient<IReadabilityApi, ReadabilityApi>((httpClient) =>
{
    httpClient.BaseAddress = new Uri("http://127.0.0.1:5002");
});

builder.Services.AddContentHandlers();


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", builder =>
    {
        builder.WithOrigins("http://localhost:3000")
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
// 启用认证和授权
app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();

app.Run();
