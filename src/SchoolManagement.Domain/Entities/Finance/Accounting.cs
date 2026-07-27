using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Finance;

/// <summary>Demande de paiement / bon de décaissement.</summary>
public class ExpenseRequest : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid DestinationId { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal RequestedAmount { get; set; }

    public Currency Currency { get; set; } = Currency.CDF;

    public DateOnly RequestDate { get; set; }

    public ExpenseRequestStatus Status { get; set; } = ExpenseRequestStatus.Brouillon;

    public DateTime? SubmittedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public School School { get; set; } = null!;

    public AcademicYear AcademicYear { get; set; } = null!;

    public RevenueAllocationDestination Destination { get; set; } = null!;
}

/// <summary>Dépense effective imputée sur un compte bénéficiaire.</summary>
public class ExpensePayment : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid DestinationId { get; set; }

    public Guid? ExpenseRequestId { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>Nom de la personne ou structure qui reçoit le paiement.</summary>
    public string BeneficiaryName { get; set; } = string.Empty;

    /// <summary>Nom de la personne ayant autorisé la dépense.</summary>
    public string AuthorizedByName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public Currency Currency { get; set; } = Currency.CDF;

    public DateOnly ExpenseDate { get; set; }

    public School School { get; set; } = null!;

    public AcademicYear AcademicYear { get; set; } = null!;

    public RevenueAllocationDestination Destination { get; set; } = null!;

    public ExpenseRequest? ExpenseRequest { get; set; }
}
