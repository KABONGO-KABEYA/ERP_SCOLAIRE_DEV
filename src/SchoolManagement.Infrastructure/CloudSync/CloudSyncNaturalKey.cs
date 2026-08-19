using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Geography;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.CloudSync;

/// <summary>
/// Catalogue plateforme (permissions, modules, devises) : unique par Code, pas par Id.
/// Une install locale vierge reseede des Guids différents du cloud → INSERT duplique IX_*_Code.
/// </summary>
internal static class CloudSyncNaturalKey
{
    public static async Task RemapForeignKeysAsync(
        SchoolDbContext local,
        SchoolDbContext remote,
        AuditableEntity entity,
        CancellationToken cancellationToken)
    {
        switch (entity)
        {
            case SecurityFunction function:
                function.ModuleId = await MapByGlobalCodeAsync<SecurityModule>(
                    local, remote, function.ModuleId, cancellationToken);
                break;
            case SecurityPage page:
                page.FunctionId = await MapSecurityFunctionIdAsync(
                    local, remote, page.FunctionId, cancellationToken);
                break;
            case SecurityAction action:
                action.PageId = await MapSecurityPageIdAsync(
                    local, remote, action.PageId, cancellationToken);
                break;
            case Permission permission when permission.SecurityActionId is Guid actionId:
                permission.SecurityActionId = await MapSecurityActionIdAsync(
                    local, remote, actionId, cancellationToken);
                break;
            case RolePermission rolePermission:
                rolePermission.PermissionId = await MapByGlobalCodeAsync<Permission>(
                    local, remote, rolePermission.PermissionId, cancellationToken);
                break;
            case PermissionDependency dependency:
                dependency.PermissionId = await MapByGlobalCodeAsync<Permission>(
                    local, remote, dependency.PermissionId, cancellationToken);
                dependency.RequiresPermissionId = await MapByGlobalCodeAsync<Permission>(
                    local, remote, dependency.RequiresPermissionId, cancellationToken);
                break;
            case UserPermissionException exception:
                exception.PermissionId = await MapByGlobalCodeAsync<Permission>(
                    local, remote, exception.PermissionId, cancellationToken);
                break;
            case Province province:
                province.CountryId = await MapByGlobalCodeAsync<Country>(
                    local, remote, province.CountryId, cancellationToken);
                break;
            case City city:
                city.ProvinceId = await MapProvinceIdAsync(
                    local, remote, city.ProvinceId, cancellationToken);
                break;
            case Commune commune:
                commune.CityId = await MapCityIdAsync(
                    local, remote, commune.CityId, cancellationToken);
                break;
            case PostalAddress address:
                if (address.CountryId is Guid countryId)
                {
                    address.CountryId = await MapByGlobalCodeAsync<Country>(
                        local, remote, countryId, cancellationToken);
                }

                if (address.ProvinceId is Guid provinceId)
                {
                    address.ProvinceId = await MapProvinceIdAsync(
                        local, remote, provinceId, cancellationToken);
                }

                if (address.CityId is Guid cityId)
                {
                    address.CityId = await MapCityIdAsync(
                        local, remote, cityId, cancellationToken);
                }

                if (address.CommuneId is Guid communeId)
                {
                    address.CommuneId = await MapCommuneIdAsync(
                        local, remote, communeId, cancellationToken);
                }

                break;
            case Course course when course.BranchId is Guid branchId:
                course.BranchId = await MapBranchIdAsync(
                    local, remote, course.SchoolId, branchId, cancellationToken);
                break;
        }
    }

