using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Students;

public class Student : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string RegistrationNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? MiddleName { get; set; }

    public Gender Gender { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public string? PlaceOfBirth { get; set; }

    public string? Nationality { get; set; } = "Congolaise";

    public Guid? AddressId { get; set; }

    public string? Address { get; set; }

    public Entities.Geography.PostalAddress? ResidenceAddress { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? PhotoPath { get; set; }

    public string? BloodGroup { get; set; }

    public string? MedicalNotes { get; set; }

    public bool IsArchived { get; set; }

    public ICollection<StudentGuardian> Guardians { get; set; } = [];

    public ICollection<StudentDocument> Documents { get; set; } = [];

    public ICollection<Enrollment> Enrollments { get; set; } = [];

    public ICollection<StudentStatusHistory> StatusHistory { get; set; } = [];
}

public class Guardian : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public Guid? AddressId { get; set; }

    public string? Address { get; set; }

    public Entities.Geography.PostalAddress? ResidenceAddress { get; set; }

    public string? Profession { get; set; }

    public string? NationalId { get; set; }

    public Gender? Gender { get; set; }

    public ICollection<StudentGuardian> Students { get; set; } = [];
}

public class StudentGuardian : AuditableEntity
{
    public Guid StudentId { get; set; }

    public Guid GuardianId { get; set; }

    public string Relationship { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    public bool CanPickup { get; set; } = true;

    public bool UsesStudentAddress { get; set; }

    public Student Student { get; set; } = null!;

    public Guardian Guardian { get; set; } = null!;
}

public class StudentDocument : AuditableEntity, IAggregateRoot
{
    public Guid StudentId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public string? MimeType { get; set; }

    public long FileSizeBytes { get; set; }

    public Student Student { get; set; } = null!;
}

public class Enrollment : AuditableEntity, IAggregateRoot
{
    public Guid StudentId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid ClassRoomId { get; set; }

    /// <summary>Catégorie tarifaire de l'élève pour cette année scolaire (détermine les montants de frais).</summary>
    public Guid FeePricingCategoryId { get; set; }

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Inscrit;

    public DateOnly EnrollmentDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public Student Student { get; set; } = null!;

    public Entities.Settings.AcademicYear AcademicYear { get; set; } = null!;

    public Entities.Settings.ClassRoom ClassRoom { get; set; } = null!;

    public Entities.Settings.FeePricingCategory FeePricingCategory { get; set; } = null!;

    public ICollection<Evaluation> Evaluations { get; set; } = [];
}

/// <summary>Historique des attributions de catégorie tarifaire sur une inscription.</summary>
public class EnrollmentPricingCategoryHistory : AuditableEntity, IAggregateRoot
{
    public Guid EnrollmentId { get; set; }

    public Guid? PreviousFeePricingCategoryId { get; set; }

    public Guid NewFeePricingCategoryId { get; set; }

    public DateTime ChangedAt { get; set; }

    public Guid? ChangedByUserId { get; set; }

    public string? Notes { get; set; }

    public Enrollment Enrollment { get; set; } = null!;

    public Entities.Settings.FeePricingCategory? PreviousFeePricingCategory { get; set; }

    public Entities.Settings.FeePricingCategory NewFeePricingCategory { get; set; } = null!;
}

public class StudentStatusHistory : AuditableEntity, IAggregateRoot
{
    public Guid StudentId { get; set; }

    public Guid? AcademicYearId { get; set; }

    public EnrollmentStatus PreviousStatus { get; set; }

    public EnrollmentStatus NewStatus { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public string? Reason { get; set; }

    public string? DestinationSchool { get; set; }

    public Student Student { get; set; } = null!;
}
