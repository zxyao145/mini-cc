using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MiniCc.Api.Core.ApiKeys.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Infrastructure.EntityConfigurations;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Core.TagNs.Infrastructure.EntityConfigurations;
using MiniCc.Api.Core.UserNs.Domain.AggregatesModel;
using MiniCc.Api.Infra;
using System.Reflection;

namespace MiniCc.Api.Shared.Data;

public class MiniCcDbContext : DbContext
{
    private readonly IEncryptionService _encryptionService;

    public MiniCcDbContext(DbContextOptions<MiniCcDbContext> options, IEncryptionService encryptionService)
        : base(options)
    {
        _encryptionService = encryptionService;
    }

    public DbSet<Article> Articles { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<Highlight> Highlights { get; set; }


    public DbSet<User> Users { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("zhparser");

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



        modelBuilder.ApplyConfiguration(new ArticleConfiguration());
        modelBuilder.ApplyConfiguration(new TagConfiguration());
        modelBuilder.ApplyConfiguration(new HighlightConfiguration());

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserName).HasMaxLength(128);
            entity.HasIndex(e => e.UserName).IsUnique(true);
            entity.Property(e => e.Password).HasMaxLength(1024);
        });

        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId).IsUnique(false);
            entity.Property(e => e.Name).HasMaxLength(32);
            entity.Property(e => e.Key).HasMaxLength(1024);
        });

        base.OnModelCreating(modelBuilder);
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