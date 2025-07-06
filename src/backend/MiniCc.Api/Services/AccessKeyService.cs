using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniCc.Api.Data;
using MiniCc.Api.Models;

namespace MiniCc.Api.Services;


public interface IAccessKeyService
{
    Task<bool> IsValid(string key);
    Task<List<AccessKey>> List(Guid userId);
}


public class AccessKeyService : IAccessKeyService
{
    private readonly MiniCcContext _context;
    private readonly ILogger<AccountService> _logger;
    private readonly IEncryptionService _encryptionService;

    public AccessKeyService(MiniCcContext context, ILogger<AccountService> logger, IEncryptionService encryptionService)
    {
        _context = context;
        _logger = logger;
        _encryptionService = encryptionService;
    }

    public Task<bool> IsValid(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult(false);
        }
        var encryptKey = _encryptionService.Encrypt(key);
        return _context.AccessKeys.AnyAsync(x => x.Key == encryptKey);
    }

    public Task<List<AccessKey>> List(Guid userId)
    {
        return _context.AccessKeys
            .Where(x => x.UserId == userId)
            .ToListAsync();
    }
}
