using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.SchoolFees.Interfaces;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.SchoolFees.Services;

public sealed class StudentFeeBalanceProvisioner : IStudentFeeBalanceProvisioner
{
    private readonly IRepository<ClassFeeAmount> _classFeeAmountRepository;
    private readonly IRepository<StudentFeeBalance> _balanceRepository;
    private readonly IRepository<FeeType> _feeTypeRepository;

    public StudentFeeBalanceProvisioner(
        IRepository<ClassFeeAmount> classFeeAmountRepository,
        IRepository<StudentFeeBalance> balanceRepository,
        IRepository<FeeType> feeTypeRepository)
    {
        _classFeeAmountRepository = classFeeAmountRepository;
        _balanceRepository = balanceRepository;
        _feeTypeRepository = feeTypeRepository;
    }

    public async Task<decimal> ProvisionForStudentAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        Guid pedagogicalClassId,
        Guid feePricingCategoryId,
        Currency currency,
        CancellationToken cancellationToken = default)
    {
        var tariffs = (await _classFeeAmountRepository.FindAsync(
                a => a.SchoolId == schoolId
                     && a.AcademicYearId == academicYearId
                     && a.PedagogicalClassId == pedagogicalClassId
                     && a.FeePricingCategoryId == feePricingCategoryId,
                cancellationToken))
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Id)
            .ToList();

        var targetIds = tariffs.Select(t => t.Id).ToHashSet();

        var existingActive = (await _balanceRepository.FindAsync(
            b => b.StudentId == studentId,
            cancellationToken)).ToList();

        var existingIncludingDeleted = (await _balanceRepository.FindIncludingDeletedAsync(
            b => b.StudentId == studentId,
            cancellationToken)).ToList();

        var byClassFeeAmountId = existingIncludingDeleted
            .GroupBy(b => b.ClassFeeAmountId)
            .ToDictionary(g => g.Key, g => g.OrderBy(b => b.IsDeleted).First());

        var feeTypeIds = tariffs.Select(t => t.FeeTypeId).Distinct().ToList();
        var feeTypes = (await _feeTypeRepository.FindAsync(
            f => feeTypeIds.Contains(f.Id),
            cancellationToken)).ToDictionary(f => f.Id);

        foreach (var tariff in tariffs)
        {
            if (byClassFeeAmountId.TryGetValue(tariff.Id, out var existing))
            {
                if (existing.IsDeleted)
                {
                    existing.IsDeleted = false;
                    existing.DeletedAt = null;
                    existing.DeletedBy = null;
                    // AmountDue historique conservé — ne pas écraser avec le tarif courant.
                    await _balanceRepository.UpdateAsync(existing, cancellationToken);
                }

                continue;
            }

            var lineCurrency = feeTypes.TryGetValue(tariff.FeeTypeId, out var feeType)
                ? feeType.Currency
                : currency;

            await _balanceRepository.AddAsync(new StudentFeeBalance
            {
                StudentId = studentId,
                ClassFeeAmountId = tariff.Id,
                AmountDue = tariff.Amount,
                AmountPaid = 0,
                Currency = lineCurrency
            }, cancellationToken);
        }

        // Soft-supprimer les soldes non payés de la même année qui ne matchent plus la config cible.
        var allTariffsForYear = (await _classFeeAmountRepository.FindAsync(
            a => a.SchoolId == schoolId && a.AcademicYearId == academicYearId,
            cancellationToken)).ToDictionary(a => a.Id);

        foreach (var balance in existingActive)
        {
            if (targetIds.Contains(balance.ClassFeeAmountId))
            {
                continue;
            }

            if (balance.AmountPaid > 0)
            {
                continue;
            }

            if (!allTariffsForYear.TryGetValue(balance.ClassFeeAmountId, out var linked))
            {
                continue;
            }

            // Même année scolaire mais autre catégorie / classe → obsolète.
            if (linked.AcademicYearId == academicYearId)
            {
                balance.IsDeleted = true;
                balance.DeletedAt = DateTime.UtcNow;
                balance.DeletedBy = null;
                await _balanceRepository.UpdateAsync(balance, cancellationToken);
            }
        }

        // Somme des AmountDue cibles : existants (historique) + nouveaux tarifs.
        var totalDue = 0m;
        foreach (var tariff in tariffs)
        {
            if (byClassFeeAmountId.TryGetValue(tariff.Id, out var existing) && !existing.IsDeleted)
            {
                totalDue += existing.AmountDue;
            }
            else
            {
                totalDue += tariff.Amount;
            }
        }

        return totalDue;
    }
}
