namespace SchoolManagement.Application.RevenueAllocation.Interfaces;

using SchoolManagement.Application.RevenueAllocation.DTOs;
using SchoolManagement.Domain.Entities.Finance;

public interface IRevenueAllocationService
{
    Task<IReadOnlyList<RevenueDestinationDto>> GetDestinationsAsync(Guid schoolId, bool activeOnly = false, CancellationToken cancellationToken = default);

    Task<RevenueDestinationDto> CreateDestinationAsync(Guid schoolId, SaveRevenueDestinationRequest request, CancellationToken cancellationToken = default);

    Task<RevenueDestinationDto> UpdateDestinationAsync(Guid schoolId, Guid destinationId, SaveRevenueDestinationRequest request, CancellationToken cancellationToken = default);

    Task DeactivateDestinationAsync(Guid schoolId, Guid destinationId, CancellationToken cancellationToken = default);

    Task EnsureDefaultDestinationsAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RevenueAllocationKeyDto>> GetKeysAsync(Guid schoolId, Guid? academicYearId = null, CancellationToken cancellationToken = default);

    Task<RevenueAllocationKeyDto?> GetKeyByIdAsync(Guid schoolId, Guid keyId, CancellationToken cancellationToken = default);

    Task<RevenueAllocationKeyDto> CreateKeyAsync(Guid schoolId, CreateRevenueAllocationKeyRequest request, CancellationToken cancellationToken = default);

    Task<RevenueAllocationKeyDto> UpdateKeyAsync(Guid schoolId, Guid keyId, UpdateRevenueAllocationKeyRequest request, CancellationToken cancellationToken = default);

    Task ActivateKeyAsync(Guid schoolId, Guid keyId, CancellationToken cancellationToken = default);

    /// <summary>Clôture la répartition : renseigne <c>EndDate</c> (aujourd'hui si omis).</summary>
    Task CloseKeyAsync(Guid schoolId, Guid keyId, DateOnly? endDate = null, CancellationToken cancellationToken = default);

    Task DeactivateKeyAsync(Guid schoolId, Guid keyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suppression définitive si la clé n'a jamais servi à un paiement.
    /// Sinon l'historique est conservé et la suppression est refusée.
    /// </summary>
    Task DeleteKeyAsync(Guid schoolId, Guid keyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applique la clé ouverte applicable à chaque type de frais du paiement (dans la transaction appelante).
    /// Lève DomainException si aucune clé applicable — le paiement doit alors être annulé.
    /// </summary>
    Task ApplyAllocationForPaymentAsync(
        Guid schoolId,
        Payment payment,
        IReadOnlyList<PaymentLine> paymentLines,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<RevenueAllocationSearchResultDto> SearchAllocationsAsync(
        Guid schoolId,
        RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportAllocationsExcelAsync(
        Guid schoolId,
        RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportAllocationsPdfAsync(
        Guid schoolId,
        RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default);
}
