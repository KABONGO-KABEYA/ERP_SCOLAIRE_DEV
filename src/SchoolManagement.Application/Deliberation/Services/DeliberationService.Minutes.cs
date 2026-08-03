using System.Globalization;
using SchoolManagement.Application.Deliberation.DTOs;
using SchoolManagement.Application.Schools;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Deliberation.Services;

public sealed partial class DeliberationService
{
    public async Task<DeliberationMinutesDto> GetMinutesAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default)
    {
        await EnsureValidatedContextAsync(
            schoolId, academicYearId, classRoomId, academicPeriodId, cancellationToken);

        var minutes = await FindMinutesAsync(
            schoolId, academicYearId, classRoomId, academicPeriodId, cancellationToken);

        return MapMinutes(minutes, academicYearId, classRoomId, academicPeriodId);
    }

    public async Task<DeliberationMinutesDto> SaveMinutesAsync(
        Guid schoolId,
        SaveDeliberationMinutesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await EnsureValidatedContextAsync(
            schoolId,
            request.AcademicYearId,
            request.ClassRoomId,
            request.AcademicPeriodId,
            cancellationToken);

        var general = NormalizeText(request.GeneralObservations, 4000);
        var decisions = NormalizeText(request.CouncilDecisions, 4000);
        var recommendations = NormalizeText(request.PedagogicalRecommendations, 4000);

        if (string.IsNullOrWhiteSpace(general)
            && string.IsNullOrWhiteSpace(decisions)
            && string.IsNullOrWhiteSpace(recommendations))
        {
            throw new DomainException(
                "Saisissez au moins une observation, une décision ou une recommandation.");
        }

        var (userId, userName) = ResolveActor();
        var now = DateTime.UtcNow;

        var minutes = await FindMinutesAsync(
            schoolId,
            request.AcademicYearId,
            request.ClassRoomId,
            request.AcademicPeriodId,
            cancellationToken);

        if (minutes is null)
        {
            minutes = new ClassPeriodDeliberationMinutes
            {
                SchoolId = schoolId,
                AcademicYearId = request.AcademicYearId,
                ClassRoomId = request.ClassRoomId,
                AcademicPeriodId = request.AcademicPeriodId,
                GeneralObservations = general,
                CouncilDecisions = decisions,
                PedagogicalRecommendations = recommendations,
                RecordedAtUtc = now,
                RecordedByUserId = userId,
                RecordedByUserName = userName
            };
            await _minutesRepository.AddAsync(minutes, cancellationToken);
        }
        else
        {
            minutes.GeneralObservations = general;
            minutes.CouncilDecisions = decisions;
            minutes.PedagogicalRecommendations = recommendations;
            minutes.RecordedAtUtc = now;
            minutes.RecordedByUserId = userId;
            minutes.RecordedByUserName = userName;
            await _minutesRepository.UpdateAsync(minutes, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapMinutes(
            minutes,
            request.AcademicYearId,
            request.ClassRoomId,
            request.AcademicPeriodId);
    }

    private async Task EnsureValidatedContextAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken)
    {
        // Le PV est accessible dès que les PeriodResult existent — la validation n'est plus un prérequis.
        await EnsureDeliberationContextAsync(
            schoolId, academicYearId, classRoomId, academicPeriodId, cancellationToken);
    }

    private async Task<ClassPeriodDeliberationMinutes?> FindMinutesAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken)
    {
        return (await _minutesRepository.FindAsync(
            m => m.SchoolId == schoolId
                 && m.AcademicYearId == academicYearId
                 && m.ClassRoomId == classRoomId
                 && m.AcademicPeriodId == academicPeriodId,
            cancellationToken)).FirstOrDefault();
    }

    private (Guid? UserId, string UserName) ResolveActor()
    {
        var name = string.IsNullOrWhiteSpace(_currentUser.UserName)
            ? "Système"
            : _currentUser.UserName!;
        return (_currentUser.UserId, name);
    }

    private static string? NormalizeText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static DeliberationMinutesDto MapMinutes(
        ClassPeriodDeliberationMinutes? minutes,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId)
    {
        if (minutes is null)
        {
            return new DeliberationMinutesDto(
                null,
                academicYearId,
                classRoomId,
                academicPeriodId,
                null,
                null,
                null,
                null,
                null,
                "—",
                false);
        }

        return new DeliberationMinutesDto(
            minutes.Id,
            minutes.AcademicYearId,
            minutes.ClassRoomId,
            minutes.AcademicPeriodId,
            minutes.GeneralObservations,
            minutes.CouncilDecisions,
            minutes.PedagogicalRecommendations,
            minutes.RecordedAtUtc,
            minutes.RecordedByUserName,
            minutes.RecordedAtUtc.ToLocalTime()
                .ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture),
            true);
    }
}
