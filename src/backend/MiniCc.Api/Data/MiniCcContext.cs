using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MiniCc.Api.Models;
using MiniCc.Api.Services;
using System.Reflection;

namespace MiniCc.Api.Data;

public class MiniCcContext : DbContext
{
    private readonly IEncryptionService _encryptionService;

    public MiniCcContext(DbContextOptions<MiniCcContext> options, IEncryptionService encryptionService)
        : base(options)
    {
        _encryptionService = encryptionService;
    }

    public DbSet<User> Users { get; set; }
    public DbSet<AccessKey> AccessKeys { get; set; }


    public DbSet<Article> Articles { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<Highlight> Highlights { get; set; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var encryptedConverter = new SensitiveConverter(_encryptionService);

        // 自动配置所有标记了 [Sensitive] 的字段
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(string) &&
                    property.PropertyInfo?.GetCustomAttribute<SensitiveAttribute>() != null)
                {
                    property.SetValueConverter(encryptedConverter);
                }
            }
        }


        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserName).HasMaxLength(128);
            entity.HasIndex(e => e.UserName).IsUnique(true);
            entity.Property(e => e.Password).HasMaxLength(1024);
        });

        modelBuilder.Entity<AccessKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId).IsUnique(false);
            entity.Property(e => e.Name).HasMaxLength(32);
            entity.Property(e => e.Key).HasMaxLength(1024);
        });


        modelBuilder.Entity<Article>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.Url).HasMaxLength(2000);
            entity.Property(e => e.Author).HasMaxLength(200);
            entity.Property(e => e.ImageUrl).HasMaxLength(2000);
            entity.HasIndex(e => e.Url).IsUnique();
            entity.HasIndex(e => e.CreatedAt);


            //entity.HasGeneratedTsVectorColumn(
            //    p => p.SearchVector,
            //    "english",  // Text search config
            //    p => new { p.Title, p.ReadableContent, p.Author })  // Included properties
            //.HasIndex(p => p.SearchVector)
            //.HasMethod("GIN"); // Index method on the search vector (GIN or GIST)

            entity.HasGeneratedTsVectorColumn(
               p => p.SearchVector,
               "mixed_zh_en",  // Text search config
               p => new { p.Title, p.ReadableContent, p.Author })  // Included properties
           .HasIndex(p => p.SearchVector)
           .HasMethod("GIN"); // Index method on the search vector (GIN or GIST)

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

public class DateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    public DateTimeOffsetConverter()
        : base(
            v => v.ToUniversalTime(),
            v => v)
    {
    }
}