namespace SchoolManagement.IntegrationTests.MultiTenant;

internal enum CrossTenantExpectation
{
    /// <summary>Ressource identifiée : la requête doit être rejetée (aucun 2xx).</summary>
    Denied,

    /// <summary>
    /// Collection filtrée par un identifiant étranger : un 2xx vide est acceptable,
    /// aucune donnée de l'autre école ne doit apparaître.
    /// </summary>
    NoForeignData
}

/// <summary>
/// Tentative d'accès à une ressource identifiée d'une autre école.
/// <paramref name="ControlPath"/> est un GET exécuté par le propriétaire légitime : il prouve que
/// la route et la donnée existent réellement, sans quoi un 404 cross-école ne prouverait rien.
/// </summary>
internal sealed record CrossTenantScenario(
    string Resource,
    string Method,
    string PathTemplate,
    string ControlPath,
    string? Body = null,
    CrossTenantExpectation Expectation = CrossTenantExpectation.Denied);

/// <summary>Endpoint de liste : la réponse ne doit jamais contenir le marqueur de l'autre école.</summary>
internal sealed record ListLeakScenario(string Resource, string PathTemplate);

internal static class CrossTenantScenarios
{
    private const string StudentControl = "api/v1/students/{studentId}";
    private const string DocumentControl = "api/v1/documents?studentId={studentId}";
    private const string EnrollmentControl = "api/v1/academic/enrollments?classRoomId={classRoomId}";
    private const string CourseControl = "api/v1/academic/courses";
    private const string EvaluationControl =
        "api/v1/grades/evaluations?classRoomId={classRoomId}&academicPeriodId={academicPeriodId}";
    private const string PaymentControl = "api/v1/payments/{paymentId}";
    private const string FeeTypeControl = "api/v1/school-fees/fee-types";
    private const string PricingCategoryControl = "api/v1/school-fees/pricing-categories";
    private const string CardControl = "api/v1/cards/{studentCardId}";
    private const string CardTemplateControl = "api/v1/card-templates/{cardTemplateId}";
    private const string UserControl = "api/v1/admin/users";
    private const string TeacherControl = "api/v1/admin/teachers";
    private const string PersonnelControl = "api/v1/personnel/{teacherId}";
    private const string MentionControl = "api/v1/mentions";

