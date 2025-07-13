using MiniCc.Api.Core.ArticleNs.Application.DTOs;

namespace MiniCc.Api.Core.TagNs.Domain.AggregatesModel;

public interface ITagRepository
{
    Task<Tag?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Tag?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<IEnumerable<Tag>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Tag> AddAsync(Tag tag, CancellationToken cancellationToken = default);

    Task UpdateAsync(Tag tag, CancellationToken cancellationToken = default);

    Task DeleteAsync(Tag tag, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}