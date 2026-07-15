namespace SchoolManagement.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Configuration.FileStorage;
using SchoolManagement.Application.Enrollment.Interfaces;
using SchoolManagement.Application.Schools;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Infrastructure.Persistence;

public sealed class EnrollmentMaintenanceService : IEnrollmentMaintenanceService
{
    private readonly SchoolDbContext _db;
    private readonly FileStorageConfigurationManager _fileStorageConfigurationManager;

    public EnrollmentMaintenanceService(
        SchoolDbContext db,
        FileStorageConfigurationManager fileStorageConfigurationManager)
    {
        _db = db;
        _fileStorageConfigurationManager = fileStorageConfigurationManager;
    }

    public async Task<EnrollmentResetResultDto> ResetEnrollmentDataAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var studentIds = await _db.Students
            .Where(s => s.SchoolId == schoolId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var enrollmentCount = await _db.Enrollments
            .Where(e => studentIds.Contains(e.StudentId))
            .CountAsync(cancellationToken);

        if (studentIds.Count > 0)
        {
            await _db.ReportCardDetails
                .Where(d => _db.ReportCards.Any(r => r.Id == d.ReportCardId && studentIds.Contains(r.StudentId)))
                .ExecuteDeleteAsync(cancellationToken);

            await _db.ReportCards
                .Where(r => studentIds.Contains(r.StudentId))
                .ExecuteDeleteAsync(cancellationToken);

            await _db.PeriodResults
                .Where(r => studentIds.Contains(r.StudentId))
                .ExecuteDeleteAsync(cancellationToken);

            await _db.GradeEntries
                .Where(g => studentIds.Contains(g.StudentId))
                .ExecuteDeleteAsync(cancellationToken);

            await _db.StudentAttendances
                .Where(a => studentIds.Contains(a.StudentId))
                .ExecuteDeleteAsync(cancellationToken);

            await _db.DisciplineRecords
                .Where(d => studentIds.Contains(d.StudentId))
                .ExecuteDeleteAsync(cancellationToken);

            await _db.MeritRecords
                .Where(m => studentIds.Contains(m.StudentId))
                .ExecuteDeleteAsync(cancellationToken);

            await _db.PaymentReversals
                .Where(r => _db.Payments.Any(p => p.Id == r.PaymentId && studentIds.Contains(p.StudentId)))
                .ExecuteDeleteAsync(cancellationToken);

            await _db.PaymentLines
                .Where(l => _db.Payments.Any(p => p.Id == l.PaymentId && studentIds.Contains(p.StudentId)))
                .ExecuteDeleteAsync(cancellationToken);

            await _db.Payments
                .Where(p => studentIds.Contains(p.StudentId))
                .ExecuteDeleteAsync(cancellationToken);

            await _db.StudentFeeBalances
                .Where(b => studentIds.Contains(b.StudentId))
                .ExecuteDeleteAsync(cancellationToken);

            await _db.StudentDocuments
                .Where(d => studentIds.Contains(d.StudentId))
                .ExecuteDeleteAsync(cancellationToken);

            await _db.StudentStatusHistory
                .Where(h => studentIds.Contains(h.StudentId))
                .ExecuteDeleteAsync(cancellationToken);

            await _db.Enrollments
                .Where(e => studentIds.Contains(e.StudentId))
                .ExecuteDeleteAsync(cancellationToken);

            await _db.StudentGuardians
                .Where(sg => studentIds.Contains(sg.StudentId))
                .ExecuteDeleteAsync(cancellationToken);

            await _db.Students
                .Where(s => studentIds.Contains(s.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        var guardiansRemoved = await _db.Guardians
            .Where(g => g.SchoolId == schoolId)
            .ExecuteDeleteAsync(cancellationToken);

        var classRoomsRepaired = await RepairClassRoomSectionsAsync(schoolId, cancellationToken);
        var filesRemoved = CleanupStudentFiles();

        return new EnrollmentResetResultDto(
            studentIds.Count,
            enrollmentCount,
            guardiansRemoved,
            filesRemoved,
            classRoomsRepaired,
            "Données d'inscription réinitialisées. Vous pouvez repartir de zéro sur l'année scolaire courante.");
    }

    private async Task<int> RepairClassRoomSectionsAsync(Guid schoolId, CancellationToken cancellationToken)
    {
        var sections = await _db.Sections
            .Where(s => s.SchoolId == schoolId)
            .ToListAsync(cancellationToken);
        var pedagogicalMap = await _db.PedagogicalClasses
            .Where(p => p.SchoolId == schoolId)
            .ToDictionaryAsync(p => p.Id, cancellationToken);
        var classRooms = await _db.ClassRooms
            .Where(c => c.SchoolId == schoolId && c.PedagogicalClassId.HasValue)
            .ToListAsync(cancellationToken);

        var repaired = 0;
        foreach (var room in classRooms)
        {
            if (!pedagogicalMap.TryGetValue(room.PedagogicalClassId!.Value, out var pedagogical))
            {
                continue;
            }

            var sectionCode = PedagogicalSectionMapping.GetSectionCode(pedagogical.Program);
            var targetSection = sections.FirstOrDefault(s => s.Code == sectionCode);
            if (targetSection is null || room.SectionId == targetSection.Id)
            {
                continue;
            }

            room.SectionId = targetSection.Id;
            repaired++;
        }

        if (repaired > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return repaired;
    }

    private int CleanupStudentFiles()
    {
        if (!_fileStorageConfigurationManager.IsConfigured())
        {
            return 0;
        }

        var root = _fileStorageConfigurationManager.GetAbsoluteRootPath();
        if (!Directory.Exists(root))
        {
            return 0;
        }

        var removed = 0;
        foreach (var entry in Directory.EnumerateFileSystemEntries(root))
        {
            if (Directory.Exists(entry))
            {
                Directory.Delete(entry, recursive: true);
            }
            else
            {
                File.Delete(entry);
            }

            removed++;
        }

        var apiUploads = Path.Combine(AppContext.BaseDirectory, "uploads", "Dossier_Elève");
        if (Directory.Exists(apiUploads))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(apiUploads))
            {
                if (Directory.Exists(entry))
                {
                    Directory.Delete(entry, recursive: true);
                }
                else
                {
                    File.Delete(entry);
                }

                removed++;
            }
        }

        return removed;
    }
}