    internal static IReadOnlyList<CrossTenantScenario> Targeted { get; } =
    [
        // Élèves
        new("Élèves", "GET", "api/v1/students/{studentId}", StudentControl),
        new("Élèves", "GET", "api/v1/students/{studentId}/profile", StudentControl),
        new("Élèves", "PUT", "api/v1/students/{studentId}", StudentControl,
            """
            {"firstName":"Intrus","lastName":"Intrus","middleName":null,"gender":1,
             "dateOfBirth":"2014-05-12","placeOfBirth":null,"address":null,"phone":null,"email":null}
            """),
        new("Élèves", "POST", "api/v1/students/{studentId}/withdraw-current-year", StudentControl,
            """{"withdrawalType":2,"reasonCode":"AUTRE","customReason":"tentative cross-tenant"}"""),

        // Documents élève
        new("Documents élève", "GET", "api/v1/documents/{studentDocumentId}/download", DocumentControl),
        new("Documents élève", "DELETE", "api/v1/documents/{studentDocumentId}", DocumentControl),

        // Inscriptions
        new("Inscriptions", "GET", EnrollmentControl, EnrollmentControl,
            Expectation: CrossTenantExpectation.NoForeignData),
        new("Inscriptions", "POST", "api/v1/academic/enrollments", EnrollmentControl,
            """
            {"studentId":"{studentId}","academicYearId":"{academicYearId}",
             "classRoomId":"{classRoomId}","enrollmentDate":"2025-09-05"}
            """),

        // Classes / salles / cours
        new("Classes et salles", "GET", "api/v1/academic/classrooms?academicYearId={academicYearId}",
            "api/v1/academic/classrooms", Expectation: CrossTenantExpectation.NoForeignData),
        new("Cours", "GET", "api/v1/academic/courses?classRoomId={classRoomId}", CourseControl,
            Expectation: CrossTenantExpectation.NoForeignData),
        new("Cours", "PUT", "api/v1/academic/courses/{courseId}", CourseControl,
            """{"code":"INTRUS","name":"Cours detourne","coefficient":1,"maxScore":20}"""),
        new("Cours", "DELETE", "api/v1/academic/courses/{courseId}", CourseControl),

        // Notes et évaluations
        new("Notes", "GET", EvaluationControl, EvaluationControl,
            Expectation: CrossTenantExpectation.NoForeignData),
        new("Notes", "GET", "api/v1/grades/evaluations/{evaluationId}/entries", EvaluationControl),
        new("Notes", "PUT", "api/v1/grades/evaluations/{evaluationId}", EvaluationControl,
            """{"title":"Interro detournee","evaluationDate":"2025-10-15","maxScore":20}"""),
        new("Notes", "DELETE", "api/v1/grades/evaluations/{evaluationId}", EvaluationControl),
        new("Notes", "POST", "api/v1/grades/entries", EvaluationControl,
            """
            {"evaluationId":"{evaluationId}",
             "grades":[{"studentId":"{studentId}","score":19,"isAbsent":false,"comment":"intrusion"}]}
            """),

        // Paiements
        new("Paiements", "GET", "api/v1/payments/{paymentId}", PaymentControl),
        new("Paiements", "PUT", "api/v1/payments/{paymentId}/notes", PaymentControl,
            """{"notes":"note injectee"}"""),
        new("Paiements", "POST", "api/v1/payments/{paymentId}/cancel", PaymentControl,
            """{"reason":"annulation cross-tenant"}"""),

        // Frais scolaires
        new("Frais scolaires", "PUT", "api/v1/school-fees/fee-types/{feeTypeId}", FeeTypeControl,
            """{"name":"Frais detourne","currency":1,"isMandatory":true,"isActive":true}"""),
        new("Frais scolaires", "DELETE", "api/v1/school-fees/fee-types/{feeTypeId}", FeeTypeControl),
        new("Frais scolaires", "PUT", "api/v1/school-fees/pricing-categories/{pricingCategoryId}",
            PricingCategoryControl,
            """{"code":"INTRUS","name":"Categorie detournee","description":null,"isActive":true}"""),
        new("Frais scolaires", "DELETE", "api/v1/school-fees/pricing-categories/{pricingCategoryId}",
            PricingCategoryControl),

        // Cartes élèves
        new("Cartes élèves", "GET", "api/v1/cards/{studentCardId}", CardControl),
        new("Cartes élèves", "PUT", "api/v1/cards/{studentCardId}", CardControl,
            """{"templateId":"{cardTemplateId}","expiresAt":null,"notes":"intrusion"}"""),
        new("Cartes élèves", "DELETE", "api/v1/cards/{studentCardId}", CardControl),
        new("Cartes élèves", "POST", "api/v1/cards/{studentCardId}/reprint", CardControl,
            """{"reason":"reimpression cross-tenant"}"""),
        new("Modèles de carte", "GET", "api/v1/card-templates/{cardTemplateId}", CardTemplateControl),
        new("Modèles de carte", "PUT", "api/v1/card-templates/{cardTemplateId}", CardTemplateControl,
            """
            {"name":"Modele detourne","description":null,"widthMm":85.6,"heightMm":53.98,
             "orientation":1,"kind":1,"layoutJsonFront":null,"layoutJsonBack":null,"isActive":true}
            """),
        new("Modèles de carte", "DELETE", "api/v1/card-templates/{cardTemplateId}", CardTemplateControl),

        // Utilisateurs et enseignants (administration)
        new("Utilisateurs", "PUT", "api/v1/admin/users/{userId}", UserControl,
            """{"email":"intrus@test.local","firstName":"Intrus","lastName":"Intrus","isActive":false}"""),
        new("Enseignants", "PUT", "api/v1/admin/teachers/{teacherId}", TeacherControl,
            """
            {"employeeNumber":"INTRUS","firstName":"Intrus","lastName":"Intrus","phone":null,
             "email":null,"specialization":null,"hireDate":null,"isActive":false,
             "residenceAddress":null,"updateAddress":false}
            """),

        // Personnel (RH)
        new("Personnel", "GET", "api/v1/personnel/{teacherId}", PersonnelControl),
        new("Personnel", "PUT", "api/v1/personnel/{teacherId}", PersonnelControl,
            """
            {"employeeNumber":"INTRUS","firstName":"Intrus","middleName":null,"lastName":"Intrus",
             "phone":null,"email":null,"specialization":null,"hireDate":null,"isActive":false,
             "residenceAddress":null,"category":1,"gender":1,"birthDate":null,"birthPlace":null,
             "nationality":null,"maritalStatus":null,"childrenCount":null,"idCardNumber":null,
             "departmentId":null,"jobFunctionId":null,"grade":null,"service":null,
             "supervisorName":null,"workLocation":null,"contractType":null,"contractStartDate":null,
             "contractEndDate":null,"baseSalary":null,"currencyCode":null,"paymentMethod":null,
             "bankName":null,"bankAccountNumber":null,"bankAccountHolder":null,"payDay":null,
             "emergencyContactName":null,"emergencyContactRelation":null,"emergencyContactPhone":null,
             "emergencyContactAddress":null,"status":1,"systemUsername":null,"systemPassword":null,
             "systemPasswordConfirm":null,"systemRoleId":null,"allowSystemLogin":false,
             "createSystemAccount":false}
            """),

        // Délibérations
        new("Délibérations", "GET",
            "api/v1/deliberation/sheet?academicYearId={academicYearId}&classRoomId={classRoomId}"
            + "&academicPeriodId={academicPeriodId}",
            EvaluationControl),
        new("Délibérations", "GET",
            "api/v1/deliberation/minutes?academicYearId={academicYearId}&classRoomId={classRoomId}"
            + "&academicPeriodId={academicPeriodId}",
            EvaluationControl),
        new("Délibérations", "GET",
            "api/v1/deliberation/decision?academicYearId={academicYearId}&classRoomId={classRoomId}"
            + "&academicPeriodId={academicPeriodId}&studentId={studentId}",
            EvaluationControl),
        new("Mentions", "PUT", "api/v1/mentions/{mentionId}", MentionControl,
            """
            {"label":"Mention detournee","minPercentageInclusive":50,
             "maxPercentageInclusive":69,"sortOrder":1,"isActive":true}
            """),
        new("Mentions", "DELETE", "api/v1/mentions/{mentionId}", MentionControl),
    ];

    /// <summary>Listes consultées avec le jeton de l'attaquant : elles ne doivent contenir aucune donnée voisine.</summary>
    internal static IReadOnlyList<ListLeakScenario> Lists { get; } =
    [
        new("Élèves", "api/v1/students?includeAll=true&pageSize=200"),
        new("Classes et salles", "api/v1/academic/classrooms"),
        new("Cours", "api/v1/academic/courses"),
        new("Sections", "api/v1/academic/sections"),
        new("Paiements", "api/v1/payments?pageSize=200"),
        new("Frais scolaires", "api/v1/school-fees/fee-types"),
        new("Frais scolaires", "api/v1/school-fees/pricing-categories"),
        new("Frais scolaires", "api/v1/school-fees/installments"),
        new("Cartes élèves", "api/v1/cards?pageSize=200"),
        new("Modèles de carte", "api/v1/card-templates"),
        new("Utilisateurs", "api/v1/admin/users"),
        new("Enseignants", "api/v1/admin/teachers"),
        new("Personnel", "api/v1/personnel"),
        new("Mentions", "api/v1/mentions"),
    ];
}
