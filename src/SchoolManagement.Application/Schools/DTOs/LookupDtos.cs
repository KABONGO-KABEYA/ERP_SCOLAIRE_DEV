namespace SchoolManagement.Application.Schools.DTOs;

using SchoolManagement.Domain.Enums;

public sealed record FeeTypeLookupDto(Guid Id, string Code, string Name, Currency Currency);

public sealed record CashRegisterLookupDto(Guid Id, string Code, string Name, Currency Currency);

public sealed record ClassRoomLookupDto(Guid Id, string Code, string Name, Guid AcademicYearId);

public sealed record AcademicPeriodLookupDto(Guid Id, string Name, Guid AcademicYearId, int OrderIndex);

public sealed record CourseLookupDto(Guid Id, string Code, string Name, Guid? ClassRoomId);

public sealed record SchoolLookupsDto(
    IReadOnlyList<AcademicYearDto> AcademicYears,
    IReadOnlyList<AcademicPeriodLookupDto> AcademicPeriods,
    IReadOnlyList<ClassRoomLookupDto> ClassRooms,
    IReadOnlyList<CourseLookupDto> Courses,
    IReadOnlyList<FeeTypeLookupDto> FeeTypes,
    IReadOnlyList<CashRegisterLookupDto> CashRegisters);
