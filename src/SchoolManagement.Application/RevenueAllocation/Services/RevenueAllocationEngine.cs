namespace SchoolManagement.Application.RevenueAllocation.Services;

using SchoolManagement.Application.RevenueAllocation.DTOs;
using SchoolManagement.Application.RevenueAllocation.Interfaces;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

/// <summary>Moteur de calcul de répartition en pourcentage — réutilisable (Comptabilité, Budget, Caisse).</summary>
public sealed class RevenueAllocationEngine : IRevenueAllocationEngine
{
    public IReadOnlyList<CalculatedAllocationLine> Calculate(
        decimal paymentAmount,
        IReadOnlyList<RevenueAllocationKeyDetail> details)
    {
        if (paymentAmount < 0)
        {
            throw new DomainException("Le montant à répartir ne peut pas être négatif.");
        }

        if (details.Count == 0)
        {
            throw new DomainException("La clé de répartition ne contient aucune ligne.");
        }

        var ordered = details
            .Where(d => !d.IsDeleted)
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Destination.Name)
            .ToList();

        var validation = ValidateKeyForActivation(ordered);
        if (validation.Count > 0)
        {
            throw new DomainException(string.Join(" ", validation));
        }

        var results = new List<CalculatedAllocationLine>();
        decimal allocated = 0;
        for (var i = 0; i < ordered.Count; i++)
        {
            var line = ordered[i];
            decimal amount;
            if (i == ordered.Count - 1)
            {
                amount = RoundMoney(paymentAmount - allocated);
            }
            else
            {
                amount = RoundMoney(paymentAmount * line.Value / 100m);
                allocated += amount;
            }

            results.Add(new CalculatedAllocationLine(
                line.DestinationId,
                line.Destination.Code,
                line.Destination.Name,
                AllocationCalculationType.Pourcentage,
                amount,
                line.Value));
        }

        return results;
    }

    public IReadOnlyList<string> ValidateKeyForActivation(IReadOnlyList<RevenueAllocationKeyDetail> details)
    {
        var errors = new List<string>();
        var active = details.Where(d => !d.IsDeleted).ToList();

        if (active.Count == 0)
        {
            errors.Add("Ajoutez au moins une ligne de répartition.");
            return errors;
        }

        if (active.Any(d => d.CalculationType != AllocationCalculationType.Pourcentage))
        {
            errors.Add("La répartition est exclusivement définie en pourcentages.");
        }

        var duplicate = active.GroupBy(d => d.DestinationId).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            errors.Add("Une destination ne peut apparaître qu'une seule fois dans la clé.");
        }

        foreach (var detail in active)
        {
            if (detail.Destination is { IsActive: false })
            {
                errors.Add($"La destination « {detail.Destination.Name} » est inactive et ne peut pas être utilisée.");
            }

            if (detail.Value <= 0)
            {
                errors.Add($"Le pourcentage pour « {detail.Destination?.Name ?? detail.DestinationId.ToString()} » doit être supérieur à zéro.");
            }
        }

        var sum = active.Sum(d => d.Value);
        if (Math.Abs(sum - 100m) > 0.0001m)
        {
            errors.Add($"Le total des pourcentages doit être égal à 100 % (actuellement {sum:N2} %).");
        }

        return errors;
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
