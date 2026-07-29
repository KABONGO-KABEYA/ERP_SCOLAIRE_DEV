namespace SchoolManagement.Infrastructure.Seeding;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Auth.Interfaces;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Application.Schools.Catalog;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Shared.Constants;

public sealed class DatabaseSeeder
{
    private readonly SchoolDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly CurriculumSeeder _curriculumSeeder;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        SchoolDbContext context,
        IPasswordHasher passwordHasher,
        CurriculumSeeder curriculumSeeder,
        ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _curriculumSeeder = curriculumSeeder;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedPermissionsAsync(cancellationToken);
        await SeedAdminUserAsync(cancellationToken);
        await SeedParentDemoAsync(cancellationToken);
        await SeedKabeyaParentAsync(cancellationToken);
        await SeedDemoAcademicStructureAsync(cancellationToken);
        await SeedTeacherDemoAsync(cancellationToken);
        await SeedDirectionDemoAsync(cancellationToken);
    }

    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        foreach (var code in Permissions.All)
        {
            if (await _context.Permissions.AnyAsync(p => p.Code == code, cancellationToken))
            {
                continue;
            }

            var lastDot = code.LastIndexOf('.');
            var module = lastDot > 0 ? code[..lastDot] : code;
            var actionToken = lastDot > 0 ? code[(lastDot + 1)..] : "read";

            _context.Permissions.Add(new Permission
            {
                Code = code,
                Module = module,
                Action = ParsePermissionAction(actionToken),
                Description = code
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Permissions système initialisées.");
    }

    private static PermissionAction ParsePermissionAction(string actionToken) =>
        actionToken.Trim().ToLowerInvariant() switch
        {
            "read" => PermissionAction.Read,
            "create" => PermissionAction.Create,
            "update" => PermissionAction.Update,
            "delete" => PermissionAction.Delete,
            "export" => PermissionAction.Export,
            "approve" => PermissionAction.Approve,
            "print" => PermissionAction.Print,
            "renew" => PermissionAction.Renew,
            "declare-lost" => PermissionAction.Update,
            _ => throw new ArgumentException($"Action de permission inconnue : '{actionToken}'.")
        };

    private async Task SeedAdminUserAsync(CancellationToken cancellationToken)
    {
        var school = await _context.Schools.FirstOrDefaultAsync(cancellationToken);
        if (school is null)
        {
            _logger.LogWarning("Aucune école trouvée — exécutez 003_SeedData.sql d'abord.");
            return;
        }

        var adminRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.SchoolId == school.Id && r.Code == "ADMIN", cancellationToken);

        if (adminRole is null)
        {
            adminRole = new Role
            {
                SchoolId = school.Id,
                Name = "Administrateur",
                Code = "ADMIN",
                SystemRole = UserRole.Administrateur
            };
            _context.Roles.Add(adminRole);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var allPermissions = await _context.Permissions.ToListAsync(cancellationToken);
        foreach (var permission in allPermissions)
        {
            if (!await _context.RolePermissions.AnyAsync(rp => rp.RoleId == adminRole.Id && rp.PermissionId == permission.Id, cancellationToken))
            {
                _context.RolePermissions.Add(new RolePermission
                {
                    RoleId = adminRole.Id,
                    PermissionId = permission.Id
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (await _context.UserAccounts.AnyAsync(u => u.UserName == "admin", cancellationToken))
        {
            return;
        }

        var adminUser = new UserAccount
        {
            SchoolId = school.Id,
            UserName = "admin",
            Email = "admin@ecole.rdc",
            PasswordHash = _passwordHasher.Hash("Admin@2026"),
            FirstName = "Système",
            LastName = "Administrateur",
            IsActive = true,
            MustChangePassword = false
        };

        _context.UserAccounts.Add(adminUser);
        await _context.SaveChangesAsync(cancellationToken);

        _context.UserRoleAssignments.Add(new UserRoleAssignment
        {
            UserId = adminUser.Id,
            RoleId = adminRole.Id
        });

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Compte admin créé (admin / Admin@2026).");
    }

    private async Task SeedParentDemoAsync(CancellationToken cancellationToken)
    {
        if (await _context.UserAccounts.AnyAsync(u => u.UserName == "parent", cancellationToken))
        {
            return;
        }

        var school = await _context.Schools.FirstOrDefaultAsync(cancellationToken);
        if (school is null)
        {
            return;
        }

        var parentRole = await _context.Roles.FirstOrDefaultAsync(r => r.SchoolId == school.Id && r.Code == "PARENT", cancellationToken);
        if (parentRole is null)
        {
            return;
        }

        var guardian = await _context.Guardians
            .FirstOrDefaultAsync(g => g.SchoolId == school.Id && g.Email == "parent@ecole.rdc", cancellationToken);

        if (guardian is null)
        {
            guardian = new Guardian
            {
                SchoolId = school.Id,
                FirstName = "Jean",
                LastName = "Kabongo",
                Phone = "+243900000000",
                Email = "parent@ecole.rdc"
            };
            _context.Guardians.Add(guardian);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.SchoolId == school.Id && s.RegistrationNumber == "ELV-2026-001", cancellationToken);

        if (student is null)
        {
            student = new Student
            {
                SchoolId = school.Id,
                RegistrationNumber = "ELV-2026-001",
                FirstName = "Marie",
                LastName = "Kabongo",
                Gender = Gender.Feminin,
                DateOfBirth = new DateOnly(2014, 5, 12)
            };
            _context.Students.Add(student);
            await _context.SaveChangesAsync(cancellationToken);
        }

        if (!await _context.StudentGuardians.AnyAsync(
                sg => sg.StudentId == student.Id && sg.GuardianId == guardian.Id, cancellationToken))
        {
            _context.StudentGuardians.Add(new StudentGuardian
            {
                StudentId = student.Id,
                GuardianId = guardian.Id,
                Relationship = "Père",
                IsPrimary = true
            });
            await _context.SaveChangesAsync(cancellationToken);
        }

        if (await _context.UserAccounts.AnyAsync(u => u.UserName == "parent", cancellationToken))
        {
            return;
        }

        var parentUser = new UserAccount
        {
            SchoolId = school.Id,
            UserName = "parent",
            Email = "parent@ecole.rdc",
            PasswordHash = _passwordHasher.Hash("Parent@2026"),
            FirstName = "Jean",
            LastName = "Kabongo",
            GuardianId = guardian.Id,
            IsActive = true
        };
        _context.UserAccounts.Add(parentUser);
        await _context.SaveChangesAsync(cancellationToken);

        if (!await _context.UserRoleAssignments.AnyAsync(
                ur => ur.UserId == parentUser.Id && ur.RoleId == parentRole.Id, cancellationToken))
        {
            _context.UserRoleAssignments.Add(new UserRoleAssignment
            {
                UserId = parentUser.Id,
                RoleId = parentRole.Id
            });
        }

        var readPermissions = await _context.Permissions
            .Where(p => p.Code == Permissions.PaymentsRead || p.Code == Permissions.GradesRead || p.Code == Permissions.ReportsRead)
            .ToListAsync(cancellationToken);

        foreach (var permission in readPermissions)
        {
            if (!await _context.RolePermissions.AnyAsync(rp => rp.RoleId == parentRole.Id && rp.PermissionId == permission.Id, cancellationToken))
            {
                _context.RolePermissions.Add(new RolePermission { RoleId = parentRole.Id, PermissionId = permission.Id });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Compte parent démo créé (parent / Parent@2026).");
    }

    /// <summary>
    /// Compte mobile pour le tuteur réel KABEYA VISA (enfants déjà inscrits en base).
    /// </summary>
    private async Task SeedKabeyaParentAsync(CancellationToken cancellationToken)
    {
        const string userName = "parent.kabeya";
        if (await _context.UserAccounts.AnyAsync(u => u.UserName == userName, cancellationToken))
        {
            return;
        }

        var school = await _context.Schools.FirstOrDefaultAsync(cancellationToken);
        if (school is null)
        {
            return;
        }

        var parentRole = await _context.Roles.FirstOrDefaultAsync(
            r => r.SchoolId == school.Id && r.Code == "PARENT", cancellationToken);
        if (parentRole is null)
        {
            return;
        }

        var guardians = await _context.Guardians
            .Where(g => g.SchoolId == school.Id &&
                        (g.LastName.Contains("KABEYA") || g.FirstName.Contains("KABEYA")))
            .ToListAsync(cancellationToken);

        if (guardians.Count == 0)
        {
            _logger.LogWarning("Aucun tuteur KABEYA trouvé — compte parent.kabeya non créé.");
            return;
        }

        var guardianIds = guardians.Select(g => g.Id).ToList();
        var linkedGuardianIds = (await _context.StudentGuardians
                .Where(sg => guardianIds.Contains(sg.GuardianId))
                .Select(sg => sg.GuardianId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var guardian = guardians
            .OrderByDescending(g => linkedGuardianIds.Contains(g.Id))
            .ThenByDescending(g =>
                g.LastName.Equals("KABEYA", StringComparison.OrdinalIgnoreCase)
                && g.FirstName.Equals("VISA", StringComparison.OrdinalIgnoreCase))
            .First();

        var childCount = await _context.StudentGuardians
            .CountAsync(sg => sg.GuardianId == guardian.Id, cancellationToken);

        var parentUser = new UserAccount
        {
            SchoolId = school.Id,
            UserName = userName,
            Email = guardian.Email ?? $"{userName}@ecole.rdc",
            PasswordHash = _passwordHasher.Hash("Parent@2026"),
            FirstName = guardian.FirstName,
            LastName = guardian.LastName,
            Phone = guardian.Phone,
            IsActive = true,
            GuardianId = guardian.Id
        };
        _context.UserAccounts.Add(parentUser);
        await _context.SaveChangesAsync(cancellationToken);

        if (!await _context.UserRoleAssignments.AnyAsync(
                ur => ur.UserId == parentUser.Id && ur.RoleId == parentRole.Id, cancellationToken))
        {
            _context.UserRoleAssignments.Add(new UserRoleAssignment
            {
                UserId = parentUser.Id,
                RoleId = parentRole.Id
            });
        }

        var readPermissions = await _context.Permissions
            .Where(p => p.Code == Permissions.PaymentsRead
                        || p.Code == Permissions.GradesRead
                        || p.Code == Permissions.ReportsRead
                        || p.Code == Permissions.StudentsRead)
            .ToListAsync(cancellationToken);

        foreach (var permission in readPermissions)
        {
            if (!await _context.RolePermissions.AnyAsync(
                    rp => rp.RoleId == parentRole.Id && rp.PermissionId == permission.Id, cancellationToken))
            {
                _context.RolePermissions.Add(new RolePermission
                {
                    RoleId = parentRole.Id,
                    PermissionId = permission.Id
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Compte parent KABEYA créé ({UserName} / Parent@2026) — tuteur {LastName} {FirstName}, {ChildCount} enfant(s).",
            userName,
            guardian.LastName,
            guardian.FirstName,
            childCount);
    }

    private async Task SeedDemoAcademicStructureAsync(CancellationToken cancellationToken)
    {
        var school = await _context.Schools.FirstOrDefaultAsync(cancellationToken);
        if (school is null)
        {
            return;
        }

        await EnsurePedagogicalStructureAsync(school.Id, cancellationToken);

        var year = await _context.AcademicYears
            .FirstOrDefaultAsync(y => y.SchoolId == school.Id && y.IsCurrent, cancellationToken)
            ?? await _context.AcademicYears.FirstOrDefaultAsync(y => y.SchoolId == school.Id, cancellationToken);

        if (year is null)
        {
            return;
        }

        var pri6 = await _context.PedagogicalClasses
            .FirstOrDefaultAsync(p => p.SchoolId == school.Id && p.TemplateCode == "PRI-6", cancellationToken);

        var section = await _context.Sections.FirstOrDefaultAsync(s => s.SchoolId == school.Id && s.Code == "PRI", cancellationToken)
            ?? await _context.Sections.FirstOrDefaultAsync(s => s.SchoolId == school.Id, cancellationToken);

        if (section is null || pri6 is null)
        {
            return;
        }

        if (!pri6.IsEnabled)
        {
            pri6.IsEnabled = true;
            await _context.SaveChangesAsync(cancellationToken);
        }

        var classRoom = await _context.ClassRooms
            .FirstOrDefaultAsync(c => c.SchoolId == school.Id && c.Code == "6A-PRIM", cancellationToken);

        if (classRoom is null)
        {
            classRoom = new ClassRoom
            {
                SchoolId = school.Id,
                AcademicYearId = year.Id,
                PedagogicalClassId = pri6.Id,
                SectionId = section.Id,
                Code = "PRI-6-A",
                Name = "A",
                Level = 6,
                MaxCapacity = 40,
                IsActive = true
            };
            _context.ClassRooms.Add(classRoom);
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            classRoom.PedagogicalClassId ??= pri6.Id;
            classRoom.Name = "A";
            classRoom.IsActive = true;
            await _context.SaveChangesAsync(cancellationToken);
        }

        if (!await _context.Courses.AnyAsync(c => c.Code == "MATH-6A", cancellationToken))
        {
            var mathCourse = new Course
            {
                Code = "MATH-6A",
                Name = "Mathématiques",
                Coefficient = 4,
                MaxScore = 20
            };
            _context.Courses.Add(mathCourse);
            await _context.SaveChangesAsync(cancellationToken);

            if (pri6 is not null)
            {
                _context.PedagogicalClassCourses.Add(new PedagogicalClassCourse
                {
                    SchoolId = school.Id,
                    PedagogicalClassId = pri6.Id,
                    CourseId = mathCourse.Id,
                    MaxScore = 20
                });
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.SchoolId == school.Id && s.RegistrationNumber == "ELV-2026-001", cancellationToken);

        if (student is not null && !await _context.Enrollments.AnyAsync(
                e => e.StudentId == student.Id && e.AcademicYearId == year.Id && e.IsActive, cancellationToken))
        {
            var generalCategory = await EnsureGeneralPricingCategoryAsync(school.Id, cancellationToken);
            _context.Enrollments.Add(new Enrollment
            {
                StudentId = student.Id,
                AcademicYearId = year.Id,
                ClassRoomId = classRoom.Id,
                FeePricingCategoryId = generalCategory.Id,
                EnrollmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Status = EnrollmentStatus.Inscrit,
                IsActive = true
            });
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Inscription démo créée pour {Matricule} en {Classe}.", student.RegistrationNumber, classRoom.Name);
        }
    }

    private async Task<FeePricingCategory> EnsureGeneralPricingCategoryAsync(
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        var categories = await _context.FeePricingCategories
            .Where(c => c.SchoolId == schoolId)
            .ToListAsync(cancellationToken);
        var general = categories.FirstOrDefault(c =>
            string.Equals(c.Code, FeePricingCategoryCodes.General, StringComparison.OrdinalIgnoreCase));

        if (general is not null)
        {
            if (!general.IsActive)
            {
                general.IsActive = true;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return general;
        }

        general = new FeePricingCategory
        {
            SchoolId = schoolId,
            Code = FeePricingCategoryCodes.General,
            Name = "Générale",
            Description = "Catégorie tarifaire par défaut (inscription)",
            IsActive = true
        };
        _context.FeePricingCategories.Add(general);
        await _context.SaveChangesAsync(cancellationToken);
        return general;
    }

    private async Task EnsurePedagogicalStructureAsync(Guid schoolId, CancellationToken cancellationToken)
    {
        var requiredSections = new (string Code, string Name, EducationCycle Cycle)[]
        {
            ("MAT", "Maternelle", EducationCycle.Primaire),
            ("PRI", "Primaire", EducationCycle.Primaire),
            ("CTEB", "Éducation de base — CTEB", EducationCycle.Secondaire),
            ("HUM", "Humanités", EducationCycle.Secondaire),
            ("HPRO", "Humanités professionnelles", EducationCycle.Secondaire),
            ("FS", "Filières spécialisées", EducationCycle.Secondaire)
        };

        foreach (var (code, name, cycle) in requiredSections)
        {
            if (!await _context.Sections.AnyAsync(s => s.SchoolId == schoolId && s.Code == code, cancellationToken))
            {
                _context.Sections.Add(new Section
                {
                    SchoolId = schoolId,
                    Code = code,
                    Name = name,
                    Cycle = cycle
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        var existing = await _context.PedagogicalClasses
            .Where(p => p.SchoolId == schoolId)
            .ToListAsync(cancellationToken);
        var existingByCode = existing.ToDictionary(p => p.TemplateCode, StringComparer.OrdinalIgnoreCase);
        var catalogCodes = RdcPedagogicalCatalog.GetAll()
            .Select(t => t.TemplateCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var template in RdcPedagogicalCatalog.GetAll())
        {
            if (existingByCode.ContainsKey(template.TemplateCode))
            {
                continue;
            }

            _context.PedagogicalClasses.Add(new PedagogicalClass
            {
                SchoolId = schoolId,
                TemplateCode = template.TemplateCode,
                Program = template.Program,
                LevelOrder = template.LevelOrder,
                DisplayName = template.DisplayName,
                HumanitiesSection = template.HumanitiesSection,
                StudyOption = template.StudyOption,
                MinAge = template.MinAge,
                MaxAge = template.MaxAge,
                IsEnabled = false
            });
        }

        foreach (var pedagogicalClass in existing)
        {
            if (!catalogCodes.Contains(pedagogicalClass.TemplateCode))
            {
                pedagogicalClass.IsEnabled = false;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Structure pédagogique RDC synchronisée pour l'école {SchoolId} ({Count} classes).",
            schoolId,
            RdcPedagogicalCatalog.GetAll().Count);

        await _curriculumSeeder.EnsureCurriculumAsync(schoolId, cancellationToken);
    }

    private async Task SeedTeacherDemoAsync(CancellationToken cancellationToken)
    {
        if (await _context.UserAccounts.AnyAsync(u => u.UserName == "enseignant", cancellationToken))
        {
            return;
        }

        var school = await _context.Schools.FirstOrDefaultAsync(cancellationToken);
        if (school is null)
        {
            return;
        }

        var teacherRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.SchoolId == school.Id && r.Code == "ENSEIGNANT", cancellationToken);
        if (teacherRole is null)
        {
            return;
        }

        var teacher = await _context.Teachers
            .FirstOrDefaultAsync(t => t.SchoolId == school.Id && t.EmployeeNumber == "ENS-2026-001", cancellationToken);

        if (teacher is null)
        {
            teacher = new Teacher
            {
                SchoolId = school.Id,
                EmployeeNumber = "ENS-2026-001",
                FirstName = "Paul",
                LastName = "Mukendi",
                Email = "enseignant@ecole.rdc",
                Specialization = "Mathématiques",
                HireDate = new DateOnly(2020, 9, 1),
                IsActive = true
            };
            _context.Teachers.Add(teacher);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var year = await _context.AcademicYears
            .FirstOrDefaultAsync(y => y.SchoolId == school.Id && y.IsCurrent, cancellationToken)
            ?? await _context.AcademicYears.FirstOrDefaultAsync(y => y.SchoolId == school.Id, cancellationToken);

        var classRoom = await _context.ClassRooms
            .FirstOrDefaultAsync(c => c.SchoolId == school.Id && c.Code == "6A-PRIM", cancellationToken);

        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Code == "MATH-6A", cancellationToken);

        if (year is not null && classRoom is not null && course is not null && classRoom.PedagogicalClassId.HasValue
            && !await _context.CourseAssignments.AnyAsync(
                a => a.TeacherId == teacher.Id && a.CourseId == course.Id && a.ClassRoomId == classRoom.Id,
                cancellationToken))
        {
            _context.CourseAssignments.Add(new CourseAssignment
            {
                TeacherId = teacher.Id,
                CourseId = course.Id,
                ClassRoomId = classRoom.Id,
                AcademicYearId = year.Id,
                PedagogicalClassId = classRoom.PedagogicalClassId.Value,
                IsActive = true,
                MaxScore = 20
            });
            await _context.SaveChangesAsync(cancellationToken);
        }

        if (await _context.UserAccounts.AnyAsync(u => u.UserName == "enseignant", cancellationToken))
        {
            return;
        }

        var teacherUser = new UserAccount
        {
            SchoolId = school.Id,
            UserName = "enseignant",
            Email = "enseignant@ecole.rdc",
            PasswordHash = _passwordHasher.Hash("Teacher@2026"),
            FirstName = "Paul",
            LastName = "Mukendi",
            TeacherId = teacher.Id,
            IsActive = true
        };
        _context.UserAccounts.Add(teacherUser);
        await _context.SaveChangesAsync(cancellationToken);

        if (!await _context.UserRoleAssignments.AnyAsync(
                ur => ur.UserId == teacherUser.Id && ur.RoleId == teacherRole.Id, cancellationToken))
        {
            _context.UserRoleAssignments.Add(new UserRoleAssignment
            {
                UserId = teacherUser.Id,
                RoleId = teacherRole.Id
            });
        }

        var gradePermissions = await _context.Permissions
            .Where(p => p.Code == Permissions.GradesRead || p.Code == Permissions.GradesCreate || p.Code == Permissions.GradesUpdate)
            .ToListAsync(cancellationToken);

        foreach (var permission in gradePermissions)
        {
            if (!await _context.RolePermissions.AnyAsync(rp => rp.RoleId == teacherRole.Id && rp.PermissionId == permission.Id, cancellationToken))
            {
                _context.RolePermissions.Add(new RolePermission { RoleId = teacherRole.Id, PermissionId = permission.Id });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Compte enseignant démo créé (enseignant / Teacher@2026).");
    }

    private async Task SeedDirectionDemoAsync(CancellationToken cancellationToken)
    {
        if (await _context.UserAccounts.AnyAsync(u => u.UserName == "direction", cancellationToken))
        {
            return;
        }

        var school = await _context.Schools.FirstOrDefaultAsync(cancellationToken);
        if (school is null)
        {
            return;
        }

        var directionRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.SchoolId == school.Id && r.Code == "DIRECTION", cancellationToken);
        if (directionRole is null)
        {
            return;
        }

        var directionUser = new UserAccount
        {
            SchoolId = school.Id,
            UserName = "direction",
            Email = "direction@ecole.rdc",
            PasswordHash = _passwordHasher.Hash("Direction@2026"),
            FirstName = "Marie",
            LastName = "Tshisekedi",
            IsActive = true
        };
        _context.UserAccounts.Add(directionUser);
        await _context.SaveChangesAsync(cancellationToken);

        if (!await _context.UserRoleAssignments.AnyAsync(
                ur => ur.UserId == directionUser.Id && ur.RoleId == directionRole.Id, cancellationToken))
        {
            _context.UserRoleAssignments.Add(new UserRoleAssignment
            {
                UserId = directionUser.Id,
                RoleId = directionRole.Id
            });
        }

        var directionPermissions = await _context.Permissions
            .Where(p => p.Code == Permissions.ReportsRead
                || p.Code == Permissions.PaymentsRead
                || p.Code == Permissions.GradesRead
                || p.Code == Permissions.StudentsRead
                || p.Code == Permissions.SchoolsRead)
            .ToListAsync(cancellationToken);

        foreach (var permission in directionPermissions)
        {
            if (!await _context.RolePermissions.AnyAsync(rp => rp.RoleId == directionRole.Id && rp.PermissionId == permission.Id, cancellationToken))
            {
                _context.RolePermissions.Add(new RolePermission { RoleId = directionRole.Id, PermissionId = permission.Id });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Compte direction démo créé (direction / Direction@2026).");
    }
}
