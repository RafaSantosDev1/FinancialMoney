using FinSight.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinSight.Api.Migrations;

/// <summary>
/// Used by the EF Core tools (migrations, scaffolding) so they can run without
/// a live database connection. The connection string here is only used for
/// provider-specific SQL generation; it is never used at runtime.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FinSightDbContext>
{
    public FinSightDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FinSightDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=finsight;Username=postgres;Password=postgres")
            .Options;
        return new FinSightDbContext(options);
    }
}
