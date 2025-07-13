using MiniCc.Api.Core.ApiKeys.Application.DTOs;

namespace MiniCc.Api.Core.ApiKeys.Domain.AggregatesModel;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ApiKey?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    Task<IEnumerable<ApiKey>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ApiKey> AddAsync(ApiKey apiKey, CancellationToken cancellationToken = default);

    Task UpdateAsync(ApiKey apiKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(ApiKey apiKey, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ApiKeyDto>> GetAllApiKeyDtoList(Guid userId);
}