    public static async Task<bool> ExistsByNaturalKeyAsync(
        SchoolDbContext remote,
        AuditableEntity entity,
        CancellationToken cancellationToken)
    {
        return entity switch
        {
            SecurityModule module => await remote.Set<SecurityModule>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(x => x.Code == module.Code && x.Id != module.Id, cancellationToken),
            Permission permission => await remote.Set<Permission>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(x => x.Code == permission.Code && x.Id != permission.Id, cancellationToken),
            CurrencyDefinition currency => await remote.Set<CurrencyDefinition>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(x => x.Code == currency.Code && x.Id != currency.Id, cancellationToken),
            Branch branch => await remote.Set<Branch>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(
                    x => x.SchoolId == branch.SchoolId
                         && x.Code == branch.Code
                         && x.Id != branch.Id,
                    cancellationToken),
            Course course => await remote.Set<Course>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(
                    x => x.SchoolId == course.SchoolId
                         && x.Code == course.Code
                         && x.ClassRoomId == course.ClassRoomId
                         && x.Id != course.Id,
                    cancellationToken),
            SecurityFunction function => await remote.Set<SecurityFunction>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(
                    x => x.ModuleId == function.ModuleId && x.Code == function.Code && x.Id != function.Id,
                    cancellationToken),
            SecurityPage page => await remote.Set<SecurityPage>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(
                    x => x.FunctionId == page.FunctionId && x.Code == page.Code && x.Id != page.Id,
                    cancellationToken),
            SecurityAction action => await remote.Set<SecurityAction>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(
                    x => x.PageId == action.PageId && x.Code == action.Code && x.Id != action.Id,
                    cancellationToken),
            PermissionDependency dependency => await remote.Set<PermissionDependency>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(
                    x => x.PermissionId == dependency.PermissionId
                         && x.RequiresPermissionId == dependency.RequiresPermissionId
                         && x.Id != dependency.Id,
                    cancellationToken),
            Country country => await remote.Set<Country>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(x => x.Code == country.Code && x.Id != country.Id, cancellationToken),
            Province province => await remote.Set<Province>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(
                    x => x.CountryId == province.CountryId && x.Code == province.Code && x.Id != province.Id,
                    cancellationToken),
            City city => await remote.Set<City>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(
                    x => x.ProvinceId == city.ProvinceId && x.Code == city.Code && x.Id != city.Id,
                    cancellationToken),
            Commune commune => await remote.Set<Commune>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(
                    x => x.CityId == commune.CityId && x.Code == commune.Code && x.Id != commune.Id,
                    cancellationToken),
            _ => false
        };
    }

    internal static async Task<Guid> MapByGlobalCodeAsync<TEntity>(
        SchoolDbContext local,
        SchoolDbContext remote,
        Guid localId,
        CancellationToken cancellationToken)
        where TEntity : AuditableEntity
    {
        if (localId == Guid.Empty)
        {
            return localId;
        }

        var exists = await remote.Set<TEntity>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(e => e.Id == localId, cancellationToken);
        if (exists)
        {
            return localId;
        }

        var localEntity = await local.Set<TEntity>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == localId, cancellationToken);
        if (localEntity is null)
        {
            return localId;
        }

        var code = GetGlobalCode(localEntity);
        if (string.IsNullOrWhiteSpace(code))
        {
            return localId;
        }

