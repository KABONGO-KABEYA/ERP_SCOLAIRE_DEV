using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Sync;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.CloudSync;
using SchoolManagement.Infrastructure.Persistence;
using Xunit;

namespace SchoolManagement.UnitTests.CloudSync;

public sealed class CloudSyncDestinationTests
{
    private static SchoolDbContext Destination(string database) => new(
        new DbContextOptionsBuilder<SchoolDbContext>()
            .UseSqlServer($"Server=localhost;Database={database};Integrated Security=true;TrustServerCertificate=true").Options);

    [Fact]
    public async Task New_destination_replays_more_than_500_old_rows_despite_completed_history_and_preserves_business_values()
    {
        await using var local = CloudSyncTariffIdentityTests.CreateContext();
        await using var remote = Destination("Destination_A");
        var school = new School { Name = "École test" };
        local.Add(school);
        var years = Enumerable.Range(0, 601).Select(i => new AcademicYear
        { SchoolId = school.Id, Label = "Année " + i }).ToList();
        local.AddRange(years);
        local.Add(new SyncWatermark { SchoolId = school.Id, TableName = "AcademicYears", LastSyncedAt = DateTime.UtcNow });
        local.Add(new SyncOutboxUnit { SchoolId = school.Id, AggregateType = "Entity", Status = SyncOutboxStatus.Completed });
        await local.SaveChangesAsync();
        local.ChangeTracker.Clear();
        var before = await local.Set<AcademicYear>().IgnoreQueryFilters().AsNoTracking()
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.Label, x.CreatedAt, x.UpdatedAt }).ToListAsync();

        (await CloudSyncEngine.PrepareDestinationAsync(local, remote)).Should().BeTrue();
        var queued = await local.Set<SyncOutboxItem>().Where(x => x.TableName == "AcademicYears").ToListAsync();
        queued.Should().HaveCount(601);
        queued.Select(x => x.EntityId).Should().BeEquivalentTo(years.Select(x => x.Id));
        local.ChangeTracker.Clear();
        (await local.Set<AcademicYear>().IgnoreQueryFilters().AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Label, x.CreatedAt, x.UpdatedAt }).ToListAsync()).Should().BeEquivalentTo(before);
        (await CloudSyncEngine.PrepareDestinationAsync(local, remote)).Should().BeFalse();
        (await local.Set<SyncOutboxItem>().CountAsync()).Should().Be(602);
    }

    [Fact]
    public async Task Switching_database_and_returning_to_previous_destination_replays_completed_rows()
    {
        await using var local = CloudSyncTariffIdentityTests.CreateContext();
        await using var first = Destination("Destination_A");
        await using var second = Destination("Destination_B");
        local.Add(new School { Name = "École test" });
        await local.SaveChangesAsync();
        foreach (var remote in new[] { first, second, first })
        {
            (await CloudSyncEngine.PrepareDestinationAsync(local, remote)).Should().BeTrue();
            foreach (var item in await local.Set<SyncOutboxItem>().ToListAsync()) item.Status = SyncOutboxStatus.Completed;
            foreach (var unit in await local.SyncOutboxUnits.ToListAsync()) unit.Status = SyncOutboxStatus.Completed;
            await local.SaveChangesAsync();
        }
        (await local.Set<SyncOutboxItem>().CountAsync()).Should().Be(3);
        (await local.Set<SyncDestination>().SingleAsync()).RemoteKey.Should().Be(CloudSyncNaturalKey.RemoteKey(first));
    }

    [Fact]
    public async Task Multi_school_source_is_blocked_without_preparing_destination_or_enqueuing_data()
    {
        await using var local = CloudSyncTariffIdentityTests.CreateContext();
        await using var remote = Destination("Destination_A");
        local.AddRange(new School { Name = "Première" }, new School { Name = "Deuxième" });
        await local.SaveChangesAsync();
        var prepare = () => CloudSyncEngine.PrepareDestinationAsync(local, remote);
        await prepare.Should().ThrowAsync<InvalidOperationException>().WithMessage("*une seule école*");
        (await local.Set<SyncDestination>().CountAsync()).Should().Be(0);
        (await local.SyncOutboxUnits.CountAsync()).Should().Be(0);
    }
}
