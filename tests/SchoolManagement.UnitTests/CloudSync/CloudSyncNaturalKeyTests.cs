using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Geography;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Settings;
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

    [Fact]
    public async Task ExistsByNaturalKey_skips_Branch_when_same_school_and_code_exist_on_cloud()
    {
        var schoolId = Guid.NewGuid();
        await using var remote = CreateContext();
        remote.Set<Branch>().Add(new Branch
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            Code = "HUM",
            Name = "Humanites"
        });
        await remote.SaveChangesAsync();

        var local = new Branch
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            Code = "HUM",
            Name = "Humanites"
        };

        (await CloudSyncNaturalKey.ExistsByNaturalKeyAsync(remote, local, CancellationToken.None))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByNaturalKey_does_not_mix_Course_between_schools()
    {
        var schoolA = Guid.NewGuid();
        var schoolB = Guid.NewGuid();
        await using var remote = CreateContext();
        remote.Set<Course>().Add(new Course
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolA,
            Code = "HUM-GEO",
            Name = "Geographie"
        });
        await remote.SaveChangesAsync();

        var local = new Course
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolB,
            Code = "HUM-GEO",
            Name = "Geographie"
        };

        (await CloudSyncNaturalKey.ExistsByNaturalKeyAsync(remote, local, CancellationToken.None))
            .Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByNaturalKey_skips_Course_when_same_school_and_code_exist_on_cloud()
    {
        var schoolId = Guid.NewGuid();
        await using var remote = CreateContext();
        remote.Set<Course>().Add(new Course
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            Code = "HUM-GEO",
            Name = "Geographie"
        });
        await remote.SaveChangesAsync();

        var local = new Course
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            Code = "HUM-GEO",
            Name = "Geographie"
        };

        (await CloudSyncNaturalKey.ExistsByNaturalKeyAsync(remote, local, CancellationToken.None))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByNaturalKey_skips_PermissionDependency_when_same_pair_exists_on_cloud()
    {
        var permissionId = Guid.NewGuid();
        var requiresId = Guid.NewGuid();
        await using var remote = CreateContext();
        remote.Set<PermissionDependency>().Add(new PermissionDependency
        {
            Id = Guid.NewGuid(),
            PermissionId = permissionId,
            RequiresPermissionId = requiresId,
            IsActive = true
        });
        await remote.SaveChangesAsync();

        var local = new PermissionDependency
        {
            Id = Guid.NewGuid(),
            PermissionId = permissionId,
            RequiresPermissionId = requiresId,
            IsActive = true
        };

        (await CloudSyncNaturalKey.ExistsByNaturalKeyAsync(remote, local, CancellationToken.None))
            .Should().BeTrue();
    }

    [Fact]
    public async Task RemapForeignKeys_rewrites_PermissionDependency_to_cloud_Permission_Ids()
    {
        var localPermissionId = Guid.NewGuid();
        var localRequiresId = Guid.NewGuid();
        var cloudPermissionId = Guid.NewGuid();
        var cloudRequiresId = Guid.NewGuid();

        await using var local = CreateContext();
        await using var remote = CreateContext();

        local.Set<Permission>().Add(CreatePermission(localPermissionId, "students.create"));
        local.Set<Permission>().Add(CreatePermission(localRequiresId, "students.read"));
        await local.SaveChangesAsync();

        remote.Set<Permission>().Add(CreatePermission(cloudPermissionId, "students.create"));
        remote.Set<Permission>().Add(CreatePermission(cloudRequiresId, "students.read"));
        await remote.SaveChangesAsync();

        var dependency = new PermissionDependency
        {
            Id = Guid.NewGuid(),
            PermissionId = localPermissionId,
            RequiresPermissionId = localRequiresId,
            IsActive = true
        };

        await CloudSyncNaturalKey.RemapForeignKeysAsync(
            local, remote, dependency, CancellationToken.None);

        dependency.PermissionId.Should().Be(cloudPermissionId);
        dependency.RequiresPermissionId.Should().Be(cloudRequiresId);
    }

    [Fact]
    public async Task RemapForeignKeys_rewrites_PostalAddress_CommuneId_to_cloud_commune()
    {
        var localCountryId = Guid.NewGuid();
        var cloudCountryId = Guid.NewGuid();
        var localProvinceId = Guid.NewGuid();
        var cloudProvinceId = Guid.NewGuid();
        var localCityId = Guid.NewGuid();
        var cloudCityId = Guid.NewGuid();
        var localCommuneId = Guid.NewGuid();
        var cloudCommuneId = Guid.NewGuid();

        await using var local = CreateContext();
        await using var remote = CreateContext();

        local.Set<Country>().Add(new Country { Id = localCountryId, Code = "CD", Name = "RDC" });
        local.Set<Province>().Add(new Province
        {
            Id = localProvinceId,
            CountryId = localCountryId,
            Code = "KN",
            Name = "Kinshasa"
        });
        local.Set<City>().Add(new City
        {
            Id = localCityId,
            ProvinceId = localProvinceId,
            Code = "KIN",
            Name = "Kinshasa"
        });
        local.Set<Commune>().Add(new Commune
        {
            Id = localCommuneId,
            CityId = localCityId,
            Code = "GOM",
            Name = "Gombe"
        });
        await local.SaveChangesAsync();

        remote.Set<Country>().Add(new Country { Id = cloudCountryId, Code = "CD", Name = "RDC" });
        remote.Set<Province>().Add(new Province
        {
            Id = cloudProvinceId,
            CountryId = cloudCountryId,
            Code = "KN",
            Name = "Kinshasa"
        });
        remote.Set<City>().Add(new City
        {
            Id = cloudCityId,
            ProvinceId = cloudProvinceId,
            Code = "KIN",
            Name = "Kinshasa"
        });
        remote.Set<Commune>().Add(new Commune
        {
            Id = cloudCommuneId,
            CityId = cloudCityId,
            Code = "GOM",
            Name = "Gombe"
        });
        await remote.SaveChangesAsync();

        var address = new PostalAddress
        {
            Id = Guid.NewGuid(),
            CountryId = localCountryId,
            ProvinceId = localProvinceId,
            CityId = localCityId,
            CommuneId = localCommuneId
        };

        await CloudSyncNaturalKey.RemapForeignKeysAsync(
            local, remote, address, CancellationToken.None);

        address.CountryId.Should().Be(cloudCountryId);
        address.ProvinceId.Should().Be(cloudProvinceId);
        address.CityId.Should().Be(cloudCityId);
        address.CommuneId.Should().Be(cloudCommuneId);
    }

    [Fact]
    public async Task ExistsByNaturalKey_skips_Commune_when_same_city_and_code_exist_on_cloud()
    {
        var cityId = Guid.NewGuid();
        await using var remote = CreateContext();
        remote.Set<Commune>().Add(new Commune
        {
            Id = Guid.NewGuid(),
            CityId = cityId,
            Code = "GOM",
            Name = "Gombe"
        });
        await remote.SaveChangesAsync();

        var local = new Commune
        {
            Id = Guid.NewGuid(),
            CityId = cityId,
            Code = "GOM",
            Name = "Gombe"
        };

        (await CloudSyncNaturalKey.ExistsByNaturalKeyAsync(remote, local, CancellationToken.None))
            .Should().BeTrue();
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
