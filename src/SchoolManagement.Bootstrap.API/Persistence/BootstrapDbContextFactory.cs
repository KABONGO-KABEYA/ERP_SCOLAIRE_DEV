using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SchoolManagement.Bootstrap.API.Persistence;

/// <summary>Factory design-time pour <c>dotnet ef migrations</c>.</summary>
public sealed class BootstrapDbContextFactory : IDesignTimeDbContextFactory<BootstrapDbContext>
{
    public BootstrapDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BootstrapDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=SchoolManagementBootstrap_Design;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsAssembly(typeof(BootstrapDbContext).Assembly.FullName))
            .Options;
        return new BootstrapDbContext(options);
    }
}
