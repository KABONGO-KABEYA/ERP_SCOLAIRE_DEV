namespace SchoolManagement.Application.SchoolFees.Services;

using Mapster;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Payments.Services;
using SchoolManagement.Application.SchoolFees;
using SchoolManagement.Application.SchoolFees.DTOs;
using SchoolManagement.Application.SchoolFees.Interfaces;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Shared.Constants;

public sealed class SchoolFeeService : ISchoolFeeService
{
    private readonly IRepository<FeeType> _feeTypeRepository;
    private readonly IRepository<FeePricingCategory> _pricingCategoryRepository;
    private readonly IRepository<FeeInstallment> _installmentRepository;
    private readonly IRepository<FeeTypeInstallment> _feeTypeInstallmentRepository;
    private readonly IRepository<ClassFeeAmount> _amountRepository;
    private readonly IRepository<StudentFeeBalance> _balanceRepository;
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IRepository<PedagogicalClass> _classRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public SchoolFeeService(
        IRepository<FeeType> feeTypeRepository,
        IRepository<FeePricingCategory> pricingCategoryRepository,
        IRepository<FeeInstallment> installmentRepository,
        IRepository<FeeTypeInstallment> feeTypeInstallmentRepository,
        IRepository<ClassFeeAmount> amountRepository,
        IRepository<StudentFeeBalance> balanceRepository,
        IRepository<AcademicYear> yearRepository,
        IRepository<PedagogicalClass> classRepository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _feeTypeRepository = feeTypeRepository;
        _pricingCategoryRepository = pricingCategoryRepository;
        _installmentRepository = installmentRepository;
        _feeTypeInstallmentRepository = feeTypeInstallmentRepository;
        _amountRepository = amountRepository;
        _balanceRepository = balanceRepository;
        _yearRepository = yearRepository;
        _classRepository = classRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<SchoolFeeCatalogDto> GetCatalogAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        await EnsureGeneralPricingCategoryAsync(schoolId, cancellationToken);
        return new(
            await GetFeeTypesAsync(schoolId, cancellationToken),
            await GetInstallmentsAsync(schoolId, cancellationToken),
            await GetPricingCategoriesAsync(schoolId, cancellationToken));
    }
    public async Task<IReadOnlyList<FeeTypeDto>> GetFeeTypesAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var items = await _feeTypeRepository.FindAsync(f => f.SchoolId == schoolId, cancellationToken);
        return items.OrderBy(f => f.Name).Adapt<List<FeeTypeDto>>();
    }

    public async Task<FeeTypeDto> CreateFeeTypeAsync(
        Guid schoolId,
        CreateFeeTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainException("Le libellé du type de frais est obligatoire.");
        }

        var existing = await _feeTypeRepository.FindAsync(f => f.SchoolId == schoolId, cancellationToken);
        var code = FeeTypeCodeGenerator.Generate(request.Name, existing.Select(f => f.Code));
        var entity = request.Adapt<FeeType>();
        entity.SchoolId = schoolId;
        entity.Code = code;
        await _feeTypeRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.Adapt<FeeTypeDto>();
    }

    public async Task<FeeTypeDto> UpdateFeeTypeAsync(
        Guid schoolId,
        Guid feeTypeId,
        UpdateFeeTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainException("Le libellé du type de frais est obligatoire.");
        }

        var entity = await GetFeeTypeEntityAsync(schoolId, feeTypeId, cancellationToken);
        var previousCurrency = entity.Currency;

        entity.Name = request.Name.Trim();
        entity.Currency = request.Currency;
        entity.IsMandatory = request.IsMandatory;
        entity.IsActive = request.IsActive;
        await _feeTypeRepository.UpdateAsync(entity, cancellationToken);

        if (previousCurrency != request.Currency)
        {
            await RealignBalanceCurrencyAsync(
                schoolId, feeTypeId, previousCurrency, request.Currency, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.Adapt<FeeTypeDto>();
    }

    /// <summary>
    /// Les montants des tarifs n'ont pas de devise propre : ils s'interprètent dans celle du
    /// type de frais. La copie figée sur les soldes élève doit donc suivre le changement,
    /// sinon un montant saisi en USD reste présenté en CDF à l'encaissement.
    /// </summary>
    private async Task RealignBalanceCurrencyAsync(
        Guid schoolId,
        Guid feeTypeId,
        Currency previousCurrency,
        Currency newCurrency,
        CancellationToken cancellationToken)
    {
        var tariffIds = (await _amountRepository.FindAsync(
                a => a.SchoolId == schoolId && a.FeeTypeId == feeTypeId,
                cancellationToken))
            .Select(a => a.Id)
            .ToHashSet();

        if (tariffIds.Count == 0)
        {
            return;
        }

        var balances = (await _balanceRepository.FindAsync(
                b => tariffIds.Contains(b.ClassFeeAmountId),
                cancellationToken))
            .ToList();

        var settled = balances.Count(b => b.AmountPaid > 0);
        if (settled > 0)
        {
            throw new DomainException(
                $"Impossible de passer ce type de frais en {newCurrency} : {settled} élève(s) ont déjà "
                + $"des paiements enregistrés en {previousCurrency}. Annulez ces paiements avant de changer la devise.");
        }

        foreach (var balance in balances.Where(b => b.Currency != newCurrency))
        {
            balance.Currency = newCurrency;
            balance.UpdatedAt = DateTime.UtcNow;
            await _balanceRepository.UpdateAsync(balance, cancellationToken);
        }
    }

    public async Task DeleteFeeTypeAsync(Guid schoolId, Guid feeTypeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetFeeTypeEntityAsync(schoolId, feeTypeId, cancellationToken);
        entity.IsActive = false;
        await _feeTypeRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FeePricingCategoryDto>> GetPricingCategoriesAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        await EnsureGeneralPricingCategoryAsync(schoolId, cancellationToken);
        var items = await _pricingCategoryRepository.FindAsync(
            c => c.SchoolId == schoolId && c.IsActive,
            cancellationToken);
        return items.OrderBy(c => c.Name).Adapt<List<FeePricingCategoryDto>>();
    }

    public async Task<FeePricingCategoryDto> CreatePricingCategoryAsync(
        Guid schoolId,
        CreateFeePricingCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainException("Le libellé de la catégorie tarifaire est obligatoire.");
        }

        if (FeePricingCategoryCodes.IsGeneralDisplayName(request.Name))
        {
            return await EnsureGeneralPricingCategoryAsync(schoolId, cancellationToken);
        }

        var existing = await _pricingCategoryRepository.FindAsync(c => c.SchoolId == schoolId, cancellationToken);
        var code = FeeTypeCodeGenerator.Generate(request.Name, existing.Select(c => c.Code));
        var entity = new FeePricingCategory
        {
            SchoolId = schoolId,
            Code = code,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = request.IsActive
        };

        await _pricingCategoryRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.Adapt<FeePricingCategoryDto>();
    }

    public async Task<FeePricingCategoryDto> UpdatePricingCategoryAsync(
        Guid schoolId,
        Guid categoryId,
        UpdateFeePricingCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainException("Le libellé de la catégorie tarifaire est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new DomainException("Le code de la catégorie tarifaire est obligatoire.");
        }

        var entity = await GetPricingCategoryEntityAsync(schoolId, categoryId, cancellationToken);
        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var duplicateCode = (await _pricingCategoryRepository.FindAsync(
            c => c.SchoolId == schoolId && c.Id != categoryId && c.Code == normalizedCode,
            cancellationToken)).Any();
        if (duplicateCode)
        {
            throw new DomainException("Ce code de catégorie tarifaire est déjà utilisé.");
        }

        entity.Code = normalizedCode;
        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.IsActive = request.IsActive;
        await _pricingCategoryRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.Adapt<FeePricingCategoryDto>();
    }

    public async Task DeletePricingCategoryAsync(Guid schoolId, Guid categoryId, CancellationToken cancellationToken = default)
    {
        var entity = await GetPricingCategoryEntityAsync(schoolId, categoryId, cancellationToken);
        entity.IsActive = false;
        await _pricingCategoryRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<FeePricingCategoryDto> EnsureGeneralPricingCategoryAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var existing = (await _pricingCategoryRepository.FindAsync(
            c => c.SchoolId == schoolId, cancellationToken)).ToList();
        var general = existing.FirstOrDefault(c =>
            string.Equals(c.Code, FeePricingCategoryCodes.General, StringComparison.OrdinalIgnoreCase));

        if (general is null)
        {
            var byName = existing.FirstOrDefault(c => FeePricingCategoryCodes.IsGeneralDisplayName(c.Name));
            if (byName is not null)
            {
                byName.Code = FeePricingCategoryCodes.General;
                if (string.IsNullOrWhiteSpace(byName.Description))
                {
                    byName.Description = "Catégorie tarifaire par défaut (inscription)";
                }

                byName.Name = "Générale";
                byName.IsActive = true;
                await _pricingCategoryRepository.UpdateAsync(byName, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                general = byName;
            }
        }

        if (general is not null)
        {
            foreach (var duplicate in existing.Where(c =>
                         c.Id != general.Id
                         && (FeePricingCategoryCodes.IsGeneralDisplayName(c.Name)
                             || string.Equals(c.Code, FeePricingCategoryCodes.General, StringComparison.OrdinalIgnoreCase))))
            {
                duplicate.IsActive = false;
                await _pricingCategoryRepository.UpdateAsync(duplicate, cancellationToken);
            }

            if (!general.IsActive)
            {
                general.IsActive = true;
                await _pricingCategoryRepository.UpdateAsync(general, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return general.Adapt<FeePricingCategoryDto>();
        }

        var entity = new FeePricingCategory
        {
            SchoolId = schoolId,
            Code = FeePricingCategoryCodes.General,
            Name = "Générale",
            Description = "Catégorie tarifaire par défaut (inscription)",
            IsActive = true
        };
        await _pricingCategoryRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.Adapt<FeePricingCategoryDto>();
    }

    public async Task<IReadOnlyList<FeeInstallmentDto>> GetInstallmentsAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var items = await _installmentRepository.FindAsync(i => i.SchoolId == schoolId, cancellationToken);
        return items.OrderBy(i => i.SortOrder).ThenBy(i => i.Name).Adapt<List<FeeInstallmentDto>>();
    }

    public async Task<FeeInstallmentDto> CreateInstallmentAsync(
        Guid schoolId,
        SaveFeeInstallmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = request.Adapt<FeeInstallment>();
        entity.SchoolId = schoolId;
        await _installmentRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.Adapt<FeeInstallmentDto>();
    }

    public async Task<FeeInstallmentDto> UpdateInstallmentAsync(
        Guid schoolId,
        Guid installmentId,
        SaveFeeInstallmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetInstallmentEntityAsync(schoolId, installmentId, cancellationToken);
        request.Adapt(entity);
        await _installmentRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.Adapt<FeeInstallmentDto>();
    }

    public async Task DeleteInstallmentAsync(Guid schoolId, Guid installmentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetInstallmentEntityAsync(schoolId, installmentId, cancellationToken);
        entity.IsActive = false;
        await _installmentRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FeeTypeInstallmentDto>> GetFeeTypeInstallmentsAsync(
        Guid schoolId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default)
    {
        _ = await GetFeeTypeEntityAsync(schoolId, feeTypeId, cancellationToken);
        var definitions = await GetFeeTypeInstallmentDefinitionsAsync(schoolId, feeTypeId, cancellationToken);
        return definitions
            .Select(d => new FeeTypeInstallmentDto(
                d.LinkId,
                d.Installment.Id,
                d.Installment.Name,
                d.SortOrder))
            .ToList();
    }

    public async Task<IReadOnlyList<FeeTypeInstallmentDto>> SaveFeeTypeInstallmentsAsync(
        Guid schoolId,
        Guid feeTypeId,
        SaveFeeTypeInstallmentsRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await GetFeeTypeEntityAsync(schoolId, feeTypeId, cancellationToken);

        if (request.Items.GroupBy(i => i.FeeInstallmentId).Any(g => g.Count() > 1))
        {
            throw new DomainException("Une tranche ne peut être affectée qu'une seule fois par type de frais.");
        }

        var activeInstallmentIds = (await _installmentRepository.FindAsync(
            i => i.SchoolId == schoolId && i.IsActive,
            cancellationToken)).Select(i => i.Id).ToHashSet();

        foreach (var item in request.Items)
        {
            if (!activeInstallmentIds.Contains(item.FeeInstallmentId))
            {
                throw new DomainException("Tranche invalide ou inactive.");
            }
        }

        var existing = (await _feeTypeInstallmentRepository.FindAsync(
            l => l.SchoolId == schoolId && l.FeeTypeId == feeTypeId,
            cancellationToken)).ToDictionary(l => l.FeeInstallmentId);

        var requestedIds = request.Items.Select(i => i.FeeInstallmentId).ToHashSet();

        foreach (var item in request.Items)
        {
            if (existing.TryGetValue(item.FeeInstallmentId, out var link))
            {
                link.SortOrder = item.SortOrder;
                link.IsDeleted = false;
                link.DeletedAt = null;
                await _feeTypeInstallmentRepository.UpdateAsync(link, cancellationToken);
            }
            else
            {
                await _feeTypeInstallmentRepository.AddAsync(new FeeTypeInstallment
                {
                    SchoolId = schoolId,
                    FeeTypeId = feeTypeId,
                    FeeInstallmentId = item.FeeInstallmentId,
                    SortOrder = item.SortOrder
                }, cancellationToken);
            }
        }

        foreach (var link in existing.Values.Where(l => !requestedIds.Contains(l.FeeInstallmentId)))
        {
            link.IsDeleted = true;
            link.DeletedAt = DateTime.UtcNow;
            await _feeTypeInstallmentRepository.UpdateAsync(link, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetFeeTypeInstallmentsAsync(schoolId, feeTypeId, cancellationToken);
    }

    public async Task<ClassFeeScheduleDto> GetScheduleAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid pedagogicalClassId,
        Guid feePricingCategoryId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default)
    {
        var year = await GetYearEntityAsync(schoolId, academicYearId, cancellationToken);
        var pedagogicalClass = await GetClassEntityAsync(schoolId, pedagogicalClassId, cancellationToken);
        var pricingCategory = await GetPricingCategoryEntityAsync(schoolId, feePricingCategoryId, cancellationToken);
        var feeType = await GetFeeTypeEntityAsync(schoolId, feeTypeId, cancellationToken);

        var definitions = await GetFeeTypeInstallmentDefinitionsAsync(schoolId, feeTypeId, cancellationToken);

        var saved = (await _amountRepository.FindAsync(
            a => a.SchoolId == schoolId
                && a.AcademicYearId == academicYearId
                && a.PedagogicalClassId == pedagogicalClassId
                && a.FeePricingCategoryId == feePricingCategoryId
                && a.FeeTypeId == feeTypeId,
            cancellationToken))
            .ToDictionary(a => a.FeeInstallmentId);

        var lines = BuildClassScheduleLines(definitions, saved);

        return BuildScheduleDto(year, pedagogicalClass, pricingCategory, feeType, lines);
    }

    public async Task<IReadOnlyList<ClassFeeScheduleSignatureDto>> GetScheduleSignaturesAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid feePricingCategoryId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default)
    {
        _ = await GetYearEntityAsync(schoolId, academicYearId, cancellationToken);
        _ = await GetPricingCategoryEntityAsync(schoolId, feePricingCategoryId, cancellationToken);
        _ = await GetFeeTypeEntityAsync(schoolId, feeTypeId, cancellationToken);

        var definitions = await GetFeeTypeInstallmentDefinitionsAsync(schoolId, feeTypeId, cancellationToken);
        var classes = (await _classRepository.FindAsync(
                c => c.SchoolId == schoolId && c.IsEnabled,
                cancellationToken))
            .OrderBy(c => c.DisplayName)
            .ToList();

        var savedRows = (await _amountRepository.FindAsync(
                a => a.SchoolId == schoolId
                    && a.AcademicYearId == academicYearId
                    && a.FeePricingCategoryId == feePricingCategoryId
                    && a.FeeTypeId == feeTypeId,
                cancellationToken))
            .GroupBy(a => a.PedagogicalClassId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(a => a.FeeInstallmentId));

        return classes
            .Select(cls =>
            {
                savedRows.TryGetValue(cls.Id, out var saved);
                saved ??= new Dictionary<Guid, ClassFeeAmount>();

                var lines = BuildClassScheduleLines(definitions, saved);

                return new ClassFeeScheduleSignatureDto(
                    cls.Id,
                    ClassFeeScheduleSignatureHelper.Compute(lines),
                    ClassFeeScheduleSignatureHelper.HasConfiguredValues(lines));
            })
            .ToList();
    }

    public async Task<ClassFeeScheduleDto> SaveScheduleAsync(
        Guid schoolId,
        SaveClassFeeScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        var year = await GetYearEntityAsync(schoolId, request.AcademicYearId, cancellationToken);
        await EnsureAcademicYearIsEditableAsync(year, cancellationToken);
        _ = await GetClassEntityAsync(schoolId, request.PedagogicalClassId, cancellationToken);
        _ = await GetPricingCategoryEntityAsync(schoolId, request.FeePricingCategoryId, cancellationToken);
        _ = await GetFeeTypeEntityAsync(schoolId, request.FeeTypeId, cancellationToken);

        await SaveScheduleLinesAsync(
            schoolId,
            request.AcademicYearId,
            request.PedagogicalClassId,
            request.FeePricingCategoryId,
            request.FeeTypeId,
            request.Lines,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetScheduleAsync(
            schoolId,
            request.AcademicYearId,
            request.PedagogicalClassId,
            request.FeePricingCategoryId,
            request.FeeTypeId,
            cancellationToken);
    }

    public async Task<SaveClassFeeScheduleBulkResult> SaveScheduleBulkAsync(
        Guid schoolId,
        SaveClassFeeScheduleBulkRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PedagogicalClassIds is not { Count: > 0 })
        {
            throw new DomainException("Sélectionnez au moins une classe.");
        }

        var year = await GetYearEntityAsync(schoolId, request.AcademicYearId, cancellationToken);
        await EnsureAcademicYearIsEditableAsync(year, cancellationToken);
        _ = await GetPricingCategoryEntityAsync(schoolId, request.FeePricingCategoryId, cancellationToken);
        _ = await GetFeeTypeEntityAsync(schoolId, request.FeeTypeId, cancellationToken);

        var classNames = new List<string>();
        foreach (var classId in request.PedagogicalClassIds.Distinct())
        {
            var pedagogicalClass = await GetClassEntityAsync(schoolId, classId, cancellationToken);
            await SaveScheduleLinesAsync(
                schoolId,
                request.AcademicYearId,
                classId,
                request.FeePricingCategoryId,
                request.FeeTypeId,
                request.Lines,
                cancellationToken);
            classNames.Add(pedagogicalClass.DisplayName);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SaveClassFeeScheduleBulkResult(classNames.Count, classNames);
    }

    public async Task<CopyClassFeeScheduleResult> CopyScheduleFromPreviousYearAsync(
        Guid schoolId,
        CopyClassFeeScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        var years = await _yearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken);
        var targetYear = years.FirstOrDefault(y => y.Id == request.TargetAcademicYearId)
            ?? throw new KeyNotFoundException("Année scolaire cible introuvable.");

        await EnsureAcademicYearIsEditableAsync(targetYear, cancellationToken);

        _ = await GetClassEntityAsync(schoolId, request.PedagogicalClassId, cancellationToken);
        _ = await GetPricingCategoryEntityAsync(schoolId, request.FeePricingCategoryId, cancellationToken);
        _ = await GetFeeTypeEntityAsync(schoolId, request.FeeTypeId, cancellationToken);

        var (copied, sourceYear) = await CopyScheduleFromPreviousYearCoreAsync(
            schoolId,
            years,
            targetYear,
            request.PedagogicalClassId,
            request.FeePricingCategoryId,
            request.FeeTypeId,
            request.SourceAcademicYearId,
            cancellationToken);

        if (copied > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new CopyClassFeeScheduleResult(copied, sourceYear.Label, targetYear.Label);
    }

    public async Task<CopyClassFeeScheduleBulkResult> CopyScheduleFromPreviousYearBulkAsync(
        Guid schoolId,
        CopyClassFeeScheduleBulkRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PedagogicalClassIds is not { Count: > 0 })
        {
            throw new DomainException("Sélectionnez au moins une classe.");
        }

        var years = await _yearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken);
        var targetYear = years.FirstOrDefault(y => y.Id == request.TargetAcademicYearId)
            ?? throw new KeyNotFoundException("Année scolaire cible introuvable.");

        await EnsureAcademicYearIsEditableAsync(targetYear, cancellationToken);
        _ = await GetPricingCategoryEntityAsync(schoolId, request.FeePricingCategoryId, cancellationToken);
        _ = await GetFeeTypeEntityAsync(schoolId, request.FeeTypeId, cancellationToken);

        var totalCopied = 0;
        AcademicYear? sourceYear = null;
        foreach (var classId in request.PedagogicalClassIds.Distinct())
        {
            var (copied, source) = await CopyScheduleFromPreviousYearCoreAsync(
                schoolId,
                years,
                targetYear,
                classId,
                request.FeePricingCategoryId,
                request.FeeTypeId,
                request.SourceAcademicYearId,
                cancellationToken);
            totalCopied += copied;
            sourceYear ??= source;
        }

        if (totalCopied > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (sourceYear is null)
        {
            throw new DomainException("Aucune année scolaire précédente disponible.");
        }

        return new CopyClassFeeScheduleBulkResult(
            totalCopied,
            request.PedagogicalClassIds.Distinct().Count(),
            sourceYear.Label,
            targetYear.Label);
    }

    public async Task<decimal> ResolveAnnualAmountAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid pedagogicalClassId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default)
    {
        var rows = (await _amountRepository.FindAsync(
            a => a.SchoolId == schoolId
                && a.AcademicYearId == academicYearId
                && a.PedagogicalClassId == pedagogicalClassId
                && a.FeeTypeId == feeTypeId,
            cancellationToken)).ToList();

        if (rows.Count == 0)
        {
            return 0;
        }

        var categories = await _pricingCategoryRepository.FindAsync(
            c => c.SchoolId == schoolId && c.IsActive,
            cancellationToken);

        var preferredCategory = categories
            .FirstOrDefault(c => string.Equals(
                c.Code,
                SchoolManagement.Shared.Constants.FeePricingCategoryCodes.General,
                StringComparison.OrdinalIgnoreCase))
            ?? categories.OrderBy(c => c.Name).FirstOrDefault();

        if (preferredCategory is not null)
        {
            var forCategory = rows.Where(a => a.FeePricingCategoryId == preferredCategory.Id).ToList();
            if (forCategory.Count > 0)
            {
                return forCategory.Sum(a => a.Amount);
            }
        }

        // Évite de sommer plusieurs catégories distinctes : prend le groupe le plus complet.
        return rows
            .GroupBy(a => a.FeePricingCategoryId)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .First()
            .Sum(a => a.Amount);
    }

    private static ClassFeeScheduleDto BuildScheduleDto(
        AcademicYear year,
        PedagogicalClass pedagogicalClass,
        FeePricingCategory pricingCategory,
        FeeType feeType,
        IReadOnlyList<ClassFeeScheduleLineDto> lines) =>
        new(
            year.Id,
            year.Label,
            pedagogicalClass.Id,
            pedagogicalClass.DisplayName,
            pricingCategory.Id,
            pricingCategory.Code,
            pricingCategory.Name,
            feeType.Id,
            feeType.Code,
            feeType.Name,
            feeType.Currency,
            lines.Sum(l => l.Amount),
            lines);

    private async Task<FeeType> GetFeeTypeEntityAsync(Guid schoolId, Guid feeTypeId, CancellationToken cancellationToken) =>
        (await _feeTypeRepository.FindAsync(f => f.SchoolId == schoolId && f.Id == feeTypeId, cancellationToken)).FirstOrDefault()
        ?? throw new KeyNotFoundException("Type de frais introuvable.");

    private async Task<FeePricingCategory> GetPricingCategoryEntityAsync(
        Guid schoolId,
        Guid categoryId,
        CancellationToken cancellationToken) =>
        (await _pricingCategoryRepository.FindAsync(
            c => c.SchoolId == schoolId && c.Id == categoryId && c.IsActive,
            cancellationToken)).FirstOrDefault()
        ?? throw new KeyNotFoundException("Catégorie tarifaire introuvable ou inactive.");

    private async Task<FeeInstallment> GetInstallmentEntityAsync(Guid schoolId, Guid installmentId, CancellationToken cancellationToken) =>
        (await _installmentRepository.FindAsync(i => i.SchoolId == schoolId && i.Id == installmentId, cancellationToken)).FirstOrDefault()
        ?? throw new KeyNotFoundException("Tranche introuvable.");

    private sealed record FeeTypeInstallmentDefinition(Guid LinkId, FeeInstallment Installment, int SortOrder);

    private async Task<IReadOnlyList<FeeTypeInstallmentDefinition>> GetFeeTypeInstallmentDefinitionsAsync(
        Guid schoolId,
        Guid feeTypeId,
        CancellationToken cancellationToken)
    {
        var links = (await _feeTypeInstallmentRepository.FindAsync(
            l => l.SchoolId == schoolId && l.FeeTypeId == feeTypeId,
            cancellationToken))
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.FeeInstallmentId)
            .ToList();

        if (links.Count == 0)
        {
            return [];
        }

        var installmentIds = links.Select(l => l.FeeInstallmentId).ToHashSet();
        var installments = (await _installmentRepository.FindAsync(
            i => i.SchoolId == schoolId && i.IsActive && installmentIds.Contains(i.Id),
            cancellationToken)).ToDictionary(i => i.Id);

        return links
            .Where(l => installments.ContainsKey(l.FeeInstallmentId))
            .Select(l => new FeeTypeInstallmentDefinition(l.Id, installments[l.FeeInstallmentId], l.SortOrder))
            .ToList();
    }

    private static List<ClassFeeScheduleLineDto> BuildClassScheduleLines(
        IReadOnlyList<FeeTypeInstallmentDefinition> definitions,
        IReadOnlyDictionary<Guid, ClassFeeAmount> savedByInstallmentId)
    {
        var definitionLookup = definitions.ToDictionary(d => d.Installment.Id);
        var activeSaved = savedByInstallmentId.Values
            .Where(row => !row.IsDeleted)
            .OrderBy(row => row.SortOrder)
            .ThenBy(row => row.FeeInstallmentId)
            .ToList();

        if (activeSaved.Count > 0)
        {
            return activeSaved
                .Select(row =>
                {
                    var installmentName = definitionLookup.TryGetValue(row.FeeInstallmentId, out var definition)
                        ? definition.Installment.Name
                        : "Tranche";

                    return new ClassFeeScheduleLineDto(
                        row.Id,
                        row.FeeInstallmentId,
                        installmentName,
                        row.SortOrder > 0 ? row.SortOrder : 1,
                        row.Amount,
                        row.DueDate);
                })
                .OrderBy(line => line.SortOrder)
                .ToList();
        }

        return definitions
            .Select(definition =>
            {
                savedByInstallmentId.TryGetValue(definition.Installment.Id, out var row);
                var sortOrder = row is { SortOrder: > 0 } ? row.SortOrder : definition.SortOrder;
                return new ClassFeeScheduleLineDto(
                    row?.Id,
                    definition.Installment.Id,
                    definition.Installment.Name,
                    sortOrder,
                    row?.Amount ?? 0,
                    row?.DueDate);
            })
            .OrderBy(line => line.SortOrder)
            .ToList();
    }

    private async Task<AcademicYear> GetYearEntityAsync(Guid schoolId, Guid academicYearId, CancellationToken cancellationToken) =>
        (await _yearRepository.FindAsync(y => y.SchoolId == schoolId && y.Id == academicYearId, cancellationToken)).FirstOrDefault()
        ?? throw new KeyNotFoundException("Année scolaire introuvable.");

    private async Task<PedagogicalClass> GetClassEntityAsync(Guid schoolId, Guid pedagogicalClassId, CancellationToken cancellationToken) =>
        (await _classRepository.FindAsync(c => c.SchoolId == schoolId && c.Id == pedagogicalClassId, cancellationToken)).FirstOrDefault()
        ?? throw new KeyNotFoundException("Classe introuvable.");

    private async Task SaveScheduleLinesAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid pedagogicalClassId,
        Guid feePricingCategoryId,
        Guid feeTypeId,
        IReadOnlyList<SaveClassFeeScheduleLineRequest> lines,
        CancellationToken cancellationToken)
    {
        var assignedInstallmentIds = (await GetFeeTypeInstallmentDefinitionsAsync(schoolId, feeTypeId, cancellationToken))
            .Select(d => d.Installment.Id)
            .ToHashSet();

        var existing = (await _amountRepository.FindAsync(
            a => a.SchoolId == schoolId
                && a.AcademicYearId == academicYearId
                && a.PedagogicalClassId == pedagogicalClassId
                && a.FeePricingCategoryId == feePricingCategoryId
                && a.FeeTypeId == feeTypeId,
            cancellationToken)).ToDictionary(a => a.FeeInstallmentId);

        var requestedIds = lines.Select(l => l.FeeInstallmentId).ToHashSet();
        await EnsurePaidInstallmentScheduleRulesAsync(existing.Values.ToList(), lines, requestedIds, cancellationToken);

        foreach (var line in lines)
        {
            if (!assignedInstallmentIds.Contains(line.FeeInstallmentId))
            {
                throw new DomainException("Cette tranche n'est pas affectée au type de frais sélectionné.");
            }

            if (line.Amount < 0)
            {
                throw new DomainException("Le montant ne peut pas être négatif.");
            }

            if (line.SortOrder <= 0)
            {
                throw new DomainException("L'ordre de priorité doit être supérieur à zéro.");
            }

            if (existing.TryGetValue(line.FeeInstallmentId, out var row))
            {
                row.Amount = line.Amount;
                row.DueDate = line.DueDate;
                row.SortOrder = line.SortOrder;
                row.IsDeleted = false;
                row.DeletedAt = null;
                await _amountRepository.UpdateAsync(row, cancellationToken);
            }
            else
            {
                await _amountRepository.AddAsync(new ClassFeeAmount
                {
                    SchoolId = schoolId,
                    AcademicYearId = academicYearId,
                    PedagogicalClassId = pedagogicalClassId,
                    FeePricingCategoryId = feePricingCategoryId,
                    FeeTypeId = feeTypeId,
                    FeeInstallmentId = line.FeeInstallmentId,
                    Amount = line.Amount,
                    DueDate = line.DueDate,
                    SortOrder = line.SortOrder
                }, cancellationToken);
            }
        }

        foreach (var (installmentId, row) in existing)
        {
            if (requestedIds.Contains(installmentId) || row.IsDeleted)
            {
                continue;
            }

            row.IsDeleted = true;
            row.DeletedAt = DateTime.UtcNow;
            await _amountRepository.UpdateAsync(row, cancellationToken);
        }
    }

    private async Task EnsurePaidInstallmentScheduleRulesAsync(
        IReadOnlyList<ClassFeeAmount> existingRows,
        IReadOnlyList<SaveClassFeeScheduleLineRequest> lines,
        HashSet<Guid> requestedIds,
        CancellationToken cancellationToken)
    {
        if (existingRows.Count == 0)
        {
            return;
        }

        var amountIds = existingRows.Select(r => r.Id).ToList();
        var balances = await _balanceRepository.FindAsync(
            b => amountIds.Contains(b.ClassFeeAmountId),
            cancellationToken);
        var paidByAmountId = balances
            .GroupBy(b => b.ClassFeeAmountId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AmountPaid));

        var paidByInstallment = existingRows
            .Select(r => (
                SortOrder: r.SortOrder,
                AmountPaid: paidByAmountId.GetValueOrDefault(r.Id),
                InstallmentId: r.FeeInstallmentId,
                Row: r))
            .ToList();

        foreach (var line in lines)
        {
            var row = existingRows.FirstOrDefault(r => r.FeeInstallmentId == line.FeeInstallmentId);
            if (row is null)
            {
                continue;
            }

            var paid = paidByAmountId.GetValueOrDefault(row.Id);
            var amountChanged = row.Amount != line.Amount || row.SortOrder != line.SortOrder;
            if (!amountChanged)
            {
                continue;
            }

            if (paid > 0)
            {
                PaymentMutationPolicy.EnsureCanMutatePaidPayment(_currentUser);
            }

            PaymentMutationPolicy.EnsureScheduleInstallmentEditable(
                row.SortOrder,
                paidByInstallment.Select(x => (x.SortOrder, x.AmountPaid)).ToList());
        }

        foreach (var row in existingRows)
        {
            if (requestedIds.Contains(row.FeeInstallmentId) || row.IsDeleted)
            {
                continue;
            }

            var paid = paidByAmountId.GetValueOrDefault(row.Id);
            if (paid > 0)
            {
                PaymentMutationPolicy.EnsureCanMutatePaidPayment(_currentUser);
            }

            PaymentMutationPolicy.EnsureScheduleInstallmentEditable(
                row.SortOrder,
                paidByInstallment.Select(x => (x.SortOrder, x.AmountPaid)).ToList());
        }
    }

    private async Task<(int Copied, AcademicYear SourceYear)> CopyScheduleFromPreviousYearCoreAsync(
        Guid schoolId,
        IReadOnlyList<AcademicYear> years,
        AcademicYear targetYear,
        Guid pedagogicalClassId,
        Guid feePricingCategoryId,
        Guid feeTypeId,
        Guid? sourceAcademicYearId,
        CancellationToken cancellationToken)
    {
        _ = await GetClassEntityAsync(schoolId, pedagogicalClassId, cancellationToken);

        var sourceYearId = sourceAcademicYearId;
        AcademicYear? sourceYear = null;
        if (sourceYearId.HasValue)
        {
            sourceYear = years.FirstOrDefault(y => y.Id == sourceYearId.Value)
                ?? throw new KeyNotFoundException("Année scolaire source introuvable.");
        }
        else
        {
            sourceYear = years
                .Where(y => y.Id != targetYear.Id && y.StartDate < targetYear.StartDate)
                .OrderByDescending(y => y.StartDate)
                .FirstOrDefault();
            sourceYearId = sourceYear?.Id;
        }

        if (sourceYearId is null || sourceYear is null)
        {
            throw new DomainException("Aucune année scolaire précédente disponible.");
        }

        var sourceRows = await _amountRepository.FindAsync(
            a => a.SchoolId == schoolId
                && a.AcademicYearId == sourceYearId
                && a.PedagogicalClassId == pedagogicalClassId
                && a.FeePricingCategoryId == feePricingCategoryId
                && a.FeeTypeId == feeTypeId,
            cancellationToken);

        var targetRows = (await _amountRepository.FindAsync(
            a => a.SchoolId == schoolId
                && a.AcademicYearId == targetYear.Id
                && a.PedagogicalClassId == pedagogicalClassId
                && a.FeePricingCategoryId == feePricingCategoryId
                && a.FeeTypeId == feeTypeId,
            cancellationToken)).Select(a => a.FeeInstallmentId).ToHashSet();

        var copied = 0;
        foreach (var source in sourceRows)
        {
            if (targetRows.Contains(source.FeeInstallmentId))
            {
                continue;
            }

            await _amountRepository.AddAsync(new ClassFeeAmount
            {
                SchoolId = schoolId,
                AcademicYearId = targetYear.Id,
                PedagogicalClassId = pedagogicalClassId,
                FeePricingCategoryId = feePricingCategoryId,
                FeeTypeId = feeTypeId,
                FeeInstallmentId = source.FeeInstallmentId,
                Amount = source.Amount,
                DueDate = source.DueDate,
                SortOrder = source.SortOrder
            }, cancellationToken);

            targetRows.Add(source.FeeInstallmentId);
            copied++;
        }

        return (copied, sourceYear);
    }

    private static Task EnsureAcademicYearIsEditableAsync(AcademicYear year, CancellationToken cancellationToken)
    {
        var dto = year.Adapt<AcademicYearDto>();
        if (!AcademicYearFeeRules.CanEditFees(dto))
        {
            throw new DomainException(AcademicYearFeeRules.GetReadOnlyReason(dto));
        }

        return Task.CompletedTask;
    }
}
