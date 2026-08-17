using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Infrastructure.CloudSync;
using SchoolManagement.Infrastructure.Persistence;
using Xunit;

namespace SchoolManagement.UnitTests.CloudSync;

public sealed class CloudSyncNaturalKeyTests
{
    [Fact]
    public async Task ExistsByNaturalKey_skips_Permission_when_cloud_already_has_same_Code()
    {
        var localId = Guid.NewGuid();
        var cloudId = Guid.NewGuid();
        await using var remote = CreateContext();
        remote.Set<Permission>().Add(CreatePermission(cloudId, "payments.validate"));
        await remote.SaveChangesAsync();

        var localPermission = CreatePermission(localId, "payments.validate");
        var exists = await CloudSyncNaturalKey.ExistsByNaturalKeyAsync(
            remote, localPermission, CancellationToken.None);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByNaturalKey_skips_SecurityModule_when_cloud_already_has_same_Code()
    {
        await using var remote = CreateContext();
        remote.Set<SecurityModule>().Add(new SecurityModule
        {
            Id = Guid.NewGuid(),
            Code = "STUDENTS",
            Name = "Élèves"
        });
        await remote.SaveChangesAsync();

        var localModule = new SecurityModule
        {
            Id = Guid.NewGuid(),
            Code = "STUDENTS",
            Name = "Élèves"
        };

        var exists = await CloudSyncNaturalKey.ExistsByNaturalKeyAsync(
            remote, localModule, CancellationToken.None);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByNaturalKey_skips_Currency_when_cloud_already_has_same_Code()
    {
        await using var remote = CreateContext();
        remote.Set<CurrencyDefinition>().Add(new CurrencyDefinition
        {
            Id = Guid.NewGuid(),
            Code = "EUR",
            Name = "Euro",
            Symbol = "€"
        });
        await remote.SaveChangesAsync();

        var local = new CurrencyDefinition
        {
            Id = Guid.NewGuid(),
            Code = "EUR",
            Name = "Euro",
            Symbol = "€"
        };

        (await CloudSyncNaturalKey.ExistsByNaturalKeyAsync(remote, local, CancellationToken.None))
            .Should().BeTrue();
    }

    [Fact]
    public async Task RemapForeignKeys_rewrites_RolePermission_to_cloud_Permission_Id()
    {
        var localPermissionId = Guid.NewGuid();
        var cloudPermissionId = Guid.NewGuid();

        await using var local = CreateContext();
        await using var remote = CreateContext();

        local.Set<Permission>().Add(CreatePermission(localPermissionId, "students.read"));
        await local.SaveChangesAsync();

        remote.Set<Permission>().Add(CreatePermission(cloudPermissionId, "students.read"));
        await remote.SaveChangesAsync();

        var rolePermission = new RolePermission
        {
            Id = Guid.NewGuid(),
            RoleId = Guid.NewGuid(),
            PermissionId = localPermissionId
        };

        await CloudSyncNaturalKey.RemapForeignKeysAsync(
            local, remote, rolePermission, CancellationToken.None);

        rolePermission.PermissionId.Should().Be(cloudPermissionId);
    }

    [Fact]
    public async Task MapByGlobalCode_returns_cloud_Id_for_Permission()
    {
        var localId = Guid.NewGuid();
        var cloudId = Guid.NewGuid();
        await using var local = CreateContext();
        await using var remote = CreateContext();

        local.Set<Permission>().Add(CreatePermission(localId, "payments.create"));
        await local.SaveChangesAsync();
        remote.Set<Permission>().Add(CreatePermission(cloudId, "payments.create"));
        await remote.SaveChangesAsync();

        var mapped = await CloudSyncNaturalKey.MapByGlobalCodeAsync<Permission>(
            local, remote, localId, CancellationToken.None);

        mapped.Should().Be(cloudId);
    }

    private static SchoolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new SchoolDbContext(options) { SuppressCloudSyncEnqueue = true };
    }

    private static Permission CreatePermission(Guid id, string code) => new()
    {
        Id = id,
        Code = code,
        Module = "test",
        DisplayName = code,
        Description = code,
        BusinessDescription = code,
        IsActive = true
    };
}
