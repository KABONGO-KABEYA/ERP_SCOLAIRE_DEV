namespace SchoolManagement.Application.Dashboard.Services;

using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

internal sealed record AnnualFeeObligation(Guid StudentId, Guid InstallmentId, Guid? PricingCategoryId, decimal Amount);

internal static class AnnualReceivableAllocation
{
    // Project the complete annual obligation, independently of payment history.
    // General fixed configurations apply once per student; installment configurations once per rubric.
    internal static (decimal NetAmount, Dictionary<Guid, decimal> Withholdings) Project(
        IReadOnlyList<AnnualFeeObligation> obligations, IReadOnlyList<WithholdingConfiguration> configurations)
    {
        var fixedApplied = new HashSet<(Guid StudentId, Guid ConfigurationId)>();
        var withheld = new Dictionary<Guid, decimal>();
        decimal net = 0;
        foreach (var obligation in obligations.Where(o => o.Amount > 0))
        {
            var applicable = configurations.Where(c => c.IsActive && !c.IsDeleted
                && (!c.FeeInstallmentId.HasValue || c.FeeInstallmentId == obligation.InstallmentId)
                && (!c.PricingCategoryId.HasValue || c.PricingCategoryId == obligation.PricingCategoryId))
                .GroupBy(c => c.WithholdingTypeId).Select(g => g.OrderByDescending(c =>
                    (c.FeeInstallmentId.HasValue ? 1 : 0) + (c.PricingCategoryId.HasValue ? 1 : 0)).First());
            decimal total = 0;
            foreach (var config in applicable)
            {
                var amount = config.CalculationMode == WithholdingCalculationMode.Pourcentage
                    ? Math.Round(obligation.Amount * config.Value / 100m, 2, MidpointRounding.AwayFromZero)
                    : fixedApplied.Add((obligation.StudentId, config.Id)) ? config.Value : 0m;
                amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
                total += amount;
                withheld[config.WithholdingTypeId] = withheld.GetValueOrDefault(config.WithholdingTypeId) + amount;
            }
            if (total > obligation.Amount)
                throw new DomainException("Le total des retenues dépasse le montant annuel de la rubrique.");
            net += obligation.Amount - total;
        }
        return (net, withheld);
    }
}
