namespace MiniCc.Api.Shared.Data.Common;

public abstract class Entity : BaseEntity
{
    protected Entity(Guid id) : base(id)
    {
    }

    protected Entity() : base()
    {
    }
}