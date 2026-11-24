using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FFM.Workforce.Infrastructure;

public class WorkforceDbContextFactory : IDesignTimeDbContextFactory<WorkforceDbContext>
{
    public WorkforceDbContext CreateDbContext(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables();

        var configuration = builder.Build();

        var connectionString =
            configuration.GetConnectionString("Postgres")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? throw new InvalidOperationException("PostgreSQL connection string is not configured for design-time factory.");

        var optionsBuilder = new DbContextOptionsBuilder<WorkforceDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new WorkforceDbContext(optionsBuilder.Options);
    }
}
