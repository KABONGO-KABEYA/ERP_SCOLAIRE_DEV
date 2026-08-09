using System.Globalization;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Mentions.DTOs;
using SchoolManagement.Application.Mentions.Interfaces;
using SchoolManagement.Domain.Entities.Deliberation;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Mentions.Services;

public sealed class ResultMentionService : IResultMentionService
{
    private readonly IRepository<ResultMentionDefinition> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ResultMentionService(
        IRepository<ResultMentionDefinition> repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ResultMentionDto>> GetAllAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(schoolId, cancellationToken);

        var items = await _repository.FindAsync(m => m.SchoolId == schoolId, cancellationToken);
        return items
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.MinPercentageInclusive)
            .Select(Map)
            .ToList();
    }

    public async Task<ResultMentionDto> CreateAsync(
        Guid schoolId,
        CreateResultMentionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (label, min, max, order) = Validate(request.Label, request.MinPercentageInclusive, request.MaxPercentageInclusive, request.SortOrder);

        await EnsureNoOverlapAsync(schoolId, null, min, max, cancellationToken);
        await EnsureUniqueLabelAsync(schoolId, null, label, cancellationToken);

        var entity = new ResultMentionDefinition
        {
            SchoolId = schoolId,
            Label = label,
            MinPercentageInclusive = min,
            MaxPercentageInclusive = max,
            SortOrder = order,
            IsActive = request.IsActive
        };
        await _repository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<ResultMentionDto> UpdateAsync(
        Guid schoolId,
        Guid id,
        UpdateResultMentionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (label, min, max, order) = Validate(request.Label, request.MinPercentageInclusive, request.MaxPercentageInclusive, request.SortOrder);

        var entity = (await _repository.FindAsync(
            m => m.Id == id && m.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Mention introuvable.");

        await EnsureNoOverlapAsync(schoolId, id, min, max, cancellationToken);
        await EnsureUniqueLabelAsync(schoolId, id, label, cancellationToken);

        entity.Label = label;
        entity.MinPercentageInclusive = min;
        entity.MaxPercentageInclusive = max;
        entity.SortOrder = order;
        entity.IsActive = request.IsActive;
        await _repository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(
        Guid schoolId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = (await _repository.FindAsync(
            m => m.Id == id && m.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Mention introuvable.");

        await _repository.DeleteAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureDefaultsAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.FindAsync(m => m.SchoolId == schoolId, cancellationToken);
        if (existing.Count > 0)
        {
            return;
        }

        var defaults = new (string Label, decimal Min, decimal Max, int Order)[]
        {
            ("Satisfaction", 55m, 69m, 1),
            ("Distinction", 70m, 79m, 2),
            ("Grande distinction", 80m, 90m, 3),
            ("Élite", 91m, 100m, 4)
        };

        foreach (var (label, min, max, order) in defaults)
        {
            await _repository.AddAsync(new ResultMentionDefinition
            {
                SchoolId = schoolId,
                Label = label,
                MinPercentageInclusive = min,
                MaxPercentageInclusive = max,
                SortOrder = order,
                IsActive = true
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureUniqueLabelAsync(
        Guid schoolId,
        Guid? excludeId,
        string label,
        CancellationToken cancellationToken)
    {
        var clash = (await _repository.FindAsync(
            m => m.SchoolId == schoolId
                 && m.Label == label
                 && (excludeId == null || m.Id != excludeId),
            cancellationToken)).Any();
        if (clash)
        {
            throw new DomainException($"Une mention « {label} » existe déjà.");
        }
    }

    private async Task EnsureNoOverlapAsync(
        Guid schoolId,
        Guid? excludeId,
        decimal min,
        decimal max,
        CancellationToken cancellationToken)
    {
        var others = await _repository.FindAsync(
            m => m.SchoolId == schoolId
                 && m.IsActive
                 && (excludeId == null || m.Id != excludeId),
            cancellationToken);

        foreach (var other in others)
        {
            var overlaps = min <= other.MaxPercentageInclusive
                           && max >= other.MinPercentageInclusive;
            if (overlaps)
            {
                throw new DomainException(
                    $"La plage {FormatRange(min, max)} chevauche « {other.Label} » ({FormatRange(other.MinPercentageInclusive, other.MaxPercentageInclusive)}).");
            }
        }
    }

    private static (string Label, decimal Min, decimal Max, int Order) Validate(
        string? label,
        decimal min,
        decimal max,
        int order)
    {
        var trimmed = (label ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new DomainException("Le libellé de la mention est obligatoire.");
        }

        if (trimmed.Length > 100)
        {
            throw new DomainException("Le libellé ne peut pas dépasser 100 caractères.");
        }

        if (min < 0 || max > 100 || min > max)
        {
            throw new DomainException(
                "Les pourcentages doivent être entre 0 et 100, avec minimum ≤ maximum.");
        }

        if (order < 0)
        {
            throw new DomainException("L'ordre d'affichage ne peut pas être négatif.");
        }

        return (trimmed, decimal.Round(min, 2), decimal.Round(max, 2), order);
    }

    private static ResultMentionDto Map(ResultMentionDefinition entity) =>
        new(
            entity.Id,
            entity.Label,
            entity.MinPercentageInclusive,
            entity.MaxPercentageInclusive,
            entity.SortOrder,
            entity.IsActive,
            FormatRange(entity.MinPercentageInclusive, entity.MaxPercentageInclusive));

    private static string FormatRange(decimal min, decimal max) =>
        $"{min.ToString("0.##", CultureInfo.CurrentCulture)} % – {max.ToString("0.##", CultureInfo.CurrentCulture)} %";
}
