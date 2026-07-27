namespace SchoolManagement.Application.Accounting.Interfaces;

using SchoolManagement.Application.Accounting.DTOs;

public interface IAccountingService
{
    Task<ExpenseRequestSearchResultDto> SearchExpenseRequestsAsync(
        Guid schoolId,
        ExpenseSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<ExpensePaymentSearchResultDto> SearchExpensePaymentsAsync(
        Guid schoolId,
        ExpenseSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<ExpenseRequestDto> CreateExpenseRequestAsync(
        Guid schoolId,
        CreateExpenseRequestRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ExpenseRequestDto> SubmitExpenseRequestAsync(
        Guid schoolId,
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<ExpenseRequestDto> ApproveExpenseRequestAsync(
        Guid schoolId,
        Guid requestId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ExpensePaymentDto> CreateExpensePaymentAsync(
        Guid schoolId,
        CreateExpensePaymentRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExpenseDestinationBalanceDto>> GetExpenseBalancesAsync(
        Guid schoolId,
        Guid academicYearId,
        CancellationToken cancellationToken = default);
}
