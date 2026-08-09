namespace SchoolManagement.IntegrationTests.MultiTenant;

/// <summary>
/// Identifiants des données semées pour une école de test.
/// <see cref="Marker"/> est une chaîne unique présente dans les libellés : sa présence dans une
/// réponse HTTP prouverait une fuite de données entre établissements.
/// </summary>
public sealed class TenantTestSchool
{
    public required string Marker { get; init; }

    public required string JwtToken { get; init; }

    public required Guid SchoolId { get; init; }

    public required Guid AcademicYearId { get; init; }

    public required Guid AcademicPeriodId { get; init; }

    public required Guid SectionId { get; init; }

    public required Guid PedagogicalClassId { get; init; }

    public required Guid ClassRoomId { get; init; }

    public required Guid CourseId { get; init; }

    public required Guid StudentId { get; init; }

    public required Guid EnrollmentId { get; init; }

    public required Guid StudentDocumentId { get; init; }

    public required Guid TeacherId { get; init; }

    public required Guid CourseAssignmentId { get; init; }

    public required Guid EvaluationId { get; init; }

    public required Guid PaymentId { get; init; }

    public required Guid FeeTypeId { get; init; }

    public required Guid PricingCategoryId { get; init; }

    public required Guid FeeInstallmentId { get; init; }

    public required Guid CardTemplateId { get; init; }

    public required Guid StudentCardId { get; init; }

    public required Guid UserId { get; init; }

    public required Guid PersonnelProfileId { get; init; }

    public required Guid MentionId { get; init; }

    /// <summary>Jetons de substitution utilisables dans les gabarits d'URL et de corps JSON.</summary>
    public IReadOnlyDictionary<string, string> Tokens => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["schoolId"] = SchoolId.ToString(),
        ["academicYearId"] = AcademicYearId.ToString(),
        ["academicPeriodId"] = AcademicPeriodId.ToString(),
        ["sectionId"] = SectionId.ToString(),
        ["pedagogicalClassId"] = PedagogicalClassId.ToString(),
        ["classRoomId"] = ClassRoomId.ToString(),
        ["courseId"] = CourseId.ToString(),
        ["studentId"] = StudentId.ToString(),
        ["enrollmentId"] = EnrollmentId.ToString(),
        ["studentDocumentId"] = StudentDocumentId.ToString(),
        ["teacherId"] = TeacherId.ToString(),
        ["courseAssignmentId"] = CourseAssignmentId.ToString(),
        ["evaluationId"] = EvaluationId.ToString(),
        ["paymentId"] = PaymentId.ToString(),
        ["feeTypeId"] = FeeTypeId.ToString(),
        ["pricingCategoryId"] = PricingCategoryId.ToString(),
        ["feeInstallmentId"] = FeeInstallmentId.ToString(),
        ["cardTemplateId"] = CardTemplateId.ToString(),
        ["studentCardId"] = StudentCardId.ToString(),
        ["userId"] = UserId.ToString(),
        ["personnelProfileId"] = PersonnelProfileId.ToString(),
        ["mentionId"] = MentionId.ToString(),
    };
}
