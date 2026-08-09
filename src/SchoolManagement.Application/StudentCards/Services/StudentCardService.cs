using System.Security.Cryptography;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.StudentCards.DTOs;
using SchoolManagement.Application.StudentCards.Interfaces;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.StudentCards.Services;

/// <summary>Services métier du module cartes élèves.</summary>
public sealed class StudentCardService : IStudentCardService
{
    private static readonly HashSet<StudentCardStatus> TerminalStatuses =
    [
        StudentCardStatus.Perdue,
        StudentCardStatus.Volee,
        StudentCardStatus.Remplacee,
        StudentCardStatus.Desactivee,
        StudentCardStatus.Expiree
    ];

    private readonly IRepository<StudentCard> _cards;
    private readonly IRepository<CardTemplate> _templates;
    private readonly IRepository<CardSchoolSettings> _settings;
    private readonly IRepository<StudentCardHistory> _histories;
    private readonly IRepository<StudentCardPrintLog> _printLogs;
    private readonly IRepository<Student> _students;
    private readonly IRepository<AcademicYear> _years;
    private readonly IRepository<Domain.Entities.Students.Enrollment> _enrollments;
    private readonly IRepository<ClassRoom> _classRooms;
    private readonly IRepository<StudyOption> _studyOptions;
    private readonly IRepository<PedagogicalClass> _pedagogicalClasses;
    private readonly IUnitOfWork _unitOfWork;

    public StudentCardService(
        IRepository<StudentCard> cards,
        IRepository<CardTemplate> templates,
        IRepository<CardSchoolSettings> settings,
        IRepository<StudentCardHistory> histories,
        IRepository<StudentCardPrintLog> printLogs,
        IRepository<Student> students,
        IRepository<AcademicYear> years,
        IRepository<Domain.Entities.Students.Enrollment> enrollments,
        IRepository<ClassRoom> classRooms,
        IRepository<StudyOption> studyOptions,
        IRepository<PedagogicalClass> pedagogicalClasses,
        IUnitOfWork unitOfWork)
    {
        _cards = cards;
        _templates = templates;
        _settings = settings;
        _histories = histories;
        _printLogs = printLogs;
        _students = students;
        _years = years;
        _enrollments = enrollments;
        _classRooms = classRooms;
        _studyOptions = studyOptions;
        _pedagogicalClasses = pedagogicalClasses;
        _unitOfWork = unitOfWork;
    }

    public async Task<StudentCardDashboardDto> GetDashboardAsync(
        Guid schoolId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        await ExpireOverdueCardsAsync(schoolId, cancellationToken);

        var yearId = academicYearId;
        var cards = await _cards.FindAsync(
            c => c.SchoolId == schoolId && (yearId == null || c.AcademicYearId == yearId),
            cancellationToken);

        var now = DateTime.UtcNow;
        var toRenew = cards.Count(c =>
            c.Status == StudentCardStatus.Active
            && c.ExpiresAt.HasValue
            && c.ExpiresAt.Value <= now.AddDays(30));

        var recentPrints = cards
            .Where(c => c.PrintedAt.HasValue)
            .OrderByDescending(c => c.PrintedAt)
            .Take(10)
            .ToList();

        var recent = await MapListItemsAsync(schoolId, recentPrints, cancellationToken);

        return new StudentCardDashboardDto(
            cards.Count(c => c.Status == StudentCardStatus.Active),
            cards.Count(c => c.Status == StudentCardStatus.Expiree),
            cards.Count(c => c.Status == StudentCardStatus.Perdue),
            cards.Count(c => c.Status == StudentCardStatus.Volee),
            toRenew,
            recent);
    }

