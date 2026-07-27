using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SchoolManagement.Application.CloudSync;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Geography;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Entities.Sync;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.CloudSync;

namespace SchoolManagement.Infrastructure.Persistence;

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

    /// <summary>Évite la réentrance outbox lors de l'écriture SyncOutbox*.</summary>
    public bool SuppressCloudSyncEnqueue { get; set; }

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
    public DbSet<EnrollmentPricingCategoryHistory> EnrollmentPricingCategoryHistory => Set<EnrollmentPricingCategoryHistory>();
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
    public DbSet<RevenueAllocationDestination> RevenueAllocationDestinations => Set<RevenueAllocationDestination>();
    public DbSet<RevenueAllocationKey> RevenueAllocationKeys => Set<RevenueAllocationKey>();
    public DbSet<RevenueAllocationKeyDetail> RevenueAllocationKeyDetails => Set<RevenueAllocationKeyDetail>();
    public DbSet<RevenueAllocationEntry> RevenueAllocationEntries => Set<RevenueAllocationEntry>();
    public DbSet<ExpenseRequest> ExpenseRequests => Set<ExpenseRequest>();
    public DbSet<ExpensePayment> ExpensePayments => Set<ExpensePayment>();
    public DbSet<WithholdingType> WithholdingTypes => Set<WithholdingType>();
    public DbSet<WithholdingConfiguration> WithholdingConfigurations => Set<WithholdingConfiguration>();
    public DbSet<WithholdingApplication> WithholdingApplications => Set<WithholdingApplication>();
    public DbSet<CurrencyDefinition> CurrencyDefinitions => Set<CurrencyDefinition>();
    public DbSet<SchoolCurrency> SchoolCurrencies => Set<SchoolCurrency>();
    public DbSet<ExchangeRateType> ExchangeRateTypes => Set<ExchangeRateType>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<ExchangeRateHistory> ExchangeRateHistories => Set<ExchangeRateHistory>();

    // Cartes élèves
    public DbSet<CardTemplate> CardTemplates => Set<CardTemplate>();
    public DbSet<CardSchoolSettings> CardSchoolSettings => Set<CardSchoolSettings>();
    public DbSet<StudentCard> StudentCards => Set<StudentCard>();
    public DbSet<StudentCardHistory> StudentCardHistories => Set<StudentCardHistory>();
    public DbSet<StudentCardPrintLog> StudentCardPrintLogs => Set<StudentCardPrintLog>();

    // Sécurité
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<LoginHistory> LoginHistory => Set<LoginHistory>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Sync cloud (outbox / journal) — tables locales uniquement
    public DbSet<SyncOutboxUnit> SyncOutboxUnits => Set<SyncOutboxUnit>();
    public DbSet<SyncOutboxItem> SyncOutboxItems => Set<SyncOutboxItem>();
    public DbSet<SyncJournalEntry> SyncJournalEntries => Set<SyncJournalEntry>();
    public DbSet<SyncWatermark> SyncWatermarks => Set<SyncWatermark>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchoolDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
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

        var cloudChanges = SuppressCloudSyncEnqueue
            ? []
            : CaptureCloudSyncChanges(ChangeTracker);

        var result = await base.SaveChangesAsync(cancellationToken);

        if (!SuppressCloudSyncEnqueue && cloudChanges.Count > 0)
        {
            try
            {
                await new CloudSyncOutboxWriter().EnqueueAsync(this, cloudChanges, cancellationToken);
            }
            catch
            {
                // Ne jamais faire échouer la transaction métier à cause de l'outbox.
            }
        }

        return result;
    }

    private static List<CloudSyncChange> CaptureCloudSyncChanges(ChangeTracker tracker)
    {
        var result = new List<CloudSyncChange>();
        foreach (var entry in tracker.Entries<AuditableEntity>())
        {
            if (entry.Entity is SyncOutboxUnit or SyncOutboxItem or SyncJournalEntry or SyncWatermark)
            {
                continue;
            }

            var clr = entry.Entity.GetType();
            if (clr.Namespace?.Contains("Proxies", StringComparison.Ordinal) == true)
            {
                clr = clr.BaseType ?? clr;
            }

            if (!CloudSyncCatalog.TryGetTableName(clr, out var tableName))
            {
                continue;
            }

            SyncOperationType? op = entry.State switch
            {
                EntityState.Added => SyncOperationType.Insert,
                EntityState.Modified => entry.Entity.IsDeleted
                    ? SyncOperationType.Delete
                    : SyncOperationType.Update,
                _ => null
            };

            if (op is null)
            {
                continue;
            }

            var (aggType, aggId) = CloudSyncCatalog.ResolveAggregate(tableName, entry.Entity.Id, entry.Entity);
            result.Add(new CloudSyncChange(
                tableName,
                entry.Entity.Id,
                op.Value,
                aggType,
                aggId,
                CloudSyncCatalog.ResolvePriority(tableName)));
        }

        return result;
    }
}
