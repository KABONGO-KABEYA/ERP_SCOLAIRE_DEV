using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Settings;

namespace SchoolManagement.Domain.Entities.Finance;

/// <summary>Devise du référentiel (FinDevise) — catalogue global.</summary>
public class CurrencyDefinition : AuditableEntity, IAggregateRoot
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Symbol { get; set; } = string.Empty;

    public int DecimalPlaces { get; set; } = 2;

    /// <summary>True pour la devise système par défaut (ex. CDF).</summary>
    public bool IsSystemLocal { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<SchoolCurrency> SchoolCurrencies { get; set; } = [];
}

/// <summary>Devise autorisée pour un établissement (FinEtablissementDevise).</summary>
public class SchoolCurrency : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid CurrencyId { get; set; }

    public bool IsPrimary { get; set; }

    public bool AllowPayment { get; set; } = true;

    public School School { get; set; } = null!;

    public CurrencyDefinition Currency { get; set; } = null!;
}

/// <summary>Type de taux de change (FinTypeTaux).</summary>
public class ExchangeRateType : AuditableEntity, IAggregateRoot
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>Taux de change (FinTauxChange) — historique conservé via Actif + Historique.</summary>
public class ExchangeRate : AuditableEntity, IAggregateRoot
{
    public Guid SourceCurrencyId { get; set; }

    public Guid TargetCurrencyId { get; set; }

    public Guid RateTypeId { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public decimal Rate { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public CurrencyDefinition SourceCurrency { get; set; } = null!;

    public CurrencyDefinition TargetCurrency { get; set; } = null!;

    public ExchangeRateType RateType { get; set; } = null!;
}

/// <summary>Historique des changements de taux (FinHistoriqueTaux).</summary>
public class ExchangeRateHistory : AuditableEntity, IAggregateRoot
{
    public Guid ExchangeRateId { get; set; }

    public Guid SourceCurrencyId { get; set; }

    public Guid TargetCurrencyId { get; set; }

    public Guid RateTypeId { get; set; }

    public decimal? OldRate { get; set; }

    public decimal NewRate { get; set; }

    public string Action { get; set; } = string.Empty;

    public Guid? UserId { get; set; }

    public string? MachineName { get; set; }

    public string? IpAddress { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public ExchangeRate ExchangeRate { get; set; } = null!;
}
