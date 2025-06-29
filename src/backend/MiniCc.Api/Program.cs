using ContentHandler;
using Microsoft.EntityFrameworkCore;
using OmeReader.Api.Data;
using OmeReader.Api.Services;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<OmeReaderContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IArticleService, ArticleService>();
builder.Services.AddHttpClient("default", (httpClient) =>
{
    httpClient.DefaultRequestHeaders
    .Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.4472.124 Safari/537.36");

});
builder.Services.AddScoped<IReadabilityApi, ReadabilityApi>();
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
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
