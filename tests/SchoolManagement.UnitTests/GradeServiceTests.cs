using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Grades.DTOs;
using SchoolManagement.Application.Grades.Services;
using SchoolManagement.Application.ResultValidation.Interfaces;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using Xunit;

namespace SchoolManagement.UnitTests;

public class GradeServiceTests
{
    [Fact]
    public async Task CalculatePeriodResultsAsync_Computes_Weighted_Averages_And_Ranks()
    {
        var schoolId = Guid.NewGuid();
        var yearId = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        var classRoomId = Guid.NewGuid();
        var pedagogicalClassId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var evaluationId = Guid.NewGuid();
        var student1Id = Guid.NewGuid();
        var student2Id = Guid.NewGuid();

        var year = new AcademicYear
        {
            Id = yearId,
            SchoolId = schoolId,
            IsCurrent = true,
            IsClosed = false
        };

        var classRoom = new ClassRoom
        {
            Id = classRoomId,
            SchoolId = schoolId,
            AcademicYearId = yearId,
            PedagogicalClassId = pedagogicalClassId,
            IsActive = true,
            Name = "6A"
        };

        var pedagogicalClass = new PedagogicalClass
        {
            Id = pedagogicalClassId,
            SchoolId = schoolId,
            IsEnabled = true
        };

        var course = new Course
        {
            Id = courseId,
            Name = "Mathématiques",
            Coefficient = 2,
            MaxScore = 20
        };

        var courseAssignmentId = Guid.NewGuid();
        var evaluationTypeId = Guid.NewGuid();

        var evaluation = new Evaluation
        {
            Id = evaluationId,
            ClassRoomId = classRoomId,
            AcademicPeriodId = periodId,
            AcademicYearId = yearId,
            CourseId = courseId,
            CourseAssignmentId = courseAssignmentId,
            EvaluationTypeId = evaluationTypeId,
            Weight = 1,
            MaxScore = 20
        };

        var students = new List<Student>
        {
            new() { Id = student1Id, SchoolId = schoolId, LastName = "Kabongo", FirstName = "Jean" },
            new() { Id = student2Id, SchoolId = schoolId, LastName = "Mputu", FirstName = "Marie" }
        };

        var enrollments = new List<Enrollment>
        {
            new() { StudentId = student1Id, ClassRoomId = classRoomId, AcademicYearId = yearId, IsActive = true },
            new() { StudentId = student2Id, ClassRoomId = classRoomId, AcademicYearId = yearId, IsActive = true }
        };

        var grades = new List<GradeEntry>
        {
            new() { EvaluationId = evaluationId, StudentId = student1Id, Score = 16, IsAbsent = false },
            new() { EvaluationId = evaluationId, StudentId = student2Id, Score = 12, IsAbsent = false }
        };

        var yearRepository = Substitute.For<IRepository<AcademicYear>>();
        yearRepository.FindAsync(Arg.Any<Expression<Func<AcademicYear, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => Filter(year, call.Arg<Expression<Func<AcademicYear, bool>>>()));

        var classRoomRepository = Substitute.For<IRepository<ClassRoom>>();
        classRoomRepository.FindAsync(Arg.Any<Expression<Func<ClassRoom, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => Filter(classRoom, call.Arg<Expression<Func<ClassRoom, bool>>>()));

        var pedagogicalClassRepository = Substitute.For<IRepository<PedagogicalClass>>();
        pedagogicalClassRepository.FindAsync(Arg.Any<Expression<Func<PedagogicalClass, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => Filter(pedagogicalClass, call.Arg<Expression<Func<PedagogicalClass, bool>>>()));

        var courseRepository = Substitute.For<IRepository<Course>>();
        courseRepository.FindAsync(Arg.Any<Expression<Func<Course, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => FilterList(new[] { course }, call.Arg<Expression<Func<Course, bool>>>()));

        var pedagogicalClassCourseRepository = Substitute.For<IRepository<PedagogicalClassCourse>>();

        var evaluationRepository = Substitute.For<IRepository<Evaluation>>();
        evaluationRepository.FindAsync(Arg.Any<Expression<Func<Evaluation, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => Filter(evaluation, call.Arg<Expression<Func<Evaluation, bool>>>()));

        var gradeRepository = Substitute.For<IRepository<GradeEntry>>();
        gradeRepository.FindAsync(Arg.Any<Expression<Func<GradeEntry, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => FilterList(grades, call.Arg<Expression<Func<GradeEntry, bool>>>()));

        var enrollmentRepository = Substitute.For<IRepository<Enrollment>>();
        enrollmentRepository.FindAsync(Arg.Any<Expression<Func<Enrollment, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => FilterList(enrollments, call.Arg<Expression<Func<Enrollment, bool>>>()));

        var studentRepository = Substitute.For<IRepository<Student>>();
        studentRepository.FindAsync(Arg.Any<Expression<Func<Student, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => FilterList(students, call.Arg<Expression<Func<Student, bool>>>()));

        var periodResultRepository = Substitute.For<IRepository<PeriodResult>>();
        periodResultRepository.FindAsync(Arg.Any<Expression<Func<PeriodResult, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PeriodResult>());
        periodResultRepository.AddAsync(Arg.Any<PeriodResult>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<PeriodResult>());

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var courseAssignmentRepository = Substitute.For<IRepository<CourseAssignment>>();
        courseAssignmentRepository.FindAsync(Arg.Any<Expression<Func<CourseAssignment, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CourseAssignment>());

        var evaluationTypeRepository = Substitute.For<IRepository<EvaluationTypeDefinition>>();

        var periodRepository = Substitute.For<IRepository<AcademicPeriod>>();
        periodRepository.FindAsync(Arg.Any<Expression<Func<AcademicPeriod, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => Filter(
                new AcademicPeriod { Id = periodId, MaxScore = 20 },
                call.Arg<Expression<Func<AcademicPeriod, bool>>>()));

        var resultCalculation = new ResultCalculationService(
            new ResultCalculationEngine(),
            NullLogger<ResultCalculationService>.Instance);

        var resultValidation = Substitute.For<IResultValidationService>();
        resultValidation.EnsureClassPeriodNotLockedAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        resultValidation.RecordCalculationAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var service = new GradeService(
            evaluationRepository,
            gradeRepository,
            periodResultRepository,
            courseRepository,
            pedagogicalClassCourseRepository,
            classRoomRepository,
            yearRepository,
            pedagogicalClassRepository,
            studentRepository,
            enrollmentRepository,
            courseAssignmentRepository,
            evaluationTypeRepository,
            Substitute.For<IRepository<Teacher>>(),
            Substitute.For<IRepository<Section>>(),
            periodRepository,
            Substitute.For<IRepository<AcademicMainPeriod>>(),
            Substitute.For<IRepository<SchoolManagement.Domain.Entities.Security.UserAccount>>(),
            Substitute.For<SchoolManagement.Application.Auth.Interfaces.IPasswordHasher>(),
            Substitute.For<ICurrentUserService>(),
            unitOfWork,
            resultCalculation,
            resultValidation);

        var results = await service.CalculatePeriodResultsAsync(
            schoolId,
            new CalculatePeriodResultsRequest(classRoomId, yearId, periodId));

        results.Should().HaveCount(2);
        results[0].StudentName.Should().Be("Kabongo Jean");
        results[0].Average.Should().Be(16);
        results[0].Percentage.Should().Be(80);
        results[0].Rank.Should().Be(1);
        results[1].StudentName.Should().Be("Mputu Marie");
        results[1].Average.Should().Be(12);
        results[1].Percentage.Should().Be(60);
        results[1].Rank.Should().Be(2);
    }

    private static IReadOnlyList<T> Filter<T>(T entity, Expression<Func<T, bool>> predicate) =>
        predicate.Compile()(entity) ? new List<T> { entity } : Array.Empty<T>();

    private static IReadOnlyList<T> FilterList<T>(IEnumerable<T> entities, Expression<Func<T, bool>> predicate)
    {
        var compiled = predicate.Compile();
        return entities.Where(compiled).ToList();
    }
}
