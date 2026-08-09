namespace SchoolManagement.Application.Grades.Services;

using SchoolManagement.Application.Grades.DTOs;
using SchoolManagement.Application.Schools;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Shared.Constants;

public sealed partial class GradeService
{
    /// <summary>
    /// Publication cotation (visibilité portail parent). Indépendant de results-validation.*.
    /// </summary>
    public async Task PublishPeriodCotationAsync(
        Guid schoolId,
        PublishPeriodCotationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCanPublishCotation();

        await SchoolConfigurationGuards.EnsureSelectableClassRoomAsync(
            _classRoomRepository,
            _pedagogicalClassRepository,
            _yearRepository,
            schoolId,
            request.ClassRoomId,
            cancellationToken);

        var periodResults = await _periodResultRepository.FindAsync(
            p => p.SchoolId == schoolId
                 && p.ClassRoomId == request.ClassRoomId
                 && p.AcademicPeriodId == request.AcademicPeriodId,
            cancellationToken);

        foreach (var result in periodResults)
        {
            result.IsPublished = true;
            await _periodResultRepository.UpdateAsync(result, cancellationToken);
        }

        var evaluations = await _evaluationRepository.FindAsync(
            e => e.ClassRoomId == request.ClassRoomId && e.AcademicPeriodId == request.AcademicPeriodId,
            cancellationToken);

        foreach (var evaluation in evaluations)
        {
            evaluation.IsPublished = true;
            await _evaluationRepository.UpdateAsync(evaluation, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UnpublishPeriodCotationAsync(
        Guid schoolId,
        PublishPeriodCotationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCanUnpublishCotation();

        await SchoolConfigurationGuards.EnsureSelectableClassRoomAsync(
            _classRoomRepository,
            _pedagogicalClassRepository,
            _yearRepository,
            schoolId,
            request.ClassRoomId,
            cancellationToken);

        var periodResults = await _periodResultRepository.FindAsync(
            p => p.SchoolId == schoolId
                 && p.ClassRoomId == request.ClassRoomId
                 && p.AcademicPeriodId == request.AcademicPeriodId,
            cancellationToken);

        foreach (var result in periodResults)
        {
            result.IsPublished = false;
            await _periodResultRepository.UpdateAsync(result, cancellationToken);
        }

        var evaluations = await _evaluationRepository.FindAsync(
            e => e.ClassRoomId == request.ClassRoomId && e.AcademicPeriodId == request.AcademicPeriodId,
            cancellationToken);

        foreach (var evaluation in evaluations)
        {
            evaluation.IsPublished = false;
            await _evaluationRepository.UpdateAsync(evaluation, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private void EnsureCanPublishCotation()
    {
        if (_currentUser.HasPermission(Permissions.AdminFull)
            || _currentUser.HasPermission(Permissions.GradesPublish))
        {
            return;
        }

        throw new DomainException("Vous n'avez pas le droit de publier la cotation.");
    }

    private void EnsureCanUnpublishCotation()
    {
        if (_currentUser.HasPermission(Permissions.AdminFull)
            || _currentUser.HasPermission(Permissions.GradesUnpublish))
        {
            return;
        }

        throw new DomainException("Vous n'avez pas le droit de dépublier la cotation.");
    }
}
