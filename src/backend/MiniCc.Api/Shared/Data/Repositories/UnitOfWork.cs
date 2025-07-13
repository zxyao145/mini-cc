using Microsoft.EntityFrameworkCore.Storage;
using MiniCc.Api.Core.ApiKeys.Domain.AggregatesModel;
using MiniCc.Api.Core.ApiKeys.Infrastructure.Repositories;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Infrastructure.Repositories;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Core.TagNs.Infrastructure.Repositories;
using MiniCc.Api.Core.UserNs.Domain.AggregatesModel;
using MiniCc.Api.Core.UserNs.Infrastructure.Repositories;

namespace MiniCc.Api.Shared.Data.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly MiniCcDbContext _context;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(MiniCcDbContext context)
    {
        _context = context;
        Articles = new ArticleRepository(_context);
        Tags = new TagRepository(_context);
        Highlights = new HighlightRepository(_context);
        Users = new UserRepository(_context);
        ApiKeys = new ApiKeyRepository(_context);
    }

    public IArticleRepository Articles { get; }
    public ITagRepository Tags { get; }
    public IHighlightRepository Highlights { get; }
    public IUserRepository Users { get; }
    public IApiKeyRepository ApiKeys { get; }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}