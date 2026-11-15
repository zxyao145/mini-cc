using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniCc.Api.Core;
using MiniCc.Api.Infra;
using MiniCc.Api.Shared.Data;

namespace MiniCc.Api.Test.Infrastructure;

public class DatabaseConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetProvider_DefaultsToPostgres(string? value)
    {
        DatabaseConfiguration.GetProvider(Config(("Database:Provider", value))).Should().Be(DatabaseProvider.Postgres);
    }

    [Theory]
    [InlineData("sqlite", DatabaseProvider.Sqlite)]
    [InlineData("SQLite", DatabaseProvider.Sqlite)]
    [InlineData("postgres", DatabaseProvider.Postgres)]
    [InlineData("Postgres", DatabaseProvider.Postgres)]
    public void GetProvider_IsCaseInsensitive(string value, DatabaseProvider expected)
    {
        DatabaseConfiguration.GetProvider(Config(("Database:Provider", value))).Should().Be(expected);
    }

    [Fact]
    public void GetProvider_RejectsUnknownValue()
    {
        var read = () => DatabaseConfiguration.GetProvider(Config(("Database:Provider", "mysql")));
        read.Should().Throw<InvalidOperationException>().WithMessage("Database:Provider must be 'postgres' or 'sqlite'.");
    }

    [Fact]
    public void ResolveSqliteConnectionString_UsesContentRootForRelativePaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "minicc-root");
        var absolute = Path.Combine(Path.GetTempPath(), "other", "mini_cc.db");

        string Resolve(string path) => new SqliteConnectionStringBuilder(
            DatabaseConfiguration.ResolveSqliteConnectionString($"Data Source={path}", root)).DataSource;

        Resolve("data/mini_cc.db").Should().Be(Path.GetFullPath(Path.Combine(root, "data", "mini_cc.db")));
        Resolve(absolute).Should().Be(absolute);
        Resolve(":memory:").Should().Be(":memory:");
    }

    [Fact]
    public void AddInfrastructureServices_Sqlite_RegistersSqliteContextUnderContentRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "minicc-root");
        var configuration = Config(("Database:Provider", "sqlite"), ("ConnectionStrings:Sqlite", "Data Source=data/x.db"),
            ("ImageProxy:SignatureKey", "test-signing-key"));
        using var services = new ServiceCollection().AddSingleton(Mock.Of<IEncryptionService>())
            .AddInfrastructureServices(configuration, root).BuildServiceProvider();
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MiniCcDbContext>();

        db.Should().BeOfType<SqliteMiniCcDbContext>();
        db.Database.GetDbConnection().DataSource.Should().Be(Path.GetFullPath(Path.Combine(root, "data", "x.db")));
    }

    [Fact]
    public void AddInfrastructureServices_PrefersMiniCcDbSetting()
    {
        var path = Path.Combine(Path.GetTempPath(), "minicc-a.db");
        var configuration = Config(("Database:Provider", "sqlite"), ("ConnectionStrings:Sqlite", "Data Source=other.db"),
            ("MiniCC_Db", $"Data Source={path}"));
        using var services = new ServiceCollection().AddSingleton(Mock.Of<IEncryptionService>())
            .AddInfrastructureServices(configuration).BuildServiceProvider();
        using var scope = services.CreateScope();

        scope.ServiceProvider.GetRequiredService<MiniCcDbContext>().Database.GetDbConnection().DataSource.Should().Be(path);
    }

    [Fact]
    public void AddInfrastructureServices_Sqlite_RequiresConnectionString()
    {
        var register = () => new ServiceCollection().AddInfrastructureServices(Config(("Database:Provider", "sqlite"),
            ("ConnectionStrings:DefaultConnection", "Host=localhost;Database=unused")));
        register.Should().Throw<InvalidOperationException>().WithMessage("SQLite requires MiniCC_Db or ConnectionStrings:Sqlite.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AddInfrastructureServices_Sqlite_RejectsExplicitBlankOverride(string value)
    {
        var register = () => new ServiceCollection().AddInfrastructureServices(Config(("Database:Provider", "sqlite"),
            ("MiniCC_Db", value), ("ConnectionStrings:Sqlite", "Data Source=fallback.db")));
        register.Should().Throw<InvalidOperationException>().WithMessage("SQLite requires MiniCC_Db or ConnectionStrings:Sqlite.");
    }

    private static IConfiguration Config(params (string Key, string? Value)[] entries) =>
        new ConfigurationBuilder().AddInMemoryCollection(entries.ToDictionary(e => e.Key, e => e.Value)).Build();
}
