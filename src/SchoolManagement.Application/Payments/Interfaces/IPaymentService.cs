namespace SchoolManagement.Application.Payments.Interfaces;

using SchoolManagement.Application.Payments.DTOs;

public interface IPaymentService
{
    Task<PaymentDto> CreatePaymentAsync(Guid schoolId, Guid userId, CreatePaymentRequest request, CancellationToken cancellationToken = default);

    Task<PaymentListDto> SearchAsync(Guid schoolId, PaymentSearchRequest request, CancellationToken cancellationToken = default);

    Task<PaymentDto?> GetByIdAsync(Guid schoolId, Guid paymentId, CancellationToken cancellationToken = default);

    Task<StudentFinancialSummaryDto?> GetStudentFinancialSummaryAsync(Guid schoolId, Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default);
}
