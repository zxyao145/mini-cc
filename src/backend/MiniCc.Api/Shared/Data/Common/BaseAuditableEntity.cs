namespace MiniCc.Api.Shared.Data.Common;

public abstract class BaseAuditableEntity : BaseEntity
{
    public DateTimeOffset CreatedAt { get; protected set; }
    public string? CreatedBy { get; protected set; }
    public DateTimeOffset? LastModifiedAt { get; protected set; }
    public string? LastModifiedBy { get; protected set; }

    protected BaseAuditableEntity(Guid id) : base(id)
    {
        CreatedAt = DateTimeOffset.UtcNow;
    }

    protected BaseAuditableEntity() : base()
    {
    }

    public void SetAuditInfo(string? createdBy = null, string? lastModifiedBy = null)
    {
        if (CreatedBy == null && createdBy != null)
        {
            CreatedBy = createdBy;
        }

        if (lastModifiedBy != null)
        {
            LastModifiedBy = lastModifiedBy;
            LastModifiedAt = DateTimeOffset.UtcNow;
        }
    }
}