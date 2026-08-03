namespace SchoolManagement.Domain.Enums;

/// <summary>Catégorie affichée / filtrable dans le centre de notifications parent.</summary>
public enum NotificationCategory
{
    Payment = 1,
    Bulletin = 2,
    Grades = 3,
    Attendance = 4,
    Discipline = 5,
    Merit = 6,
    Communication = 7,
    Administration = 8,
    Assignment = 9,
}

/// <summary>Type d'événement métier déclencheur.</summary>
public enum NotificationEventType
{
    Generic = 0,
    EnrollmentCreated = 1,
    PaymentReceived = 2,
    PaymentCancelled = 3,
    BulletinPublished = 4,
    GradeRecorded = 5,
    GradeModified = 6,
    AbsenceRecorded = 7,
    LateRecorded = 8,
    SanctionRecorded = 9,
    MeritRecorded = 10,
    AnnouncementPublished = 11,
    AssignmentCreated = 12,
    StudentUpdated = 13,
    ClassChanged = 14,
}
