namespace SchoolManagement.Application.Grades.DTOs;

using SchoolManagement.Domain.Enums;

public sealed record EvaluationTypeDto(
    Guid Id,
    string Code,
    string Name,
    bool IsActive);

public sealed record EvaluationDto(
    Guid Id,
    string Title,
    Guid EvaluationTypeId,
    string EvaluationTypeName,
    Guid? EnrollmentId,
    Guid CourseAssignmentId,
    Guid CourseId,
    string CourseName,
    Guid ClassRoomId,
    string ClassRoomName,
    Guid AcademicPeriodId,
    decimal Weight,
    int MaxScore,
    DateOnly EvaluationDate,
    bool IsOpen,
    bool IsPublished,
    int GradedCount = 0,
    int StudentCount = 0);

public sealed record CreateEvaluationRequest(
    Guid AcademicYearId,
    Guid AcademicPeriodId,
    Guid CourseId,
    Guid ClassRoomId,
    Guid EvaluationTypeId,
    Guid? EnrollmentId,
    string Title,
    decimal Weight,
    int MaxScore,
    DateOnly EvaluationDate);

public sealed record UpdateEvaluationRequest(
    string Title,
    DateOnly EvaluationDate,
    int MaxScore);

public sealed record GradeEntryDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    decimal Score,
    bool IsAbsent,
    string? Comment);

public sealed record SubmitGradesRequest(
    Guid EvaluationId,
    IReadOnlyList<GradeEntryInput> Grades);

public sealed record GradeEntryInput(
    Guid StudentId,
    decimal Score,
    bool IsAbsent,
    string? Comment);

public sealed record PeriodResultDto(
    Guid StudentId,
    string StudentName,
    decimal Average,
    decimal Percentage,
    int Rank,
    int ClassSize,
    string? Appreciation,
    ClassCouncilDecision CouncilDecision);

public sealed record CalculatePeriodResultsRequest(
    Guid ClassRoomId,
    Guid AcademicYearId,
    Guid AcademicPeriodId);

public sealed record PublishPeriodCotationRequest(
    Guid ClassRoomId,
    Guid AcademicYearId,
    Guid AcademicPeriodId);
