using SchoolManagement.Application.Payments.DTOs;

namespace SchoolManagement.Application.Payments.Interfaces;

public interface IFeeTypeStatementService
{
    /// <summary>
    /// Construit le relevé pour un paiement.
    /// Si <paramref name="feeTypeId"/> est omis, le premier type de frais du paiement est utilisé.
    /// </summary>
    Task<FeeTypeStatementDto> GetStatementAsync(
        Guid schoolId,
        Guid paymentId,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Relevé pour un élève / année / type de frais, même sans aucun versement.
    /// </summary>
    Task<FeeTypeStatementDto> GetStatementForStudentAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportPdfAsync(
        Guid schoolId,
        Guid paymentId,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportPdfForStudentAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default);
}
