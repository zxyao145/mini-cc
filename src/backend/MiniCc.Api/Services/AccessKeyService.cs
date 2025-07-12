using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Data;
using MiniCc.Api.Models;
using MiniCc.Api.Common;

namespace MiniCc.Api.Services;

public class CreateAccessKeyResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = "";
    public AccessKey? AccessKey { get; set; }
}

public class AccessKeyOperationResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = "";
}

public interface IAccessKeyService
{
    Task<bool> IsValid(string key);
    Task<List<AccessKey>> List(Guid userId);
    Task<CreateAccessKeyResult> CreateAsync(Guid userId, string name, DateTimeOffset? expiredTime = null);
    Task<AccessKeyOperationResult> UpdateAsync(Guid userId, Guid keyId, string name, DateTimeOffset? expiredTime, bool disabled);
    Task<AccessKeyOperationResult> DeleteAsync(Guid userId, Guid keyId);
}

public class AccessKeyService : IAccessKeyService
{
    private readonly MiniCcContext _context;
    private readonly ILogger<AccessKeyService> _logger;
    private const int MaxAccessKeysPerUser = 10;

    public AccessKeyService(MiniCcContext context, ILogger<AccessKeyService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> IsValid(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }
        var query = _context.AccessKeys.Where(x => x.Key == key);
        _logger.LogDebug("Checking if access key is valid: {query}", query.ToQueryString());
        return await  _context.AccessKeys.AnyAsync(x=>x.Key == key
         && !x.Disabled
         && (x.ExpiredTime == null || x.ExpiredTime > DateTimeOffset.UtcNow)
         );
    }

    public Task<List<AccessKey>> List(Guid userId)
    {
        return _context.AccessKeys
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Id)
            .ToListAsync();
    }

    public async Task<CreateAccessKeyResult> CreateAsync(Guid userId, string name, DateTimeOffset? expiredTime = null)
    {
        // 检查用户的 Access Key 数量限制
        var existingCount = await _context.AccessKeys.CountAsync(x => x.UserId == userId);
        if (existingCount >= MaxAccessKeysPerUser)
        {
            return new CreateAccessKeyResult 
            { 
                Success = false, 
                ErrorMessage = $"每个用户最多只能创建 {MaxAccessKeysPerUser} 个 Access Key" 
            };
        }

        // 检查名称是否重复
        var existingKey = await _context.AccessKeys
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Name == name);
        if (existingKey != null)
        {
            return new CreateAccessKeyResult 
            { 
                Success = false, 
                ErrorMessage = "Access Key 名称已存在" 
            };
        }

        var accessKey = new AccessKey
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Key = KeyGen.Generate(32),
            ExpiredTime = expiredTime,
            Disabled = false
        };

        _context.AccessKeys.Add(accessKey);
        await _context.SaveChangesAsync();

        return new CreateAccessKeyResult 
        { 
            Success = true, 
            AccessKey = accessKey 
        };
    }

    public async Task<AccessKeyOperationResult> UpdateAsync(Guid userId, Guid keyId, string name, DateTimeOffset? expiredTime, bool disabled)
    {
        var accessKey = await _context.AccessKeys
            .FirstOrDefaultAsync(x => x.Id == keyId && x.UserId == userId);
        
        if (accessKey == null)
        {
            return new AccessKeyOperationResult 
            { 
                Success = false, 
                ErrorMessage = "Access Key 不存在" 
            };
        }

        // 检查名称是否重复（排除自己）
        var existingKey = await _context.AccessKeys
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Name == name && x.Id != keyId);
        if (existingKey != null)
        {
            return new AccessKeyOperationResult 
            { 
                Success = false, 
                ErrorMessage = "Access Key 名称已存在" 
            };
        }

        accessKey.Name = name;
        accessKey.ExpiredTime = expiredTime;
        accessKey.Disabled = disabled;

        await _context.SaveChangesAsync();

        return new AccessKeyOperationResult { Success = true };
    }

    public async Task<AccessKeyOperationResult> DeleteAsync(Guid userId, Guid keyId)
    {
        var accessKey = await _context.AccessKeys
            .FirstOrDefaultAsync(x => x.Id == keyId && x.UserId == userId);
        
        if (accessKey == null)
        {
            return new AccessKeyOperationResult 
            { 
                Success = false, 
                ErrorMessage = "Access Key 不存在" 
            };
        }

        _context.AccessKeys.Remove(accessKey);
        await _context.SaveChangesAsync();

        return new AccessKeyOperationResult { Success = true };
    }
}
