using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Students;

/// <summary>
/// Modèle graphique de carte (CarteModele) — layout JSON pour le concepteur (phases ultérieures).
/// </summary>
public class CardTemplate : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Largeur en millimètres.</summary>
    public decimal WidthMm { get; set; } = 85.6m;

    /// <summary>Hauteur en millimètres.</summary>
    public decimal HeightMm { get; set; } = 53.98m;

    public CardTemplateOrientation Orientation { get; set; } = CardTemplateOrientation.Landscape;

    public CardTemplateKind Kind { get; set; } = CardTemplateKind.Eleve;

    /// <summary>Définition visuelle recto (JSON) — éditable sans recompilation.</summary>
    public string? LayoutJsonFront { get; set; }

    /// <summary>Définition visuelle verso (JSON).</summary>
    public string? LayoutJsonBack { get; set; }

    public bool IsActive { get; set; } = true;

    public School School { get; set; } = null!;

    public ICollection<StudentCard> Cards { get; set; } = [];
}

/// <summary>
/// Paramètres module cartes par établissement (préfixe numéro, validité, renouvellement QR).
/// </summary>
public class CardSchoolSettings : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    /// <summary>Préfixe du numéro de carte (ex. CSB).</summary>
    public string CardNumberPrefix { get; set; } = "CARD";

    /// <summary>Durée de validité par défaut en mois à l'émission.</summary>
    public int DefaultValidityMonths { get; set; } = 12;

    /// <summary>Si true, un renouvellement conserve le même jeton QR.</summary>
    public bool KeepQrOnRenewal { get; set; }

    /// <summary>Compteur séquentiel pour génération CSB-2026-000001.</summary>
    public int NextSequence { get; set; } = 1;

    public School School { get; set; } = null!;
}

/// <summary>
/// Carte élève — objet métier indépendant du matricule, avec cycle de vie propre.
/// </summary>
public class StudentCard : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid StudentId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid TemplateId { get; set; }

    /// <summary>Numéro métier unique (indépendant du matricule), ex. CSB-2026-000001.</summary>
    public string CardNumber { get; set; } = string.Empty;

    /// <summary>
    /// Identifiant sécurisé opaque. Le QR encode uniquement <c>ERP_CARD:{QrToken}</c>.
    /// Aucune donnée personnelle.
    /// </summary>
    public string QrToken { get; set; } = string.Empty;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PrintedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public StudentCardStatus Status { get; set; } = StudentCardStatus.Brouillon;

    public string? DeactivationReason { get; set; }

    public int PrintCount { get; set; }

    /// <summary>Version métier (incrémentée à chaque renouvellement / remplacement).</summary>
    public int Version { get; set; } = 1;

    /// <summary>Carte précédente remplacée / renouvelée (historique).</summary>
    public Guid? ReplacesCardId { get; set; }

    public Student Student { get; set; } = null!;

    public AcademicYear AcademicYear { get; set; } = null!;

    public CardTemplate Template { get; set; } = null!;

    public StudentCard? ReplacesCard { get; set; }

    public ICollection<StudentCardHistory> Histories { get; set; } = [];

    public ICollection<StudentCardPrintLog> PrintLogs { get; set; } = [];

    /// <summary>Payload QR sans PII — exploitable par les futurs modules.</summary>
    public string QrPayload => $"ERP_CARD:{QrToken}";
}

/// <summary>Journal métier des opérations sur une carte.</summary>
public class StudentCardHistory : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid CardId { get; set; }

    public StudentCardHistoryAction Action { get; set; }

    public Guid? UserId { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? Notes { get; set; }

    public StudentCard Card { get; set; } = null!;
}

/// <summary>Trace d'impression / réimpression (nombre, date, utilisateur, raison).</summary>
public class StudentCardPrintLog : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid CardId { get; set; }

    public DateTime PrintedAt { get; set; } = DateTime.UtcNow;

    public Guid? PrintedBy { get; set; }

    public string? Reason { get; set; }

    public bool IsReprint { get; set; }

    public StudentCard Card { get; set; } = null!;
}
