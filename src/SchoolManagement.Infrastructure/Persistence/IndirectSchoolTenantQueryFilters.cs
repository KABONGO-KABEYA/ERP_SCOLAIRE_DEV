namespace SchoolManagement.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Deliberation;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Entities.Notifications;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Entities.Sync;

/// <summary>
/// Filtres tenant pour les entités sans SchoolId direct : la condition passe par la navigation
/// documentée dans <c>SchoolTenancyCatalog.IndirectOwnershipChains</c>.
/// La condition tenant est écrite en ligne dans chaque lambda : toute extraction vers une méthode
/// utilitaire casse la traduction SQL Server (EF ne sait pas traduire un appel de méthode).
/// </summary>
internal static class IndirectSchoolTenantQueryFilters
{
    internal static void Apply(SchoolDbContext context, ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Evaluation>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.ClassRoom.SchoolId == context.EffectiveTenantSchoolId)));

        modelBuilder.Entity<GradeEntry>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.Student.SchoolId == context.EffectiveTenantSchoolId)));

        modelBuilder.Entity<Enrollment>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.Student.SchoolId == context.EffectiveTenantSchoolId)));

        modelBuilder.Entity<CourseAssignment>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.ClassRoom.SchoolId == context.EffectiveTenantSchoolId)));

        modelBuilder.Entity<ScheduleSlot>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.CourseAssignment.ClassRoom.SchoolId == context.EffectiveTenantSchoolId)));

        modelBuilder.Entity<StudentGuardian>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.Student.SchoolId == context.EffectiveTenantSchoolId)));

        modelBuilder.Entity<StudentDocument>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.Student.SchoolId == context.EffectiveTenantSchoolId)));

        modelBuilder.Entity<EnrollmentPricingCategoryHistory>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.Enrollment.Student.SchoolId == context.EffectiveTenantSchoolId)));

        modelBuilder.Entity<StudentStatusHistory>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.Student.SchoolId == context.EffectiveTenantSchoolId)));

        modelBuilder.Entity<ReportCard>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.Student.SchoolId == context.EffectiveTenantSchoolId)));

        modelBuilder.Entity<ReportCardDetail>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.ReportCard.Student.SchoolId == context.EffectiveTenantSchoolId)));

        modelBuilder.Entity<PaymentLine>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.Payment.SchoolId == context.EffectiveTenantSchoolId)));

        modelBuilder.Entity<PaymentReversal>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.Payment.SchoolId == context.EffectiveTenantSchoolId)));

        modelBuilder.Entity<StudentFeeBalance>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.Student.SchoolId == context.EffectiveTenantSchoolId)));

        modelBuilder.Entity<RevenueAllocationKeyDetail>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.AllocationKey.SchoolId == context.EffectiveTenantSchoolId)));

        modelBuilder.Entity<NotificationRecipient>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.Notification.SchoolId == context.EffectiveTenantSchoolId)));

        modelBuilder.Entity<UserRoleAssignment>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.User.SchoolId == context.EffectiveTenantSchoolId)));

        modelBuilder.Entity<RolePermission>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.Role.SchoolId == context.EffectiveTenantSchoolId)));

        modelBuilder.Entity<RefreshToken>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.User.SchoolId == context.EffectiveTenantSchoolId)));

        modelBuilder.Entity<SyncOutboxItem>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.Unit.SchoolId == context.EffectiveTenantSchoolId)));

        modelBuilder.Entity<StudentRemedialCourse>().HasQueryFilter(e =>
            !e.IsDeleted && (context.IgnoreSchoolScope || (context.EffectiveTenantSchoolId != null
                && e.RemedialSession.SchoolId == context.EffectiveTenantSchoolId)));
    }
}
