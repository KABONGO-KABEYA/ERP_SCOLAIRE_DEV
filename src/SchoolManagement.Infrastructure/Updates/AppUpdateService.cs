using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Updates.DTOs;
using SchoolManagement.Application.Updates.Interfaces;
using SchoolManagement.Domain.Entities.System;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Updates;

public sealed class AppUpdateService : IAppUpdateService
{
    private readonly SchoolDbContext _db;

    public AppUpdateService(SchoolDbContext db)
    {
        _db = db;
    }

    public async Task<UpdateCheckResponseDto?> GetLatestAsync(
        string platform,
        string? currentVersion,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.ApplicationVersions
            .AsNoTracking()
            .Where(x => x.Active)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            return null;
        }

        return MapCheck(entity, platform);
    }

    public async Task<IReadOnlyList<ApplicationVersionAdminDto>> ListVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        var items = await _db.ApplicationVersions
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        return items.Select(MapAdmin).ToList();
    }

    public async Task<ApplicationVersionAdminDto> PublishAsync(
        PublishApplicationVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Version))
        {
            throw new ArgumentException("La version est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(request.Sha256) && request.Active)
        {
            throw new ArgumentException("Le SHA256 est obligatoire pour activer une version.");
        }

        if (string.IsNullOrWhiteSpace(request.DesktopUrl) && string.IsNullOrWhiteSpace(request.MobileUrl))
        {
            throw new ArgumentException("Au moins une URL (Desktop ou Mobile) est requise.");
        }

        var version = request.Version.Trim();
        var existing = await _db.ApplicationVersions
            .FirstOrDefaultAsync(x => x.Version == version, cancellationToken);

        if (request.DeactivateOthers && request.Active)
        {
            var actives = await _db.ApplicationVersions
                .Where(x => x.Active)
                .ToListAsync(cancellationToken);
            foreach (var item in actives)
            {
                item.Active = false;
            }
        }

        var notesJson = JsonSerializer.Serialize(
            request.ReleaseNotes.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToList());

        if (existing is null)
        {
            existing = new ApplicationVersion
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.ApplicationVersions.Add(existing);
        }

        existing.Version = version;
        existing.MinimumVersion = string.IsNullOrWhiteSpace(request.MinimumVersion)
            ? "1.0.0"
            : request.MinimumVersion.Trim();
        existing.Mandatory = request.Mandatory;
        existing.ReleaseDate = request.ReleaseDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        existing.ReleaseNotes = notesJson;
        existing.DesktopUrl = NullIfEmpty(request.DesktopUrl);
        existing.MobileUrl = NullIfEmpty(request.MobileUrl);
        existing.Sha256 = NullIfEmpty(request.Sha256)?.ToLowerInvariant();
        existing.Size = request.Size is > 0 ? request.Size : null;
        existing.SchemaVersion = Math.Max(0, request.SchemaVersion);
        existing.Active = request.Active;

        await _db.SaveChangesAsync(cancellationToken);
        return MapAdmin(existing);
    }

    public async Task<ApplicationVersionAdminDto> SetActiveAsync(
        Guid id,
        bool active,
        bool deactivateOthers,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.ApplicationVersions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Version introuvable.");

        if (active && deactivateOthers)
        {
            var actives = await _db.ApplicationVersions
                .Where(x => x.Active && x.Id != id)
                .ToListAsync(cancellationToken);
            foreach (var item in actives)
            {
                item.Active = false;
            }
        }

        entity.Active = active;
        await _db.SaveChangesAsync(cancellationToken);
        return MapAdmin(entity);
    }

    private static UpdateCheckResponseDto MapCheck(ApplicationVersion entity, string platform)
    {
        var notes = ParseNotes(entity.ReleaseNotes);
        var isMobile = platform.Equals("mobile", StringComparison.OrdinalIgnoreCase)
                       || platform.Equals("android", StringComparison.OrdinalIgnoreCase);

        return new UpdateCheckResponseDto
        {
            LatestVersion = entity.Version,
            MinimumVersion = entity.MinimumVersion,
            Mandatory = entity.Mandatory,
            ReleaseDate = entity.ReleaseDate,
            ReleaseNotes = notes,
            DesktopUrl = entity.DesktopUrl,
            MobileUrl = entity.MobileUrl,
            DownloadUrl = isMobile ? entity.MobileUrl : entity.DesktopUrl,
            Sha256 = entity.Sha256,
            Size = entity.Size,
            SchemaVersion = entity.SchemaVersion
        };
    }

    private static ApplicationVersionAdminDto MapAdmin(ApplicationVersion entity) =>
        new()
        {
            Id = entity.Id,
            Version = entity.Version,
            MinimumVersion = entity.MinimumVersion,
            Mandatory = entity.Mandatory,
            ReleaseDate = entity.ReleaseDate,
            ReleaseNotes = ParseNotes(entity.ReleaseNotes),
            DesktopUrl = entity.DesktopUrl,
            MobileUrl = entity.MobileUrl,
            Sha256 = entity.Sha256,
            Size = entity.Size,
            SchemaVersion = entity.SchemaVersion,
            Active = entity.Active,
            CreatedAtUtc = entity.CreatedAtUtc
        };

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> ParseNotes(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(raw);
            return list is { Count: > 0 } ? list : Array.Empty<string>();
        }
        catch
        {
            return raw.Split(['\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}
