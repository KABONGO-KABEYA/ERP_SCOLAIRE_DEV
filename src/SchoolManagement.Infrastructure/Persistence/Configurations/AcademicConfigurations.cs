using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class TeacherConfiguration : AuditableEntityConfiguration<Teacher>
{
    public override void Configure(EntityTypeBuilder<Teacher> builder)
    {
        base.Configure(builder);
        builder.ToTable("Teachers");
        builder.Property(t => t.EmployeeNumber).HasMaxLength(30).IsRequired();
        builder.Property(t => t.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(t => t.LastName).HasMaxLength(100).IsRequired();
        builder.HasIndex(t => new { t.SchoolId, t.EmployeeNumber }).IsUnique();
        builder.HasOne(t => t.ResidenceAddress).WithMany().HasForeignKey(t => t.AddressId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class CourseAssignmentConfiguration : AuditableEntityConfiguration<CourseAssignment>
{
    public override void Configure(EntityTypeBuilder<CourseAssignment> builder)
    {
        base.Configure(builder);
        builder.ToTable("CourseAssignments");
        builder.HasOne(a => a.Teacher).WithMany(t => t.CourseAssignments).HasForeignKey(a => a.TeacherId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(a => a.Course).WithMany().HasForeignKey(a => a.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.ClassRoom).WithMany().HasForeignKey(a => a.ClassRoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.AcademicYear).WithMany().HasForeignKey(a => a.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.PedagogicalClass).WithMany().HasForeignKey(a => a.PedagogicalClassId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(a => a.MaxScore).HasDefaultValue(20);
        builder.Property(a => a.WeeklyHours).HasDefaultValue(0);
        builder.HasIndex(a => new { a.ClassRoomId, a.AcademicYearId, a.CourseId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}

public class ScheduleSlotConfiguration : AuditableEntityConfiguration<ScheduleSlot>
{
    public override void Configure(EntityTypeBuilder<ScheduleSlot> builder)
    {
        base.Configure(builder);
        builder.ToTable("ScheduleSlots");
        builder.HasOne(s => s.CourseAssignment).WithMany().HasForeignKey(s => s.CourseAssignmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => new { s.CourseAssignmentId, s.DayOfWeek, s.StartTime });
    }
}

public class StudentAttendanceConfiguration : AuditableEntityConfiguration<StudentAttendance>
{
    public override void Configure(EntityTypeBuilder<StudentAttendance> builder)
    {
        base.Configure(builder);
        builder.ToTable("StudentAttendances");
        builder.Property(a => a.Presence).HasConversion<int>();
        builder.HasOne(a => a.Enrollment).WithMany(e => e.Attendances).HasForeignKey(a => a.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Student).WithMany().HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.ClassRoom).WithMany().HasForeignKey(a => a.ClassRoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.EnrollmentId, a.AttendanceDate });
        builder.HasIndex(a => new { a.SchoolId, a.StudentId, a.AttendanceDate });
        builder.HasIndex(a => new { a.ClassRoomId, a.AttendanceDate });
        builder.HasIndex(a => new { a.EnrollmentId, a.AttendanceDate, a.CourseAssignmentId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}

public class TeacherAttendanceConfiguration : AuditableEntityConfiguration<TeacherAttendance>
{
    public override void Configure(EntityTypeBuilder<TeacherAttendance> builder)
    {
        base.Configure(builder);
        builder.ToTable("TeacherAttendances");
        builder.HasOne(a => a.Teacher).WithMany().HasForeignKey(a => a.TeacherId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => new { a.SchoolId, a.TeacherId, a.AttendanceDate }).IsUnique();
    }
}

public class CalendarEventConfiguration : AuditableEntityConfiguration<CalendarEvent>
{
    public override void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        base.Configure(builder);
        builder.ToTable("CalendarEvents");
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.EventType).HasMaxLength(50).IsRequired();
        builder.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.AcademicYear).WithMany().HasForeignKey(e => e.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.SchoolId, e.StartDate });
    }
}

public class DisciplineRecordConfiguration : AuditableEntityConfiguration<DisciplineRecord>
{
    public override void Configure(EntityTypeBuilder<DisciplineRecord> builder)
    {
        base.Configure(builder);
        builder.ToTable("DisciplineRecords");
        builder.HasOne(d => d.Student).WithMany().HasForeignKey(d => d.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => new { d.SchoolId, d.StudentId, d.IncidentDate });
    }
}

public class MeritRecordConfiguration : AuditableEntityConfiguration<MeritRecord>
{
    public override void Configure(EntityTypeBuilder<MeritRecord> builder)
    {
        base.Configure(builder);
        builder.ToTable("MeritRecords");
        builder.HasOne(m => m.Student).WithMany().HasForeignKey(m => m.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.SchoolId, m.StudentId, m.AwardDate });
    }
}

public class AnnouncementConfiguration : AuditableEntityConfiguration<Announcement>
{
    public override void Configure(EntityTypeBuilder<Announcement> builder)
    {
        base.Configure(builder);
        builder.ToTable("Announcements");
        builder.Property(a => a.Title).HasMaxLength(200).IsRequired();
        builder.HasOne(a => a.School).WithMany().HasForeignKey(a => a.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.SchoolId, a.PublishedAt });
    }
}