        var remoteId = await remote.Set<TEntity>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => EF.Property<string>(e, "Code") == code)
            .Select(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return remoteId == Guid.Empty ? localId : remoteId;
    }

    private static string? GetGlobalCode(AuditableEntity entity) => entity switch
    {
        SecurityModule module => module.Code,
        Permission permission => permission.Code,
        CurrencyDefinition currency => currency.Code,
        Country country => country.Code,
        _ => null
    };

    private static async Task<Guid> MapSecurityFunctionIdAsync(
        SchoolDbContext local,
        SchoolDbContext remote,
        Guid localId,
        CancellationToken cancellationToken)
    {
        if (await ExistsRemoteIdAsync<SecurityFunction>(remote, localId, cancellationToken))
        {
            return localId;
        }

        var localFunction = await local.Set<SecurityFunction>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == localId, cancellationToken);
        if (localFunction is null)
        {
            return localId;
        }

        var remoteModuleId = await MapByGlobalCodeAsync<SecurityModule>(
            local, remote, localFunction.ModuleId, cancellationToken);
        var remoteId = await remote.Set<SecurityFunction>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(f => f.ModuleId == remoteModuleId && f.Code == localFunction.Code)
            .Select(f => f.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return remoteId == Guid.Empty ? localId : remoteId;
    }

    private static async Task<Guid> MapSecurityPageIdAsync(
        SchoolDbContext local,
        SchoolDbContext remote,
        Guid localId,
        CancellationToken cancellationToken)
    {
        if (await ExistsRemoteIdAsync<SecurityPage>(remote, localId, cancellationToken))
        {
            return localId;
        }

        var localPage = await local.Set<SecurityPage>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == localId, cancellationToken);
        if (localPage is null)
        {
            return localId;
        }

        var remoteFunctionId = await MapSecurityFunctionIdAsync(
            local, remote, localPage.FunctionId, cancellationToken);
        var remoteId = await remote.Set<SecurityPage>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.FunctionId == remoteFunctionId && p.Code == localPage.Code)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return remoteId == Guid.Empty ? localId : remoteId;
    }

    private static async Task<Guid> MapSecurityActionIdAsync(
        SchoolDbContext local,
        SchoolDbContext remote,
        Guid localId,
        CancellationToken cancellationToken)
    {
        if (await ExistsRemoteIdAsync<SecurityAction>(remote, localId, cancellationToken))
        {
            return localId;
        }

        var localAction = await local.Set<SecurityAction>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == localId, cancellationToken);
        if (localAction is null)
        {
            return localId;
        }

        var remotePageId = await MapSecurityPageIdAsync(
            local, remote, localAction.PageId, cancellationToken);
        var remoteId = await remote.Set<SecurityAction>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => a.PageId == remotePageId && a.Code == localAction.Code)
            .Select(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return remoteId == Guid.Empty ? localId : remoteId;
    }

    private static async Task<Guid> MapProvinceIdAsync(
        SchoolDbContext local,
        SchoolDbContext remote,
        Guid localId,
        CancellationToken cancellationToken)
    {
        if (await ExistsRemoteIdAsync<Province>(remote, localId, cancellationToken))
        {
            return localId;
        }

        var localProvince = await local.Set<Province>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == localId, cancellationToken);
        if (localProvince is null)
        {
            return localId;
        }

        var remoteCountryId = await MapByGlobalCodeAsync<Country>(
            local, remote, localProvince.CountryId, cancellationToken);
        var remoteId = await remote.Set<Province>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.CountryId == remoteCountryId && p.Code == localProvince.Code)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return remoteId == Guid.Empty ? localId : remoteId;
    }

    private static async Task<Guid> MapCityIdAsync(
        SchoolDbContext local,
        SchoolDbContext remote,
        Guid localId,
        CancellationToken cancellationToken)
    {
        if (await ExistsRemoteIdAsync<City>(remote, localId, cancellationToken))
        {
            return localId;
        }

        var localCity = await local.Set<City>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == localId, cancellationToken);
        if (localCity is null)
        {
            return localId;
        }

        var remoteProvinceId = await MapProvinceIdAsync(
            local, remote, localCity.ProvinceId, cancellationToken);
        var remoteId = await remote.Set<City>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.ProvinceId == remoteProvinceId && c.Code == localCity.Code)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return remoteId == Guid.Empty ? localId : remoteId;
    }

    private static async Task<Guid> MapCommuneIdAsync(
        SchoolDbContext local,
        SchoolDbContext remote,
        Guid localId,
        CancellationToken cancellationToken)
    {
        if (await ExistsRemoteIdAsync<Commune>(remote, localId, cancellationToken))
        {
            return localId;
        }

        var localCommune = await local.Set<Commune>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == localId, cancellationToken);
        if (localCommune is null)
        {
            return localId;
        }

        var remoteCityId = await MapCityIdAsync(
            local, remote, localCommune.CityId, cancellationToken);
        var remoteId = await remote.Set<Commune>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.CityId == remoteCityId && c.Code == localCommune.Code)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return remoteId == Guid.Empty ? localId : remoteId;
    }

    private static async Task<Guid> MapBranchIdAsync(
        SchoolDbContext local,
        SchoolDbContext remote,
        Guid schoolId,
        Guid localId,
        CancellationToken cancellationToken)
    {
        if (await ExistsRemoteIdAsync<Branch>(remote, localId, cancellationToken))
        {
            return localId;
        }

        var localBranch = await local.Set<Branch>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == localId, cancellationToken);
        if (localBranch is null)
        {
            return localId;
        }

        var remoteId = await remote.Set<Branch>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(b => b.SchoolId == schoolId && b.Code == localBranch.Code)
            .Select(b => b.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return remoteId == Guid.Empty ? localId : remoteId;
    }

    private static Task<bool> ExistsRemoteIdAsync<TEntity>(
        SchoolDbContext remote,
        Guid id,
        CancellationToken cancellationToken)
        where TEntity : AuditableEntity =>
        remote.Set<TEntity>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(e => e.Id == id, cancellationToken);
}
