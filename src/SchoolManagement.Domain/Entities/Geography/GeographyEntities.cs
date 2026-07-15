using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities.Geography;

public class Country : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<Province> Provinces { get; set; } = [];
}

public class Province : AuditableEntity
{
    public Guid CountryId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public Country Country { get; set; } = null!;

    public ICollection<City> Cities { get; set; } = [];
}

public class City : AuditableEntity
{
    public Guid ProvinceId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public Province Province { get; set; } = null!;

    public ICollection<Commune> Communes { get; set; } = [];
}

public class Commune : AuditableEntity
{
    public Guid CityId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public City City { get; set; } = null!;

    public ICollection<PostalAddress> Addresses { get; set; } = [];
}

public class PostalAddress : AuditableEntity
{
    public Guid? CountryId { get; set; }

    public Guid? ProvinceId { get; set; }

    public Guid? CityId { get; set; }

    public Guid? CommuneId { get; set; }

    public string? Neighborhood { get; set; }

    public string? Avenue { get; set; }

    public string? HouseNumber { get; set; }

    public Country? Country { get; set; }

    public Province? Province { get; set; }

    public City? City { get; set; }

    public Commune? Commune { get; set; }
}
