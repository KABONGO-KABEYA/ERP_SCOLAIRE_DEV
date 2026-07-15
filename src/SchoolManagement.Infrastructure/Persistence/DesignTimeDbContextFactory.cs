using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SchoolManagement.Application.Configuration.Database;

namespace SchoolManagement.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SchoolDbContext>
{
    public SchoolDbContext CreateDbContext(string[] args)
    {
        var apiDirectory = Path.Combine(Directory.GetCurrentDirectory(), "../SchoolManagement.API");
        var bootstrap = new DatabaseConnectionBootstrap(apiDirectory);
        bootstrap.ConfigurationManager.EnsureDefaultFileExists();

        var configuration = bootstrap.LoadConfiguration();
        var validation = bootstrap.ConfigurationManager.Validate(configuration);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                "ServeurDonnees.txt invalide pour les migrations EF : "
                + string.Join("; ", validation.FieldErrors.Values));
        }

        var connectionString = bootstrap.BuildConnectionString(configuration);
        var optionsBuilder = new DbContextOptionsBuilder<SchoolDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new SchoolDbContext(optionsBuilder.Options);
    }
}
