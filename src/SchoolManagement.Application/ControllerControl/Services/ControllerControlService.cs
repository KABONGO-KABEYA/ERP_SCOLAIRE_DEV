namespace SchoolManagement.Application.ControllerControl.Services;

using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.ControllerControl.DTOs;
using SchoolManagement.Application.ControllerControl.Interfaces;
using SchoolManagement.Application.Finance.DTOs;
using SchoolManagement.Application.Finance.Interfaces;
using SchoolManagement.Application.Schools.Interfaces;
using SchoolManagement.Application.Security;
using SchoolManagement.Application.StudentCards.DTOs;
using SchoolManagement.Application.StudentCards.Interfaces;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;
using System.Text.Json;

public sealed class ControllerControlService : IControllerControlService
{
    private readonly ISchoolService _schoolService;
    private readonly IFinanceOperationService _financeService;
    private readonly IStudentCardService _cardService;
    private readonly IRepository<ClassFeeAmount> _amounts;
    private readonly IRepository<FeeType> _feeTypes;
    private readonly IRepository<FeeInstallment> _installments;
    private readonly ISecurityAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public ControllerControlService(
        ISchoolService schoolService,
        IFinanceOperationService financeService,
        IStudentCardService cardService,
        IRepository<ClassFeeAmount> amounts,
        IRepository<FeeType> feeTypes,
        IRepository<FeeInstallment> installments,
        ISecurityAuditService audit,
        ICurrentUserService currentUser)
    {
        _schoolService = schoolService;
        _financeService = financeService;
        _cardService = cardService;
        _amounts = amounts;
        _feeTypes = feeTypes;
        _installments = installments;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<ControllerSetupDto> GetSetupAsync(
        Guid schoolId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        var years = await _schoolService.GetAcademicYearsAsync(schoolId, cancellationToken);
        var selectedYearId = academicYearId ?? years.FirstOrDefault(y => y.IsCurrent)?.Id;
        var types = new List<ControllerFeeTypeDto>();

        if (selectedYearId.HasValue && years.Any(y => y.Id == selectedYearId.Value))
        {
            var configuredTypeIds = (await _amounts.FindAsync(
                    a => a.SchoolId == schoolId && a.AcademicYearId == selectedYearId.Value,
                    cancellationToken))
                .Select(a => a.FeeTypeId)
                .Distinct()
                .ToHashSet();

            var feeTypes = await _feeTypes.FindAsync(
                f => f.SchoolId == schoolId && f.IsActive && configuredTypeIds.Contains(f.Id),
                cancellationToken);
            types.AddRange(feeTypes
                .OrderBy(f => f.Name)
                .Select(f => new ControllerFeeTypeDto(f.Id, f.Code, f.Name, f.Currency)));
        }

        return new ControllerSetupDto(
            years.OrderByDescending(y => y.StartDate)
                .Select(y => new ControllerAcademicYearDto(y.Id, y.Label, y.IsCurrent, y.IsClosed))
                .ToList(),
            years.FirstOrDefault(y => y.IsCurrent)?.Id,
            types);
    }

    public async Task<IReadOnlyList<ControllerInstallmentDto>> GetInstallmentsAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default)
    {
        var configured = (await _amounts.FindAsync(
                a => a.SchoolId == schoolId
                     && a.AcademicYearId == academicYearId
                     && a.FeeTypeId == feeTypeId,
                cancellationToken))
            .GroupBy(a => a.FeeInstallmentId)
            .ToDictionary(g => g.Key, g => g.Min(a => a.SortOrder));

        var installments = await _installments.FindAsync(
            i => i.SchoolId == schoolId && i.IsActive && configured.Keys.Contains(i.Id),
            cancellationToken);

        return installments
            .Select(i => new ControllerInstallmentDto(i.Id, i.Name, configured[i.Id]))
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Name)
            .ToList();
    }

    public async Task<ControllerStudentSearchResultDto> SearchStudentsAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid feeTypeId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(search) || search.Trim().Length < 2)
            return new ControllerStudentSearchResultDto([]);

        var result = await _financeService.SearchPaymentSituationsAsync(
            schoolId,
            new StudentPaymentSituationSearchRequest(
                AcademicYearId: academicYearId,
                FeeTypeId: feeTypeId,
                Search: search.Trim(),
                PageSize: 20),
            cancellationToken);

        return new ControllerStudentSearchResultDto(result.Items
            .Select(MapStudent)
            .ToList());
    }

    public async Task<ControllerCheckResultDto> CheckByQrAsync(
        Guid schoolId,
        ControllerCheckByQrRequest request,
        CancellationToken cancellationToken = default)
    {
        var card = await _cardService.ResolveByQrAsync(
            schoolId,
            new ResolveCardByQrRequest(request.QrPayload),
            cancellationToken);

        if (card is null)
            throw new DomainException("QR Code inconnu pour cet établissement.");
        if (!card.IsUsable)
            throw new DomainException("Cette carte est expirée, désactivée, perdue, volée ou remplacée.");

        var result = await CheckAsync(
            schoolId,
            card.StudentId,
            request.AcademicYearId,
            request.FeeTypeId,
            request.Mode,
            request.FeeInstallmentId,
            cancellationToken);

        var checkedResult = result with { CardId = card.CardId, CardNumber = card.CardNumber };
        await WriteAuditAsync(schoolId, checkedResult, "QR", cancellationToken);
        return checkedResult;
    }

    public Task<ControllerCheckResultDto> CheckByStudentAsync(
        Guid schoolId,
        ControllerCheckByStudentRequest request,
        CancellationToken cancellationToken = default) =>
        CheckAndAuditByStudentAsync(
            schoolId,
            request.StudentId,
            request.AcademicYearId,
            request.FeeTypeId,
            request.Mode,
            request.FeeInstallmentId,
            cancellationToken);

    private async Task<ControllerCheckResultDto> CheckAndAuditByStudentAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        ControllerCheckMode mode,
        Guid? feeInstallmentId,
        CancellationToken cancellationToken)
    {
        var result = await CheckAsync(
            schoolId,
            studentId,
            academicYearId,
            feeTypeId,
            mode,
            feeInstallmentId,
            cancellationToken);
        await WriteAuditAsync(schoolId, result, "RECHERCHE", cancellationToken);
        return result;
    }

    private async Task<ControllerCheckResultDto> CheckAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        ControllerCheckMode mode,
        Guid? feeInstallmentId,
        CancellationToken cancellationToken)
    {
        if (mode == ControllerCheckMode.Installment && !feeInstallmentId.HasValue)
            throw new DomainException("La tranche de paiement est obligatoire.");

        var situations = await _financeService.SearchPaymentSituationsAsync(
            schoolId,
            new StudentPaymentSituationSearchRequest(
                AcademicYearId: academicYearId,
                FeeTypeId: feeTypeId,
                PageSize: 2,
                StudentId: studentId),
            cancellationToken);
        var situation = situations.Items.SingleOrDefault()
            ?? throw new DomainException("Cet élève n'est pas inscrit pour l'année scolaire sélectionnée.");

        decimal expected;
        decimal paid;
        DateOnly? dueDate = null;
        Guid? selectedInstallmentId = null;
        string? installmentName = null;

        var plan = await _financeService.GetInstallmentPaymentPlanAsync(
            schoolId,
            situation.EnrollmentId,
            feeTypeId,
            cancellationToken);
        if (plan.Lines.Count == 0 || plan.Lines.All(l => l.AmountExpected <= 0))
            throw new DomainException("Ce type de frais n'est pas configuré pour la classe et la catégorie tarifaire de l'élève.");

        if (mode == ControllerCheckMode.Installment)
        {
            var requiredInstallmentId = feeInstallmentId
                ?? throw new DomainException("La tranche de paiement est obligatoire.");
            var line = plan.Lines.SingleOrDefault(l => l.FeeInstallmentId == requiredInstallmentId)
                ?? throw new DomainException("Cette tranche n'est pas configurée pour la classe et la catégorie tarifaire de l'élève.");
            if (line.AmountExpected <= 0)
                throw new DomainException("Cette tranche n'a pas de tarif configuré pour l'élève.");
            expected = line.AmountExpected;
            paid = line.AmountPaid;
            dueDate = line.DueDate;
            selectedInstallmentId = line.FeeInstallmentId;
            installmentName = line.InstallmentName;
        }
        else
        {
            expected = plan.Lines.Sum(l => l.AmountExpected);
            paid = plan.Lines.Sum(l => l.AmountPaid);
            dueDate = plan.Lines
                .Where(l => l.AmountExpected > l.AmountPaid && l.DueDate.HasValue)
                .Select(l => l.DueDate)
                .Min();
        }

        var balance = expected - paid;
        var status = ResolveStatus(expected, paid, balance, dueDate);
        return new ControllerCheckResultDto(
            situation.StudentId,
            situation.RegistrationNumber,
            situation.FullName,
            situation.ClassName,
            situation.PhotoPath,
            situation.AcademicYearId,
            situation.AcademicYearLabel,
            feeTypeId,
            situation.FeeTypeName,
            plan.Currency,
            mode,
            selectedInstallmentId,
            installmentName,
            dueDate,
            expected,
            paid,
            balance,
            status,
            StatusLabel(status),
            null,
            null);
    }

    private static ControllerStudentSearchItemDto MapStudent(StudentPaymentSituationDto item) =>
        new(item.StudentId, item.RegistrationNumber, item.FullName, item.ClassName, item.PhotoPath);

    private Task WriteAuditAsync(
        Guid schoolId,
        ControllerCheckResultDto result,
        string method,
        CancellationToken cancellationToken) =>
        _audit.WriteAsync(
            actionType: "CONTROLLER_FEE_CHECK",
            summary: $"Contrôle {result.StatusLabel} — {result.FullName} — {result.FeeTypeName}",
            schoolId: schoolId,
            actorUserId: _currentUser.UserId,
            actorUserName: _currentUser.UserName,
            actorKind: SecurityAuditActorKind.User,
            targetEntityType: "Student",
            targetEntityId: result.StudentId,
            targetUserName: result.RegistrationNumber,
            newValuesJson: JsonSerializer.Serialize(new
            {
                Method = method,
                result.AcademicYearId,
                result.FeeTypeId,
                result.Mode,
                result.FeeInstallmentId,
                result.AmountExpected,
                result.AmountPaid,
                result.Balance,
                result.Status,
                result.CardId
            }),
            cancellationToken: cancellationToken);

    internal static ControllerPaymentStatus ResolveStatus(
        decimal expected,
        decimal paid,
        decimal balance,
        DateOnly? dueDate,
        DateOnly? today = null)
    {
        if (balance < 0) return ControllerPaymentStatus.Credit;
        if (expected <= 0 || balance <= 0) return ControllerPaymentStatus.UpToDate;
        if (dueDate.HasValue && dueDate.Value < (today ?? DateOnly.FromDateTime(DateTime.UtcNow)))
            return ControllerPaymentStatus.Overdue;
        if (paid <= 0) return ControllerPaymentStatus.Unpaid;
        return ControllerPaymentStatus.Partial;
    }

    private static string StatusLabel(ControllerPaymentStatus status) => status switch
    {
        ControllerPaymentStatus.UpToDate => "À jour",
        ControllerPaymentStatus.Partial => "Partiellement payé",
        ControllerPaymentStatus.Unpaid => "Impayé",
        ControllerPaymentStatus.Overdue => "En retard",
        ControllerPaymentStatus.Credit => "Crédit",
        _ => "Inconnu"
    };
}
