namespace SchoolManagement.Application.Parent.DTOs;

using SchoolManagement.Application.Payments.DTOs;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Domain.Enums;

public sealed record ParentChildDto(
    Guid StudentId,
    string RegistrationNumber,
    string FullName,
    string? ClassName);

public sealed record ParentPaymentDto(
    Guid Id,
    string ReceiptNumber,
    DateTime PaymentDate,
    decimal TotalAmount,
    Currency Currency,
    PaymentStatus Status);

public sealed record ParentBulletinSummaryDto(
    Guid AcademicPeriodId,
    string PeriodName,
    decimal Average,
    decimal Percentage,
    int Rank,
    int ClassSize,
    bool IsPublished);
