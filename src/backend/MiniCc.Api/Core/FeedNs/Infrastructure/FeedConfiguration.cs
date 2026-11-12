using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniCc.Api.Core.FeedNs.Domain;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
namespace MiniCc.Api.Core.FeedNs.Infrastructure;

public class FeedConfiguration : IEntityTypeConfiguration<Feed>
{
    public void Configure(EntityTypeBuilder<Feed> builder)
    {
        builder.ToTable("Feeds"); builder.HasKey(f => f.Id); builder.Ignore(f => f.DomainEvents);
        builder.Property(f => f.FeedUrl).HasColumnType("text");
        builder.Property(f => f.FeedUrlHash).HasMaxLength(64); builder.HasIndex(f => f.FeedUrlHash).IsUnique();
        builder.Property(f => f.Title).HasMaxLength(500); builder.Property(f => f.OriginalTitle).HasMaxLength(500);
        builder.Property(f => f.LastError).HasMaxLength(1000); builder.HasIndex(f => f.NextFetchAt);
        builder.HasMany(f => f.Tags).WithMany().UsingEntity<Dictionary<string, object>>("FeedTags",
            j => j.HasOne<Tag>().WithMany().HasForeignKey("TagId").OnDelete(DeleteBehavior.Cascade),
            j => j.HasOne<Feed>().WithMany().HasForeignKey("FeedId").OnDelete(DeleteBehavior.Cascade));
    }
}
public class FeedEntryConfiguration : IEntityTypeConfiguration<FeedEntry>
{
    public void Configure(EntityTypeBuilder<FeedEntry> builder)
    {
        builder.ToTable("FeedEntries"); builder.HasKey(e => e.Id);
        builder.Property(e => e.EntryKey).HasColumnType("text"); builder.Property(e => e.EntryKeyHash).HasMaxLength(64);
        builder.HasIndex(e => new { e.FeedId, e.EntryKeyHash }).IsUnique(); builder.Property(e => e.ContentHash).HasMaxLength(64);
        builder.HasOne(e => e.Feed).WithMany(f => f.Entries).HasForeignKey(e => e.FeedId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Article).WithMany(a => a.FeedEntries).HasForeignKey(e => e.ArticleId).OnDelete(DeleteBehavior.SetNull);
    }
}
