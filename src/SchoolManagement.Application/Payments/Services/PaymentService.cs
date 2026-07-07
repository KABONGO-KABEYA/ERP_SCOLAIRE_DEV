namespace SchoolManagement.Application.Payments.Services;

using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Payments.DTOs;
using SchoolManagement.Application.Payments.Interfaces;
using SchoolManagement.Application.Schools;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

public sealed class PaymentService : IPaymentService
{
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<PaymentLine> _paymentLineRepository;
    private readonly IRepository<CashMovement> _cashMovementRepository;
    private readonly IRepository<StudentFeeBalance> _balanceRepository;
    private readonly IRepository<Student> _studentRepository;
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PaymentService(
        IRepository<Payment> paymentRepository,
        IRepository<PaymentLine> paymentLineRepository,
        IRepository<CashMovement> cashMovementRepository,
        IRepository<StudentFeeBalance> balanceRepository,
        IRepository<Student> studentRepository,
        IRepository<AcademicYear> yearRepository,
        IUnitOfWork unitOfWork)
    {
        _paymentRepository = paymentRepository;
        _paymentLineRepository = paymentLineRepository;
        _cashMovementRepository = cashMovementRepository;
        _balanceRepository = balanceRepository;
        _studentRepository = studentRepository;
        _yearRepository = yearRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PaymentDto> CreatePaymentAsync(
        Guid schoolId,
        Guid userId,
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var student = (await _studentRepository.FindAsync(
            s => s.Id == request.StudentId && s.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Élève introuvable.");

        await SchoolConfigurationGuards.EnsureActiveAcademicYearAsync(
            _yearRepository,
            schoolId,
            request.AcademicYearId,
            cancellationToken);

        var totalAmount = request.Lines.Sum(l => l.Amount);
        var receiptNumber = $"REC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var payment = new Payment
            {
                SchoolId = schoolId,
                StudentId = request.StudentId,
                AcademicYearId = request.AcademicYearId,
                CashRegisterId = request.CashRegisterId,
                BankId = request.BankId,
                ReceiptNumber = receiptNumber,
                PaymentDate = DateTime.UtcNow,
                TotalAmount = totalAmount,
                Currency = request.Currency,
                Status = PaymentStatus.Complet,
                PaymentMethod = request.PaymentMethod,
                Notes = request.Notes,
                ReceivedByUserId = userId
            };

            await _paymentRepository.AddAsync(payment, cancellationToken);

            foreach (var line in request.Lines)
            {
                await _paymentLineRepository.AddAsync(new PaymentLine
                {
                    PaymentId = payment.Id,
                    FeeTypeId = line.FeeTypeId,
                    Amount = line.Amount,
                    Currency = line.Currency,
                    Description = line.Description
                }, cancellationToken);

                await UpdateStudentBalanceAsync(request.StudentId, request.AcademicYearId, line.FeeTypeId, line.Amount, line.Currency, cancellationToken);
            }

            var lastMovement = (await _cashMovementRepository.FindAsync(
                m => m.CashRegisterId == request.CashRegisterId, cancellationToken))
                .OrderByDescending(m => m.MovementDate)
                .FirstOrDefault();

            var balanceAfter = (lastMovement?.BalanceAfter ?? 0) + totalAmount;

            await _cashMovementRepository.AddAsync(new CashMovement
            {
                CashRegisterId = request.CashRegisterId,
                PaymentId = payment.Id,
                MovementDate = DateTime.UtcNow,
                MovementType = "IN",
                Amount = totalAmount,
                Currency = request.Currency,
                BalanceAfter = balanceAfter,
                Description = $"Paiement {receiptNumber}",
                UserId = userId
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new PaymentDto(
                payment.Id,
                payment.ReceiptNumber,
                payment.StudentId,
                $"{student.LastName} {student.FirstName}",
                payment.PaymentDate,
                payment.TotalAmount,
                payment.Currency,
                payment.Status,
                payment.PaymentMethod);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PaymentListDto> SearchAsync(Guid schoolId, PaymentSearchRequest request, CancellationToken cancellationToken = default)
    {
        var payments = await _paymentRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken);
        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId, cancellationToken);
        var studentMap = students.ToDictionary(s => s.Id);

        var query = payments.AsEnumerable();

        if (request.StudentId.HasValue)
        {
            query = query.Where(p => p.StudentId == request.StudentId);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(p => DateOnly.FromDateTime(p.PaymentDate) >= request.FromDate);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(p => DateOnly.FromDateTime(p.PaymentDate) <= request.ToDate);
        }

        var total = query.Count();
        var items = query
            .OrderByDescending(p => p.PaymentDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p =>
            {
                studentMap.TryGetValue(p.StudentId, out var student);
                var name = student is null ? "—" : $"{student.LastName} {student.FirstName}";
                return new PaymentDto(p.Id, p.ReceiptNumber, p.StudentId, name, p.PaymentDate, p.TotalAmount, p.Currency, p.Status, p.PaymentMethod);
            })
            .ToList();

        return new PaymentListDto { Items = items, Page = request.Page, PageSize = request.PageSize, TotalCount = total };
    }

    public async Task<PaymentDto?> GetByIdAsync(Guid schoolId, Guid paymentId, CancellationToken cancellationToken = default)
    {
        var payment = (await _paymentRepository.FindAsync(
            p => p.Id == paymentId && p.SchoolId == schoolId, cancellationToken)).FirstOrDefault();

        if (payment is null)
        {
            return null;
        }

        var student = (await _studentRepository.FindAsync(s => s.Id == payment.StudentId, cancellationToken)).FirstOrDefault();
        var name = student is null ? "—" : $"{student.LastName} {student.FirstName}";
        return new PaymentDto(payment.Id, payment.ReceiptNumber, payment.StudentId, name, payment.PaymentDate, payment.TotalAmount, payment.Currency, payment.Status, payment.PaymentMethod);
    }

    public async Task<StudentFinancialSummaryDto?> GetStudentFinancialSummaryAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        CancellationToken cancellationToken = default)
    {
        await SchoolConfigurationGuards.EnsureActiveAcademicYearAsync(
            _yearRepository,
            schoolId,
            academicYearId,
            cancellationToken);

        var student = (await _studentRepository.FindAsync(
            s => s.Id == studentId && s.SchoolId == schoolId, cancellationToken)).FirstOrDefault();

        if (student is null)
        {
            return null;
        }

        var balances = await _balanceRepository.FindAsync(
            b => b.StudentId == studentId && b.AcademicYearId == academicYearId, cancellationToken);

        var totalDue = balances.Sum(b => b.AmountDue);
        var totalPaid = balances.Sum(b => b.AmountPaid);
        var currency = balances.FirstOrDefault()?.Currency ?? Currency.CDF;

        return new StudentFinancialSummaryDto(
            studentId,
            $"{student.LastName} {student.FirstName}",
            totalDue,
            totalPaid,
            totalDue - totalPaid,
            currency);
    }

    private async Task UpdateStudentBalanceAsync(
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        decimal amountPaid,
        Currency currency,
        CancellationToken cancellationToken)
    {
        var balances = await _balanceRepository.FindAsync(
            b => b.StudentId == studentId && b.AcademicYearId == academicYearId && b.FeeTypeId == feeTypeId,
            cancellationToken);

        var balance = balances.FirstOrDefault();
        if (balance is null)
        {
            balance = new StudentFeeBalance
            {
                StudentId = studentId,
                AcademicYearId = academicYearId,
                FeeTypeId = feeTypeId,
                AmountDue = amountPaid,
                AmountPaid = amountPaid,
                Currency = currency
            };
            await _balanceRepository.AddAsync(balance, cancellationToken);
        }
        else
        {
            balance.AmountPaid += amountPaid;
            await _balanceRepository.UpdateAsync(balance, cancellationToken);
        }
    }
}
