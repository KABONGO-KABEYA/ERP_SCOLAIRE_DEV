namespace SchoolManagement.Application.Payments.Interfaces;

using SchoolManagement.Application.Payments.DTOs;

public interface IPaymentService
{
    Task<PaymentDto> CreatePaymentAsync(Guid schoolId, Guid userId, CreatePaymentRequest request, CancellationToken cancellationToken = default);

    Task<PaymentListDto> SearchAsync(Guid schoolId, PaymentSearchRequest request, CancellationToken cancellationToken = default);

    Task<PaymentDetailDto?> GetByIdAsync(Guid schoolId, Guid paymentId, CancellationToken cancellationToken = default);

    Task<StudentFinancialSummaryDto?> GetStudentFinancialSummaryAsync(Guid schoolId, Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default);

    Task CancelPaymentAsync(Guid schoolId, Guid userId, Guid paymentId, string reason, CancellationToken cancellationToken = default);

    Task<PaymentDetailDto> UpdatePaymentNotesAsync(Guid schoolId, Guid paymentId, string? notes, CancellationToken cancellationToken = default);

    Task<PaymentDetailDto> UpdatePaymentAmountAsync(
        Guid schoolId,
        Guid userId,
        Guid paymentId,
        UpdatePaymentAmountRequest request,
        CancellationToken cancellationToken = default);
}
