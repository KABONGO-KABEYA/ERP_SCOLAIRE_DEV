using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.DocumentBranding;

public static class DocumentBrandingTypeCodec
{
    public static string Serialize(IEnumerable<DocumentBrandingType> types)
    {
        var values = types
            .Distinct()
            .OrderBy(t => (int)t)
            .Select(t => ((int)t).ToString());
        return string.Join(",", values);
    }

    public static IReadOnlyList<DocumentBrandingType> Deserialize(string? serialized, DocumentBrandingType fallback)
    {
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return [fallback];
        }

        var parsed = serialized
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var value) ? (DocumentBrandingType?)value : null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .Distinct()
            .ToList();

        return parsed.Count == 0 ? [fallback] : parsed;
    }

    public static string FormatLabels(IEnumerable<DocumentBrandingType> types) =>
        string.Join(", ", types.Select(DocumentBrandingLabels.GetDocumentTypeLabel));

    public static bool AppliesTo(SchoolDocumentHeader header, DocumentBrandingType documentType) =>
        Deserialize(header.ApplicableDocumentTypes, header.DocumentType).Contains(documentType);

    public static bool AppliesTo(SchoolSignature signature, DocumentBrandingType documentType)
    {
        if (string.IsNullOrWhiteSpace(signature.ApplicableDocumentTypes))
        {
            return true;
        }

        return Deserialize(signature.ApplicableDocumentTypes, signature.DocumentType).Contains(documentType);
    }
}
