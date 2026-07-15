namespace SchoolManagement.Infrastructure.Persistence;



using Microsoft.EntityFrameworkCore;

using SchoolManagement.Application.Common.Interfaces;

using SchoolManagement.Domain.Entities.Academic;

using SchoolManagement.Domain.Entities.Finance;

using SchoolManagement.Domain.Entities.Grades;

using SchoolManagement.Domain.Entities.Security;

using SchoolManagement.Domain.Entities.Settings;

using SchoolManagement.Domain.Entities.Geography;

using SchoolManagement.Domain.Entities.Students;



public class SchoolDbContext : DbContext

{

    private readonly ICurrentUserService? _currentUser;



    public SchoolDbContext(DbContextOptions<SchoolDbContext> options) : base(options)

    {

    }



    public SchoolDbContext(DbContextOptions<SchoolDbContext> options, ICurrentUserService currentUser)

        : base(options)

    {

        _currentUser = currentUser;

    }



    // Paramétrage

    public DbSet<School> Schools => Set<School>();

    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();

    public DbSet<Section> Sections => Set<Section>();

    public DbSet<StudyOption> StudyOptions => Set<StudyOption>();

    public DbSet<PedagogicalClass> PedagogicalClasses => Set<PedagogicalClass>();

    public DbSet<ClassRoom> ClassRooms => Set<ClassRoom>();

    public DbSet<Course> Courses => Set<Course>();

    public DbSet<AcademicPeriod> AcademicPeriods => Set<AcademicPeriod>();

    public DbSet<FeeType> FeeTypes => Set<FeeType>();

    public DbSet<FeePricingCategory> FeePricingCategories => Set<FeePricingCategory>();

    public DbSet<FeeInstallment> FeeInstallments => Set<FeeInstallment>();

    public DbSet<FeeTypeInstallment> FeeTypeInstallments => Set<FeeTypeInstallment>();

    public DbSet<ClassFeeAmount> ClassFeeAmounts => Set<ClassFeeAmount>();

    public DbSet<Bank> Banks => Set<Bank>();

    public DbSet<CashRegister> CashRegisters => Set<CashRegister>();

    public DbSet<AppConfiguration> AppConfigurations => Set<AppConfiguration>();

    public DbSet<SchoolLogo> SchoolLogos => Set<SchoolLogo>();

    public DbSet<SchoolDocumentHeader> SchoolDocumentHeaders => Set<SchoolDocumentHeader>();

    public DbSet<SchoolSignature> SchoolSignatures => Set<SchoolSignature>();

    public DbSet<SchoolStamp> SchoolStamps => Set<SchoolStamp>();

    public DbSet<SchoolDocumentFooter> SchoolDocumentFooters => Set<SchoolDocumentFooter>();



    // Élèves

    public DbSet<Student> Students => Set<Student>();

    public DbSet<Guardian> Guardians => Set<Guardian>();

    public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();

    public DbSet<StudentDocument> StudentDocuments => Set<StudentDocument>();

    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    public DbSet<StudentStatusHistory> StudentStatusHistory => Set<StudentStatusHistory>();

    public DbSet<Country> Countries => Set<Country>();

    public DbSet<Province> Provinces => Set<Province>();

    public DbSet<City> Cities => Set<City>();

    public DbSet<Commune> Communes => Set<Commune>();

    public DbSet<PostalAddress> PostalAddresses => Set<PostalAddress>();



    // Académique

    public DbSet<Teacher> Teachers => Set<Teacher>();

    public DbSet<CourseAssignment> CourseAssignments => Set<CourseAssignment>();

    public DbSet<ScheduleSlot> ScheduleSlots => Set<ScheduleSlot>();

    public DbSet<StudentAttendance> StudentAttendances => Set<StudentAttendance>();

    public DbSet<TeacherAttendance> TeacherAttendances => Set<TeacherAttendance>();

    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();

    public DbSet<DisciplineRecord> DisciplineRecords => Set<DisciplineRecord>();

    public DbSet<MeritRecord> MeritRecords => Set<MeritRecord>();

    public DbSet<Announcement> Announcements => Set<Announcement>();



    // Notes

    public DbSet<Evaluation> Evaluations => Set<Evaluation>();

    public DbSet<GradeEntry> GradeEntries => Set<GradeEntry>();

    public DbSet<PeriodResult> PeriodResults => Set<PeriodResult>();

    public DbSet<ReportCard> ReportCards => Set<ReportCard>();

    public DbSet<ReportCardDetail> ReportCardDetails => Set<ReportCardDetail>();



    // Financier

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentLine> PaymentLines => Set<PaymentLine>();

    public DbSet<PaymentReversal> PaymentReversals => Set<PaymentReversal>();

    public DbSet<CashMovement> CashMovements => Set<CashMovement>();

    public DbSet<StudentFeeBalance> StudentFeeBalances => Set<StudentFeeBalance>();



    // Sécurité

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<LoginHistory> LoginHistory => Set<LoginHistory>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();



    protected override void OnModelCreating(ModelBuilder modelBuilder)

    {

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchoolDbContext).Assembly);

        base.OnModelCreating(modelBuilder);

    }



    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)

    {

        foreach (var entry in ChangeTracker.Entries<Domain.Common.AuditableEntity>())

        {

            switch (entry.State)

            {

                case EntityState.Added:

                    entry.Entity.CreatedAt = DateTime.UtcNow;

                    entry.Entity.CreatedBy = _currentUser?.UserId;

                    break;

                case EntityState.Modified:

                    entry.Entity.UpdatedAt = DateTime.UtcNow;

                    entry.Entity.UpdatedBy = _currentUser?.UserId;

                    break;

                case EntityState.Deleted:

                    entry.State = EntityState.Modified;

                    entry.Entity.IsDeleted = true;

                    entry.Entity.DeletedAt = DateTime.UtcNow;

                    entry.Entity.DeletedBy = _currentUser?.UserId;

                    break;

            }

        }



        return base.SaveChangesAsync(cancellationToken);

    }

}


