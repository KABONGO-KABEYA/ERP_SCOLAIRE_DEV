namespace SchoolManagement.Application.Payments.Validators;

using FluentValidation;
using SchoolManagement.Application.Payments.DTOs;

public sealed class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    public CreatePaymentRequestValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.AcademicYearId).NotEmpty();
        RuleFor(x => x.CashRegisterId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty();
        RuleFor(x => x.Lines).Must(lines => lines.Sum(l => l.Amount) > 0).WithMessage("Le montant total doit être supérieur à zéro.");
    }
}
