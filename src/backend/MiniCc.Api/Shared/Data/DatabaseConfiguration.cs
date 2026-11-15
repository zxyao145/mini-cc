using Microsoft.Data.Sqlite;

namespace MiniCc.Api.Shared.Data;

public enum DatabaseProvider { Postgres, Sqlite }

public static class DatabaseConfiguration
{
    public static DatabaseProvider GetProvider(IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"];
        if (string.IsNullOrEmpty(provider) || provider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
            return DatabaseProvider.Postgres;
        if (provider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
            return DatabaseProvider.Sqlite;
        throw new InvalidOperationException("Database:Provider must be 'postgres' or 'sqlite'.");
    }

    public static string ResolveSqliteConnectionString(string connectionString, string contentRootPath)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        var path = builder.DataSource;
        if (path != ":memory:" && !Path.IsPathFullyQualified(path))
            builder.DataSource = Path.GetFullPath(path, contentRootPath);
        return builder.ToString();
    }
}
