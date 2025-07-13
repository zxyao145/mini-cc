using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniCc.Api.Core.ApiKeys.Domain.AggregatesModel;
using MiniCc.Api.Core.UserNs.Domain.AggregatesModel;

namespace MiniCc.Api.Core.UserNs.Infrastructure.EntityConfigurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.UserName)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(u => u.UserName)
            .IsUnique();

        builder.Property(u => u.Password)
            .HasMaxLength(500)
            .IsRequired();

        builder.HasMany(u => u.ApiKeys)
            .WithOne(ak => ak.User)
            .HasForeignKey(ak => ak.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        //builder.Ignore(u => u.DomainEvents);
    }
}

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.HasKey(ak => ak.Id);

        builder.Property(ak => ak.UserId)
            .IsRequired();

        builder.Property(ak => ak.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(ak => ak.Key)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(ak => ak.Key)
            .IsUnique();

        builder.Property(ak => ak.ExpiredTime);

        builder.Property(ak => ak.Disabled)
            .IsRequired();

        builder.Property(ak => ak.CreatedAt)
            .IsRequired();

        builder.Ignore(ak => ak.DomainEvents);
    }
}