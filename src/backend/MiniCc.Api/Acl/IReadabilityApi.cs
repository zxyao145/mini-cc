using MiniCc.Api.Core.ArticleNs.Infrastructure.Service;

namespace MiniCc.Api.Acl;

public interface IReadabilityApi
{
    Task<ReadabilityResult> ParseAsync(string url, string content);
}