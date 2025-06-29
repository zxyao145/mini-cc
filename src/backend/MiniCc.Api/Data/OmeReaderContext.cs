using Microsoft.EntityFrameworkCore;
using OmeReader.Api.Models;

namespace OmeReader.Api.Data;

public class OmeReaderContext : DbContext
{
    public OmeReaderContext(DbContextOptions<OmeReaderContext> options) : base(options)
    {
    }

    public DbSet<Article> Articles { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<Highlight> Highlights { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Article>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.Url).HasMaxLength(2000);
            entity.Property(e => e.Author).HasMaxLength(200);
            entity.Property(e => e.ImageUrl).HasMaxLength(2000);
            entity.HasIndex(e => e.Url).IsUnique();
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Color).HasMaxLength(10);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<Highlight>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Color).HasMaxLength(10);
            entity.HasIndex(e => e.ArticleId);
            entity.HasOne(e => e.Article)
                .WithMany(e => e.Highlights)
                .HasForeignKey(e => e.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Article>()
            .HasMany(e => e.Tags)
            .WithMany(e => e.Articles)
            .UsingEntity("ArticleTags");
    }
}