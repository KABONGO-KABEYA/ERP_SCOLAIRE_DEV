namespace SchoolManagement.Application.SchoolFees;

using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Entities.Settings;

public static class ClassFeeScheduleProvisioner
{
    public static async Task<int> ProvisionForYearAsync(
        Guid schoolId,
        Guid targetYearId,
        Guid? sourceYearId,
        IRepository<ClassFeeAmount> amountRepository,
        IRepository<FeeType> feeTypeRepository,
        IRepository<PedagogicalClass> classRepository,
        IRepository<FeeInstallment> installmentRepository,
        IRepository<AcademicYear> yearRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken = default)
    {
        var years = await yearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken);
        var targetYear = years.FirstOrDefault(y => y.Id == targetYearId)
            ?? throw new KeyNotFoundException("Année scolaire cible introuvable.");

        if (sourceYearId is null)
        {
            sourceYearId = years
                .Where(y => y.Id != targetYearId && y.StartDate < targetYear.StartDate)
                .OrderByDescending(y => y.StartDate)
                .FirstOrDefault()
                ?.Id;
        }

        if (sourceYearId is null || sourceYearId == targetYearId)
        {
            return 0;
        }

        var activeFeeTypeIds = (await feeTypeRepository.FindAsync(
            f => f.SchoolId == schoolId && f.IsActive,
            cancellationToken)).Select(f => f.Id).ToHashSet();

        var activeClassIds = (await classRepository.FindAsync(
            c => c.SchoolId == schoolId && c.IsEnabled,
            cancellationToken)).Select(c => c.Id).ToHashSet();

        var activeInstallmentIds = (await installmentRepository.FindAsync(
            i => i.SchoolId == schoolId && i.IsActive,
            cancellationToken)).Select(i => i.Id).ToHashSet();

        var sourceRows = (await amountRepository.FindAsync(
            a => a.SchoolId == schoolId && a.AcademicYearId == sourceYearId,
            cancellationToken))
            .Where(a => activeFeeTypeIds.Contains(a.FeeTypeId)
                && activeClassIds.Contains(a.PedagogicalClassId)
                && activeInstallmentIds.Contains(a.FeeInstallmentId))
            .ToList();

        if (sourceRows.Count == 0)
        {
            return 0;
        }

        var targetRows = (await amountRepository.FindAsync(
            a => a.SchoolId == schoolId && a.AcademicYearId == targetYearId,
            cancellationToken)).ToList();

        var existingKeys = targetRows
            .Select(a => (a.PedagogicalClassId, a.FeePricingCategoryId, a.FeeTypeId, a.FeeInstallmentId))
            .ToHashSet();

        var copied = 0;
        foreach (var source in sourceRows)
        {
            var key = (source.PedagogicalClassId, source.FeePricingCategoryId, source.FeeTypeId, source.FeeInstallmentId);
            if (existingKeys.Contains(key))
            {
                continue;
            }

            await amountRepository.AddAsync(new ClassFeeAmount
            {
                SchoolId = schoolId,
                AcademicYearId = targetYearId,
                PedagogicalClassId = source.PedagogicalClassId,
                FeePricingCategoryId = source.FeePricingCategoryId,
                FeeTypeId = source.FeeTypeId,
                FeeInstallmentId = source.FeeInstallmentId,
                Amount = source.Amount,
                DueDate = source.DueDate,
                SortOrder = source.SortOrder
            }, cancellationToken);

            existingKeys.Add(key);
            copied++;
        }

        if (copied > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return copied;
    }
}
