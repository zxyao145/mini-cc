using Microsoft.EntityFrameworkCore;
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

    public AccessKeyService(MiniCcContext context, ILogger<AccountService> logger)
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
            .ToListAsync();
    }
}
