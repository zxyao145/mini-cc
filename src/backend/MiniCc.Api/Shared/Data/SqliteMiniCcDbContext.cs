using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.UserNs.Domain.AggregatesModel;
using MiniCc.Api.Infra;

namespace MiniCc.Api.Shared.Data;

public class SqliteMiniCcDbContext(
    DbContextOptions<SqliteMiniCcDbContext> options,
    IEncryptionService encryptionService) : MiniCcDbContext(options, encryptionService)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
        configurationBuilder.DefaultTypeMapping<DateTimeOffset>().HasConversion<UtcTicksConverter>();
    }

    protected override void ConfigureProviderModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Article>().Ignore(a => a.SearchVector);
        modelBuilder.Entity<User>().Property(u => u.ReaderPreferences).HasColumnType("TEXT");
    }
}

public class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
    value => value.UtcTicks,
    ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