    /// <summary>
    /// Bascule en <see cref="StudentCardStatus.Expiree"/> les cartes actives dont l'échéance
    /// est dépassée. Sans cette normalisation le statut stocké reste « Active » indéfiniment :
    /// le filtre « Expirée » ne remonte rien et l'index d'unicité « une carte active par élève
    /// et par année » bloque l'émission d'une carte de remplacement.
    /// </summary>
    private async Task<int> ExpireOverdueCardsAsync(Guid schoolId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var overdue = await _cards.FindAsync(
            c => c.SchoolId == schoolId
                 && c.Status == StudentCardStatus.Active
                 && c.ExpiresAt != null
                 && c.ExpiresAt < now,
            cancellationToken);

        if (overdue.Count == 0)
            return 0;

        foreach (var card in overdue)
        {
            card.Status = StudentCardStatus.Expiree;
            card.UpdatedAt = now;
            await _cards.UpdateAsync(card, cancellationToken);
            await AddHistoryAsync(
                schoolId,
                card.Id,
                StudentCardHistoryAction.Modification,
                Guid.Empty,
                StudentCardStatus.Active.ToString(),
                StudentCardStatus.Expiree.ToString(),
                "Expiration automatique (échéance dépassée)",
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return overdue.Count;
    }

    public async Task<StudentCardPagedResult> SearchAsync(
        Guid schoolId,
        StudentCardSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        await ExpireOverdueCardsAsync(schoolId, cancellationToken);

        // Année et statut sont poussés en SQL (index SchoolId/Status/ExpiresAt) ; seuls les
        // filtres nécessitant une jointure inscription restent évalués en mémoire.
        var yearId = request.AcademicYearId;
        var status = request.Status;
        var cards = (await _cards.FindAsync(
                c => c.SchoolId == schoolId
                     && (yearId == null || c.AcademicYearId == yearId)
                     && (status == null || c.Status == status),
                cancellationToken))
            .AsEnumerable();

        if (request.ClassRoomId.HasValue)
        {
            var enrollments = await _enrollments.FindAsync(
                e => e.ClassRoomId == request.ClassRoomId.Value
                     && e.IsActive
                     && (!yearId.HasValue || e.AcademicYearId == yearId.Value),
                cancellationToken);
            var studentIds = enrollments.Select(e => e.StudentId).ToHashSet();
            cards = cards.Where(c => studentIds.Contains(c.StudentId));
        }
        else if (request.SectionId.HasValue)
        {
            var rooms = await _classRooms.FindAsync(
                r => r.SchoolId == schoolId
                     && r.SectionId == request.SectionId.Value
                     && (!yearId.HasValue || r.AcademicYearId == yearId.Value),
                cancellationToken);
            var roomIds = rooms.Select(r => r.Id).ToHashSet();
            var enrollments = roomIds.Count == 0
                ? []
                : await _enrollments.FindAsync(
                    e => e.IsActive
                         && roomIds.Contains(e.ClassRoomId)
                         && (!yearId.HasValue || e.AcademicYearId == yearId.Value),
                    cancellationToken);
            var studentIds = enrollments.Select(e => e.StudentId).ToHashSet();
            cards = cards.Where(c => studentIds.Contains(c.StudentId));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            var tokens = term.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var lead = tokens[0];

            // Présélection en SQL sur le premier mot, puis affinage en mémoire sur les mots
            // suivants : « KABONGO Christian » retrouve ainsi l'élève, ce que ne permettait pas
            // la comparaison mot à mot précédente.
            var candidates = await _students.FindAsync(
                s => s.SchoolId == schoolId
                     && (s.RegistrationNumber.Contains(lead)
                         || s.FirstName.Contains(lead)
                         || s.LastName.Contains(lead)
                         || (s.MiddleName != null && s.MiddleName.Contains(lead))),
                cancellationToken);

            var matchingStudentIds = candidates
                .Where(s => tokens.All(t => MatchesStudent(s, t)))
                .Select(s => s.Id)
                .ToHashSet();

            cards = cards.Where(c =>
                c.CardNumber.Contains(term, StringComparison.OrdinalIgnoreCase)
                || matchingStudentIds.Contains(c.StudentId));
        }

        var ordered = cards
            .OrderByDescending(c => c.CreatedAt)
            .ToList();

        var total = ordered.Count;
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var items = await MapListItemsAsync(schoolId, pageItems, cancellationToken);

        return new StudentCardPagedResult(items, page, pageSize, total);
    }

    public async Task<StudentCardDetailDto> GetByIdAsync(
        Guid schoolId,
        Guid cardId,
        CancellationToken cancellationToken = default)
    {
        var card = await RequireCardAsync(schoolId, cardId, cancellationToken);
        return await MapDetailAsync(card, cancellationToken);
    }

    public async Task<ResolvedStudentCardDto?> ResolveByQrAsync(
        Guid schoolId,
        ResolveCardByQrRequest request,
        CancellationToken cancellationToken = default)
    {
        var token = ExtractQrToken(request.QrPayloadOrToken);
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var cards = await _cards.FindAsync(
            c => c.SchoolId == schoolId && c.QrToken == token,
            cancellationToken);
        var card = cards.FirstOrDefault();
        if (card is null)
            return null;

        var usable = IsCardUsable(card);
        return new ResolvedStudentCardDto(
            card.Id,
            card.CardNumber,
            card.StudentId,
            card.AcademicYearId,
            card.Status,
            card.ExpiresAt,
            usable);
    }

    public async Task<StudentCardDetailDto> CreateAsync(
        Guid schoolId,
        CreateStudentCardRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var student = (await _students.FindAsync(
            s => s.Id == request.StudentId && s.SchoolId == schoolId,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Élève introuvable.");

        var year = (await _years.FindAsync(
            y => y.Id == request.AcademicYearId && y.SchoolId == schoolId,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Année scolaire introuvable.");

        var template = await RequireTemplateAsync(schoolId, request.TemplateId, cancellationToken);
        if (!template.IsActive)
            throw new DomainException("Le modèle de carte sélectionné est inactif.");

        var settings = await GetOrCreateSettingsAsync(schoolId, cancellationToken);
        var status = request.ActivateImmediately ? StudentCardStatus.Active : StudentCardStatus.Brouillon;

        if (status == StudentCardStatus.Active)
            await EnsureNoOtherActiveAsync(schoolId, student.Id, year.Id, excludeCardId: null, cancellationToken);

        var allocator = await CreateAllocatorAsync(schoolId, settings, year, cancellationToken);
        var cardNumber = allocator.Next();
        var qrToken = GenerateQrToken();
        var expiresAt = request.ExpiresAt
            ?? DateTime.UtcNow.AddMonths(Math.Max(1, settings.DefaultValidityMonths));

        if (expiresAt <= DateTime.UtcNow)
            throw new DomainException("La date d'expiration doit être postérieure à aujourd'hui.");

        var card = new StudentCard
        {
            SchoolId = schoolId,
            StudentId = student.Id,
            AcademicYearId = year.Id,
            TemplateId = template.Id,
            CardNumber = cardNumber,
            QrToken = qrToken,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            Status = status,
            Version = 1
        };

        await _cards.AddAsync(card, cancellationToken);
        await AddHistoryAsync(
            schoolId,
            card.Id,
            StudentCardHistoryAction.Creation,
            userId,
            oldValue: null,
            newValue: $"{card.CardNumber}|{status}",
            notes: null,
            cancellationToken);

        if (status == StudentCardStatus.Active)
        {
            await AddHistoryAsync(
                schoolId,
                card.Id,
                StudentCardHistoryAction.Activation,
                userId,
                oldValue: StudentCardStatus.Brouillon.ToString(),
                newValue: status.ToString(),
                notes: null,
                cancellationToken);
        }

        await PersistSettingsAsync(settings, userId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapDetailAsync(card, cancellationToken);
    }

    public async Task<BulkCreateStudentCardsResult> BulkCreateAsync(
        Guid schoolId,
        BulkCreateStudentCardsRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var year = (await _years.FindAsync(
            y => y.Id == request.AcademicYearId && y.SchoolId == schoolId,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Année scolaire introuvable.");

        var template = await RequireTemplateAsync(schoolId, request.TemplateId, cancellationToken);
        if (!template.IsActive)
            throw new DomainException("Le modèle de carte sélectionné est inactif.");

        var studentIds = await ResolveBulkStudentIdsAsync(schoolId, request, cancellationToken);
        if (studentIds.Count == 0)
            throw new DomainException("Aucun élève inscrit trouvé pour ce périmètre.");

        var settings = await GetOrCreateSettingsAsync(schoolId, cancellationToken);
        var status = request.ActivateImmediately ? StudentCardStatus.Active : StudentCardStatus.Brouillon;
        var expiresAt = request.ExpiresAt
            ?? DateTime.UtcNow.AddMonths(Math.Max(1, settings.DefaultValidityMonths));

        if (expiresAt <= DateTime.UtcNow)
            throw new DomainException("La date d'expiration doit être postérieure à aujourd'hui.");

        var allocator = await CreateAllocatorAsync(schoolId, settings, year, cancellationToken);

        var existingActive = status == StudentCardStatus.Active || request.SkipExistingActive
            ? (await _cards.FindAsync(
                    c => c.SchoolId == schoolId
                         && c.AcademicYearId == year.Id
                         && c.Status == StudentCardStatus.Active,
                    cancellationToken))
                .Select(c => c.StudentId)
                .ToHashSet()
            : [];

        var schoolStudents = (await _students.FindAsync(s => s.SchoolId == schoolId, cancellationToken))
            .Select(s => s.Id)
            .ToHashSet();

        var createdIds = new List<Guid>();
        var skipped = 0;

        foreach (var studentId in studentIds.Distinct())
        {
            if (!schoolStudents.Contains(studentId))
            {
                skipped++;
                continue;
            }

            if (request.SkipExistingActive && existingActive.Contains(studentId))
            {
                skipped++;
                continue;
            }

            if (status == StudentCardStatus.Active && existingActive.Contains(studentId))
            {
                skipped++;
                continue;
            }

            var cardNumber = allocator.Next();
            var card = new StudentCard
            {
                SchoolId = schoolId,
                StudentId = studentId,
                AcademicYearId = year.Id,
                TemplateId = template.Id,
                CardNumber = cardNumber,
                QrToken = GenerateQrToken(),
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                Status = status,
                Version = 1
            };

            await _cards.AddAsync(card, cancellationToken);
            await AddHistoryAsync(
                schoolId,
                card.Id,
                StudentCardHistoryAction.Creation,
                userId,
                null,
                $"{card.CardNumber}|{status}|bulk",
                "Création en lot",
                cancellationToken);

            if (status == StudentCardStatus.Active)
            {
                await AddHistoryAsync(
                    schoolId,
                    card.Id,
                    StudentCardHistoryAction.Activation,
                    userId,
                    StudentCardStatus.Brouillon.ToString(),
                    status.ToString(),
                    "Activation en lot",
                    cancellationToken);
                existingActive.Add(studentId);
            }

            createdIds.Add(card.Id);
        }

        if (createdIds.Count > 0)
        {
            await PersistSettingsAsync(settings, userId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var summary =
            $"{createdIds.Count} carte(s) créée(s) sur {studentIds.Count} élève(s) ciblé(s)"
            + (skipped > 0 ? $", {skipped} ignoré(s) (déjà équipés ou hors périmètre)" : string.Empty)
            + ".";

        return new BulkCreateStudentCardsResult(
            studentIds.Count,
            createdIds.Count,
            skipped,
            createdIds,
            summary);
    }

    private async Task<IReadOnlyList<Guid>> ResolveBulkStudentIdsAsync(
        Guid schoolId,
        BulkCreateStudentCardsRequest request,
        CancellationToken cancellationToken)
    {
        var scopes = new[]
        {
            request.ClassRoomId.HasValue,
            request.SectionId.HasValue,
            request.EntireSchool
        }.Count(x => x);

        if (scopes != 1)
            throw new DomainException("Choisissez un seul périmètre : classe, section ou toute l'école.");

        if (request.ClassRoomId.HasValue)
        {
            var room = await _classRooms.GetByIdAsync(request.ClassRoomId.Value, cancellationToken)
                ?? throw new DomainException("Classe introuvable.");
            if (room.SchoolId != schoolId)
                throw new DomainException("Classe introuvable.");

            var enrollments = await _enrollments.FindAsync(
                e => e.ClassRoomId == room.Id
                     && e.AcademicYearId == request.AcademicYearId
                     && e.IsActive,
                cancellationToken);
            return enrollments.Select(e => e.StudentId).Distinct().ToList();
        }

        if (request.SectionId.HasValue)
        {
            var rooms = await _classRooms.FindAsync(
                r => r.SchoolId == schoolId
                     && r.SectionId == request.SectionId.Value
                     && r.AcademicYearId == request.AcademicYearId
                     && r.IsActive,
                cancellationToken);
            var roomIds = rooms.Select(r => r.Id).ToHashSet();
            if (roomIds.Count == 0)
                return [];

            var enrollments = await _enrollments.FindAsync(
                e => e.AcademicYearId == request.AcademicYearId && e.IsActive,
                cancellationToken);
            return enrollments
                .Where(e => roomIds.Contains(e.ClassRoomId))
                .Select(e => e.StudentId)
                .Distinct()
                .ToList();
        }

        // Toute l'école (année sélectionnée)
        var allEnrollments = await _enrollments.FindAsync(
            e => e.AcademicYearId == request.AcademicYearId && e.IsActive,
            cancellationToken);
        var yearRooms = (await _classRooms.FindAsync(
                r => r.SchoolId == schoolId && r.AcademicYearId == request.AcademicYearId,
                cancellationToken))
            .Select(r => r.Id)
            .ToHashSet();
        return allEnrollments
            .Where(e => yearRooms.Contains(e.ClassRoomId))
            .Select(e => e.StudentId)
            .Distinct()
            .ToList();
    }

    public async Task<StudentCardDetailDto> UpdateAsync(
        Guid schoolId,
        Guid cardId,
        UpdateStudentCardRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var card = await RequireCardAsync(schoolId, cardId, cancellationToken);
        EnsureMutable(card);

        await RequireTemplateAsync(schoolId, request.TemplateId, cancellationToken);
        var old = $"{card.TemplateId}|{card.ExpiresAt:O}";
        card.TemplateId = request.TemplateId;
        card.ExpiresAt = request.ExpiresAt;
        card.UpdatedAt = DateTime.UtcNow;
        card.UpdatedBy = userId;

        await _cards.UpdateAsync(card, cancellationToken);
        await AddHistoryAsync(
            schoolId,
            card.Id,
            StudentCardHistoryAction.Modification,
            userId,
            old,
            $"{card.TemplateId}|{card.ExpiresAt:O}",
            request.Notes,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapDetailAsync(card, cancellationToken);
    }

    public async Task SoftDeleteAsync(
        Guid schoolId,
        Guid cardId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var card = await RequireCardAsync(schoolId, cardId, cancellationToken);
        await AddHistoryAsync(
            schoolId,
            card.Id,
            StudentCardHistoryAction.SuppressionLogique,
            userId,
            card.Status.ToString(),
            null,
            null,
            cancellationToken);
        await _cards.DeleteAsync(card, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PrintStudentCardsResult> PrintAsync(
        Guid schoolId,
        PrintStudentCardsRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var targets = await ResolvePrintTargetsAsync(schoolId, request, cancellationToken);
        if (targets.Count == 0)
            throw new DomainException("Aucune carte à imprimer pour les critères fournis.");

        foreach (var card in targets)
        {
            EnsurePrintable(card);
            await RecordPrintAsync(card, userId, request.Reason, isReprint: card.PrintCount > 0, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new PrintStudentCardsResult(targets.Count, targets.Select(c => c.Id).ToList());
    }

    public async Task<StudentCardDetailDto> ReprintAsync(
        Guid schoolId,
        Guid cardId,
        ReprintStudentCardRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var card = await RequireCardAsync(schoolId, cardId, cancellationToken);
        EnsurePrintable(card);
        await RecordPrintAsync(card, userId, request.Reason, isReprint: true, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapDetailAsync(card, cancellationToken);
    }

    public async Task<StudentCardDetailDto> RenewAsync(
        Guid schoolId,
        Guid cardId,
        RenewStudentCardRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var oldCard = await RequireCardAsync(schoolId, cardId, cancellationToken);
        if (oldCard.Status is StudentCardStatus.Remplacee or StudentCardStatus.Desactivee)
            throw new DomainException("Cette carte ne peut plus être renouvelée.");

        var settings = await GetOrCreateSettingsAsync(schoolId, cancellationToken);
        var keepQr = request.KeepQrToken ?? settings.KeepQrOnRenewal;
        var templateId = request.TemplateId ?? oldCard.TemplateId;
        await RequireTemplateAsync(schoolId, templateId, cancellationToken);

        var previousStatus = oldCard.Status;
        oldCard.Status = StudentCardStatus.Remplacee;
        oldCard.DeactivationReason = "Renouvellement";
        oldCard.UpdatedAt = DateTime.UtcNow;
        oldCard.UpdatedBy = userId;
        await _cards.UpdateAsync(oldCard, cancellationToken);
        await AddHistoryAsync(
            schoolId,
            oldCard.Id,
            StudentCardHistoryAction.Renouvellement,
            userId,
            previousStatus.ToString(),
            StudentCardStatus.Remplacee.ToString(),
            "Carte remplacée par renouvellement",
            cancellationToken);

        var year = (await _years.FindAsync(y => y.Id == oldCard.AcademicYearId, cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Année scolaire introuvable.");
        var allocator = await CreateAllocatorAsync(schoolId, settings, year, cancellationToken);
        var cardNumber = allocator.Next();
        var qrToken = keepQr ? oldCard.QrToken : GenerateQrToken();
        var expiresAt = request.ExpiresAt
            ?? DateTime.UtcNow.AddMonths(Math.Max(1, settings.DefaultValidityMonths));

        if (expiresAt <= DateTime.UtcNow)
            throw new DomainException("La date d'expiration doit être postérieure à aujourd'hui.");

        await EnsureNoOtherActiveAsync(schoolId, oldCard.StudentId, oldCard.AcademicYearId, excludeCardId: oldCard.Id, cancellationToken);

        var newCard = new StudentCard
        {
            SchoolId = schoolId,
            StudentId = oldCard.StudentId,
            AcademicYearId = oldCard.AcademicYearId,
            TemplateId = templateId,
            CardNumber = cardNumber,
            QrToken = qrToken,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            Status = StudentCardStatus.Active,
            Version = oldCard.Version + 1,
            ReplacesCardId = oldCard.Id
        };

        await _cards.AddAsync(newCard, cancellationToken);
        await AddHistoryAsync(
            schoolId,
            newCard.Id,
            StudentCardHistoryAction.Creation,
            userId,
            null,
            $"{newCard.CardNumber}|Active|renew-from:{oldCard.CardNumber}|keepQr:{keepQr}",
            null,
            cancellationToken);

        await PersistSettingsAsync(settings, userId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapDetailAsync(newCard, cancellationToken);
    }

    public async Task<StudentCardDetailDto> DeclareLostAsync(
        Guid schoolId,
        Guid cardId,
        DeclareCardIncidentRequest request,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await DisableForIncidentAsync(
            schoolId,
            cardId,
            StudentCardStatus.Perdue,
            StudentCardHistoryAction.Perte,
            request.Reason ?? "Carte déclarée perdue",
            userId,
            cancellationToken);

    public async Task<StudentCardDetailDto> DeclareStolenAsync(
        Guid schoolId,
        Guid cardId,
        DeclareCardIncidentRequest request,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await DisableForIncidentAsync(
            schoolId,
            cardId,
            StudentCardStatus.Volee,
            StudentCardHistoryAction.Vol,
            request.Reason ?? "Carte déclarée volée",
            userId,
            cancellationToken);

    public async Task<StudentCardDetailDto> DeactivateAsync(
        Guid schoolId,
        Guid cardId,
        DeactivateStudentCardRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new DomainException("Le motif de désactivation est obligatoire.");

        return await DisableForIncidentAsync(
            schoolId,
            cardId,
            StudentCardStatus.Desactivee,
            StudentCardHistoryAction.Desactivation,
            request.Reason.Trim(),
            userId,
            cancellationToken);
    }

    public async Task<StudentCardDetailDto> ActivateAsync(
        Guid schoolId,
        Guid cardId,
        ActivateStudentCardRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var card = await RequireCardAsync(schoolId, cardId, cancellationToken);

        if (card.Status == StudentCardStatus.Active)
            throw new DomainException("Cette carte est déjà active.");

        if (card.Status is not (StudentCardStatus.Brouillon or StudentCardStatus.Suspendue))
            throw new DomainException(
                "Seule une carte en brouillon ou suspendue peut être activée. Utilisez le renouvellement pour une carte expirée, perdue, volée ou désactivée.");

        if (card.ExpiresAt.HasValue && card.ExpiresAt.Value <= DateTime.UtcNow)
            throw new DomainException("Cette carte est arrivée à échéance : renouvelez-la au lieu de l'activer.");

        await EnsureNoOtherActiveAsync(
            schoolId, card.StudentId, card.AcademicYearId, excludeCardId: card.Id, cancellationToken);

        var previous = card.Status;
        card.Status = StudentCardStatus.Active;
        card.DeactivationReason = null;
        card.UpdatedAt = DateTime.UtcNow;
        card.UpdatedBy = userId;

        await _cards.UpdateAsync(card, cancellationToken);
        await AddHistoryAsync(
            schoolId,
            card.Id,
            StudentCardHistoryAction.Activation,
            userId,
            previous.ToString(),
            StudentCardStatus.Active.ToString(),
            request.Notes,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapDetailAsync(card, cancellationToken);
    }

    public async Task<StudentCardDetailDto> SuspendAsync(
        Guid schoolId,
        Guid cardId,
        SuspendStudentCardRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new DomainException("Le motif de suspension est obligatoire.");

        var card = await RequireCardAsync(schoolId, cardId, cancellationToken);

        if (card.Status == StudentCardStatus.Suspendue)
            throw new DomainException("Cette carte est déjà suspendue.");

        if (card.Status != StudentCardStatus.Active)
            throw new DomainException("Seule une carte active peut être suspendue.");

        card.Status = StudentCardStatus.Suspendue;
        card.DeactivationReason = request.Reason.Trim();
        card.UpdatedAt = DateTime.UtcNow;
        card.UpdatedBy = userId;

        await _cards.UpdateAsync(card, cancellationToken);
        await AddHistoryAsync(
            schoolId,
            card.Id,
            StudentCardHistoryAction.Suspension,
            userId,
            StudentCardStatus.Active.ToString(),
            StudentCardStatus.Suspendue.ToString(),
            card.DeactivationReason,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapDetailAsync(card, cancellationToken);
    }

    public async Task<IReadOnlyList<CardTemplateDto>> ListTemplatesAsync(
        Guid schoolId,
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var items = await _templates.FindAsync(t => t.SchoolId == schoolId, cancellationToken);
        if (activeOnly)
            items = items.Where(t => t.IsActive).ToList();

        return items.OrderBy(t => t.Name).Select(MapTemplate).ToList();
    }

    public async Task<CardTemplateDto> GetTemplateAsync(
        Guid schoolId,
        Guid templateId,
        CancellationToken cancellationToken = default) =>
        MapTemplate(await RequireTemplateAsync(schoolId, templateId, cancellationToken));

    public async Task<CardTemplateDto> CreateTemplateAsync(
        Guid schoolId,
        SaveCardTemplateRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateTemplate(request);
        var existing = await _templates.FindAsync(
            t => t.SchoolId == schoolId && t.Name == request.Name.Trim(),
            cancellationToken);
        if (existing.Count > 0)
            throw new DomainException("Un modèle porte déjà ce nom.");

        var template = new CardTemplate
        {
            SchoolId = schoolId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            WidthMm = request.WidthMm,
            HeightMm = request.HeightMm,
            Orientation = request.Orientation,
            Kind = request.Kind,
            LayoutJsonFront = request.LayoutJsonFront,
            LayoutJsonBack = request.LayoutJsonBack,
            IsActive = request.IsActive,
            CreatedBy = userId
        };

        await _templates.AddAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapTemplate(template);
    }

    public async Task<CardTemplateDto> UpdateTemplateAsync(
        Guid schoolId,
        Guid templateId,
        SaveCardTemplateRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateTemplate(request);
        var template = await RequireTemplateAsync(schoolId, templateId, cancellationToken);
        var clash = await _templates.FindAsync(
            t => t.SchoolId == schoolId && t.Name == request.Name.Trim() && t.Id != templateId,
            cancellationToken);
        if (clash.Count > 0)
            throw new DomainException("Un modèle porte déjà ce nom.");

        template.Name = request.Name.Trim();
        template.Description = request.Description?.Trim();
        template.WidthMm = request.WidthMm;
        template.HeightMm = request.HeightMm;
        template.Orientation = request.Orientation;
        template.Kind = request.Kind;
        template.LayoutJsonFront = request.LayoutJsonFront;
        template.LayoutJsonBack = request.LayoutJsonBack;
        template.IsActive = request.IsActive;
        template.UpdatedAt = DateTime.UtcNow;
        template.UpdatedBy = userId;

        await _templates.UpdateAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapTemplate(template);
    }

    public async Task DeleteTemplateAsync(
        Guid schoolId,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var template = await RequireTemplateAsync(schoolId, templateId, cancellationToken);
        var linked = await _cards.FindAsync(
            c => c.SchoolId == schoolId && c.TemplateId == templateId,
            cancellationToken);
        if (linked.Count > 0)
            throw new DomainException("Impossible de supprimer un modèle utilisé par des cartes. Désactivez-le plutôt.");

        await _templates.DeleteAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<CardTemplateDto> PreviewTemplateAsync(
        Guid schoolId,
        SaveCardTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateTemplate(request);
        var preview = new CardTemplateDto(
            Guid.Empty,
            request.Name.Trim(),
            request.Description?.Trim(),
            request.WidthMm,
            request.HeightMm,
            request.Orientation,
            request.Kind,
            request.LayoutJsonFront,
            request.LayoutJsonBack,
            request.IsActive);
        return Task.FromResult(preview);
    }

    public async Task<CardSchoolSettingsDto> GetSettingsAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateSettingsAsync(schoolId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapSettings(settings);
    }

    public async Task<CardSchoolSettingsDto> SaveSettingsAsync(
        Guid schoolId,
        SaveCardSchoolSettingsRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CardNumberPrefix))
            throw new DomainException("Le préfixe du numéro de carte est obligatoire.");
        if (request.DefaultValidityMonths < 1 || request.DefaultValidityMonths > 120)
            throw new DomainException("La validité doit être comprise entre 1 et 120 mois.");

        var settings = await GetOrCreateSettingsAsync(schoolId, cancellationToken);
        settings.CardNumberPrefix = request.CardNumberPrefix.Trim().ToUpperInvariant();
        settings.DefaultValidityMonths = request.DefaultValidityMonths;
        settings.KeepQrOnRenewal = request.KeepQrOnRenewal;
        await PersistSettingsAsync(settings, userId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapSettings(settings);
    }

    private async Task<StudentCardDetailDto> DisableForIncidentAsync(
        Guid schoolId,
        Guid cardId,
        StudentCardStatus newStatus,
        StudentCardHistoryAction action,
        string reason,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var card = await RequireCardAsync(schoolId, cardId, cancellationToken);
        if (TerminalStatuses.Contains(card.Status) && card.Status != StudentCardStatus.Expiree)
            throw new DomainException("Cette carte est déjà désactivée ; aucune opération n'est autorisée.");

        var old = card.Status.ToString();
        card.Status = newStatus;
        card.DeactivationReason = reason;
        card.UpdatedAt = DateTime.UtcNow;
        card.UpdatedBy = userId;
        await _cards.UpdateAsync(card, cancellationToken);
        await AddHistoryAsync(schoolId, card.Id, action, userId, old, newStatus.ToString(), reason, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapDetailAsync(card, cancellationToken);
    }

    private async Task RecordPrintAsync(
        StudentCard card,
        Guid userId,
        string? reason,
        bool isReprint,
        CancellationToken cancellationToken)
    {
        card.PrintCount += 1;
        card.PrintedAt = DateTime.UtcNow;
        card.UpdatedAt = DateTime.UtcNow;
        card.UpdatedBy = userId;
        await _cards.UpdateAsync(card, cancellationToken);

        await _printLogs.AddAsync(new StudentCardPrintLog
        {
            SchoolId = card.SchoolId,
            CardId = card.Id,
            PrintedAt = DateTime.UtcNow,
            PrintedBy = userId,
            Reason = reason,
            IsReprint = isReprint
        }, cancellationToken);

        await AddHistoryAsync(
            card.SchoolId,
            card.Id,
            isReprint ? StudentCardHistoryAction.Reimpression : StudentCardHistoryAction.Impression,
            userId,
            null,
            $"printCount={card.PrintCount}",
            reason,
            cancellationToken);
    }

    private async Task<IReadOnlyList<StudentCard>> ResolvePrintTargetsAsync(
        Guid schoolId,
        PrintStudentCardsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CardIds is { Count: > 0 })
        {
            var ids = request.CardIds.ToHashSet();
            var cards = await _cards.FindAsync(
                c => c.SchoolId == schoolId && ids.Contains(c.Id),
                cancellationToken);

            var missing = ids.Count - cards.Count;
            if (missing > 0)
                throw new DomainException($"{missing} carte(s) demandée(s) sont introuvables dans cet établissement.");

            return cards;
        }

        // Une liste de cartes vide mais non nulle signifie « rien de sélectionné » : sans ce
        // garde-fou la demande basculait sur le périmètre global et marquait imprimées toutes
        // les cartes de l'établissement.
        if (request.CardIds is not null)
            throw new DomainException("Aucune carte sélectionnée pour l'impression.");

        if (!request.ClassRoomId.HasValue && !request.EntireSchool)
            throw new DomainException("Précisez des cartes, une classe, ou EntireSchool=true.");

        var yearId = request.AcademicYearId;
        var query = (await _cards.FindAsync(
                c => c.SchoolId == schoolId
                     && (c.Status == StudentCardStatus.Active || c.Status == StudentCardStatus.Brouillon)
                     && (yearId == null || c.AcademicYearId == yearId),
                cancellationToken))
            .AsEnumerable();

        if (request.ClassRoomId.HasValue)
        {
            var enrollments = await _enrollments.FindAsync(
                e => e.ClassRoomId == request.ClassRoomId.Value && e.IsActive,
                cancellationToken);
            var studentIds = enrollments.Select(e => e.StudentId).ToHashSet();
            query = query.Where(c => studentIds.Contains(c.StudentId));
        }

        return query.ToList();
    }

    private async Task EnsureNoOtherActiveAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        Guid? excludeCardId,
        CancellationToken cancellationToken)
    {
        var actives = await _cards.FindAsync(
            c => c.SchoolId == schoolId
                 && c.StudentId == studentId
                 && c.AcademicYearId == academicYearId
                 && c.Status == StudentCardStatus.Active,
            cancellationToken);

        if (excludeCardId.HasValue)
            actives = actives.Where(c => c.Id != excludeCardId.Value).ToList();

        if (actives.Count > 0)
            throw new DomainException("Une carte active existe déjà pour cet élève sur cette année scolaire.");
    }

    /// <summary>
    /// Alloue des numéros de carte uniques en sautant ceux déjà consommés en base.
    /// Le compteur <see cref="CardSchoolSettings.NextSequence"/> s'auto-répare ainsi
    /// même après un incident ayant laissé la séquence en retard.
    /// </summary>
    private sealed class CardNumberAllocator
    {
        private readonly CardSchoolSettings _settings;
        private readonly string _yearPart;
        private readonly HashSet<string> _used;

        public CardNumberAllocator(CardSchoolSettings settings, string yearPart, HashSet<string> used)
        {
            _settings = settings;
            _yearPart = yearPart;
            _used = used;
        }

        public string Next()
        {
            for (var guard = 0; guard < 1_000_000; guard++)
            {
                var seq = _settings.NextSequence;
                _settings.NextSequence = seq + 1;
                var candidate = $"{_settings.CardNumberPrefix}-{_yearPart}-{seq:D6}";
                if (_used.Add(candidate))
                    return candidate;
            }

            throw new DomainException("Impossible d'allouer un numéro de carte unique.");
        }
    }

    private async Task<CardNumberAllocator> CreateAllocatorAsync(
        Guid schoolId,
        CardSchoolSettings settings,
        AcademicYear year,
        CancellationToken cancellationToken)
    {
        var yearPart = year.StartDate.Year.ToString();
        var prefix = $"{settings.CardNumberPrefix}-{yearPart}-";
        var used = (await _cards.FindAsync(
                c => c.SchoolId == schoolId && c.CardNumber.StartsWith(prefix),
                cancellationToken))
            .Select(c => c.CardNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new CardNumberAllocator(settings, yearPart, used);
    }

    /// <summary>
    /// <see cref="IRepository{T}"/> expose uniquement des lectures AsNoTracking : les mutations
    /// de l'entité paramètres doivent être réattachées explicitement avant l'enregistrement,
    /// sans quoi le compteur de séquence et les réglages sont silencieusement perdus.
    /// </summary>
    private async Task PersistSettingsAsync(CardSchoolSettings settings, Guid userId, CancellationToken cancellationToken)
    {
        settings.UpdatedAt = DateTime.UtcNow;
        if (userId != Guid.Empty)
            settings.UpdatedBy = userId;
        await _settings.UpdateAsync(settings, cancellationToken);
    }

    private async Task<CardSchoolSettings> GetOrCreateSettingsAsync(Guid schoolId, CancellationToken cancellationToken)
    {
        var existing = await _settings.FindAsync(s => s.SchoolId == schoolId, cancellationToken);
        var current = existing.FirstOrDefault();
        if (current is not null)
            return current;

        var settings = new CardSchoolSettings
        {
            SchoolId = schoolId,
            CardNumberPrefix = "CARD",
            DefaultValidityMonths = 12,
            KeepQrOnRenewal = false,
            NextSequence = 1
        };
        await _settings.AddAsync(settings, cancellationToken);
        return settings;
    }

    private async Task<StudentCard> RequireCardAsync(Guid schoolId, Guid cardId, CancellationToken cancellationToken)
    {
        var card = await _cards.GetByIdAsync(cardId, cancellationToken);
        if (card is null || card.SchoolId != schoolId)
            throw new KeyNotFoundException("Carte introuvable.");
        return card;
    }

    private async Task<CardTemplate> RequireTemplateAsync(Guid schoolId, Guid templateId, CancellationToken cancellationToken)
    {
        var template = await _templates.GetByIdAsync(templateId, cancellationToken);
        if (template is null || template.SchoolId != schoolId)
            throw new KeyNotFoundException("Modèle de carte introuvable.");
        return template;
    }

    private async Task AddHistoryAsync(
        Guid schoolId,
        Guid cardId,
        StudentCardHistoryAction action,
        Guid userId,
        string? oldValue,
        string? newValue,
        string? notes,
        CancellationToken cancellationToken)
    {
        await _histories.AddAsync(new StudentCardHistory
        {
            SchoolId = schoolId,
            CardId = cardId,
            Action = action,
            UserId = userId == Guid.Empty ? null : userId,
            OccurredAt = DateTime.UtcNow,
            OldValue = oldValue,
            NewValue = newValue,
            Notes = notes
        }, cancellationToken);
    }

    private async Task<IReadOnlyList<StudentCardListItemDto>> MapListItemsAsync(
        Guid schoolId,
        IReadOnlyList<StudentCard> cards,
        CancellationToken cancellationToken)
    {
        if (cards.Count == 0)
            return [];

        var studentIds = cards.Select(c => c.StudentId).Distinct().ToList();
        var students = (await _students.FindAsync(
                s => s.SchoolId == schoolId && studentIds.Contains(s.Id),
                cancellationToken))
            .ToDictionary(s => s.Id);

        var yearIds = cards.Select(c => c.AcademicYearId).Distinct().ToHashSet();
        var enrollments = (await SchoolScopedEnrollmentQueries.GetActiveForStudentsAsync(
                _enrollments, studentIds, cancellationToken))
            .Where(e => studentIds.Contains(e.StudentId) && yearIds.Contains(e.AcademicYearId))
            .ToList();

        var classIds = enrollments.Select(e => e.ClassRoomId).Distinct().ToList();
        var classRoomList = classIds.Count == 0
            ? []
            : await _classRooms.FindAsync(c => c.SchoolId == schoolId && classIds.Contains(c.Id), cancellationToken);
        var classRooms = classRoomList.ToDictionary(c => c.Id);

        var enrollmentByStudentYear = enrollments
            .GroupBy(e => (e.StudentId, e.AcademicYearId))
            .ToDictionary(g => g.Key, g => g.First());

        return cards.Select(card =>
        {
            students.TryGetValue(card.StudentId, out var student);
            string? className = null;
            if (enrollmentByStudentYear.TryGetValue((card.StudentId, card.AcademicYearId), out var enrollment)
                && classRooms.TryGetValue(enrollment.ClassRoomId, out var room))
            {
                className = room.Name;
            }

            return new StudentCardListItemDto(
                card.Id,
                card.StudentId,
                student is null ? "—" : FormatFullName(student),
                student?.PhotoPath,
                className,
                card.CardNumber,
                card.Status,
                card.PrintedAt,
                card.ExpiresAt,
                card.PrintCount,
                card.Version);
        }).ToList();
    }

    private async Task<StudentCardDetailDto> MapDetailAsync(StudentCard card, CancellationToken cancellationToken)
    {
        var student = await _students.GetByIdAsync(card.StudentId, cancellationToken);
        var year = await _years.GetByIdAsync(card.AcademicYearId, cancellationToken);
        var template = await _templates.GetByIdAsync(card.TemplateId, cancellationToken);
        var (className, studyOption) = await ResolveClassInfoAsync(
            card.StudentId, card.AcademicYearId, cancellationToken);
        var histories = (await _histories.FindAsync(h => h.CardId == card.Id, cancellationToken))
            .OrderByDescending(h => h.OccurredAt)
            .Select(h => new StudentCardHistoryDto(
                h.Id, h.Action, h.UserId, h.OccurredAt, h.OldValue, h.NewValue, h.Notes))
            .ToList();
        var prints = (await _printLogs.FindAsync(p => p.CardId == card.Id, cancellationToken))
            .OrderByDescending(p => p.PrintedAt)
            .Select(p => new StudentCardPrintLogDto(p.Id, p.PrintedAt, p.PrintedBy, p.Reason, p.IsReprint))
            .ToList();

        return new StudentCardDetailDto(
            card.Id,
            card.StudentId,
            student is null ? "—" : FormatFullName(student),
            student?.PhotoPath,
            student?.LastName ?? string.Empty,
            student?.FirstName ?? string.Empty,
            student?.MiddleName,
            student?.RegistrationNumber ?? string.Empty,
            FormatGender(student?.Gender),
            student is null ? string.Empty : student.DateOfBirth.ToString("dd/MM/yyyy"),
            className,
            studyOption,
            card.AcademicYearId,
            year?.Label ?? "—",
            card.TemplateId,
            template?.Name ?? "—",
            card.CardNumber,
            card.QrToken,
            card.QrPayload,
            card.IssuedAt,
            card.PrintedAt,
            card.ExpiresAt,
            card.Status,
            card.DeactivationReason,
            card.PrintCount,
            card.Version,
            card.ReplacesCardId,
            histories,
            prints);
    }

    private async Task<(string? ClassName, string? StudyOption)> ResolveClassInfoAsync(
        Guid studentId,
        Guid academicYearId,
        CancellationToken cancellationToken)
    {
        var enrollment = (await _enrollments.FindAsync(
                e => e.IsActive && e.StudentId == studentId && e.AcademicYearId == academicYearId,
                cancellationToken))
            .FirstOrDefault();
        if (enrollment is null)
            return (null, null);

        var room = await _classRooms.GetByIdAsync(enrollment.ClassRoomId, cancellationToken);
        if (room is null)
            return (null, null);

        string? className = room.Name;
        string? studyOption = null;

        if (room.PedagogicalClassId is Guid pedId)
        {
            var ped = await _pedagogicalClasses.GetByIdAsync(pedId, cancellationToken);
            if (ped is not null)
            {
                className = string.IsNullOrWhiteSpace(room.Name) || room.Name.Length <= 2
                    ? $"{ped.DisplayName} {room.Name}".Trim()
                    : ped.DisplayName;
                if (!string.IsNullOrWhiteSpace(ped.StudyOption))
                    studyOption = ped.StudyOption;
            }
        }

        if (room.StudyOptionId is Guid optId)
        {
            var option = await _studyOptions.GetByIdAsync(optId, cancellationToken);
            if (option is not null)
                studyOption = option.Name;
        }

        return (className, studyOption);
    }

    private static string FormatGender(Gender? gender) =>
        gender switch
        {
            Gender.Feminin => "Féminin",
            Gender.Masculin => "Masculin",
            _ => string.Empty
        };

    private static CardTemplateDto MapTemplate(CardTemplate t) =>
        new(t.Id, t.Name, t.Description, t.WidthMm, t.HeightMm, t.Orientation, t.Kind,
            t.LayoutJsonFront, t.LayoutJsonBack, t.IsActive);

    private static CardSchoolSettingsDto MapSettings(CardSchoolSettings s) =>
        new(s.Id, s.CardNumberPrefix, s.DefaultValidityMonths, s.KeepQrOnRenewal, s.NextSequence);

    private static void ValidateTemplate(SaveCardTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("Le nom du modèle est obligatoire.");
        if (request.WidthMm <= 0 || request.HeightMm <= 0)
            throw new DomainException("Les dimensions du modèle doivent être positives.");
    }

    private static void EnsureMutable(StudentCard card)
    {
        if (TerminalStatuses.Contains(card.Status))
            throw new DomainException("Cette carte est désactivée ; aucune modification n'est autorisée.");
    }

    private static void EnsurePrintable(StudentCard card)
    {
        if (card.Status is not (StudentCardStatus.Active or StudentCardStatus.Brouillon))
            throw new DomainException(
                $"Carte {card.CardNumber} : impossible d'imprimer une carte au statut « {card.Status} ».");
        if (card.ExpiresAt.HasValue && card.ExpiresAt.Value < DateTime.UtcNow)
            throw new DomainException($"Carte {card.CardNumber} : échéance dépassée, renouvelez-la avant impression.");
    }

    private static bool IsCardUsable(StudentCard card)
    {
        if (card.Status != StudentCardStatus.Active)
            return false;
        if (card.ExpiresAt.HasValue && card.ExpiresAt.Value < DateTime.UtcNow)
            return false;
        return true;
    }

    private static string GenerateQrToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToHexString(bytes);
    }

    /// <summary>Accepte <c>ERP_CARD:TOKEN</c> ou le token seul.</summary>
    public static string ExtractQrToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var value = raw.Trim();
        const string prefix = "ERP_CARD:";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return value[prefix.Length..].Trim();
        return value;
    }

    private static bool MatchesStudent(Student s, string token) =>
        s.RegistrationNumber.Contains(token, StringComparison.OrdinalIgnoreCase)
        || s.FirstName.Contains(token, StringComparison.OrdinalIgnoreCase)
        || s.LastName.Contains(token, StringComparison.OrdinalIgnoreCase)
        || (s.MiddleName?.Contains(token, StringComparison.OrdinalIgnoreCase) ?? false);

    private static string FormatFullName(Student s)
    {
        var parts = new[] { s.LastName, s.MiddleName, s.FirstName }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(" ", parts);
    }
}
