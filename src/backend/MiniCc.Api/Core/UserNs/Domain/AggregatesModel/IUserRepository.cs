using System.Linq.Expressions;

namespace MiniCc.Api.Core.UserNs.Domain.AggregatesModel;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);

    Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<User> AddAsync(User user, CancellationToken cancellationToken = default);

    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    Task DeleteAsync(User user, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> FirstOrDefaultAsync(Expression<Func<User, bool>> predicate, CancellationToken cancellationToken = default);
}
