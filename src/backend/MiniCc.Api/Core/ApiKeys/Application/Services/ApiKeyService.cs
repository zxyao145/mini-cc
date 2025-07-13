using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ApiKeys.Application.Commands;
using MiniCc.Api.Core.ApiKeys.Application.DTOs;
using MiniCc.Api.Core.ApiKeys.Domain.AggregatesModel;
using MiniCc.Api.Shared.Data;
using MiniCc.Api.Shared.Utils;

namespace MiniCc.Api.Core.ApiKeys.Application.Handlers;

public class ApiKeyService : IApiKeyService
{
    private readonly MiniCcDbContext _context;
    private readonly ILogger<ApiKeyService> _logger;
    private const int MaxApiKeysPerUser = 10;

    public ApiKeyService(MiniCcDbContext context, ILogger<ApiKeyService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> ValidateAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }
#if DEBUG
        var query = _context.ApiKeys.Where(x => x.Key == key);
        _logger.LogDebug("Checking if api key is valid: {query}", query.ToQueryString());
#endif
        return await _context.ApiKeys.AnyAsync(x => 
            x.Key == key 
            && !x.Disabled 
            && (x.ExpiredTime == null || x.ExpiredTime > DateTimeOffset.UtcNow)
             );
    }

    public Task<List<ApiKey>> List(Guid userId)
    {
        return _context.ApiKeys
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Id)
            .ToListAsync();
    }

    public async Task<CreateApiKeyResult> CreateAsync(CreateApiKeyRequest request)
    {
        Guid userId = request.UserId;
        string name = request.Name;
        DateTimeOffset? expiredTime = request.ExpiredTime;

        // 检查用户的 Api Key 数量限制
        var existingCount = await _context.ApiKeys.CountAsync(x => x.UserId == userId);
        if (existingCount >= MaxApiKeysPerUser)
        {
            return new CreateApiKeyResult
            {
                Success = false,
                ErrorMessage = $"每个用户最多只能创建 {MaxApiKeysPerUser} 个 Api Key"
            };
        }

        // 检查名称是否重复
        var existingKey = await _context.ApiKeys
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Name == name);
        if (existingKey != null)
        {
            return new CreateApiKeyResult
            {
                Success = false,
                ErrorMessage = "Api Key 名称已存在"
            };
        }

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Key = KeyGen.Generate(32),
            ExpiredTime = expiredTime,
            Disabled = false
        };

        _context.ApiKeys.Add(apiKey);
        await _context.SaveChangesAsync();

        return new CreateApiKeyResult
        {
            Success = true,
            ApiKey = apiKey
        };
    }

    public async Task<ApiKeyOperationResult> UpdateAsync(UpdateApiKeyRequest request)
    {
        Guid userId = request.UserId;
        Guid keyId = request.Id;
        string name = request.Name;
        DateTimeOffset? expiredTime = request.ExpiredTime;
        bool disabled = request.Disabled;

        var apiKey = await _context.ApiKeys
            .FirstOrDefaultAsync(x => x.Id == keyId && x.UserId == userId);

        if (apiKey == null)
        {
            return new ApiKeyOperationResult
            {
                Success = false,
                ErrorMessage = "Api Key 不存在"
            };
        }

        // 检查名称是否重复（排除自己）
        var existingKey = await _context.ApiKeys
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Name == name && x.Id != keyId);
        if (existingKey != null)
        {
            return new ApiKeyOperationResult
            {
                Success = false,
                ErrorMessage = "Api Key 名称已存在"
            };
        }

        apiKey.Name = name;
        apiKey.ExpiredTime = expiredTime;
        apiKey.Disabled = disabled;

        await _context.SaveChangesAsync();

        return new ApiKeyOperationResult { Success = true };
    }

    public async Task<ApiKeyOperationResult> DeleteAsync(DeleteApiKeyRequest request)
    {
        var apiKey = await _context.ApiKeys
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.UserId == request.UserId);

        if (apiKey == null)
        {
            return new ApiKeyOperationResult
            {
                Success = false,
                ErrorMessage = "Api Key 不存在"
            };
        }

        _context.ApiKeys.Remove(apiKey);
        await _context.SaveChangesAsync();

        return new ApiKeyOperationResult { Success = true };
    }
}


