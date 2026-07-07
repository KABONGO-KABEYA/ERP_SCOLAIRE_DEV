namespace SchoolManagement.Application.Teacher.DTOs;

public sealed record TeacherAssignmentDto(
    Guid Id,
    Guid CourseId,
    string CourseName,
    Guid ClassRoomId,
    string ClassRoomName,
    Guid AcademicYearId,
    string AcademicYearLabel);

public sealed record TeacherPeriodDto(Guid Id, string Name, int OrderIndex);

public sealed record TeacherStudentDto(
    Guid StudentId,
    string RegistrationNumber,
    string FullName);
