namespace SchoolManagement.Application.Withholdings.Services;

using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Withholdings.DTOs;
using SchoolManagement.Application.Withholdings.Interfaces;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

public sealed class WithholdingService : IWithholdingService
{
    private readonly IRepository<WithholdingType> _typeRepository;
    private readonly IRepository<WithholdingConfiguration> _configRepository;
    private readonly IRepository<WithholdingApplication> _applicationRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<PaymentLine> _paymentLineRepository;
    private readonly IRepository<RevenueAllocationEntry> _allocationEntryRepository;
    private readonly IRepository<StudentFeeBalance> _balanceRepository;
    private readonly IRepository<ClassFeeAmount> _classFeeAmountRepository;
    private readonly IRepository<Enrollment> _enrollmentRepository;
    private readonly IRepository<ClassRoom> _classRoomRepository;
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IRepository<FeeType> _feeTypeRepository;
    private readonly IRepository<FeeInstallment> _installmentRepository;
    private readonly IRepository<FeePricingCategory> _categoryRepository;
    private readonly IWithholdingEngine _engine;
    private readonly IUnitOfWork _unitOfWork;

    public WithholdingService(
        IRepository<WithholdingType> typeRepository,
        IRepository<WithholdingConfiguration> configRepository,
        IRepository<WithholdingApplication> applicationRepository,
        IRepository<Payment> paymentRepository,
        IRepository<PaymentLine> paymentLineRepository,
        IRepository<RevenueAllocationEntry> allocationEntryRepository,
        IRepository<StudentFeeBalance> balanceRepository,
        IRepository<ClassFeeAmount> classFeeAmountRepository,
        IRepository<Enrollment> enrollmentRepository,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<AcademicYear> yearRepository,
        IRepository<FeeType> feeTypeRepository,
        IRepository<FeeInstallment> installmentRepository,
        IRepository<FeePricingCategory> categoryRepository,
        IWithholdingEngine engine,
        IUnitOfWork unitOfWork)
    {
        _typeRepository = typeRepository;
        _configRepository = configRepository;
        _applicationRepository = applicationRepository;
        _paymentRepository = paymentRepository;
        _paymentLineRepository = paymentLineRepository;
        _allocationEntryRepository = allocationEntryRepository;
        _balanceRepository = balanceRepository;
        _classFeeAmountRepository = classFeeAmountRepository;
        _enrollmentRepository = enrollmentRepository;
        _classRoomRepository = classRoomRepository;
        _yearRepository = yearRepository;
        _feeTypeRepository = feeTypeRepository;
        _installmentRepository = installmentRepository;
        _categoryRepository = categoryRepository;
        _engine = engine;
        _unitOfWork = unitOfWork;
    }

