using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Settings;

public class SchoolLogo : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ImagePath { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    public bool IsActive { get; set; } = true;

    public School School { get; set; } = null!;
}

public class SchoolDocumentHeader : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DocumentBrandingType DocumentType { get; set; }

    /// <summary>Types de documents concernés (CSV d'entiers enum). Si vide, seul <see cref="DocumentType"/> s'applique.</summary>
    public string? ApplicableDocumentTypes { get; set; }

    public HeaderPrintMode PrintMode { get; set; }

    public string? ImagePath { get; set; }

    public int? WidthPx { get; set; }

    public int? HeightPx { get; set; }

    public int? ResolutionDpi { get; set; }

    /// <summary>Marge gauche additionnelle de l'image d'en-tête (mm), au sein de la zone contenu.</summary>
    public decimal MarginLeftMm { get; set; }

    /// <summary>Marge droite additionnelle de l'image d'en-tête (mm), au sein de la zone contenu.</summary>
    public decimal MarginRightMm { get; set; }

    /// <summary>Hauteur max de l'image d'en-tête (mm). Null = hauteur automatique (~20 mm).</summary>
    public decimal? MaxHeightMm { get; set; }

    public bool IsActive { get; set; } = true;

    public School School { get; set; } = null!;
}

public class SchoolSignature : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string SignatoryName { get; set; } = string.Empty;

    public string Function { get; set; } = string.Empty;

    public DocumentBrandingType DocumentType { get; set; } = DocumentBrandingType.Autre;

    /// <summary>Types de documents où cette signature apparaît (CSV d'entiers enum).</summary>
    public string? ApplicableDocumentTypes { get; set; }

    public string ImagePath { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public School School { get; set; } = null!;
}

public class SchoolStamp : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ImagePath { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public School School { get; set; } = null!;
}

public class SchoolDocumentFooter : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public string? PoBox { get; set; }

    public string? SchoolMotto { get; set; }

    public string? FreeText { get; set; }

    public School School { get; set; } = null!;
}
