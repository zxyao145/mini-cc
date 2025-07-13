using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;

namespace MiniCc.Api.Core.TagNs.Infrastructure.EntityConfigurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(t => t.Name)
            .IsUnique();

        builder.OwnsOne(t => t.Color, color =>
        {
            color.Property(c => c.Value)
                .HasColumnName("Color")
                .HasMaxLength(7)
                .IsRequired();
        });

        builder.Property(t => t.CreatedAt).IsRequired();

        builder.Ignore(t => t.DomainEvents);
    }
}

public class HighlightConfiguration : IEntityTypeConfiguration<Highlight>
{
    public void Configure(EntityTypeBuilder<Highlight> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.ArticleId)
            .IsRequired();

        builder.Property(h => h.Text)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(h => h.Note)
            .HasMaxLength(1000);

        builder.Property(h => h.StartOffset)
            .IsRequired();

        builder.Property(h => h.EndOffset)
            .IsRequired();

        builder.Property(h => h.CreatedAt)
            .IsRequired();

        builder.Ignore(h => h.DomainEvents);
    }
}