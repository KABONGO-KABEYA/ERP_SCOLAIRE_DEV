namespace SchoolManagement.Application.Mapping;

using Mapster;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;

public class ApplicationMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<School, SchoolDto>();
        config.NewConfig<AcademicYear, AcademicYearDto>();
        config.NewConfig<UpdateSchoolRequest, School>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.CreatedAt)
            .Ignore(dest => dest.IsActive);

        config.NewConfig<CreateAcademicYearRequest, AcademicYear>()
            .Map(dest => dest.IsCurrent, src => src.SetAsCurrent)
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.SchoolId)
            .Ignore(dest => dest.IsClosed);

        config.NewConfig<Student, StudentDto>()
            .MapWith(s => new StudentDto(
                s.Id,
                s.RegistrationNumber,
                s.FirstName,
                s.LastName,
                s.MiddleName,
                s.Gender,
                s.DateOfBirth,
                s.Phone,
                s.Email,
                s.IsArchived));
        config.NewConfig<CreateStudentRequest, Student>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.SchoolId)
            .Ignore(dest => dest.IsArchived);
        config.NewConfig<UpdateStudentRequest, Student>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.SchoolId)
            .Ignore(dest => dest.RegistrationNumber)
            .Ignore(dest => dest.IsArchived);
    }
}
