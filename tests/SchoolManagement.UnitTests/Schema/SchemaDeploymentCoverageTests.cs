using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Infrastructure.Persistence;
using Xunit;

namespace SchoolManagement.UnitTests.Schema;

public sealed class SchemaDeploymentCoverageTests
{
    [Fact]
    public void Every_post_baseline_ef_migration_is_declared_in_schema_coverage()
    {
        var discovered = DiscoverSchoolDbMigrations()
            .Where(id => id != SchemaDeploymentCoverage.InitialCreateMigrationId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var declared = SchemaDeploymentCoverage.Entries
            .Select(e => e.MigrationId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        discovered.Should().Equal(
            declared,
            "toute nouvelle migration EF post-InitialCreate doit être déclarée dans SchemaDeploymentCoverage " +
            "(Complete / Partial / Excluded). Database.Migrate() n'est pas le mécanisme de déploiement.");
    }

    [Fact]
    public void Excluded_entries_require_justification()
    {
        foreach (var entry in SchemaDeploymentCoverage.Entries.Where(e => e.Kind == SchemaCoverageKind.Excluded))
        {
            entry.Justification.Should().NotBeNullOrWhiteSpace(entry.MigrationId);
        }
    }

    [Fact]
    public void Coverage_entries_have_an_official_mechanism()
    {
        SchemaDeploymentCoverage.Entries.Should().OnlyContain(e =>
            !string.IsNullOrWhiteSpace(e.OfficialMechanism)
            && !string.IsNullOrWhiteSpace(e.MigrationId));
    }

    private static IReadOnlyList<string> DiscoverSchoolDbMigrations()
    {
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseSqlServer("Server=.;Database=unused;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new SchoolDbContext(options);
        return context.Database.GetMigrations().ToList();
    }
}
