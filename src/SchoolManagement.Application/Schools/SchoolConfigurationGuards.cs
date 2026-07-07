namespace SchoolManagement.Application.Schools;

using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Exceptions;

public static class SchoolConfigurationGuards
{
    public static async Task<AcademicYear> EnsureActiveAcademicYearAsync(
        IRepository<AcademicYear> yearRepository,
        Guid schoolId,
        Guid academicYearId,
        CancellationToken cancellationToken = default)
    {
        var year = (await yearRepository.FindAsync(
            y => y.Id == academicYearId && y.SchoolId == schoolId,
            cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Année scolaire introuvable.");

        if (!year.IsCurrent)
        {
            throw new DomainException("Cette opération n'est autorisée que sur l'année scolaire courante.");
        }

        if (year.IsClosed)
        {
            throw new DomainException("Cette année scolaire est clôturée.");
        }

        return year;
    }

    public static async Task<ClassRoom> EnsureSelectableClassRoomAsync(
        IRepository<ClassRoom> classRoomRepository,
        IRepository<PedagogicalClass> pedagogicalClassRepository,
        Guid schoolId,
        Guid classRoomId,
        CancellationToken cancellationToken = default)
    {
        var classRoom = (await classRoomRepository.FindAsync(
            c => c.Id == classRoomId && c.SchoolId == schoolId,
            cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Classe introuvable.");

        var pedagogicalMap = ClassRoomAvailability.BuildMap(
            await pedagogicalClassRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken));

        if (!ClassRoomAvailability.IsSelectable(classRoom, pedagogicalMap))
        {
            throw new DomainException("Cette classe n'est pas active dans le paramétrage pédagogique.");
        }

        return classRoom;
    }

    public static async Task<IReadOnlyDictionary<Guid, PedagogicalClass>> BuildPedagogicalMapAsync(
        IRepository<PedagogicalClass> pedagogicalClassRepository,
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var classes = await pedagogicalClassRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken);
        return ClassRoomAvailability.BuildMap(classes);
    }
}
