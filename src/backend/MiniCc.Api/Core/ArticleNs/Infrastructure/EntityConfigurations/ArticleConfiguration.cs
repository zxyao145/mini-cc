using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Query;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Shared.Data.Common;
using System.Reflection.Emit;

namespace MiniCc.Api.Core.ArticleNs.Infrastructure.EntityConfigurations;

public class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.HasKey(a => a.Id);

        builder.OwnsOne(a => a.Url, url =>
        {
            url.Property(u => u.Value)
                .HasColumnName("Url")
                .HasMaxLength(2000)
                .IsRequired();
        });

        builder.Property(a => a.Title)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(a => a.Author)
            .HasMaxLength(200);

        builder.Property(a => a.Summary)
            .HasMaxLength(1000);

        builder.Property(a => a.ImageUrl)
            .HasMaxLength(2000);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.ReadAt);

        builder.Property(a => a.IsArchived)
            .IsRequired();

        builder.Property(a => a.IsFavorite)
            .IsRequired();

        builder.HasMany(a => a.Tags)
            .WithMany(t => t.Articles)
            .UsingEntity("ArticleTags");

        builder.HasMany(a => a.Highlights)
            .WithOne(h => h.Article)
            .HasForeignKey(h => h.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        //entity.HasGeneratedTsVectorColumn(
        //    p => p.SearchVector,
        //    "english",  // Text search config
        //    p => new { p.Title, p.ReadableContent, p.Author })  // Included properties
        //.HasIndex(p => p.SearchVector)
        //.HasMethod("GIN"); // Index method on the search vector (GIN or GIST)

        builder.HasGeneratedTsVectorColumn(
           p => p.SearchVector,
           "mixed_zh_en",  // Text search config
        p => new { p.Title, p.ReadableContent, p.Author })  // Included properties
       .HasIndex(p => p.SearchVector)
       .HasMethod("GIN"); // Index method on the search vector (GIN or GIST)

        //builder
        //    .Property(p => p.SearchVector)
        //    .IsGeneratedTsVectorColumn("mixed_zh_en", new[] { "Title", "ReadableContent", "Author" });
        //builder.HasIndex(p => p.SearchVector).HasMethod("GIN"); // Index method on the search vector (GIN or GIST)


        builder.Ignore(a => a.DomainEvents);
    }
}