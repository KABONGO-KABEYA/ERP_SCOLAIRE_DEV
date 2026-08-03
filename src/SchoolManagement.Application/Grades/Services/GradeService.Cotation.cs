namespace SchoolManagement.Application.Grades.Services;

using SchoolManagement.Application.Common;
using SchoolManagement.Application.Grades.DTOs;
using SchoolManagement.Application.Schools;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Shared.Constants;

public sealed partial class GradeService
{
    public async Task<CotationSessionDto> OpenCotationSessionAsync(
        Guid schoolId,
        OpenCotationSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeNumber))
        {
            throw new DomainException("Le matricule / identifiant enseignant est obligatoire.");
        }

        EnsureCanEnterGrades();

        var year = await SchoolConfigurationGuards.EnsureActiveAcademicYearAsync(
            _yearRepository,
            schoolId,
            request.AcademicYearId,
            cancellationToken);

        var accessScope = ResolveCotationAccessScope();
        var currentUser = await GetCurrentUserAccountAsync(schoolId, cancellationToken);
        var teacher = await ResolveSessionTeacherAsync(
            schoolId,
            request.EmployeeNumber.Trim(),
            accessScope,
            currentUser,
            cancellationToken);

        if (!teacher.IsActive)
        {
            throw new DomainException("Cet enseignant est inactif. Cotation impossible.");
        }

        var passwordValidated = await ValidateTeacherPasswordIfRequiredAsync(
            schoolId,
            teacher.Id,
            request.Password,
            accessScope,
            currentUser,
            cancellationToken);

        var pedagogicalMap = await SchoolConfigurationGuards.BuildPedagogicalMapAsync(
            _pedagogicalClassRepository,
            schoolId,
            cancellationToken);

        var allAssignments = (await _courseAssignmentRepository.FindAsync(
            a => a.AcademicYearId == year.Id && a.IsActive,
            cancellationToken)).ToList();

        var scopedAssignments = FilterAssignmentsByScope(allAssignments, teacher.Id, accessScope);

        var classIds = scopedAssignments.Select(a => a.ClassRoomId).Distinct().ToList();
        var courseIds = scopedAssignments.Select(a => a.CourseId).Distinct().ToList();
        var assignmentTeacherIds = scopedAssignments
            .Where(a => a.TeacherId.HasValue)
            .Select(a => a.TeacherId!.Value)
            .Distinct()
            .ToList();

        var classRooms = (await _classRoomRepository.FindAsync(
            c => c.SchoolId == schoolId && classIds.Contains(c.Id),
            cancellationToken))
            .Where(c => ClassRoomAvailability.IsSelectable(c, pedagogicalMap))
            .ToList();

        var sectionIds = classRooms.Select(c => c.SectionId).Distinct().ToList();
        var sections = (await _sectionRepository.FindAsync(
            s => s.SchoolId == schoolId && sectionIds.Contains(s.Id),
            cancellationToken)).ToDictionary(s => s.Id);

        var courses = (await _courseRepository.FindAsync(
            c => courseIds.Contains(c.Id),
            cancellationToken)).ToDictionary(c => c.Id);

        var teachersById = (await _teacherRepository.FindAsync(
            t => t.SchoolId == schoolId && assignmentTeacherIds.Contains(t.Id),
            cancellationToken)).ToDictionary(t => t.Id);

        var selectableClassIds = classRooms.Select(c => c.Id).ToHashSet();
        scopedAssignments = scopedAssignments
            .Where(a => selectableClassIds.Contains(a.ClassRoomId) && courses.ContainsKey(a.CourseId))
            .ToList();

        if (scopedAssignments.Count == 0)
        {
            throw new DomainException(
                "Aucune affectation de cours active pour cet enseignant sur l'année sélectionnée.");
        }

        var classDtos = classRooms
            .OrderBy(c => pedagogicalMap.GetValueOrDefault(c.PedagogicalClassId ?? Guid.Empty)?.DisplayName)
            .ThenBy(c => c.Name)
            .Select(c =>
            {
                pedagogicalMap.TryGetValue(c.PedagogicalClassId ?? Guid.Empty, out var ped);
                sections.TryGetValue(c.SectionId, out var section);
                var program = ped?.Program;
                var periodType = ResolvePeriodType(program, section?.Cycle);
                return new CotationClassDto(
                    c.Id,
                    ped is null ? c.Name : $"{ped.DisplayName} {c.Name}".Trim(),
                    c.PedagogicalClassId,
                    ped?.DisplayName,
                    c.SectionId,
                    section?.Name ?? "—",
                    section?.Cycle ?? EducationCycle.Primaire,
                    program,
                    periodType);
            })
            .ToList();

        var sessionTeacherName = StudentDisplayName.Format(teacher.LastName, null, teacher.FirstName);
        var classDtoById = classDtos.ToDictionary(c => c.ClassRoomId);

        var enrollmentCounts = (await _enrollmentRepository.FindAsync(
                e => e.AcademicYearId == year.Id
                     && selectableClassIds.Contains(e.ClassRoomId)
                     && e.IsActive,
                cancellationToken))
            .GroupBy(e => e.ClassRoomId)
            .ToDictionary(g => g.Key, g => g.Count());

        var assignmentDtos = await BuildCotationAssignmentDtosAsync(
            year.Id,
            teacher.Id,
            sessionTeacherName,
            scopedAssignments,
            classDtoById,
            courses,
            teachersById,
            enrollmentCounts,
            cancellationToken);

        var evaluationTypes = await GetEvaluationTypesAsync(schoolId, cancellationToken);

        return new CotationSessionDto(
            teacher.Id,
            teacher.EmployeeNumber,
            sessionTeacherName,
            accessScope,
            year.Id,
            year.Label,
            passwordValidated,
            classDtos,
            assignmentDtos,
            evaluationTypes);
    }

    public async Task<IReadOnlyList<CotationAssignmentDto>> GetCotationAssignmentsAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid teacherId,
        CancellationToken cancellationToken = default)
    {
        EnsureCanEnterGrades();

        var year = await SchoolConfigurationGuards.EnsureActiveAcademicYearAsync(
            _yearRepository,
            schoolId,
            academicYearId,
            cancellationToken);

        var accessScope = ResolveCotationAccessScope();
        var teacher = (await _teacherRepository.FindAsync(
            t => t.Id == teacherId && t.SchoolId == schoolId,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Enseignant introuvable.");

        var pedagogicalMap = await SchoolConfigurationGuards.BuildPedagogicalMapAsync(
            _pedagogicalClassRepository,
            schoolId,
            cancellationToken);

        var allAssignments = (await _courseAssignmentRepository.FindAsync(
            a => a.AcademicYearId == year.Id && a.IsActive,
            cancellationToken)).ToList();

        var scopedAssignments = FilterAssignmentsByScope(allAssignments, teacher.Id, accessScope);

        var classIds = scopedAssignments.Select(a => a.ClassRoomId).Distinct().ToList();
        var courseIds = scopedAssignments.Select(a => a.CourseId).Distinct().ToList();
        var assignmentTeacherIds = scopedAssignments
            .Where(a => a.TeacherId.HasValue)
            .Select(a => a.TeacherId!.Value)
            .Distinct()
            .ToList();

        var classRooms = (await _classRoomRepository.FindAsync(
            c => c.SchoolId == schoolId && classIds.Contains(c.Id),
            cancellationToken))
            .Where(c => ClassRoomAvailability.IsSelectable(c, pedagogicalMap))
            .ToList();

        var sectionIds = classRooms.Select(c => c.SectionId).Distinct().ToList();
        var sections = (await _sectionRepository.FindAsync(
            s => s.SchoolId == schoolId && sectionIds.Contains(s.Id),
            cancellationToken)).ToDictionary(s => s.Id);

        var courses = (await _courseRepository.FindAsync(
            c => courseIds.Contains(c.Id),
            cancellationToken)).ToDictionary(c => c.Id);

        var teachersById = (await _teacherRepository.FindAsync(
            t => t.SchoolId == schoolId && assignmentTeacherIds.Contains(t.Id),
            cancellationToken)).ToDictionary(t => t.Id);

        var selectableClassIds = classRooms.Select(c => c.Id).ToHashSet();
        scopedAssignments = scopedAssignments
            .Where(a => selectableClassIds.Contains(a.ClassRoomId) && courses.ContainsKey(a.CourseId))
            .ToList();

        var classDtoById = classRooms.ToDictionary(
            c => c.Id,
            c =>
            {
                pedagogicalMap.TryGetValue(c.PedagogicalClassId ?? Guid.Empty, out var ped);
                sections.TryGetValue(c.SectionId, out var section);
                var program = ped?.Program;
                var periodType = ResolvePeriodType(program, section?.Cycle);
                return new CotationClassDto(
                    c.Id,
                    ped is null ? c.Name : $"{ped.DisplayName} {c.Name}".Trim(),
                    c.PedagogicalClassId,
                    ped?.DisplayName,
                    c.SectionId,
                    section?.Name ?? "—",
                    section?.Cycle ?? EducationCycle.Primaire,
                    program,
                    periodType);
            });

        var enrollmentCounts = (await _enrollmentRepository.FindAsync(
                e => e.AcademicYearId == year.Id
                     && selectableClassIds.Contains(e.ClassRoomId)
                     && e.IsActive,
                cancellationToken))
            .GroupBy(e => e.ClassRoomId)
            .ToDictionary(g => g.Key, g => g.Count());

        var sessionTeacherName = StudentDisplayName.Format(teacher.LastName, null, teacher.FirstName);
        return await BuildCotationAssignmentDtosAsync(
            year.Id,
            teacher.Id,
            sessionTeacherName,
            scopedAssignments,
            classDtoById,
            courses,
            teachersById,
            enrollmentCounts,
            cancellationToken);
    }

    private async Task<IReadOnlyList<CotationAssignmentDto>> BuildCotationAssignmentDtosAsync(
        Guid academicYearId,
        Guid sessionTeacherId,
        string sessionTeacherName,
        IReadOnlyList<CourseAssignment> scopedAssignments,
        IReadOnlyDictionary<Guid, CotationClassDto> classDtoById,
        IReadOnlyDictionary<Guid, Course> courses,
        IReadOnlyDictionary<Guid, Teacher> teachersById,
        IReadOnlyDictionary<Guid, int> enrollmentCounts,
        CancellationToken cancellationToken)
    {
        var yearPeriods = (await _periodRepository.FindAsync(
            p => p.AcademicYearId == academicYearId,
            cancellationToken)).ToList();

        var openPeriodByType = new Dictionary<AcademicPeriodType, AcademicPeriod>();
        foreach (var periodType in classDtoById.Values.Select(c => c.PeriodType).Distinct())
        {
            var open = ResolveActiveCotationPeriod(yearPeriods, periodType);
            if (open is not null)
            {
                openPeriodByType[periodType] = open;
            }
        }

        var openPeriodIds = openPeriodByType.Values.Select(p => p.Id).ToHashSet();
        var classRoomIds = scopedAssignments.Select(a => a.ClassRoomId).Distinct().ToList();
        var progressEvaluations = openPeriodIds.Count == 0 || classRoomIds.Count == 0
            ? []
            : (await _evaluationRepository.FindAsync(
                e => openPeriodIds.Contains(e.AcademicPeriodId)
                     && classRoomIds.Contains(e.ClassRoomId),
                cancellationToken)).ToList();

        var evalByClassCourse = progressEvaluations
            .GroupBy(e => (e.ClassRoomId, e.CourseId))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(e => e.EvaluationDate)
                    .ThenByDescending(e => e.CreatedAt)
                    .ToList());

        return scopedAssignments
            .Select(a =>
            {
                var assignedTeacherId = a.TeacherId ?? sessionTeacherId;
                teachersById.TryGetValue(assignedTeacherId, out var assignmentTeacher);
                var teacherName = assignmentTeacher is null
                    ? sessionTeacherName
                    : StudentDisplayName.Format(assignmentTeacher.LastName, null, assignmentTeacher.FirstName);
                classDtoById.TryGetValue(a.ClassRoomId, out var classDto);
                enrollmentCounts.TryGetValue(a.ClassRoomId, out var studentCount);

                var hasOpenPeriod = classDto is not null
                                    && openPeriodByType.ContainsKey(classDto.PeriodType);
                var evaluationCount = 0;
                string? lastTitle = null;
                DateOnly? lastDate = null;
                if (hasOpenPeriod
                    && evalByClassCourse.TryGetValue((a.ClassRoomId, a.CourseId), out var list)
                    && list.Count > 0)
                {
                    evaluationCount = list.Count;
                    lastTitle = list[0].Title;
                    lastDate = list[0].EvaluationDate;
                }

                return new CotationAssignmentDto(
                    a.Id,
                    a.ClassRoomId,
                    classDto?.DisplayName ?? "—",
                    classDto?.SectionName ?? "—",
                    a.CourseId,
                    courses[a.CourseId].Name,
                    assignedTeacherId,
                    teacherName,
                    a.MaxScore <= 0 ? 20 : a.MaxScore,
                    a.WeeklyHours,
                    studentCount,
                    evaluationCount,
                    lastTitle,
                    lastDate,
                    hasOpenPeriod);
            })
            .OrderBy(a => a.ClassDisplayName)
            .ThenBy(a => a.CourseName)
            .ToList();
    }

    /// <summary>Aligné sur GetCotationPeriodsAsync : sous-période ouverte du cycle, sinon null.</summary>
    private static AcademicPeriod? ResolveActiveCotationPeriod(
        IReadOnlyList<AcademicPeriod> periods,
        AcademicPeriodType expectedType)
    {
        var structuredOpen = periods
            .Where(p => p.MainPeriodId.HasValue
                        && p.Status == AcademicSubPeriodStatus.Ouverte
                        && MatchesPeriodType(p, expectedType))
            .OrderBy(p => p.OrderIndex)
            .FirstOrDefault();
        if (structuredOpen is not null)
        {
            return structuredOpen;
        }

        if (periods.Any(p => p.MainPeriodId.HasValue && MatchesPeriodType(p, expectedType)))
        {
            return null;
        }

        var filtered = periods
            .Where(p => MatchesPeriodType(p, expectedType))
            .OrderBy(p => p.OrderIndex)
            .ThenBy(p => p.Name)
            .ToList();
        if (filtered.Count == 0)
        {
            filtered = periods
                .OrderBy(p => p.OrderIndex)
                .ThenBy(p => p.Name)
                .ToList();
        }

        return filtered.FirstOrDefault(p =>
            !p.IsClosed
            && p.Status is not AcademicSubPeriodStatus.Cloturee
            && p.Status is not AcademicSubPeriodStatus.Verrouillee);
    }

    public async Task<IReadOnlyList<CotationPeriodDto>> GetCotationPeriodsAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        CancellationToken cancellationToken = default)
    {
        var year = await SchoolConfigurationGuards.EnsureActiveAcademicYearAsync(
            _yearRepository,
            schoolId,
            academicYearId,
            cancellationToken);

        var classRoom = await SchoolConfigurationGuards.EnsureSelectableClassRoomAsync(
            _classRoomRepository,
            _pedagogicalClassRepository,
            _yearRepository,
            schoolId,
            classRoomId,
            cancellationToken);

        PedagogicalClass? ped = null;
        if (classRoom.PedagogicalClassId.HasValue)
        {
            ped = (await _pedagogicalClassRepository.FindAsync(
                p => p.Id == classRoom.PedagogicalClassId.Value && p.SchoolId == schoolId,
                cancellationToken)).FirstOrDefault();
        }

        var section = (await _sectionRepository.FindAsync(
            s => s.Id == classRoom.SectionId && s.SchoolId == schoolId,
            cancellationToken)).FirstOrDefault();

        var expectedType = ResolvePeriodType(ped?.Program, section?.Cycle);

        var periods = await _periodRepository.FindAsync(
            p => p.AcademicYearId == year.Id,
            cancellationToken);

        // Moteur pédagogique : uniquement la sous-période OUVERTE du cycle.
        var structuredOpen = periods
            .Where(p => p.MainPeriodId.HasValue
                        && p.Status == AcademicSubPeriodStatus.Ouverte
                        && MatchesPeriodType(p, expectedType))
            .OrderBy(p => p.OrderIndex)
            .Select(MapCotationPeriod)
            .ToList();

        if (structuredOpen.Count > 0
            || periods.Any(p => p.MainPeriodId.HasValue && MatchesPeriodType(p, expectedType)))
        {
            return structuredOpen;
        }

        var filtered = periods
            .Where(p => MatchesPeriodType(p, expectedType))
            .OrderBy(p => p.OrderIndex)
            .ThenBy(p => p.Name)
            .Select(MapCotationPeriod)
            .ToList();

        if (filtered.Count > 0)
        {
            return filtered;
        }

        // Fallback : toutes les périodes de l'année si le typage n'est pas renseigné.
        return periods
            .OrderBy(p => p.OrderIndex)
            .ThenBy(p => p.Name)
            .Select(MapCotationPeriod)
            .ToList();
    }

    private static CotationPeriodDto MapCotationPeriod(AcademicPeriod p) =>
        new(
            p.Id,
            p.Name,
            p.PeriodType,
            p.OrderIndex,
            p.IsClosed || p.Status is AcademicSubPeriodStatus.Cloturee or AcademicSubPeriodStatus.Verrouillee,
            p.Kind,
            p.Kind == AcademicSubPeriodKind.Examen ? "Examen" : "Travaux",
            p.StartDate,
            p.EndDate);

    private static PedagogicalCycleGroup ResolveCycleGroup(SchoolProgram? program, EducationCycle? cycle)
    {
        if (program is SchoolProgram.Maternelle or SchoolProgram.Primaire)
        {
            return PedagogicalCycleGroup.MaternellePrimaire;
        }

        if (program is SchoolProgram.CTEB
            or SchoolProgram.Humanites
            or SchoolProgram.HumanitesProfessionnelles
            or SchoolProgram.FilieresSpecialisees)
        {
            return PedagogicalCycleGroup.Secondaire;
        }

        return cycle == EducationCycle.Secondaire
            ? PedagogicalCycleGroup.Secondaire
            : PedagogicalCycleGroup.MaternellePrimaire;
    }

    private async Task<UserAccount?> GetCurrentUserAccountAsync(
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return null;
        }

        return (await _userRepository.FindAsync(
            u => u.Id == userId && u.SchoolId == schoolId,
            cancellationToken)).FirstOrDefault();
    }

    private async Task<Teacher> ResolveSessionTeacherAsync(
        Guid schoolId,
        string employeeNumber,
        CotationAccessScope accessScope,
        UserAccount? currentUser,
        CancellationToken cancellationToken)
    {
        // Enseignant / titulaire : session verrouillée sur le personnel lié au compte connecté.
        if (accessScope is CotationAccessScope.Teacher or CotationAccessScope.ClassHolder)
        {
            if (currentUser?.TeacherId is not Guid linkedTeacherId)
            {
                throw new DomainException(
                    "Votre compte n'est lié à aucun personnel. Contactez l'administrateur.");
            }

            var linkedTeacher = (await _teacherRepository.FindAsync(
                t => t.Id == linkedTeacherId && t.SchoolId == schoolId,
                cancellationToken)).FirstOrDefault()
                ?? throw new DomainException("Fiche personnel liée introuvable.");

            if (!MatchesTeacherIdentity(linkedTeacher, currentUser, employeeNumber))
            {
                throw new DomainException(
                    "L'identifiant saisi ne correspond pas à votre compte connecté. " +
                    $"Utilisez « {currentUser.UserName} » ou le matricule « {linkedTeacher.EmployeeNumber} ».");
            }

            return linkedTeacher;
        }

        // Direction / Préfet : peut ouvrir la session d'un enseignant précis.
        var teacher = (await _teacherRepository.FindAsync(
            t => t.SchoolId == schoolId && t.EmployeeNumber == employeeNumber,
            cancellationToken)).FirstOrDefault();

        if (teacher is null)
        {
            var linked = (await _userRepository.FindAsync(
                u => u.SchoolId == schoolId
                     && u.UserName == employeeNumber
                     && u.TeacherId != null,
                cancellationToken)).FirstOrDefault();
            if (linked?.TeacherId is Guid teacherId)
            {
                teacher = (await _teacherRepository.FindAsync(
                    t => t.Id == teacherId && t.SchoolId == schoolId,
                    cancellationToken)).FirstOrDefault();
            }
        }

        if (teacher is null)
        {
            throw new DomainException($"Enseignant introuvable pour l'identifiant « {employeeNumber} ».");
        }

        return teacher;
    }

    private static bool MatchesTeacherIdentity(Teacher teacher, UserAccount user, string identity)
    {
        return string.Equals(teacher.EmployeeNumber, identity, StringComparison.OrdinalIgnoreCase)
               || string.Equals(user.UserName, identity, StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureCanEnterGrades()
    {
        if (_currentUser.IsAdministrator
            || _currentUser.HasPermission(Permissions.AdminFull)
            || _currentUser.HasPermission(Permissions.GradesUpdate)
            || _currentUser.HasPermission(Permissions.GradesCreate))
        {
            return;
        }

        throw new DomainException("Vous n'avez pas le droit de saisir des notes.");
    }

    private CotationAccessScope ResolveCotationAccessScope()
    {
        if (_currentUser.IsAdministrator
            || _currentUser.HasPermission(Permissions.AdminFull)
            || HasRole("ADMIN", "DIRECTION", "PROMOTEUR"))
        {
            return CotationAccessScope.Full;
        }

        if (HasRole("PREFET", "PREFET_ETUDES", "PREFÉT", "PREFÉT_ÉTUDES"))
        {
            return CotationAccessScope.Prefet;
        }

        if (HasRole("TITULAIRE"))
        {
            return CotationAccessScope.ClassHolder;
        }

        return CotationAccessScope.Teacher;
    }

    private bool HasRole(params string[] codes)
    {
        var roles = _currentUser.Roles;
        if (roles.Count == 0)
        {
            return false;
        }

        return roles.Any(r => codes.Any(c =>
            string.Equals(r, c, StringComparison.OrdinalIgnoreCase)));
    }

    private async Task<bool> ValidateTeacherPasswordIfRequiredAsync(
        Guid schoolId,
        Guid teacherId,
        string? password,
        CotationAccessScope accessScope,
        UserAccount? currentUser,
        CancellationToken cancellationToken)
    {
        // Admin / Direction / Préfet : pas de mot de passe enseignant requis.
        if (accessScope is CotationAccessScope.Full or CotationAccessScope.Prefet)
        {
            return false;
        }

        // Déjà authentifié sur le compte lié à cet enseignant.
        if (currentUser is { IsActive: true, TeacherId: Guid linkedId }
            && linkedId == teacherId)
        {
            return true;
        }

        var linkedUsers = await _userRepository.FindAsync(
            u => u.SchoolId == schoolId && u.TeacherId == teacherId && u.IsActive,
            cancellationToken);

        var linkedUser = linkedUsers.FirstOrDefault();
        if (linkedUser is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new DomainException("Mot de passe requis pour cet enseignant.");
        }

        if (!_passwordHasher.Verify(password, linkedUser.PasswordHash))
        {
            throw new DomainException("Mot de passe incorrect.");
        }

        return true;
    }

    private static List<CourseAssignment> FilterAssignmentsByScope(
        IReadOnlyList<CourseAssignment> all,
        Guid teacherId,
        CotationAccessScope scope)
    {
        return scope switch
        {
            // Direction / Préfet : données de l'enseignant identifié uniquement (pas tout l'établissement).
            CotationAccessScope.Full or CotationAccessScope.Prefet =>
                all.Where(a => a.TeacherId == teacherId).ToList(),

            CotationAccessScope.ClassHolder =>
                ExpandClassHolderAssignments(all, teacherId),

            _ =>
                all.Where(a => a.TeacherId == teacherId).ToList()
        };
    }

    /// <summary>
    /// Titulaire : toutes les affectations des classes où il enseigne déjà.
    /// </summary>
    private static List<CourseAssignment> ExpandClassHolderAssignments(
        IReadOnlyList<CourseAssignment> all,
        Guid teacherId)
    {
        var classIds = all
            .Where(a => a.TeacherId == teacherId)
            .Select(a => a.ClassRoomId)
            .ToHashSet();

        return all.Where(a => classIds.Contains(a.ClassRoomId)).ToList();
    }

    private static AcademicPeriodType ResolvePeriodType(SchoolProgram? program, EducationCycle? cycle)
    {
        if (program is SchoolProgram.Maternelle or SchoolProgram.Primaire)
        {
            return AcademicPeriodType.Trimestre;
        }

        if (program is SchoolProgram.CTEB
            or SchoolProgram.Humanites
            or SchoolProgram.HumanitesProfessionnelles
            or SchoolProgram.FilieresSpecialisees)
        {
            return AcademicPeriodType.Semestre;
        }

        return cycle == EducationCycle.Secondaire
            ? AcademicPeriodType.Semestre
            : AcademicPeriodType.Trimestre;
    }

    private static bool MatchesPeriodType(AcademicPeriod period, AcademicPeriodType expected)
    {
        if (period.PeriodType == expected)
        {
            return true;
        }

        var name = period.Name ?? string.Empty;
        if (expected == AcademicPeriodType.Trimestre)
        {
            return name.Contains("trimestre", StringComparison.OrdinalIgnoreCase)
                   || name.Contains("trim", StringComparison.OrdinalIgnoreCase);
        }

        return name.Contains("semestre", StringComparison.OrdinalIgnoreCase)
               || name.Contains("sem", StringComparison.OrdinalIgnoreCase);
    }
}
