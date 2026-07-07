namespace SchoolManagement.Application.EnrollmentWizard.DTOs;

using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Domain.Enums;

public sealed record EnrollmentPrerequisiteIssueDto(
    string Code,
    string Message,
    string SettingsRoute,
    string ActionLabel);

public sealed record EnrollmentPrerequisitesDto(
    bool IsReady,
    IReadOnlyList<EnrollmentPrerequisiteIssueDto> Issues,
    Guid? CurrentAcademicYearId,
    string? CurrentAcademicYearLabel,
    PedagogicalStructureSummaryDto? PedagogicalSummary,
    int FeeTypeCount);

public sealed record GuardianInputDto(
    string FirstName,
    string LastName,
    string? Phone,
    string? Email,
    string? Address,
    string? Profession,
    string? Employer,
    string Relationship,
    bool IsPrimary,
    bool CanPickup);

public sealed record EnrollmentScolariteDto(
    Guid SectionId,
    Guid ClassRoomId,
    Guid? PedagogicalClassId,
    int? OrderNumber,
    DateOnly EnrollmentDate,
    RegistrationKind RegistrationKind,
    string? PreviousSchool,
    string? PreviousStudentCode,
    string? PermanentNumber);

public sealed record EnrollmentMedicalDto(
    string? BloodGroup,
    string? Allergies,
    string? ChronicDiseases,
    string? Treatment,
    string? DoctorName,
    string? MedicalCenter,
    string? Disability,
    string? Observations,
    bool MedicalEmergency);

public sealed record EnrollmentDocumentStatusDto(
    string DocumentType,
    string Status,
    string? FileName,
    string? StoragePath = null);

public sealed record EnrollmentFeeLineDto(
    Guid FeeTypeId,
    string Code,
    string Name,
    decimal DefaultAmount,
    decimal DiscountAmount,
    decimal ExemptionAmount,
    decimal NetAmount,
    bool IsMandatory);

public sealed record EnrollmentFeeSummaryDto(
    IReadOnlyList<EnrollmentFeeLineDto> Lines,
    decimal TotalDue,
    Currency Currency);

public sealed record EnrollmentStudentSearchResultDto(
    Guid Id,
    string RegistrationNumber,
    string FirstName,
    string LastName,
    string? MiddleName,
    Gender Gender,
    DateOnly DateOfBirth,
    string? PhotoPath,
    string? Phone,
    string? PreviousClassName,
    string? PreviousAcademicYear,
    string StatusLabel)
{
    public string FullName => string.IsNullOrWhiteSpace(MiddleName)
        ? $"{LastName} {FirstName}"
        : $"{LastName} {MiddleName} {FirstName}";
}

public sealed record CompleteEnrollmentRequest(
    Guid? ExistingStudentId,
    string FirstName,
    string LastName,
    string? MiddleName,
    Gender Gender,
    DateOnly DateOfBirth,
    string? PlaceOfBirth,
    string? Nationality,
    string? Province,
    string? Territory,
    string? City,
    string? Country,
    string? Language,
    string? Religion,
    string? Address,
    string? Phone,
    string? Email,
    string? PhotoPath,
    EnrollmentMedicalDto Medical,
    EnrollmentScolariteDto Scolarite,
    IReadOnlyList<GuardianInputDto> Guardians,
    IReadOnlyList<EnrollmentDocumentStatusDto> Documents,
    EnrollmentFeeSummaryDto? FeeSummary,
    bool ConfirmAccuracy);

public sealed record EnrollmentValidationIssueDto(string Code, string Message, string? StepHint);

public sealed record EnrollmentValidationResultDto(
    bool IsValid,
    IReadOnlyList<EnrollmentValidationIssueDto> Issues);

public sealed record CompleteEnrollmentResultDto(
    Guid StudentId,
    Guid EnrollmentId,
    string RegistrationNumber,
    string StudentFullName,
    string ClassName,
    decimal TotalDue,
    string Message);

public sealed record GeneratedRegistrationNumberDto(string RegistrationNumber);

public sealed record ClassCapacityDto(
    Guid ClassRoomId,
    int? MaxCapacity,
    int CurrentCount,
    int Remaining,
    bool IsFull);

public sealed record EnrollmentClassOptionDto(
    Guid ClassRoomId,
    string Code,
    string FullDisplayName,
    string LocalName,
    string? PedagogicalDisplayName,
    string? HumanitiesSection,
    string? StudyOption,
    Guid SectionId,
    string SectionName,
    Guid? PedagogicalClassId,
    int? MaxCapacity,
    int CurrentCount,
    int? MinAge,
    int? MaxAge,
    bool IsSelectable);

public sealed record EnrollmentStructureOptionsDto(
    Guid AcademicYearId,
    string AcademicYearLabel,
    IReadOnlyList<SectionDto> Sections,
    IReadOnlyList<EnrollmentClassOptionDto> Classes);
