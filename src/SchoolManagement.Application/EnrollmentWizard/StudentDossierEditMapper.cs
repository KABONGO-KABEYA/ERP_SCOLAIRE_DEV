namespace SchoolManagement.Application.EnrollmentWizard;

using SchoolManagement.Application.EnrollmentWizard.DTOs;
using SchoolManagement.Application.Geography.DTOs;
using SchoolManagement.Application.Geography.Interfaces;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;

internal static class StudentDossierEditMapper
{
    public static async Task<CompleteEnrollmentRequest> BuildRequestAsync(
        Student student,
        Enrollment enrollment,
        ClassRoom classRoom,
        StudentStatusHistory? statusHistory,
        IReadOnlyList<StudentGuardian> guardianLinks,
        IReadOnlyList<Guardian> guardians,
        IReadOnlyList<StudentDocument> documents,
        IAddressService addressService,
        CancellationToken cancellationToken)
    {
        var residenceAddress = await ResolveAddressInputAsync(student.AddressId, addressService, cancellationToken);
        var language = EnrollmentFormFieldParser.ExtractLabeledValue(student.Address, "Langue");
        var religion = EnrollmentFormFieldParser.ExtractLabeledValue(student.Address, "Religion");
        var guardianInputs = await BuildGuardianInputsAsync(
            guardianLinks,
            guardians,
            residenceAddress,
            addressService,
            cancellationToken);

        var medical = BuildMedicalDto(student);
        var scolarite = BuildScolariteDto(enrollment, classRoom, statusHistory);
        var documentStatuses = documents
            .OrderBy(d => d.DocumentType, StringComparer.OrdinalIgnoreCase)
            .Select(d => new EnrollmentDocumentStatusDto(
                d.DocumentType,
                "Complet",
                d.FileName,
                d.StoragePath,
                d.FileSizeBytes))
            .ToList();

        return new CompleteEnrollmentRequest(
            student.Id,
            student.FirstName,
            student.LastName,
            student.MiddleName,
            student.Gender,
            student.DateOfBirth,
            student.PlaceOfBirth,
            student.Nationality,
            residenceAddress,
            language,
            religion,
            student.Phone,
            student.Email,
            student.PhotoPath,
            medical,
            scolarite,
            guardianInputs,
            documentStatuses,
            null,
            true);
    }

    private static async Task<AddressInputDto?> ResolveAddressInputAsync(
        Guid? addressId,
        IAddressService addressService,
        CancellationToken cancellationToken)
    {
        if (!addressId.HasValue)
        {
            return null;
        }

        var address = await addressService.GetAsync(addressId.Value, cancellationToken);
        if (address is null)
        {
            return null;
        }

        return new AddressInputDto(
            address.CountryId,
            address.ProvinceId,
            address.CityId,
            address.CommuneId,
            address.Neighborhood,
            address.Avenue,
            address.HouseNumber);
    }

    private static async Task<IReadOnlyList<GuardianInputDto>> BuildGuardianInputsAsync(
        IReadOnlyList<StudentGuardian> links,
        IReadOnlyList<Guardian> guardians,
        AddressInputDto? studentAddress,
        IAddressService addressService,
        CancellationToken cancellationToken)
    {
        var guardianMap = guardians.ToDictionary(g => g.Id);
        var results = new List<GuardianInputDto>();

        foreach (var link in links.OrderByDescending(l => l.IsPrimary).ThenBy(l => l.Relationship))
        {
            if (!guardianMap.TryGetValue(link.GuardianId, out var guardian))
            {
                continue;
            }

            AddressInputDto? residenceAddress = null;
            if (!link.UsesStudentAddress)
            {
                residenceAddress = await ResolveAddressInputAsync(guardian.AddressId, addressService, cancellationToken);
            }

            var (profession, employer) = SplitProfession(guardian.Profession);
            results.Add(new GuardianInputDto(
                guardian.FirstName,
                guardian.LastName,
                guardian.Phone,
                guardian.Email,
                link.UsesStudentAddress ? studentAddress : residenceAddress,
                profession,
                employer,
                link.Relationship,
                link.IsPrimary,
                link.CanPickup,
                guardian.Gender,
                link.UsesStudentAddress,
                guardian.Id));
        }

        return results;
    }

    private static (string? Profession, string? Employer) SplitProfession(string? profession)
    {
        if (string.IsNullOrWhiteSpace(profession))
        {
            return (null, null);
        }

        var separator = " — ";
        var index = profession.IndexOf(separator, StringComparison.Ordinal);
        if (index < 0)
        {
            return (profession.Trim(), null);
        }

        return (
            profession[..index].Trim(),
            profession[(index + separator.Length)..].Trim());
    }

    private static EnrollmentMedicalDto BuildMedicalDto(Student student)
    {
        var notes = student.MedicalNotes;
        return new EnrollmentMedicalDto(
            student.BloodGroup,
            EnrollmentFormFieldParser.ExtractMedicalValue(notes, "Allergies"),
            EnrollmentFormFieldParser.ExtractMedicalValue(notes, "Maladies chroniques"),
            EnrollmentFormFieldParser.ExtractMedicalValue(notes, "Traitement"),
            EnrollmentFormFieldParser.ExtractMedicalValue(notes, "Médecin"),
            EnrollmentFormFieldParser.ExtractMedicalValue(notes, "Centre médical"),
            EnrollmentFormFieldParser.ExtractMedicalValue(notes, "Handicap"),
            EnrollmentFormFieldParser.ExtractMedicalValue(notes, "Observations"),
            !string.IsNullOrWhiteSpace(notes)
            && notes.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(line => line.Equals("URGENCE MÉDICALE", StringComparison.OrdinalIgnoreCase)));
    }

    private static EnrollmentScolariteDto BuildScolariteDto(
        Enrollment enrollment,
        ClassRoom classRoom,
        StudentStatusHistory? statusHistory)
    {
        var registrationKind = ParseRegistrationKind(statusHistory?.Reason);
        return new EnrollmentScolariteDto(
            classRoom.SectionId,
            enrollment.ClassRoomId,
            classRoom.PedagogicalClassId,
            ParseOrderNumber(enrollment.Notes),
            enrollment.EnrollmentDate,
            registrationKind,
            EnrollmentFormFieldParser.ExtractNoteValue(enrollment.Notes, "Provenance:"),
            EnrollmentFormFieldParser.ExtractNoteValue(enrollment.Notes, "Code élève:"),
            EnrollmentFormFieldParser.ExtractNoteValue(enrollment.Notes, "N° permanent:"));
    }

    private static int? ParseOrderNumber(string? notes)
    {
        var raw = EnrollmentFormFieldParser.ExtractNoteValue(notes, "N° ordre:");
        return int.TryParse(raw, out var value) ? value : null;
    }

    private static RegistrationKind ParseRegistrationKind(string? reason)
    {
        if (!string.IsNullOrWhiteSpace(reason)
            && Enum.TryParse<RegistrationKind>(reason, out var parsed))
        {
            return parsed;
        }

        return RegistrationKind.NouvelleInscription;
    }
}
