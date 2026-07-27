namespace SchoolManagement.Application.Finance.Services;

using SchoolManagement.Application.Common;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Finance.DTOs;
using SchoolManagement.Application.Finance.Interfaces;
using SchoolManagement.Application.Payments.Services;
using SchoolManagement.Application.Reports.DTOs;
using SchoolManagement.Application.SchoolFees.Interfaces;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

public sealed class FinanceOperationService : IFinanceOperationService
{
    private readonly IRepository<Enrollment> _enrollmentRepository;
    private readonly IRepository<Student> _studentRepository;
    private readonly IRepository<StudentFeeBalance> _balanceRepository;
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IRepository<ClassRoom> _classRoomRepository;
    private readonly IRepository<PedagogicalClass> _pedagogicalClassRepository;
    private readonly IRepository<Section> _sectionRepository;
    private readonly IRepository<FeePricingCategory> _categoryRepository;
    private readonly IRepository<FeeType> _feeTypeRepository;
    private readonly IRepository<ClassFeeAmount> _classFeeAmountRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<PaymentLine> _paymentLineRepository;
    private readonly IRepository<EnrollmentPricingCategoryHistory> _pricingHistoryRepository;
    private readonly ISchoolFeeService _schoolFeeService;
    private readonly IStudentFeeBalanceProvisioner _feeBalanceProvisioner;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public FinanceOperationService(
        IRepository<Enrollment> enrollmentRepository,
        IRepository<Student> studentRepository,
        IRepository<StudentFeeBalance> balanceRepository,
        IRepository<AcademicYear> yearRepository,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<PedagogicalClass> pedagogicalClassRepository,
        IRepository<Section> sectionRepository,
        IRepository<FeePricingCategory> categoryRepository,
        IRepository<FeeType> feeTypeRepository,
        IRepository<ClassFeeAmount> classFeeAmountRepository,
        IRepository<Payment> paymentRepository,
        IRepository<PaymentLine> paymentLineRepository,
        IRepository<EnrollmentPricingCategoryHistory> pricingHistoryRepository,
        ISchoolFeeService schoolFeeService,
        IStudentFeeBalanceProvisioner feeBalanceProvisioner,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _enrollmentRepository = enrollmentRepository;
        _studentRepository = studentRepository;
        _balanceRepository = balanceRepository;
        _yearRepository = yearRepository;
        _classRoomRepository = classRoomRepository;
        _pedagogicalClassRepository = pedagogicalClassRepository;
        _sectionRepository = sectionRepository;
        _categoryRepository = categoryRepository;
        _feeTypeRepository = feeTypeRepository;
        _classFeeAmountRepository = classFeeAmountRepository;
        _paymentRepository = paymentRepository;
        _paymentLineRepository = paymentLineRepository;
        _pricingHistoryRepository = pricingHistoryRepository;
        _schoolFeeService = schoolFeeService;
        _feeBalanceProvisioner = feeBalanceProvisioner;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<StudentPaymentSituationSearchResultDto> SearchPaymentSituationsAsync(
        Guid schoolId,
        StudentPaymentSituationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var year = await ResolveYearAsync(schoolId, request.AcademicYearId, cancellationToken);
        var enrollments = await LoadActiveEnrollmentsAsync(schoolId, year.Id, cancellationToken);
        enrollments = await ApplyStructureFiltersAsync(
            schoolId,
            enrollments,
            request.SectionId,
            request.PedagogicalClassId,
            request.ClassRoomId,
            cancellationToken);

        if (request.FeePricingCategoryId.HasValue)
        {
            enrollments = enrollments
                .Where(e => e.FeePricingCategoryId == request.FeePricingCategoryId.Value)
                .ToList();
        }

        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        var students = (await _studentRepository.FindAsync(
            s => s.SchoolId == schoolId && studentIds.Contains(s.Id),
            cancellationToken)).ToDictionary(s => s.Id);

        var yearTariffs = await _classFeeAmountRepository.FindAsync(
            a => a.SchoolId == schoolId && a.AcademicYearId == year.Id,
            cancellationToken);
        var tariffIdsForYear = yearTariffs.Select(a => a.Id).ToHashSet();
        if (request.FeeTypeId.HasValue)
        {
            tariffIdsForYear = yearTariffs
                .Where(a => a.FeeTypeId == request.FeeTypeId.Value)
                .Select(a => a.Id)
                .ToHashSet();
        }

        var balancesQuery = await _balanceRepository.FindAsync(
            b => studentIds.Contains(b.StudentId) && tariffIdsForYear.Contains(b.ClassFeeAmountId),
            cancellationToken);
        var balances = balancesQuery.ToList();

        FeeType? feeType = null;
        if (request.FeeTypeId.HasValue)
        {
            feeType = (await _feeTypeRepository.FindAsync(
                f => f.Id == request.FeeTypeId.Value && f.SchoolId == schoolId,
                cancellationToken)).FirstOrDefault();
        }

        var balancesByStudent = balances
            .GroupBy(b => b.StudentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var classRooms = await LoadClassRoomDetailsAsync(enrollments.Select(e => e.ClassRoomId), cancellationToken);
        var categories = await LoadCategoryLookupAsync(enrollments.Select(e => e.FeePricingCategoryId), cancellationToken);

        // Tarifs annuels (classe × catégorie × type de frais) pour calculer l'attendu
        // quand le solde élève n'existe pas encore.
        var tariffTotals = await LoadTariffTotalsAsync(
            schoolId,
            year.Id,
            request.FeeTypeId,
            cancellationToken);

        var items = new List<StudentPaymentSituationDto>();
        foreach (var enrollment in enrollments)
        {
            if (!students.TryGetValue(enrollment.StudentId, out var student))
            {
                continue;
            }

            balancesByStudent.TryGetValue(student.Id, out var studentBalances);
            studentBalances ??= [];

            classRooms.TryGetValue(enrollment.ClassRoomId, out var classInfo);

            var amountPaid = studentBalances.Sum(b => b.AmountPaid);
            var amountExpectedFromBalance = studentBalances.Sum(b => b.AmountDue);

            // Attendu = tarif de la classe × catégorie de l'élève × type de frais.
            // Le solde élève complète : payé + éventuel attendu si aucun tarif n'est configuré.
            decimal amountExpectedFromTariff = 0;
            if (request.FeeTypeId.HasValue
                && classInfo.PedagogicalClassId.HasValue
                && tariffTotals.TryGetValue(
                    (classInfo.PedagogicalClassId.Value, enrollment.FeePricingCategoryId),
                    out var tariffAmount))
            {
                amountExpectedFromTariff = tariffAmount;
            }

            var amountExpected = amountExpectedFromTariff > 0
                ? amountExpectedFromTariff
                : amountExpectedFromBalance;

            var balance = amountExpected - amountPaid;
            var status = ResolvePaymentStatus(amountExpected, amountPaid, balance);
            if (request.PaymentStatus.HasValue && status != request.PaymentStatus.Value)
            {
                continue;
            }

            categories.TryGetValue(enrollment.FeePricingCategoryId, out var category);
            var currency = studentBalances.FirstOrDefault()?.Currency
                ?? feeType?.Currency
                ?? Currency.CDF;

            var dto = new StudentPaymentSituationDto(
                enrollment.Id,
                student.Id,
                student.RegistrationNumber,
                StudentDisplayName.Format(student),
                FormatGender(student.Gender),
                string.IsNullOrWhiteSpace(classInfo.ClassName) ? "—" : classInfo.ClassName,
                classInfo.SectionName,
                year.Id,
                year.Label,
                enrollment.FeePricingCategoryId,
                string.IsNullOrWhiteSpace(category.Code) ? "—" : category.Code,
                string.IsNullOrWhiteSpace(category.Name) ? "—" : category.Name,
                feeType?.Id,
                string.IsNullOrWhiteSpace(feeType?.Code) ? "—" : feeType.Code,
                string.IsNullOrWhiteSpace(feeType?.Name) ? "Tous types" : feeType.Name,
                amountPaid,
                amountExpected,
                balance,
                status,
                FormatPaymentStatus(status),
                currency,
                student.PhotoPath);

            if (!MatchesSearch(request.Search, dto.RegistrationNumber, dto.FullName, dto.ClassName))
            {
                continue;
            }

            items.Add(dto);
        }

        items = items
            .OrderBy(i => i.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var pageItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new StudentPaymentSituationSearchResultDto(pageItems, page, pageSize, items.Count);
    }

    public async Task<PaymentSituationReportResultDto> GetPaymentSituationReportAsync(
        Guid schoolId,
        PaymentSituationReportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.AcademicYearId == Guid.Empty)
        {
            throw new ArgumentException("L'année scolaire est obligatoire.", nameof(request));
        }

        if (request.FeeTypeId == Guid.Empty)
        {
            throw new ArgumentException("Le type de frais est obligatoire.", nameof(request));
        }

        var selectedInstallmentIds = request.ScopeKind == PaymentSituationScopeKind.SelectedInstallments
            ? (request.FeeInstallmentIds ?? Array.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList()
            : [];

        if (request.ScopeKind == PaymentSituationScopeKind.SelectedInstallments && selectedInstallmentIds.Count == 0)
        {
            throw new DomainException("Sélectionnez au moins une tranche pour ce périmètre.");
        }

        var year = await ResolveYearAsync(schoolId, request.AcademicYearId, cancellationToken);
        var feeType = (await _feeTypeRepository.FindAsync(
            f => f.Id == request.FeeTypeId && f.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Type de frais introuvable.");

        var enrollments = await LoadActiveEnrollmentsAsync(schoolId, year.Id, cancellationToken);
        enrollments = await ApplyStructureFiltersAsync(
            schoolId,
            enrollments,
            request.SectionId,
            request.PedagogicalClassId,
            request.ClassRoomId,
            cancellationToken);

        if (request.FeePricingCategoryId.HasValue)
        {
            enrollments = enrollments
                .Where(e => e.FeePricingCategoryId == request.FeePricingCategoryId.Value)
                .ToList();
        }

        var classRooms = await LoadClassRoomDetailsAsync(enrollments.Select(e => e.ClassRoomId), cancellationToken);

        if (request.EducationCycle.HasValue || !string.IsNullOrWhiteSpace(request.StudyOption))
        {
            var roomEntities = await _classRoomRepository.FindAsync(
                c => enrollments.Select(e => e.ClassRoomId).Contains(c.Id),
                cancellationToken);
            var pedIds = roomEntities.Where(r => r.PedagogicalClassId.HasValue)
                .Select(r => r.PedagogicalClassId!.Value).Distinct().ToList();
            var secIds = roomEntities.Select(r => r.SectionId).Distinct().ToList();
            var sections = (await _sectionRepository.FindAsync(s => secIds.Contains(s.Id), cancellationToken))
                .ToDictionary(s => s.Id);
            var pedagogical = (await _pedagogicalClassRepository.FindAsync(p => pedIds.Contains(p.Id), cancellationToken))
                .ToDictionary(p => p.Id);

            var allowedRoomIds = roomEntities
                .Where(room =>
                {
                    if (request.EducationCycle.HasValue
                        && (!sections.TryGetValue(room.SectionId, out var section)
                            || section.Cycle != request.EducationCycle.Value))
                    {
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(request.StudyOption))
                    {
                        if (!room.PedagogicalClassId.HasValue
                            || !pedagogical.TryGetValue(room.PedagogicalClassId.Value, out var ped)
                            || !string.Equals(ped.StudyOption, request.StudyOption.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }

                    return true;
                })
                .Select(r => r.Id)
                .ToHashSet();

            enrollments = enrollments.Where(e => allowedRoomIds.Contains(e.ClassRoomId)).ToList();
        }

        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        var students = (await _studentRepository.FindAsync(
            s => s.SchoolId == schoolId && studentIds.Contains(s.Id),
            cancellationToken)).ToDictionary(s => s.Id);

        // Colonnes = tranches du type de frais (ordonnées), filtrées si portée « sélection ».
        var feeTypeInstallments = await _schoolFeeService.GetFeeTypeInstallmentsAsync(
            schoolId, feeType.Id, cancellationToken);
        var installmentColumns = feeTypeInstallments
            .Where(i => selectedInstallmentIds.Count == 0 || selectedInstallmentIds.Contains(i.FeeInstallmentId))
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.InstallmentName, StringComparer.OrdinalIgnoreCase)
            .Select(i => new PaymentSituationInstallmentColumnDto(
                i.FeeInstallmentId,
                i.InstallmentName,
                i.SortOrder))
            .ToList();

        var columnIds = installmentColumns.Select(c => c.FeeInstallmentId).ToHashSet();

        // Tarifs de l'année pour le type de frais (tranches du pivot uniquement).
        var allTariffs = await _classFeeAmountRepository.FindAsync(
            a => a.SchoolId == schoolId
                 && a.AcademicYearId == year.Id
                 && a.FeeTypeId == feeType.Id
                 && columnIds.Contains(a.FeeInstallmentId),
            cancellationToken);

        var scopedTariffs = allTariffs.ToList();

        var expectedByTariff = scopedTariffs
            .GroupBy(a => (a.PedagogicalClassId, a.FeePricingCategoryId, a.FeeInstallmentId))
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));

        var tariffIdToInstallment = scopedTariffs
            .GroupBy(a => a.Id)
            .ToDictionary(g => g.Key, g => g.First().FeeInstallmentId);

        var tariffIds = tariffIdToInstallment.Keys.ToHashSet();
        var balances = tariffIds.Count == 0 || studentIds.Count == 0
            ? []
            : await _balanceRepository.FindAsync(
                b => studentIds.Contains(b.StudentId) && tariffIds.Contains(b.ClassFeeAmountId),
                cancellationToken);

        var paidByStudentInstallment = balances
            .Where(b => tariffIdToInstallment.ContainsKey(b.ClassFeeAmountId))
            .GroupBy(b => (b.StudentId, FeeInstallmentId: tariffIdToInstallment[b.ClassFeeAmountId]))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AmountPaid));

        var pivotRows = new List<PaymentSituationPivotRowDto>();
        foreach (var enrollment in enrollments)
        {
            if (!students.TryGetValue(enrollment.StudentId, out var student))
            {
                continue;
            }

            classRooms.TryGetValue(enrollment.ClassRoomId, out var classInfo);
            var expectedList = new List<decimal>(installmentColumns.Count);
            var paidList = new List<decimal>(installmentColumns.Count);
            var balanceList = new List<decimal>(installmentColumns.Count);
            var applicableList = new List<bool>(installmentColumns.Count);

            foreach (var column in installmentColumns)
            {
                var applicable = false;
                decimal expected = 0;
                if (classInfo.PedagogicalClassId.HasValue
                    && expectedByTariff.TryGetValue(
                        (classInfo.PedagogicalClassId.Value, enrollment.FeePricingCategoryId, column.FeeInstallmentId),
                        out var tariffAmount))
                {
                    applicable = true;
                    expected = tariffAmount;
                }

                paidByStudentInstallment.TryGetValue((student.Id, column.FeeInstallmentId), out var paid);
                if (!applicable)
                {
                    paid = 0;
                }

                expectedList.Add(expected);
                paidList.Add(paid);
                balanceList.Add(applicable ? expected - paid : 0);
                applicableList.Add(applicable);
            }

            var amountExpected = expectedList.Sum();
            var amountPaid = paidList.Sum();
            var balance = amountExpected - amountPaid;
            var inOrder = balance <= 0;

            if (request.SituationFilter == PaymentSituationReportFilter.InOrder && !inOrder)
            {
                continue;
            }

            if (request.SituationFilter == PaymentSituationReportFilter.NotInOrder && inOrder)
            {
                continue;
            }

            pivotRows.Add(new PaymentSituationPivotRowDto(
                student.Id,
                student.RegistrationNumber,
                StudentDisplayName.Format(student),
                string.IsNullOrWhiteSpace(classInfo.ClassName) ? "—" : classInfo.ClassName,
                string.IsNullOrWhiteSpace(classInfo.SectionName) ? "Sans section" : classInfo.SectionName!,
                expectedList,
                paidList,
                balanceList,
                applicableList,
                amountExpected,
                amountPaid,
                balance,
                inOrder));
        }

        // Ne garder que les tranches configurées pour au moins un élève du résultat filtré.
        var usedIndexes = Enumerable.Range(0, installmentColumns.Count)
            .Where(i => pivotRows.Any(r => i < r.InstallmentApplicable.Count && r.InstallmentApplicable[i]))
            .ToList();
        if (usedIndexes.Count != installmentColumns.Count)
        {
            installmentColumns = usedIndexes.Select(i => installmentColumns[i]).ToList();
            pivotRows = pivotRows.Select(r => new PaymentSituationPivotRowDto(
                r.StudentId,
                r.RegistrationNumber,
                r.FullName,
                r.ClassName,
                r.SectionName,
                usedIndexes.Select(i => r.InstallmentExpected[i]).ToList(),
                usedIndexes.Select(i => r.InstallmentPaid[i]).ToList(),
                usedIndexes.Select(i => r.InstallmentBalances[i]).ToList(),
                usedIndexes.Select(i => r.InstallmentApplicable[i]).ToList(),
                r.AmountExpected,
                r.AmountPaid,
                r.Balance,
                r.IsInOrder)).ToList();
        }

        pivotRows = request.SortBy switch
        {
            PaymentSituationSortKind.RegistrationNumber => pivotRows
                .OrderBy(r => r.SectionName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.ClassName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.RegistrationNumber, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            PaymentSituationSortKind.ClassName => pivotRows
                .OrderBy(r => r.SectionName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.ClassName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            PaymentSituationSortKind.BalanceDescending => pivotRows
                .OrderBy(r => r.SectionName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.ClassName, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(r => r.Balance)
                .ThenBy(r => r.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            _ => pivotRows
                .OrderBy(r => r.SectionName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.ClassName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        var rows = pivotRows
            .Select(r => new PaymentSituationReportRowDto(
                r.RegistrationNumber,
                r.FullName,
                r.ClassName,
                r.SectionName,
                r.AmountExpected,
                r.AmountPaid,
                r.Balance,
                feeType.Currency.ToString(),
                r.IsInOrder))
            .ToList();

        var situationLabel = request.SituationFilter switch
        {
            PaymentSituationReportFilter.InOrder => "Élèves en ordre",
            PaymentSituationReportFilter.NotInOrder => "Élèves non en ordre",
            _ => "Tous les élèves"
        };

        var scopeLabel = selectedInstallmentIds.Count > 0
            ? $"Tranche(s) : {string.Join(", ", installmentColumns.Select(c => c.InstallmentName))}"
            : "Totalité du type de frais";

        var filterParts = new List<string>();
        if (request.EducationCycle.HasValue)
        {
            filterParts.Add($"Cycle : {request.EducationCycle}");
        }

        if (request.SectionId.HasValue)
        {
            var section = (await _sectionRepository.FindAsync(s => s.Id == request.SectionId.Value, cancellationToken))
                .FirstOrDefault();
            if (section is not null)
            {
                filterParts.Add($"Section : {section.Name}");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.StudyOption))
        {
            filterParts.Add($"Option : {request.StudyOption.Trim()}");
        }

        if (request.FeePricingCategoryId.HasValue)
        {
            var cat = (await _categoryRepository.FindAsync(c => c.Id == request.FeePricingCategoryId.Value, cancellationToken))
                .FirstOrDefault();
            if (cat is not null)
            {
                filterParts.Add($"Catégorie : {cat.Name}");
            }
        }

        return new PaymentSituationReportResultDto(
            year.Label,
            feeType.Name,
            scopeLabel,
            situationLabel,
            filterParts.Count == 0 ? null : string.Join(" · ", filterParts),
            installmentColumns,
            pivotRows,
            rows,
            rows.Count,
            rows.Count(r => r.IsInOrder),
            rows.Count(r => !r.IsInOrder),
            rows.Sum(r => r.AmountExpected),
            rows.Sum(r => r.AmountPaid),
            rows.Sum(r => r.Balance),
            feeType.Currency.ToString());
    }

    public async Task<StudentInstallmentPaymentPlanDto> GetInstallmentPaymentPlanAsync(
        Guid schoolId,
        Guid enrollmentId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default)
    {
        var enrollment = (await _enrollmentRepository.FindAsync(
            e => e.Id == enrollmentId && e.IsActive, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Inscription introuvable.");

        if (enrollment.Status is not (EnrollmentStatus.Inscrit or EnrollmentStatus.Reinscrit or EnrollmentStatus.PreInscription))
        {
            throw new DomainException("L'inscription n'est pas active.");
        }

        var student = (await _studentRepository.FindAsync(
            s => s.Id == enrollment.StudentId && s.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Élève introuvable.");

        var feeType = (await _feeTypeRepository.FindAsync(
            f => f.Id == feeTypeId && f.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Type de frais introuvable.");

        var classRooms = await LoadClassRoomDetailsAsync([enrollment.ClassRoomId], cancellationToken);
        if (!classRooms.TryGetValue(enrollment.ClassRoomId, out var classInfo)
            || !classInfo.PedagogicalClassId.HasValue)
        {
            throw new DomainException("La classe pédagogique de l'inscription est introuvable.");
        }

        var pedagogicalClassId = classInfo.PedagogicalClassId.Value;
        var schedule = await _schoolFeeService.GetScheduleAsync(
            schoolId,
            enrollment.AcademicYearId,
            pedagogicalClassId,
            enrollment.FeePricingCategoryId,
            feeTypeId,
            cancellationToken);

        var completedPaymentIds = (await _paymentRepository.FindAsync(
                p => p.SchoolId == schoolId
                     && p.StudentId == student.Id
                     && p.AcademicYearId == enrollment.AcademicYearId
                     && p.Status == PaymentStatus.Complet,
                cancellationToken))
            .Select(p => p.Id)
            .ToHashSet();

        var paidByInstallment = new Dictionary<Guid, decimal>();
        if (completedPaymentIds.Count > 0)
        {
            var lines = await _paymentLineRepository.FindAsync(
                l => completedPaymentIds.Contains(l.PaymentId)
                     && l.FeeTypeId == feeTypeId
                     && l.FeeInstallmentId != null,
                cancellationToken);

            paidByInstallment = lines
                .GroupBy(l => l.FeeInstallmentId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Amount));
        }

        var planLines = schedule.Lines
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.InstallmentName)
            .Select(l =>
            {
                var expected = l.Amount;
                paidByInstallment.TryGetValue(l.FeeInstallmentId, out var paid);
                var remaining = Math.Max(0, expected - paid);
                return new InstallmentPaymentPlanLineDto(
                    l.FeeInstallmentId,
                    l.InstallmentName,
                    l.SortOrder,
                    expected,
                    paid,
                    remaining,
                    l.DueDate);
            })
            .ToList();

        return new StudentInstallmentPaymentPlanDto(
            student.Id,
            enrollment.Id,
            enrollment.AcademicYearId,
            feeType.Id,
            feeType.Name,
            enrollment.FeePricingCategoryId,
            pedagogicalClassId,
            schedule.Currency,
            planLines);
    }

    public async Task<StudentPricingAssignmentSearchResultDto> SearchPricingAssignmentsAsync(
        Guid schoolId,
        StudentPricingAssignmentSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var year = await ResolveYearAsync(schoolId, request.AcademicYearId, cancellationToken);
        var enrollments = await LoadActiveEnrollmentsAsync(schoolId, year.Id, cancellationToken);
        enrollments = await ApplyStructureFiltersAsync(
            schoolId,
            enrollments,
            request.SectionId,
            request.PedagogicalClassId,
            request.ClassRoomId,
            cancellationToken);

        if (request.FeePricingCategoryId.HasValue)
        {
            enrollments = enrollments
                .Where(e => e.FeePricingCategoryId == request.FeePricingCategoryId.Value)
                .ToList();
        }

        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        var students = (await _studentRepository.FindAsync(
            s => s.SchoolId == schoolId && studentIds.Contains(s.Id),
            cancellationToken)).ToDictionary(s => s.Id);

        var classRooms = await LoadClassRoomDetailsAsync(enrollments.Select(e => e.ClassRoomId), cancellationToken);
        var categories = await LoadCategoryLookupAsync(enrollments.Select(e => e.FeePricingCategoryId), cancellationToken);

        var items = new List<StudentPricingAssignmentDto>();
        foreach (var enrollment in enrollments)
        {
            if (!students.TryGetValue(enrollment.StudentId, out var student))
            {
                continue;
            }

            classRooms.TryGetValue(enrollment.ClassRoomId, out var classInfo);
            categories.TryGetValue(enrollment.FeePricingCategoryId, out var category);
            var assignedAt = enrollment.UpdatedAt.HasValue
                ? DateOnly.FromDateTime(enrollment.UpdatedAt.Value)
                : enrollment.EnrollmentDate;

            var dto = new StudentPricingAssignmentDto(
                enrollment.Id,
                student.Id,
                student.RegistrationNumber,
                StudentDisplayName.Format(student),
                string.IsNullOrWhiteSpace(classInfo.ClassName) ? "—" : classInfo.ClassName,
                classInfo.SectionName,
                year.Id,
                year.Label,
                enrollment.FeePricingCategoryId,
                string.IsNullOrWhiteSpace(category.Code) ? "—" : category.Code,
                string.IsNullOrWhiteSpace(category.Name) ? "—" : category.Name,
                assignedAt,
                enrollment.UpdatedAt,
                classInfo.PedagogicalClassId);

            if (!MatchesSearch(
                    request.Search,
                    dto.RegistrationNumber,
                    dto.FullName,
                    dto.ClassName,
                    dto.FeePricingCategoryName,
                    dto.FeePricingCategoryCode))
            {
                continue;
            }

            items.Add(dto);
        }

        items = items.OrderBy(i => i.FullName, StringComparer.OrdinalIgnoreCase).ToList();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var pageItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new StudentPricingAssignmentSearchResultDto(pageItems, page, pageSize, items.Count);
    }

    public async Task<StudentPricingAssignmentDto> UpdateEnrollmentPricingCategoryAsync(
        Guid schoolId,
        Guid enrollmentId,
        UpdateEnrollmentPricingCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        PaymentMutationPolicy.EnsureAdministrator(
            _currentUser,
            "Seul l'administrateur peut attribuer ou modifier la catégorie tarifaire d'un élève.");

        await _schoolFeeService.EnsureGeneralPricingCategoryAsync(schoolId, cancellationToken);

        var enrollment = (await _enrollmentRepository.FindAsync(
            e => e.Id == enrollmentId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Inscription introuvable.");

        var student = (await _studentRepository.FindAsync(
            s => s.Id == enrollment.StudentId && s.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Élève introuvable.");

        var category = (await _categoryRepository.FindAsync(
            c => c.Id == request.FeePricingCategoryId && c.SchoolId == schoolId && c.IsActive,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Catégorie tarifaire introuvable ou inactive.");

        var previousCategoryId = enrollment.FeePricingCategoryId;
        if (previousCategoryId == category.Id)
        {
            throw new DomainException("L'élève est déjà affecté à cette catégorie tarifaire.");
        }

        enrollment.FeePricingCategoryId = category.Id;
        await _enrollmentRepository.UpdateAsync(enrollment, cancellationToken);

        var changedAt = DateTime.UtcNow;
        await _pricingHistoryRepository.AddAsync(new EnrollmentPricingCategoryHistory
        {
            EnrollmentId = enrollment.Id,
            PreviousFeePricingCategoryId = previousCategoryId,
            NewFeePricingCategoryId = category.Id,
            ChangedAt = changedAt,
            ChangedByUserId = _currentUser.UserId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        }, cancellationToken);

        var classRooms = await LoadClassRoomDetailsAsync([enrollment.ClassRoomId], cancellationToken);
        classRooms.TryGetValue(enrollment.ClassRoomId, out var classInfo);
        if (!classInfo.PedagogicalClassId.HasValue)
        {
            throw new DomainException("La classe pédagogique est obligatoire pour régénérer les soldes de frais.");
        }

        var feeCurrency = (await _feeTypeRepository.FindAsync(f => f.SchoolId == schoolId, cancellationToken))
            .FirstOrDefault()?.Currency ?? Currency.CDF;

        await _feeBalanceProvisioner.ProvisionForStudentAsync(
            schoolId,
            student.Id,
            enrollment.AcademicYearId,
            classInfo.PedagogicalClassId.Value,
            category.Id,
            feeCurrency,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var year = await ResolveYearAsync(schoolId, enrollment.AcademicYearId, cancellationToken);

        return new StudentPricingAssignmentDto(
            enrollment.Id,
            student.Id,
            student.RegistrationNumber,
            StudentDisplayName.Format(student),
            string.IsNullOrWhiteSpace(classInfo.ClassName) ? "—" : classInfo.ClassName,
            classInfo.SectionName,
            year.Id,
            year.Label,
            category.Id,
            category.Code,
            category.Name,
            enrollment.UpdatedAt.HasValue
                ? DateOnly.FromDateTime(enrollment.UpdatedAt.Value)
                : enrollment.EnrollmentDate,
            enrollment.UpdatedAt,
            classInfo.PedagogicalClassId);
    }

    public async Task<IReadOnlyList<PricingCategoryHistoryLineDto>> GetPricingCategoryHistoryAsync(
        Guid schoolId,
        Guid enrollmentId,
        CancellationToken cancellationToken = default)
    {
        var enrollment = (await _enrollmentRepository.FindAsync(
            e => e.Id == enrollmentId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Inscription introuvable.");

        var student = (await _studentRepository.FindAsync(
            s => s.Id == enrollment.StudentId && s.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Élève introuvable.");

        var history = (await _pricingHistoryRepository.FindAsync(
                h => h.EnrollmentId == enrollmentId, cancellationToken))
            .OrderByDescending(h => h.ChangedAt)
            .ThenByDescending(h => h.CreatedAt)
            .ToList();

        var categoryIds = history
            .SelectMany(h => new[] { h.PreviousFeePricingCategoryId, h.NewFeePricingCategoryId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Append(enrollment.FeePricingCategoryId)
            .Distinct()
            .ToList();
        var categories = await LoadCategoryLookupAsync(categoryIds, cancellationToken);

        if (history.Count == 0)
        {
            var currentName = categories.TryGetValue(enrollment.FeePricingCategoryId, out var current)
                ? (string.IsNullOrWhiteSpace(current.Name) ? "—" : current.Name)
                : "—";
            var assignedAt = enrollment.UpdatedAt ?? enrollment.CreatedAt;
            return
            [
                new PricingCategoryHistoryLineDto(
                    assignedAt,
                    null,
                    currentName,
                    "Affectation actuelle (aucun changement enregistré)")
            ];
        }

        return history.Select(h =>
        {
            string? previousName = null;
            if (h.PreviousFeePricingCategoryId.HasValue
                && categories.TryGetValue(h.PreviousFeePricingCategoryId.Value, out var prev)
                && !string.IsNullOrWhiteSpace(prev.Name))
            {
                previousName = prev.Name;
            }

            var nextName = categories.TryGetValue(h.NewFeePricingCategoryId, out var next)
                && !string.IsNullOrWhiteSpace(next.Name)
                    ? next.Name
                    : "—";
            return new PricingCategoryHistoryLineDto(
                h.ChangedAt,
                previousName,
                nextName,
                h.Notes);
        }).ToList();
    }

    public async Task<StudentApplicableFeesDto> GetApplicableFeesAsync(
        Guid schoolId,
        Guid enrollmentId,
        CancellationToken cancellationToken = default)
    {
        var enrollment = (await _enrollmentRepository.FindAsync(
            e => e.Id == enrollmentId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Inscription introuvable.");

        var student = (await _studentRepository.FindAsync(
            s => s.Id == enrollment.StudentId && s.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Élève introuvable.");

        var year = await ResolveYearAsync(schoolId, enrollment.AcademicYearId, cancellationToken);
        var classRooms = await LoadClassRoomDetailsAsync([enrollment.ClassRoomId], cancellationToken);
        classRooms.TryGetValue(enrollment.ClassRoomId, out var classInfo);
        if (!classInfo.PedagogicalClassId.HasValue)
        {
            throw new DomainException("La classe pédagogique est introuvable pour cet élève.");
        }

        var categories = await LoadCategoryLookupAsync([enrollment.FeePricingCategoryId], cancellationToken);
        var categoryName = categories.TryGetValue(enrollment.FeePricingCategoryId, out var category)
            && !string.IsNullOrWhiteSpace(category.Name)
                ? category.Name
                : "—";

        var tariffs = (await _classFeeAmountRepository.FindAsync(
            a => a.SchoolId == schoolId
                 && a.AcademicYearId == enrollment.AcademicYearId
                 && a.PedagogicalClassId == classInfo.PedagogicalClassId.Value
                 && a.FeePricingCategoryId == enrollment.FeePricingCategoryId,
            cancellationToken)).ToList();

        var feeTypes = (await _feeTypeRepository.FindAsync(f => f.SchoolId == schoolId, cancellationToken))
            .ToDictionary(f => f.Id);
        var installments = (await _schoolFeeService.GetInstallmentsAsync(schoolId, cancellationToken))
            .ToDictionary(i => i.Id);

        var lines = new List<StudentApplicableFeeLineDto>();
        foreach (var tariff in tariffs.Where(t => t.Amount > 0))
        {
            feeTypes.TryGetValue(tariff.FeeTypeId, out var feeType);
            installments.TryGetValue(tariff.FeeInstallmentId, out var installment);
            var currency = feeType?.Currency.ToString() ?? Currency.CDF.ToString();
            lines.Add(new StudentApplicableFeeLineDto(
                feeType?.Name ?? "—",
                installment?.Name ?? "—",
                installment?.SortOrder ?? int.MaxValue,
                tariff.Amount,
                currency));
        }

        lines = lines
            .OrderBy(l => l.FeeTypeName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(l => l.SortOrder)
            .ThenBy(l => l.InstallmentName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var currencyLabel = lines.Select(l => l.Currency).Distinct().DefaultIfEmpty(Currency.CDF.ToString()).First();
        return new StudentApplicableFeesDto(
            enrollment.Id,
            StudentDisplayName.Format(student),
            string.IsNullOrWhiteSpace(classInfo.ClassName) ? "—" : classInfo.ClassName,
            categoryName,
            year.Label,
            lines,
            lines.Sum(l => l.Amount),
            currencyLabel);
    }

    private async Task<List<Enrollment>> LoadActiveEnrollmentsAsync(
        Guid schoolId,
        Guid academicYearId,
        CancellationToken cancellationToken)
    {
        await _schoolFeeService.EnsureGeneralPricingCategoryAsync(schoolId, cancellationToken);

        var schoolStudentIds = (await _studentRepository.FindAsync(
            s => s.SchoolId == schoolId, cancellationToken)).Select(s => s.Id).ToHashSet();

        return (await _enrollmentRepository.FindAsync(
                e => e.AcademicYearId == academicYearId
                     && e.IsActive
                     && schoolStudentIds.Contains(e.StudentId),
                cancellationToken))
            .Where(e => e.Status is EnrollmentStatus.Inscrit or EnrollmentStatus.Reinscrit or EnrollmentStatus.PreInscription)
            .ToList();
    }

    private async Task<List<Enrollment>> ApplyStructureFiltersAsync(
        Guid schoolId,
        List<Enrollment> enrollments,
        Guid? sectionId,
        Guid? pedagogicalClassId,
        Guid? classRoomId,
        CancellationToken cancellationToken)
    {
        if (classRoomId.HasValue)
        {
            return enrollments.Where(e => e.ClassRoomId == classRoomId.Value).ToList();
        }

        if (!sectionId.HasValue && !pedagogicalClassId.HasValue)
        {
            return enrollments;
        }

        var roomIds = enrollments.Select(e => e.ClassRoomId).Distinct().ToList();
        var rooms = (await _classRoomRepository.FindAsync(c => roomIds.Contains(c.Id), cancellationToken)).ToList();
        if (sectionId.HasValue)
        {
            // Sections can be duplicated across years / structure rebuilds. Match by name
            // (same approach as StudentService) so "Primaire" includes all classrooms
            // linked to any Primaire section record.
            var selectedSection = (await _sectionRepository.FindAsync(
                    s => s.Id == sectionId.Value && s.SchoolId == schoolId,
                    cancellationToken))
                .FirstOrDefault();

            if (selectedSection is not null)
            {
                var matchingSectionIds = (await _sectionRepository.FindAsync(
                        s => s.SchoolId == schoolId,
                        cancellationToken))
                    .Where(s =>
                        string.Equals(s.Name.Trim(), selectedSection.Name.Trim(), StringComparison.OrdinalIgnoreCase)
                        || string.Equals(s.Code.Trim(), selectedSection.Code.Trim(), StringComparison.OrdinalIgnoreCase))
                    .Select(s => s.Id)
                    .ToHashSet();
                rooms = rooms.Where(r => matchingSectionIds.Contains(r.SectionId)).ToList();
            }
            else
            {
                rooms = rooms.Where(r => r.SectionId == sectionId.Value).ToList();
            }
        }

        if (pedagogicalClassId.HasValue)
        {
            rooms = rooms.Where(r => r.PedagogicalClassId == pedagogicalClassId.Value).ToList();
        }

        var allowedRoomIds = rooms.Select(r => r.Id).ToHashSet();
        return enrollments.Where(e => allowedRoomIds.Contains(e.ClassRoomId)).ToList();
    }

    private async Task<AcademicYear> ResolveYearAsync(
        Guid schoolId,
        Guid? academicYearId,
        CancellationToken cancellationToken)
    {
        if (academicYearId.HasValue)
        {
            return (await _yearRepository.FindAsync(
                y => y.Id == academicYearId.Value && y.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
                ?? throw new KeyNotFoundException("Année scolaire introuvable.");
        }

        return (await _yearRepository.FindAsync(
            y => y.SchoolId == schoolId && y.IsCurrent, cancellationToken)).FirstOrDefault()
            ?? (await _yearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken))
                .OrderByDescending(y => y.StartDate)
                .FirstOrDefault()
            ?? throw new DomainException("Aucune année scolaire n'est configurée.");
    }

    private async Task<Dictionary<Guid, (string ClassName, string? SectionName, Guid? PedagogicalClassId)>> LoadClassRoomDetailsAsync(
        IEnumerable<Guid> classRoomIds,
        CancellationToken cancellationToken)
    {
        var ids = classRoomIds.Distinct().ToList();
        var rooms = (await _classRoomRepository.FindAsync(c => ids.Contains(c.Id), cancellationToken)).ToList();
        var pedIds = rooms.Where(r => r.PedagogicalClassId.HasValue).Select(r => r.PedagogicalClassId!.Value).Distinct().ToList();
        var sectionIds = rooms.Select(r => r.SectionId).Distinct().ToList();
        var pedagogical = (await _pedagogicalClassRepository.FindAsync(p => pedIds.Contains(p.Id), cancellationToken))
            .ToDictionary(p => p.Id);
        var sections = (await _sectionRepository.FindAsync(s => sectionIds.Contains(s.Id), cancellationToken))
            .ToDictionary(s => s.Id);

        var result = new Dictionary<Guid, (string ClassName, string? SectionName, Guid? PedagogicalClassId)>();
        foreach (var room in rooms)
        {
            var className = room.Name;
            if (room.PedagogicalClassId.HasValue && pedagogical.TryGetValue(room.PedagogicalClassId.Value, out var ped))
            {
                className = string.IsNullOrWhiteSpace(room.Name)
                    ? ped.DisplayName
                    : $"{ped.DisplayName} {room.Name}".Trim();
            }

            sections.TryGetValue(room.SectionId, out var section);
            result[room.Id] = (className, section?.Name, room.PedagogicalClassId);
        }

        return result;
    }

    private async Task<Dictionary<(Guid PedagogicalClassId, Guid FeePricingCategoryId), decimal>> LoadTariffTotalsAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid? feeTypeId,
        CancellationToken cancellationToken)
    {
        if (!feeTypeId.HasValue)
        {
            return new Dictionary<(Guid, Guid), decimal>();
        }

        var rows = await _classFeeAmountRepository.FindAsync(
            a => a.SchoolId == schoolId
                 && a.AcademicYearId == academicYearId
                 && a.FeeTypeId == feeTypeId.Value,
            cancellationToken);

        return rows
            .GroupBy(a => (a.PedagogicalClassId, a.FeePricingCategoryId))
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));
    }

    private async Task<Dictionary<Guid, (string Code, string Name)>> LoadCategoryLookupAsync(
        IEnumerable<Guid> categoryIds,
        CancellationToken cancellationToken)
    {
        var ids = categoryIds.Distinct().ToList();
        var categories = await _categoryRepository.FindAsync(c => ids.Contains(c.Id), cancellationToken);
        return categories.ToDictionary(c => c.Id, c => (c.Code, c.Name));
    }

    private static PaymentSituationStatus ResolvePaymentStatus(decimal expected, decimal paid, decimal balance)
    {
        if (expected <= 0 && paid <= 0)
        {
            return PaymentSituationStatus.AJour;
        }

        if (paid > expected)
        {
            return PaymentSituationStatus.Credit;
        }

        if (balance <= 0)
        {
            return PaymentSituationStatus.AJour;
        }

        if (paid <= 0)
        {
            return PaymentSituationStatus.Impaye;
        }

        return PaymentSituationStatus.EnRetard;
    }

    private static string FormatPaymentStatus(PaymentSituationStatus status) => status switch
    {
        PaymentSituationStatus.AJour => "À jour",
        PaymentSituationStatus.EnRetard => "En retard",
        PaymentSituationStatus.Impaye => "Impayé",
        PaymentSituationStatus.Credit => "Crédit",
        _ => "—"
    };

    private static string FormatGender(Gender gender) => gender switch
    {
        Gender.Masculin => "M",
        Gender.Feminin => "F",
        _ => "—"
    };

    private static bool MatchesSearch(string? search, params string?[] fields)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var term = search.Trim();
        return fields.Any(f => !string.IsNullOrWhiteSpace(f)
            && f.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
