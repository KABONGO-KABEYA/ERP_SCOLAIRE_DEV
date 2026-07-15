namespace SchoolManagement.Application.Schools.Interfaces;

using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Domain.Enums;

public interface IPedagogicalStructureService
{
    Task EnsureInitializedAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<PedagogicalStructureSummaryDto> GetSummaryAsync(
        Guid schoolId,
        bool skipEnsure = false,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PedagogicalClassDto>> GetClassesAsync(
        Guid schoolId,
        string? search = null,
        SchoolProgram? program = null,
        bool? enabledOnly = null,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<PedagogicalClassDto> UpdateClassAsync(
        Guid schoolId,
        Guid classId,
        UpdatePedagogicalClassRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PedagogicalClassDto>> BulkUpdateClassesAsync(
        Guid schoolId,
        BulkUpdatePedagogicalClassesRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassLocalDto>> GetLocalsAsync(
        Guid schoolId,
        Guid pedagogicalClassId,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<ClassLocalDto> CreateLocalAsync(
        Guid schoolId,
        CreateClassLocalRequest request,
        CancellationToken cancellationToken = default);

    Task<ClassLocalDto> UpdateLocalAsync(
        Guid schoolId,
        Guid localId,
        UpdateClassLocalRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteLocalAsync(Guid schoolId, Guid localId, CancellationToken cancellationToken = default);
}
