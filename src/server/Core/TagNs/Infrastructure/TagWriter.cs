using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.TagNs.Application.Services;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Core.TagNs.Infrastructure.EntityConfigurations;
using MiniCc.Api.Shared.Data;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.TagNs.Infrastructure;

internal sealed class TagWriter(IUnitOfWork unitOfWork) : ITagWriter
{
    public async Task<Guid?> CreateOrGetExistingIdAsync(Tag tag, CancellationToken cancellationToken)
    {
        var existing = await unitOfWork.Tags.GetByNameAsync(tag.Name, cancellationToken);
        if (existing != null)
        {
            return existing.Id;
        }

        await unitOfWork.Tags.AddAsync(tag, cancellationToken);
        return await SaveOrGetExistingIdAsync(tag, cancellationToken);
    }

    public async Task<Guid?> UpdateOrGetExistingIdAsync(Tag tag, CancellationToken cancellationToken)
    {
        var existing = await unitOfWork.Tags.GetByNameAsync(tag.Name, cancellationToken);
        if (existing != null && existing.Id != tag.Id)
        {
            return existing.Id;
        }

        return await SaveOrGetExistingIdAsync(tag, cancellationToken);
    }

    private async Task<Guid?> SaveOrGetExistingIdAsync(Tag tag, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException exception) when (DatabaseErrors.IsUniqueViolation(exception, "Tags", TagConfiguration.NameIndex, "Name"))
        {
            var entry = exception.Entries.FirstOrDefault(e => ReferenceEquals(e.Entity, tag));
            if (entry == null)
            {
                throw;
            }

            entry.State = EntityState.Detached;
            var existing = await unitOfWork.Tags.GetByNameAsync(tag.Name, cancellationToken);
            if (existing == null)
            {
                throw;
            }

            return existing.Id;
        }
    }
}
