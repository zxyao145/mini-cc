using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ApiKeys.Application.DTOs;
using MiniCc.Api.Core.ApiKeys.Domain.AggregatesModel;
using MiniCc.Api.Shared.Data;

namespace MiniCc.Api.Core.ApiKeys.Infrastructure.Repositories;

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly MiniCcDbContext _context;

    public ApiKeyRepository(MiniCcDbContext context)
    {
        _context = context;
    }

    public async Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ApiKeys
            .Include(ak => ak.User)
            .FirstOrDefaultAsync(ak => ak.Id == id, cancellationToken);
    }

    public async Task<ApiKey?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _context.ApiKeys
            .Include(ak => ak.User)
            .FirstOrDefaultAsync(ak => ak.Key == key, cancellationToken);
    }

    public async Task<IEnumerable<ApiKey>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.ApiKeys
            .Where(ak => ak.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<ApiKey> AddAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        var entityEntry = await _context.ApiKeys.AddAsync(apiKey, cancellationToken);
        return entityEntry.Entity;
    }

    public Task UpdateAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        _context.ApiKeys.Update(apiKey);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        _context.ApiKeys.Remove(apiKey);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ApiKeys.AnyAsync(ak => ak.Id == id, cancellationToken);
    }


    public async Task<IEnumerable<ApiKeyDto>> GetAllApiKeyDtoList(Guid userId)
    {
        return await _context.ApiKeys 
            .Where(x => x.UserId == userId)
            .ProjectToType<ApiKeyDto>()
            .ToListAsync();
    }
}