namespace SchoolManagement.Shared.Constants;

public static class Permissions
{
    public const string StudentsRead = "students.read";
    public const string StudentsCreate = "students.create";
    public const string StudentsUpdate = "students.update";
    public const string StudentsDelete = "students.delete";

    public const string SchoolsRead = "schools.read";
    public const string SchoolsUpdate = "schools.update";

    public const string PaymentsRead = "payments.read";
    public const string PaymentsCreate = "payments.create";
    public const string PaymentsValidate = "payments.validate";

    public const string GradesRead = "grades.read";
    public const string GradesCreate = "grades.create";
    public const string GradesUpdate = "grades.update";

    public const string ReportsRead = "reports.read";

    public const string AdminFull = "admin.full";

    public static IReadOnlyList<string> All { get; } =
    [
        StudentsRead, StudentsCreate, StudentsUpdate, StudentsDelete,
        SchoolsRead, SchoolsUpdate,
        PaymentsRead, PaymentsCreate, PaymentsValidate,
        GradesRead, GradesCreate, GradesUpdate,
        ReportsRead, AdminFull
    ];
}
