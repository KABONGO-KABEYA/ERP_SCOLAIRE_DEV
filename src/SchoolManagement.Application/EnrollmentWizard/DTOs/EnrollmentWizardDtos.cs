namespace SchoolManagement.Application.EnrollmentWizard.DTOs;

using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.DocumentBranding.DTOs;
using SchoolManagement.Application.Geography.DTOs;
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
    AddressInputDto? ResidenceAddress,
    string? Profession,
    string? Employer,
    string Relationship,
    bool IsPrimary,
    bool CanPickup,
    Gender? Gender = null,
    bool UsesStudentAddress = false,
    Guid? ExistingGuardianId = null);

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
    string? StoragePath = null,
    long FileSizeBytes = 0);

public sealed record StoredEnrollmentFileDto(
    string StoragePath,
    string FileName,
    long FileSizeBytes);

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
    string StatusLabel,
    int? LastClassLevel = null)
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
    AddressInputDto? ResidenceAddress,
    string? Language,
    string? Religion,
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

public sealed record StudentDossierEditDto(
    Guid StudentId,
    Guid EnrollmentId,
    string RegistrationNumber,
    bool CanChangeClass,
    string? ClassChangeBlockedReason,
    CompleteEnrollmentRequest Dossier);

public sealed record UpdateStudentDossierResultDto(
    Guid StudentId,
    Guid EnrollmentId,
    string RegistrationNumber,
    string StudentFullName,
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
    int Level,
    int? MaxCapacity,
    int CurrentCount,
    int? MinAge,
    int? MaxAge,
    bool IsSelectable);

public sealed record EnrollmentGuardianSearchResultDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? Phone,
    string? Email,
    string? Address,
    string? Profession,
    Gender? Gender)
{
    public string FullName => $"{LastName} {FirstName}".Trim();
}

public sealed record EnrollmentStructureOptionsDto(
    Guid AcademicYearId,
    string AcademicYearLabel,
    IReadOnlyList<SectionDto> Sections,
    IReadOnlyList<EnrollmentClassOptionDto> Classes);

public sealed record EnrollmentFormGuardianDto(
    string LastName,
    string FirstName,
    string Relationship,
    string? Phone,
    string? Email,
    string? Address,
    string? Profession,
    bool IsPrimary,
    bool CanPickup)
{
    public string FullName => $"{LastName} {FirstName}".Trim();
}

public sealed record EnrollmentFormDocumentDto(
    string SchoolName,
    string AcademicYearLabel,
    DateTime GeneratedAt,
    DocumentPrintBrandingDto Branding,
    string RegistrationNumber,
    string LastName,
    string FirstName,
    string? MiddleName,
    string GenderLabel,
    DateOnly DateOfBirth,
    int Age,
    string? PlaceOfBirth,
    string? Nationality,
    string? Province,
    string? Territory,
    string? Commune,
    string? Avenue,
    string? HouseNumber,
    string? Phone,
    string? Email,
    string? PhotoPath,
    string ClassName,
    string? SectionName,
    string EducationRegime,
    string RegistrationStatut,
    string RegistrationKindLabel,
    DateOnly EnrollmentDate,
    string? PreviousSchool,
    string? PreviousClass,
    string? PreviousStudentCode,
    string? BloodGroup,
    string? Allergies,
    string? ChronicDiseases,
    string? Disability,
    string? DoctorName,
    string? MedicalCenter,
    string? Observations,
    IReadOnlyList<string> ProvidedDocuments,
    decimal? RegistrationFee,
    decimal AmountPaid,
    string? Currency,
    string? PrintedBy,
    string Workstation,
    string ErpVersion,
    EnrollmentFormGuardianDto? Father,
    EnrollmentFormGuardianDto? Mother,
    EnrollmentFormGuardianDto? LegalGuardian,
    IReadOnlyList<EnrollmentFormGuardianDto> Guardians)
{
    public string FullName => string.IsNullOrWhiteSpace(MiddleName)
        ? $"{LastName} {FirstName}".Trim()
        : $"{LastName} {MiddleName} {FirstName}".Trim();

    public decimal? BalanceDue => RegistrationFee.HasValue ? RegistrationFee - AmountPaid : null;
}
