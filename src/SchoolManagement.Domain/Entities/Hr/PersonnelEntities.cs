using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Hr;

public class HrDepartment : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public School School { get; set; } = null!;

    public ICollection<HrJobFunction> JobFunctions { get; set; } = [];
}

public class HrJobFunction : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid? DepartmentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public HrDepartment? Department { get; set; }
}

/// <summary>Profil RH étendu lié au personnel (Teacher).</summary>
public class PersonnelHrProfile : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid TeacherId { get; set; }

    public PersonnelCategory Category { get; set; } = PersonnelCategory.Enseignant;

    public string? MiddleName { get; set; }

    public Gender? Gender { get; set; }

    public DateOnly? BirthDate { get; set; }

    public string? BirthPlace { get; set; }

    public string? Nationality { get; set; }

    public string? MaritalStatus { get; set; }

    public int? ChildrenCount { get; set; }

    public string? IdCardNumber { get; set; }

    public Guid? DepartmentId { get; set; }

    public Guid? JobFunctionId { get; set; }

    public string? Grade { get; set; }

    public string? Service { get; set; }

    public string? SupervisorName { get; set; }

    public string? WorkLocation { get; set; }

    public PersonnelContractType? ContractType { get; set; }

    public DateOnly? ContractStartDate { get; set; }

    public DateOnly? ContractEndDate { get; set; }

    public decimal? BaseSalary { get; set; }

    public string? CurrencyCode { get; set; }

    public PersonnelPaymentMethod? PaymentMethod { get; set; }

    public string? BankName { get; set; }

    public string? BankAccountNumber { get; set; }

    public string? BankAccountHolder { get; set; }

    public int? PayDay { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactRelation { get; set; }

    public string? EmergencyContactPhone { get; set; }

    public string? EmergencyContactAddress { get; set; }

    public string? PhotoPath { get; set; }

    public PersonnelStatus Status { get; set; } = PersonnelStatus.Actif;

    public Teacher Teacher { get; set; } = null!;

    public HrDepartment? Department { get; set; }

    public HrJobFunction? JobFunction { get; set; }
}
