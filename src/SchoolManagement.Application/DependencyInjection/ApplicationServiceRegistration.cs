namespace SchoolManagement.Application.DependencyInjection;

using FluentValidation;
using Mapster;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Admin.Interfaces;
using SchoolManagement.Application.Admin.Services;
using SchoolManagement.Application.Academic.Interfaces;
using SchoolManagement.Application.Academic.Services;
using SchoolManagement.Application.Documents.Interfaces;
using SchoolManagement.Application.Documents.Services;
using SchoolManagement.Application.Grades.Interfaces;
using SchoolManagement.Application.Grades.Services;
using SchoolManagement.Application.Parent.Interfaces;
using SchoolManagement.Application.Parent.Services;
using SchoolManagement.Application.Reports.Interfaces;
using SchoolManagement.Application.Reports.Services;
using SchoolManagement.Application.Payments.Interfaces;
using SchoolManagement.Application.Payments.Services;
using SchoolManagement.Application.Schools.Interfaces;
using SchoolManagement.Application.Schools.Services;
using SchoolManagement.Application.Teacher.Interfaces;
using SchoolManagement.Application.Teacher.Services;
using SchoolManagement.Application.EnrollmentWizard.Interfaces;
using SchoolManagement.Application.EnrollmentWizard.Services;
using SchoolManagement.Application.Students.Interfaces;
using SchoolManagement.Application.Students.Services;
using System.Reflection;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        TypeAdapterConfig.GlobalSettings.Scan(Assembly.GetExecutingAssembly());

        services.AddScoped<ISchoolService, SchoolService>();
        services.AddScoped<IPedagogicalStructureService, PedagogicalStructureService>();
        services.AddScoped<IStudentService, StudentService>();
        services.AddScoped<IEnrollmentWizardService, EnrollmentWizardService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IGradeService, GradeService>();
        services.AddScoped<IParentService, ParentService>();
        services.AddScoped<IAcademicService, AcademicService>();
        services.AddScoped<ITeacherService, TeacherService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAdminService, AdminService>();

        return services;
    }
}
