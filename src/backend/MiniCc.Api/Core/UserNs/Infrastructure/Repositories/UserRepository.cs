using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.UserNs.Domain.AggregatesModel;
using MiniCc.Api.Shared.Data;
using System.Linq.Expressions;

namespace MiniCc.Api.Core.UserNs.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly MiniCcDbContext _context;

    public UserRepository(MiniCcDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.ApiKeys)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.ApiKeys)
            .FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);
    }

    public async Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.ApiKeys)
            .ToListAsync(cancellationToken);
    }

    public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        var entityEntry = await _context.Users.AddAsync(user, cancellationToken);
        return entityEntry.Entity;
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(user);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Remove(user);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users.AnyAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> FirstOrDefaultAsync(Expression<Func<User, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _context.Users.FirstOrDefaultAsync(predicate, cancellationToken);
    }
}
