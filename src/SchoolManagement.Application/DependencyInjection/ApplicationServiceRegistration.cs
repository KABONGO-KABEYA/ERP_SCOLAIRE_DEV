namespace SchoolManagement.Application.DependencyInjection;

using FluentValidation;
using Mapster;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Admin.Interfaces;
using SchoolManagement.Application.Admin.Services;
using SchoolManagement.Application.Academic.Interfaces;
using SchoolManagement.Application.Academic.Services;
using SchoolManagement.Application.DocumentBranding.Interfaces;
using SchoolManagement.Application.DocumentBranding.Services;
using SchoolManagement.Application.Documents.Interfaces;
using SchoolManagement.Application.Documents.Services;
using SchoolManagement.Application.Grades.Interfaces;
using SchoolManagement.Application.Grades.Services;
using SchoolManagement.Application.Personnel.Interfaces;
using SchoolManagement.Application.Personnel.Services;
using SchoolManagement.Application.Parent.Interfaces;
using SchoolManagement.Application.Parent.Services;
using SchoolManagement.Application.Dashboard.Interfaces;
using SchoolManagement.Application.Dashboard.Services;
using SchoolManagement.Application.Reports.Interfaces;
using SchoolManagement.Application.Reports.Services;
using SchoolManagement.Application.Payments.Interfaces;
using SchoolManagement.Application.Payments.Services;
using SchoolManagement.Application.Accounting.Interfaces;
using SchoolManagement.Application.Accounting.Services;
using SchoolManagement.Application.RevenueAllocation.Interfaces;
using SchoolManagement.Application.RevenueAllocation.Services;
using SchoolManagement.Application.Withholdings.Interfaces;
using SchoolManagement.Application.Withholdings.Services;
using SchoolManagement.Application.CurrencyManagement.Interfaces;
using SchoolManagement.Application.CurrencyManagement.Services;
using SchoolManagement.Application.StudentCards.Interfaces;
using SchoolManagement.Application.StudentCards.Services;
using SchoolManagement.Application.Finance.Interfaces;
using SchoolManagement.Application.Finance.Services;
using SchoolManagement.Application.Schools.Interfaces;
using SchoolManagement.Application.Schools.Services;
using SchoolManagement.Application.Teacher.Interfaces;
using SchoolManagement.Application.Teacher.Services;
using SchoolManagement.Application.Geography.Interfaces;
using SchoolManagement.Application.Geography.Services;
using SchoolManagement.Application.EnrollmentWizard.Interfaces;
using SchoolManagement.Application.EnrollmentWizard.Services;
using SchoolManagement.Application.SchoolFees.Interfaces;
using SchoolManagement.Application.SchoolFees.Services;
using SchoolManagement.Application.Students.Interfaces;
using SchoolManagement.Application.Students.Services;
using SchoolManagement.Application.CourseConfiguration.Interfaces;
using SchoolManagement.Application.PedagogicalPeriods.Interfaces;
using SchoolManagement.Application.PedagogicalPeriods.Services;
using SchoolManagement.Application.CourseConfiguration.Services;
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
        services.AddScoped<IEnrollmentFormService, EnrollmentFormService>();
        services.AddScoped<IGeographyService, GeographyService>();
        services.AddScoped<IGeographyAdminService, GeographyAdminService>();
        services.AddScoped<IAddressService, AddressService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IFeeTypeStatementService, FeeTypeStatementService>();
        services.AddScoped<IRevenueAllocationEngine, RevenueAllocationEngine>();
        services.AddScoped<IRevenueAllocationService, RevenueAllocationService>();
        services.AddScoped<IAccountingService, AccountingService>();
        services.AddScoped<IWithholdingEngine, WithholdingEngine>();
        services.AddScoped<IWithholdingService, WithholdingService>();
        services.AddScoped<ICurrencyService, CurrencyService>();
        services.AddScoped<IStudentCardService, StudentCardService>();
        services.AddScoped<IFinanceOperationService, FinanceOperationService>();
        services.AddScoped<ISchoolFeeService, SchoolFeeService>();
        services.AddScoped<ICourseConfigurationService, CourseConfigurationService>();
        services.AddScoped<IStudentFeeBalanceProvisioner, StudentFeeBalanceProvisioner>();
        services.AddScoped<IGradeService, GradeService>();
        services.AddScoped<IPedagogicalPeriodService, PedagogicalPeriodService>();
        services.AddScoped<IParentService, ParentService>();
        services.AddScoped<IParentAccessProvisioningService, ParentAccessProvisioningService>();
        services.AddScoped<IAcademicService, AcademicService>();
        services.AddScoped<ITeacherService, TeacherService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IDocumentBrandingService, DocumentBrandingService>();
        services.AddScoped<IDocumentPrintBrandingResolver, DocumentPrintBrandingResolver>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IPromoterDashboardService, PromoterDashboardService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ITeacherAdminService, TeacherAdminService>();
        services.AddScoped<IPersonnelAdminService, PersonnelAdminService>();

        return services;
    }
}
