namespace SchoolManagement.Application.Withholdings.Services;

using SchoolManagement.Application.Withholdings.DTOs;
using SchoolManagement.Application.Withholdings.Interfaces;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

public sealed class WithholdingEngine : IWithholdingEngine
{
    public WithholdingCalculationResult Calculate(
        decimal grossAmount,
        IReadOnlyList<WithholdingConfiguration> configurations)
    {
        if (grossAmount < 0)
        {
            throw new DomainException("Le montant brut ne peut pas être négatif.");
        }

        var lines = new List<CalculatedWithholdingLine>();
        decimal total = 0;

        foreach (var config in configurations.Where(c => c.IsActive && !c.IsDeleted).OrderBy(c => c.WithholdingType.Code))
        {
            var amount = config.CalculationMode switch
            {
                WithholdingCalculationMode.Pourcentage => RoundMoney(grossAmount * config.Value / 100m),
                WithholdingCalculationMode.MontantFixe => RoundMoney(config.Value),
                _ => throw new DomainException("Mode de calcul de retenue inconnu.")
            };

            if (amount < 0)
            {
                throw new DomainException($"La retenue « {config.WithholdingType.Name} » produit un montant négatif.");
            }

            total += amount;
            lines.Add(new CalculatedWithholdingLine(
                config.Id,
                config.WithholdingTypeId,
                config.WithholdingType.Code,
                config.WithholdingType.Name,
                config.CalculationMode,
                config.Value,
                amount));
        }

        if (total > grossAmount)
        {
            throw new DomainException(
                $"Le total des retenues ({total:N2}) dépasse le montant brut ({grossAmount:N2}).");
        }

        return new WithholdingCalculationResult(grossAmount, total, RoundMoney(grossAmount - total), lines);
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