    public async Task EnsureDefaultTypesAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var existing = await _typeRepository.FindAsync(t => t.SchoolId == schoolId, cancellationToken);
        var codes = existing.Select(t => t.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var defaults = new (string Code, string Name, string Description)[]
        {
            ("RET_DIO", "Contribution diocésaine", "Retenue diocésaine"),
            ("RET_SOC", "Fonds social", "Fonds social de l'établissement"),
            ("RET_ASS", "Assurance scolaire", "Assurance scolaire"),
            ("RET_MUT", "Mutuelle", "Cotisation mutuelle")
        };

        var added = false;
        foreach (var item in defaults)
        {
            if (codes.Contains(item.Code))
            {
                continue;
            }

            await _typeRepository.AddAsync(new WithholdingType
            {
                SchoolId = schoolId,
                Code = item.Code,
                Name = item.Name,
                Description = item.Description,
                IsActive = true
            }, cancellationToken);
            codes.Add(item.Code);
            added = true;
        }

        if (added)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<WithholdingTypeDto>> GetTypesAsync(
        Guid schoolId,
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        await EnsureDefaultTypesAsync(schoolId, cancellationToken);
        var items = await _typeRepository.FindAsync(t => t.SchoolId == schoolId, cancellationToken);
        if (activeOnly)
        {
            items = items.Where(t => t.IsActive).ToList();
        }

        return items
            .OrderBy(t => t.Code)
            .Select(MapType)
            .ToList();
    }

    public async Task<WithholdingTypeDto> CreateTypeAsync(
        Guid schoolId,
        SaveWithholdingTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(request.Code);
        var duplicate = await _typeRepository.FindAsync(
            t => t.SchoolId == schoolId && t.Code == code, cancellationToken);
        if (duplicate.Count > 0)
        {
            throw new DomainException($"Un type de retenue avec le code « {code} » existe déjà.");
        }

        var entity = new WithholdingType
        {
            SchoolId = schoolId,
            Code = code,
            Name = RequireName(request.Name),
            Description = NormalizeOptional(request.Description),
            IsActive = request.IsActive
        };
        await _typeRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapType(entity);
    }

    public async Task<WithholdingTypeDto> UpdateTypeAsync(
        Guid schoolId,
        Guid typeId,
        SaveWithholdingTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetTypeEntityAsync(schoolId, typeId, cancellationToken);
        var code = NormalizeCode(request.Code);
        var duplicate = (await _typeRepository.FindAsync(
            t => t.SchoolId == schoolId && t.Code == code && t.Id != typeId, cancellationToken)).Any();
        if (duplicate)
        {
            throw new DomainException($"Un type de retenue avec le code « {code} » existe déjà.");
        }

        entity.Code = code;
        entity.Name = RequireName(request.Name);
        entity.Description = NormalizeOptional(request.Description);
        entity.IsActive = request.IsActive;
        await _typeRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapType(entity);
    }

    public async Task DeactivateTypeAsync(Guid schoolId, Guid typeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetTypeEntityAsync(schoolId, typeId, cancellationToken);
        entity.IsActive = false;
        await _typeRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<WithholdingConfigurationSearchResultDto> SearchConfigurationsAsync(
        Guid schoolId,
        WithholdingConfigurationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var items = await FilterConfigurationsAsync(schoolId, request, cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 500);
        var pageItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var dtos = new List<WithholdingConfigurationDto>();
        foreach (var item in pageItems)
        {
            dtos.Add(await MapConfigurationAsync(schoolId, item, cancellationToken));
        }

        return new WithholdingConfigurationSearchResultDto(dtos, page, pageSize, items.Count);
    }

    public async Task<WithholdingConfigurationDto?> GetConfigurationByIdAsync(
        Guid schoolId,
        Guid configurationId,
        CancellationToken cancellationToken = default)
    {
        var entity = (await _configRepository.FindAsync(
            c => c.Id == configurationId && c.SchoolId == schoolId, cancellationToken)).FirstOrDefault();
        return entity is null ? null : await MapConfigurationAsync(schoolId, entity, cancellationToken);
    }

    public async Task<WithholdingConfigurationDto> CreateConfigurationAsync(
        Guid schoolId,
        SaveWithholdingConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidateConfigurationRequestAsync(schoolId, request, excludeId: null, cancellationToken);

        var entity = new WithholdingConfiguration
        {
            SchoolId = schoolId,
            AcademicYearId = request.AcademicYearId,
            WithholdingTypeId = request.WithholdingTypeId,
            FeeTypeId = request.FeeTypeId,
            FeeInstallmentId = request.FeeInstallmentId,
            PricingCategoryId = request.PricingCategoryId,
            CalculationMode = request.CalculationMode,
            Value = request.Value,
            IsActive = request.IsActive
        };
        await _configRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (await GetConfigurationByIdAsync(schoolId, entity.Id, cancellationToken))!;
    }

    public async Task<WithholdingConfigurationDto> UpdateConfigurationAsync(
        Guid schoolId,
        Guid configurationId,
        SaveWithholdingConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetConfigurationEntityAsync(schoolId, configurationId, cancellationToken);
        await ValidateConfigurationRequestAsync(schoolId, request, configurationId, cancellationToken);

        entity.AcademicYearId = request.AcademicYearId;
        entity.WithholdingTypeId = request.WithholdingTypeId;
        entity.FeeTypeId = request.FeeTypeId;
        entity.FeeInstallmentId = request.FeeInstallmentId;
        entity.PricingCategoryId = request.PricingCategoryId;
        entity.CalculationMode = request.CalculationMode;
        entity.Value = request.Value;
        entity.IsActive = request.IsActive;
        await _configRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (await GetConfigurationByIdAsync(schoolId, configurationId, cancellationToken))!;
    }

    public async Task DeactivateConfigurationAsync(Guid schoolId, Guid configurationId, CancellationToken cancellationToken = default)
    {
        var entity = await GetConfigurationEntityAsync(schoolId, configurationId, cancellationToken);
        entity.IsActive = false;
        await _configRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteConfigurationAsync(Guid schoolId, Guid configurationId, CancellationToken cancellationToken = default)
    {
        var entity = await GetConfigurationEntityAsync(schoolId, configurationId, cancellationToken);
        await _configRepository.DeleteAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WithholdingConfigurationDto>> ResolveApplicableAsync(
        Guid schoolId,
        WithholdingResolveContext context,
        CancellationToken cancellationToken = default)
    {
        var configs = await LoadApplicableEntitiesAsync(schoolId, context, cancellationToken);
        var result = new List<WithholdingConfigurationDto>();
        foreach (var config in configs)
        {
            result.Add(await MapConfigurationAsync(schoolId, config, cancellationToken));
        }

        return result;
    }

    public async Task<WithholdingCalculationResult> CalculateForPaymentLineAsync(
        Guid schoolId,
        decimal grossAmount,
        WithholdingResolveContext context,
        CancellationToken cancellationToken = default)
    {
        var configs = await LoadApplicableEntitiesAsync(schoolId, context, cancellationToken);
        if (context.StudentId.HasValue)
        {
            configs = await FilterConfigsForStudentAsync(
                schoolId,
                context.StudentId.Value,
                context,
                configs,
                grossAmount,
                cancellationToken);
        }

        return _engine.Calculate(grossAmount, configs);
    }

    public async Task RecordApplicationsAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        Guid paymentId,
        Guid paymentLineId,
        WithholdingCalculationResult result,
        CancellationToken cancellationToken = default)
    {
        foreach (var line in result.Lines.Where(l =>
                     l.WithheldAmount > 0
                     && l.CalculationMode == WithholdingCalculationMode.MontantFixe))
        {
            await _applicationRepository.AddAsync(new WithholdingApplication
            {
                SchoolId = schoolId,
                StudentId = studentId,
                AcademicYearId = academicYearId,
                WithholdingConfigurationId = line.ConfigurationId,
                PaymentId = paymentId,
                PaymentLineId = paymentLineId,
                Amount = line.WithheldAmount
            }, cancellationToken);
        }
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlySet<Guid>>> GetFixedApplicationConfigurationIdsByLineAsync(
        Guid schoolId,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var applications = await _applicationRepository.FindAsync(
            a => a.SchoolId == schoolId && a.PaymentId == paymentId,
            cancellationToken);
        if (applications.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlySet<Guid>>();
        }

        var configIds = applications.Select(a => a.WithholdingConfigurationId).Distinct().ToList();
        var fixedConfigIds = (await _configRepository.FindAsync(
                c => configIds.Contains(c.Id)
                     && c.CalculationMode == WithholdingCalculationMode.MontantFixe,
                cancellationToken))
            .Select(c => c.Id)
            .ToHashSet();

        return applications
            .Where(a => fixedConfigIds.Contains(a.WithholdingConfigurationId))
            .GroupBy(a => a.PaymentLineId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlySet<Guid>)g.Select(a => a.WithholdingConfigurationId).ToHashSet());
    }

    public async Task RemoveApplicationsForPaymentAsync(
        Guid schoolId,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var applications = await _applicationRepository.FindAsync(
            a => a.SchoolId == schoolId && a.PaymentId == paymentId,
            cancellationToken);
        foreach (var application in applications)
        {
            await _applicationRepository.DeleteAsync(application, cancellationToken);
        }
    }

    public async Task<byte[]> ExportConfigurationsExcelAsync(
        Guid schoolId,
        WithholdingConfigurationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var search = request with { Page = 1, PageSize = 50000 };
        var data = await SearchConfigurationsAsync(schoolId, search, cancellationToken);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Retenues");
        var headers = new[]
        {
            "Code", "Libellé", "Année", "Type de frais", "Tranche", "Catégorie", "Mode", "Valeur", "Statut"
        };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        var row = 2;
        foreach (var item in data.Items)
        {
            sheet.Cell(row, 1).Value = item.WithholdingTypeCode;
            sheet.Cell(row, 2).Value = item.WithholdingTypeName;
            sheet.Cell(row, 3).Value = item.AcademicYearLabel;
            sheet.Cell(row, 4).Value = item.FeeTypeName;
            sheet.Cell(row, 5).Value = item.FeeInstallmentName ?? "Toutes";
            sheet.Cell(row, 6).Value = item.PricingCategoryName ?? "Toutes";
            sheet.Cell(row, 7).Value = item.CalculationMode == WithholdingCalculationMode.Pourcentage
                ? "Pourcentage"
                : "Montant fixe";
            sheet.Cell(row, 8).Value = item.Value;
            sheet.Cell(row, 9).Value = item.IsActive ? "Active" : "Inactive";
            row++;
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportConfigurationsPdfAsync(
        Guid schoolId,
        WithholdingConfigurationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var search = request with { Page = 1, PageSize = 2000 };
        var data = await SearchConfigurationsAsync(schoolId, search, cancellationToken);
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Header().Text("Configuration des retenues").SemiBold().FontSize(16);
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });
                    table.Header(header =>
                    {
                        header.Cell().Text("Code").SemiBold();
                        header.Cell().Text("Libellé").SemiBold();
                        header.Cell().Text("Année").SemiBold();
                        header.Cell().Text("Type frais").SemiBold();
                        header.Cell().Text("Mode").SemiBold();
                        header.Cell().Text("Valeur").SemiBold();
                    });
                    foreach (var item in data.Items)
                    {
                        table.Cell().Text(item.WithholdingTypeCode);
                        table.Cell().Text(item.WithholdingTypeName);
                        table.Cell().Text(item.AcademicYearLabel);
                        table.Cell().Text(item.FeeTypeName);
                        table.Cell().Text(item.CalculationMode == WithholdingCalculationMode.Pourcentage
                            ? "Pourcentage"
                            : "Montant fixe");
                        table.Cell().Text(item.Value.ToString("N2"));
                    }
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                });
            });
        });

        return document.GeneratePdf();
    }

    private async Task<List<WithholdingConfiguration>> LoadApplicableEntitiesAsync(
        Guid schoolId,
        WithholdingResolveContext context,
        CancellationToken cancellationToken)
    {
        var all = await _configRepository.FindAsync(
            c => c.SchoolId == schoolId
                 && c.AcademicYearId == context.AcademicYearId
                 && c.FeeTypeId == context.FeeTypeId
                 && c.IsActive,
            cancellationToken);

        // Matching : config générale (tranche/catégorie null) OU égalité stricte.
        var matched = all
            .Where(c =>
                (!c.FeeInstallmentId.HasValue || c.FeeInstallmentId == context.FeeInstallmentId)
                && (!c.PricingCategoryId.HasValue || c.PricingCategoryId == context.PricingCategoryId))
            .ToList();

        var typeIds = matched.Select(c => c.WithholdingTypeId).Distinct().ToList();
        var types = (await _typeRepository.FindAsync(
            t => t.SchoolId == schoolId && typeIds.Contains(t.Id) && t.IsActive, cancellationToken))
            .ToDictionary(t => t.Id);

        foreach (var config in matched.ToList())
        {
            if (!types.TryGetValue(config.WithholdingTypeId, out var type))
            {
                matched.Remove(config);
                continue;
            }

            config.WithholdingType = type;
        }

        // Préférer la config la plus spécifique (tranche+catégorie) en dédupliquant par type.
        return matched
            .GroupBy(c => c.WithholdingTypeId)
            .Select(g => g
                .OrderByDescending(c => (c.FeeInstallmentId.HasValue ? 1 : 0) + (c.PricingCategoryId.HasValue ? 1 : 0))
                .First())
            .OrderBy(c => c.WithholdingType.Code)
            .ToList();
    }

    /// <summary>
    /// Montant fixe : une seule fois par rubrique.
    /// Pourcentage : à chaque versement tant que la rubrique n'est pas soldée.
    /// </summary>
    private async Task<List<WithholdingConfiguration>> FilterConfigsForStudentAsync(
        Guid schoolId,
        Guid studentId,
        WithholdingResolveContext context,
        List<WithholdingConfiguration> configs,
        decimal grossAmount,
        CancellationToken cancellationToken)
    {
        if (configs.Count == 0)
        {
            return configs;
        }

        var result = new List<WithholdingConfiguration>();
        var fixedConfigs = configs
            .Where(c => c.CalculationMode == WithholdingCalculationMode.MontantFixe)
            .ToList();
        var percentConfigs = configs
            .Where(c => c.CalculationMode == WithholdingCalculationMode.Pourcentage)
            .ToList();

        if (fixedConfigs.Count > 0)
        {
            var fixedInstallmentConfigs = fixedConfigs
                .Where(c => c.FeeInstallmentId.HasValue)
                .ToList();
            var fixedGeneralConfigs = fixedConfigs
                .Where(c => !c.FeeInstallmentId.HasValue)
                .ToList();

            var preserveFixed = context.PreserveFixedConfigurationIds ?? new HashSet<Guid>();

            // Règle demandée : un montant fixe s'applique à la première fois où la rubrique
            // (tranche) est payée par l'élève. En modification de paiement, on conserve
            // les retenues fixes déjà attachées à ce même paiement.
            foreach (var config in fixedInstallmentConfigs)
            {
                if (preserveFixed.Contains(config.Id)
                    || await IsFirstPaymentOnInstallmentRubriqueAsync(
                        schoolId,
                        studentId,
                        context,
                        config,
                        grossAmount,
                        cancellationToken))
                {
                    result.Add(config);
                }
            }

            // Cas "général" (sans tranche) : on retombe sur la déduplication par configuration déjà appliquée.
            if (fixedGeneralConfigs.Count > 0)
            {
                var appliedConfigIds = await GetAppliedConfigurationIdsAsync(
                    schoolId,
                    studentId,
                    context.AcademicYearId,
                    cancellationToken);

                var pendingFixed = fixedGeneralConfigs
                    .Where(c => preserveFixed.Contains(c.Id) || !appliedConfigIds.Contains(c.Id))
                    .ToList();

                var legacyCandidates = pendingFixed
                    .Where(c => !preserveFixed.Contains(c.Id))
                    .ToList();
                var legacyApplied = await GetLegacyAppliedConfigurationIdsAsync(
                    schoolId,
                    studentId,
                    context.AcademicYearId,
                    legacyCandidates,
                    cancellationToken);

                result.AddRange(pendingFixed.Where(c =>
                    preserveFixed.Contains(c.Id) || !legacyApplied.Contains(c.Id)));
            }
        }

        foreach (var config in percentConfigs)
        {
            if (await HasRemainingOnRubriqueAsync(
                    schoolId,
                    studentId,
                    context,
                    config,
                    grossAmount,
                    cancellationToken))
            {
                result.Add(config);
            }
        }

        return result
            .OrderBy(c => c.WithholdingType.Code)
            .ToList();
    }

    private async Task<bool> HasRemainingOnRubriqueAsync(
        Guid schoolId,
        Guid studentId,
        WithholdingResolveContext context,
        WithholdingConfiguration config,
        decimal grossAmount,
        CancellationToken cancellationToken)
    {
        var installmentId = config.FeeInstallmentId ?? context.FeeInstallmentId;
        if (!installmentId.HasValue)
        {
            return true;
        }

        var remainingBefore = await GetRubriqueRemainingBeforePaymentAsync(
            schoolId,
            studentId,
            context.AcademicYearId,
            context.FeeTypeId,
            installmentId.Value,
            grossAmount,
            context.BalanceIncludesCurrentPayment,
            cancellationToken);

        return remainingBefore > 0;
    }

    private async Task<bool> IsFirstPaymentOnInstallmentRubriqueAsync(
        Guid schoolId,
        Guid studentId,
        WithholdingResolveContext context,
        WithholdingConfiguration config,
        decimal grossAmount,
        CancellationToken cancellationToken)
    {
        if (!config.FeeInstallmentId.HasValue)
        {
            return false;
        }

        var classFeeAmountId = await ResolveClassFeeAmountIdAsync(
            schoolId,
            studentId,
            context.AcademicYearId,
            config.FeeTypeId,
            config.FeeInstallmentId.Value,
            cancellationToken);

        if (!classFeeAmountId.HasValue)
        {
            // Pas de solde connu : on considère que c'est la "première fois".
            return true;
        }

        var balance = (await _balanceRepository.FindAsync(
            b => b.StudentId == studentId && b.ClassFeeAmountId == classFeeAmountId.Value,
            cancellationToken)).FirstOrDefault();

        if (balance is null)
        {
            return true;
        }

        // En encaissement (réel), AmountPaid a déjà été incrémenté avant calcul.
        var amountPaidBefore = balance.AmountPaid;
        if (context.BalanceIncludesCurrentPayment)
        {
            amountPaidBefore -= grossAmount;
        }

        return amountPaidBefore <= 0m;
    }

    /// <summary>
    /// Reste à payer sur la rubrique avant le versement en cours
    /// (le solde élève inclut déjà ce versement au moment du calcul).
    /// </summary>
    private async Task<decimal> GetRubriqueRemainingBeforePaymentAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        Guid feeInstallmentId,
        decimal grossAmount,
        bool balanceIncludesCurrentPayment,
        CancellationToken cancellationToken)
    {
        var classFeeAmountId = await ResolveClassFeeAmountIdAsync(
            schoolId,
            studentId,
            academicYearId,
            feeTypeId,
            feeInstallmentId,
            cancellationToken);
        if (!classFeeAmountId.HasValue)
        {
            return grossAmount;
        }

        var balance = (await _balanceRepository.FindAsync(
            b => b.StudentId == studentId && b.ClassFeeAmountId == classFeeAmountId.Value,
            cancellationToken)).FirstOrDefault();

        if (balance is null)
        {
            return grossAmount;
        }

        var remaining = balance.AmountDue - balance.AmountPaid;
        if (balanceIncludesCurrentPayment)
        {
            remaining += grossAmount;
        }

        return Math.Max(0, remaining);
    }

    private async Task<Guid?> ResolveClassFeeAmountIdAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        Guid feeInstallmentId,
        CancellationToken cancellationToken)
    {
        var enrollment = (await _enrollmentRepository.FindAsync(
            e => e.StudentId == studentId
                 && e.AcademicYearId == academicYearId
                 && e.IsActive,
            cancellationToken)).FirstOrDefault();
        if (enrollment is null)
        {
            return null;
        }

        var classRoom = (await _classRoomRepository.FindAsync(
            c => c.Id == enrollment.ClassRoomId, cancellationToken)).FirstOrDefault();
        if (classRoom?.PedagogicalClassId is not Guid pedagogicalClassId)
        {
            return null;
        }

        var tariff = (await _classFeeAmountRepository.FindAsync(
            a => a.SchoolId == schoolId
                 && a.AcademicYearId == academicYearId
                 && a.PedagogicalClassId == pedagogicalClassId
                 && a.FeePricingCategoryId == enrollment.FeePricingCategoryId
                 && a.FeeTypeId == feeTypeId
                 && a.FeeInstallmentId == feeInstallmentId,
            cancellationToken)).FirstOrDefault();

        return tariff?.Id;
    }

    private async Task<HashSet<Guid>> GetAppliedConfigurationIdsAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        CancellationToken cancellationToken)
    {
        var applications = await _applicationRepository.FindAsync(
            a => a.SchoolId == schoolId
                 && a.StudentId == studentId
                 && a.AcademicYearId == academicYearId,
            cancellationToken);
        return applications.Select(a => a.WithholdingConfigurationId).ToHashSet();
    }

    /// <summary>
    /// Paiements antérieurs à la table FinRetenueApplication : retenue déjà constatée via répartition.
    /// </summary>
    private async Task<HashSet<Guid>> GetLegacyAppliedConfigurationIdsAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        IReadOnlyList<WithholdingConfiguration> configsToCheck,
        CancellationToken cancellationToken)
    {
        if (configsToCheck.Count == 0)
        {
            return [];
        }

        var payments = (await _paymentRepository.FindAsync(
                p => p.SchoolId == schoolId
                     && p.StudentId == studentId
                     && p.AcademicYearId == academicYearId
                     && p.Status == PaymentStatus.Complet,
                cancellationToken))
            .ToList();
        if (payments.Count == 0)
        {
            return [];
        }

        var paymentIds = payments.Select(p => p.Id).ToHashSet();
        var lines = (await _paymentLineRepository.FindAsync(l => paymentIds.Contains(l.PaymentId), cancellationToken))
            .ToList();
        var entries = (await _allocationEntryRepository.FindAsync(
                e => e.SchoolId == schoolId && e.WithholdingTypeId != null,
                cancellationToken))
            .Where(e => paymentIds.Contains(e.PaymentId))
            .ToList();
        if (entries.Count == 0)
        {
            return [];
        }

        var applied = new HashSet<Guid>();
        foreach (var config in configsToCheck)
        {
            foreach (var payment in payments)
            {
                var hasWithholding = lines
                    .Where(l => l.PaymentId == payment.Id && l.FeeTypeId == config.FeeTypeId)
                    .Where(l => !config.FeeInstallmentId.HasValue || l.FeeInstallmentId == config.FeeInstallmentId)
                    .Any(_ => entries.Any(e =>
                        e.PaymentId == payment.Id && e.WithholdingTypeId == config.WithholdingTypeId));

                if (hasWithholding)
                {
                    applied.Add(config.Id);
                    break;
                }
            }
        }

        return applied;
    }

    private async Task ValidateConfigurationRequestAsync(
        Guid schoolId,
        SaveWithholdingConfigurationRequest request,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        if (request.Value < 0)
        {
            throw new DomainException("La valeur de la retenue ne peut pas être négative.");
        }

        if (request.CalculationMode == WithholdingCalculationMode.Pourcentage && request.Value > 100m)
        {
            throw new DomainException("Un pourcentage de retenue ne peut pas dépasser 100 %.");
        }

        if (request.CalculationMode == WithholdingCalculationMode.MontantFixe && request.Value == 0)
        {
            throw new DomainException("Le montant fixe de retenue doit être supérieur à zéro.");
        }

        _ = await GetYearAsync(schoolId, request.AcademicYearId, cancellationToken);
        _ = await GetTypeEntityAsync(schoolId, request.WithholdingTypeId, cancellationToken);
        _ = await GetFeeTypeAsync(schoolId, request.FeeTypeId, cancellationToken);
        if (request.FeeInstallmentId.HasValue)
        {
            _ = await GetInstallmentAsync(schoolId, request.FeeInstallmentId.Value, cancellationToken);
        }

        if (request.PricingCategoryId.HasValue)
        {
            _ = await GetCategoryAsync(schoolId, request.PricingCategoryId.Value, cancellationToken);
        }

        var existing = await _configRepository.FindAsync(
            c => c.SchoolId == schoolId
                 && c.AcademicYearId == request.AcademicYearId
                 && c.WithholdingTypeId == request.WithholdingTypeId
                 && c.FeeTypeId == request.FeeTypeId
                 && (!excludeId.HasValue || c.Id != excludeId.Value),
            cancellationToken);

        var duplicate = existing.Any(c =>
            NullableEquals(c.FeeInstallmentId, request.FeeInstallmentId)
            && NullableEquals(c.PricingCategoryId, request.PricingCategoryId));
        if (duplicate)
        {
            throw new DomainException(
                "Une configuration identique existe déjà pour cette combinaison "
                + "(année + type de retenue + type de frais + tranche + catégorie).");
        }
    }

    private async Task<List<WithholdingConfiguration>> FilterConfigurationsAsync(
        Guid schoolId,
        WithholdingConfigurationSearchRequest request,
        CancellationToken cancellationToken)
    {
        var items = await _configRepository.FindAsync(c => c.SchoolId == schoolId, cancellationToken);
        IEnumerable<WithholdingConfiguration> query = items;

        if (request.AcademicYearId.HasValue)
        {
            query = query.Where(c => c.AcademicYearId == request.AcademicYearId);
        }

        if (request.WithholdingTypeId.HasValue)
        {
            query = query.Where(c => c.WithholdingTypeId == request.WithholdingTypeId);
        }

        if (request.FeeTypeId.HasValue)
        {
            query = query.Where(c => c.FeeTypeId == request.FeeTypeId);
        }

        if (request.FeeInstallmentId.HasValue)
        {
            query = query.Where(c => c.FeeInstallmentId == request.FeeInstallmentId);
        }

        if (request.PricingCategoryId.HasValue)
        {
            query = query.Where(c => c.PricingCategoryId == request.PricingCategoryId);
        }

        if (request.CalculationMode.HasValue)
        {
            query = query.Where(c => c.CalculationMode == request.CalculationMode);
        }

        if (request.ActiveOnly == true)
        {
            query = query.Where(c => c.IsActive);
        }

        var list = query.OrderByDescending(c => c.CreatedAt).ToList();
        if (string.IsNullOrWhiteSpace(request.Search))
        {
            return list;
        }

        var term = request.Search.Trim();
        var typed = await _typeRepository.FindAsync(t => t.SchoolId == schoolId, cancellationToken);
        var typeMap = typed.ToDictionary(t => t.Id);
        var feeTypes = (await _feeTypeRepository.FindAsync(f => f.SchoolId == schoolId, cancellationToken))
            .ToDictionary(f => f.Id);

        return list.Where(c =>
        {
            typeMap.TryGetValue(c.WithholdingTypeId, out var type);
            feeTypes.TryGetValue(c.FeeTypeId, out var fee);
            return (type?.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                   || (type?.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                   || (fee?.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
        }).ToList();
    }

    private async Task<WithholdingConfigurationDto> MapConfigurationAsync(
        Guid schoolId,
        WithholdingConfiguration entity,
        CancellationToken cancellationToken)
    {
        var year = await GetYearAsync(schoolId, entity.AcademicYearId, cancellationToken);
        var type = await GetTypeEntityAsync(schoolId, entity.WithholdingTypeId, cancellationToken);
        var feeType = await GetFeeTypeAsync(schoolId, entity.FeeTypeId, cancellationToken);
        FeeInstallment? installment = null;
        FeePricingCategory? category = null;
        if (entity.FeeInstallmentId.HasValue)
        {
            installment = await GetInstallmentAsync(schoolId, entity.FeeInstallmentId.Value, cancellationToken);
        }

        if (entity.PricingCategoryId.HasValue)
        {
            category = await GetCategoryAsync(schoolId, entity.PricingCategoryId.Value, cancellationToken);
        }

        return new WithholdingConfigurationDto(
            entity.Id,
            entity.AcademicYearId,
            year.Label,
            entity.WithholdingTypeId,
            type.Code,
            type.Name,
            entity.FeeTypeId,
            feeType.Code,
            feeType.Name,
            entity.FeeInstallmentId,
            installment?.Name,
            entity.PricingCategoryId,
            category?.Name,
            entity.CalculationMode,
            entity.Value,
            entity.IsActive);
    }

    private static WithholdingTypeDto MapType(WithholdingType entity) =>
        new(entity.Id, entity.Code, entity.Name, entity.Description, entity.IsActive);

    private async Task<WithholdingType> GetTypeEntityAsync(Guid schoolId, Guid typeId, CancellationToken cancellationToken) =>
        (await _typeRepository.FindAsync(t => t.Id == typeId && t.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
        ?? throw new KeyNotFoundException("Type de retenue introuvable.");

    private async Task<WithholdingConfiguration> GetConfigurationEntityAsync(
        Guid schoolId,
        Guid configurationId,
        CancellationToken cancellationToken) =>
        (await _configRepository.FindAsync(c => c.Id == configurationId && c.SchoolId == schoolId, cancellationToken))
            .FirstOrDefault()
        ?? throw new KeyNotFoundException("Configuration de retenue introuvable.");

    private async Task<AcademicYear> GetYearAsync(Guid schoolId, Guid yearId, CancellationToken cancellationToken) =>
        (await _yearRepository.FindAsync(y => y.Id == yearId && y.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
        ?? throw new KeyNotFoundException("Année scolaire introuvable.");

    private async Task<FeeType> GetFeeTypeAsync(Guid schoolId, Guid feeTypeId, CancellationToken cancellationToken) =>
        (await _feeTypeRepository.FindAsync(f => f.Id == feeTypeId && f.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
        ?? throw new KeyNotFoundException("Type de frais introuvable.");

    private async Task<FeeInstallment> GetInstallmentAsync(Guid schoolId, Guid installmentId, CancellationToken cancellationToken) =>
        (await _installmentRepository.FindAsync(i => i.Id == installmentId && i.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
        ?? throw new KeyNotFoundException("Tranche introuvable.");

    private async Task<FeePricingCategory> GetCategoryAsync(Guid schoolId, Guid categoryId, CancellationToken cancellationToken) =>
        (await _categoryRepository.FindAsync(c => c.Id == categoryId && c.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
        ?? throw new KeyNotFoundException("Catégorie tarifaire introuvable.");

    private static bool NullableEquals(Guid? left, Guid? right) => left == right;

    private static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Le code de la retenue est obligatoire.");
        }

        return code.Trim().ToUpperInvariant();
    }

    private static string RequireName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Le libellé de la retenue est obligatoire.");
        }

        return name.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
