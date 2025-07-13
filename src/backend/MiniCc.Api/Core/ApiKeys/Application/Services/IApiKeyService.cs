using Microsoft.EntityFrameworkCore.Metadata.Internal;
using MiniCc.Api.Core.ApiKeys.Application.Commands;
using MiniCc.Api.Core.ApiKeys.Application.DTOs;
using MiniCc.Api.Core.ApiKeys.Domain.AggregatesModel;

namespace MiniCc.Api.Core.ApiKeys.Application.Handlers;


public interface IApiKeyService
{
    Task<bool> ValidateAsync(string apiKey);

    Task<List<ApiKey>> List(Guid userId);
    Task<CreateApiKeyResult> CreateAsync(CreateApiKeyRequest request);
    Task<ApiKeyOperationResult> UpdateAsync(UpdateApiKeyRequest request);
    Task<ApiKeyOperationResult> DeleteAsync(DeleteApiKeyRequest request);
}


