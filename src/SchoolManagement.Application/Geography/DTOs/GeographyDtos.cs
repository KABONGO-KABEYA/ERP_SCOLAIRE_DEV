namespace SchoolManagement.Application.Geography.DTOs;

public sealed record GeographyItemDto(
    Guid Id,
    string Code,
    string Name);

public sealed record AddressInputDto(
    Guid? CountryId,
    Guid? ProvinceId,
    Guid? CityId,
    Guid? CommuneId,
    string? Neighborhood,
    string? Avenue,
    string? HouseNumber);

public sealed record AddressDto(
    Guid Id,
    Guid? CountryId,
    string? CountryName,
    Guid? ProvinceId,
    string? ProvinceName,
    Guid? CityId,
    string? CityName,
    Guid? CommuneId,
    string? CommuneName,
    string? Neighborhood,
    string? Avenue,
    string? HouseNumber)
{
    public string FormattedLine => AddressFormatting.FormatSingleLine(this);
}

public static class AddressFormatting
{
    public static bool HasContent(AddressInputDto? input) =>
        input is not null && (
            input.CountryId.HasValue
            || input.ProvinceId.HasValue
            || input.CityId.HasValue
            || input.CommuneId.HasValue
            || !string.IsNullOrWhiteSpace(input.Neighborhood)
            || !string.IsNullOrWhiteSpace(input.Avenue)
            || !string.IsNullOrWhiteSpace(input.HouseNumber));

    public static string FormatSingleLine(AddressDto? address)
    {
        if (address is null)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(address.Avenue))
        {
            parts.Add(address.Avenue.Trim());
        }

        if (!string.IsNullOrWhiteSpace(address.HouseNumber))
        {
            parts.Add($"N° {address.HouseNumber.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(address.Neighborhood))
        {
            parts.Add(address.Neighborhood.Trim());
        }

        if (!string.IsNullOrWhiteSpace(address.CommuneName))
        {
            parts.Add(address.CommuneName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(address.CityName))
        {
            parts.Add(address.CityName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(address.ProvinceName))
        {
            parts.Add(address.ProvinceName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(address.CountryName))
        {
            parts.Add(address.CountryName.Trim());
        }

        return string.Join(", ", parts);
    }

    public static string? ToLegacyStorage(
        AddressInputDto? input,
        IReadOnlyDictionary<Guid, string>? countryNames = null,
        IReadOnlyDictionary<Guid, string>? provinceNames = null,
        IReadOnlyDictionary<Guid, string>? cityNames = null,
        IReadOnlyDictionary<Guid, string>? communeNames = null,
        string? language = null,
        string? religion = null)
    {
        if (!HasContent(input))
        {
            return AppendProfileNotes(null, language, religion);
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(input!.Avenue))
        {
            parts.Add(input.Avenue.Trim());
        }

        if (!string.IsNullOrWhiteSpace(input.HouseNumber))
        {
            parts.Add($"N° maison: {input.HouseNumber.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(input.Neighborhood))
        {
            parts.Add($"Quartier: {input.Neighborhood.Trim()}");
        }

        AppendNamed(parts, input.CommuneId, communeNames, "Commune");
        AppendNamed(parts, input.CityId, cityNames, "Ville");
        AppendNamed(parts, input.ProvinceId, provinceNames, "Province");
        AppendNamed(parts, input.CountryId, countryNames, "Pays");

        return AppendProfileNotes(parts.Count == 0 ? null : string.Join(" | ", parts), language, religion);
    }

    private static void AppendNamed(
        List<string> parts,
        Guid? id,
        IReadOnlyDictionary<Guid, string>? names,
        string label)
    {
        if (!id.HasValue || names is null || !names.TryGetValue(id.Value, out var name))
        {
            return;
        }

        parts.Add($"{label}: {name}");
    }

    private static string? AppendProfileNotes(string? baseAddress, string? language, string? religion)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(baseAddress))
        {
            parts.Add(baseAddress.Trim());
        }

        if (!string.IsNullOrWhiteSpace(language))
        {
            parts.Add($"Langue: {language.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(religion))
        {
            parts.Add($"Religion: {religion.Trim()}");
        }

        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }
}
