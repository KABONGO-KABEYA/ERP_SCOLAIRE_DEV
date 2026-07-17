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
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

public sealed class WithholdingService : IWithholdingService
{
    private readonly IRepository<WithholdingType> _typeRepository;
    private readonly IRepository<WithholdingConfiguration> _configRepository;
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IRepository<FeeType> _feeTypeRepository;
    private readonly IRepository<FeeInstallment> _installmentRepository;
    private readonly IRepository<FeePricingCategory> _categoryRepository;
    private readonly IWithholdingEngine _engine;
    private readonly IUnitOfWork _unitOfWork;

    public WithholdingService(
        IRepository<WithholdingType> typeRepository,
        IRepository<WithholdingConfiguration> configRepository,
        IRepository<AcademicYear> yearRepository,
        IRepository<FeeType> feeTypeRepository,
        IRepository<FeeInstallment> installmentRepository,
        IRepository<FeePricingCategory> categoryRepository,
        IWithholdingEngine engine,
        IUnitOfWork unitOfWork)
    {
        _typeRepository = typeRepository;
        _configRepository = configRepository;
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
        return _engine.Calculate(grossAmount, configs);
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
