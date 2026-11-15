using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;

namespace MiniCc.Api.Core.TagNs.Application.Services;

public interface ITagWriter
{
    /// <returns>The existing ID on a name conflict, or null when the tag was created.</returns>
    Task<Guid?> CreateOrGetExistingIdAsync(Tag tag, CancellationToken cancellationToken);

    /// <summary>Saves changes to a tracked tag.</summary>
    /// <returns>The other tag's ID when its new name is taken, or null when the changes were saved.</returns>
    Task<Guid?> UpdateOrGetExistingIdAsync(Tag tag, CancellationToken cancellationToken);
}
