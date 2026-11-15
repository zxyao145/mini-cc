using System.Runtime.Versioning;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Shared.Data;

namespace MiniCc.Api.Test.Infrastructure;

public class SqliteStartupTests
{
    [Fact]
    public async Task InitAsync_CreatesDatabaseInMissingDirectoryOnce()
    {
        var fixture = new SqliteFixture();
        var path = new SqliteConnectionStringBuilder(fixture.ConnectionString).DataSource;
        try
        {
            Directory.Exists(Path.GetDirectoryName(path)).Should().BeFalse();
            await fixture.InitializeAsync();
            Guid userId;
            await using (var initial = fixture.CreateContext())
                userId = (await initial.Users.SingleAsync()).Id;

            await fixture.InitializeAsync();
            File.Exists(path).Should().BeTrue();

            await using var db = fixture.CreateContext();
            var user = await db.Users.SingleAsync();
            user.Id.Should().Be(userId);
            user.UserName.Should().Be("mini_cc");
            (await db.ApiKeys.CountAsync()).Should().Be(1);

            await db.Database.OpenConnectionAsync();
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA journal_mode";
            (await command.ExecuteScalarAsync()).Should().Be("wal");
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [UnprivilegedUnixFact]
    [UnsupportedOSPlatform("windows")]
    public async Task InitAsync_ExistingUnwritableDirectory_ReportsFullPath()
    {
        var fixture = new SqliteFixture();
        var path = new SqliteConnectionStringBuilder(fixture.ConnectionString).DataSource;
        var directory = Path.GetDirectoryName(path)!;
        try
        {
            await fixture.InitializeAsync();
            SqliteConnection.ClearAllPools();
            var mode = File.GetUnixFileMode(directory);
            try
            {
                File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
                await using var db = fixture.CreateContext();
                var failure = await ((Func<Task>)new DbSeeder(db).InitAsync).Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage($"无法打开 SQLite 数据库文件：{path}");
                failure.Which.InnerException.Should().BeOfType<SqliteException>();
            }
            finally
            {
                File.SetUnixFileMode(directory, mode);
            }
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    private sealed class UnprivilegedUnixFactAttribute : FactAttribute
    {
        public UnprivilegedUnixFactAttribute()
        {
            if (OperatingSystem.IsWindows() || Environment.UserName == "root")
                Skip = "Requires Unix directory permissions to be enforced for the test process.";
        }
    }

    [Fact]
    public async Task InitAsync_CannotCreateDirectory_ReportsFullPath()
    {
        var fixture = new SqliteFixture();
        var blocker = new SqliteConnectionStringBuilder(fixture.ConnectionString).DataSource;
        Directory.CreateDirectory(Path.GetDirectoryName(blocker)!);
        await File.WriteAllTextAsync(blocker, "This file occupies the directory path.");
        var path = Path.Combine(blocker, "data", "mini_cc.db");
        try
        {
            await using var db = fixture.CreateContext($"Data Source={path}");
            await ((Func<Task>)new DbSeeder(db).InitAsync).Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"无法打开 SQLite 数据库文件：{path}");
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }
}
