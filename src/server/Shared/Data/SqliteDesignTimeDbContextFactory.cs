using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MiniCc.Api.Infra;

namespace MiniCc.Api.Shared.Data;

public class SqliteDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SqliteMiniCcDbContext>
{
    public SqliteMiniCcDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SqliteMiniCcDbContext>()
            .UseSqlite("Data Source=design-time.db").Options;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AesKeys:0:Id"] = "design-time",
            ["AesKeys:0:Key"] = Convert.ToBase64String(new byte[32]),
            ["AesKeys:0:IV"] = Convert.ToBase64String(new byte[16])
        }).Build();

        return new SqliteMiniCcDbContext(options, new AesEncryptionService(configuration));
    }
}
