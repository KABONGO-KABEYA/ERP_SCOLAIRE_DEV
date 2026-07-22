using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Application.SchoolFees.DTOs;

namespace SchoolManagement.Desktop.Helpers;

/// <summary>Sélectionne le frais principal configuré, sinon un fallback heuristique.</summary>
public static class DefaultFeeTypeHelper
{
    public static FeeTypeLookupDto? Resolve(
        IEnumerable<FeeTypeLookupDto> feeTypes,
        Guid? defaultFeeTypeId)
    {
        var list = feeTypes as IList<FeeTypeLookupDto> ?? feeTypes.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        if (defaultFeeTypeId is Guid id)
        {
            var match = list.FirstOrDefault(f => f.Id == id);
            if (match is not null)
            {
                return match;
            }
        }

        return list.FirstOrDefault(f =>
                   string.Equals(f.Name, "Frais scolaire", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(f.Name, "Frais scolaires", StringComparison.OrdinalIgnoreCase))
               ?? list.FirstOrDefault(f =>
                   f.Name.Contains("scolaire", StringComparison.OrdinalIgnoreCase))
               ?? list.FirstOrDefault();
    }

    public static FeeTypeDto? Resolve(
        IEnumerable<FeeTypeDto> feeTypes,
        Guid? defaultFeeTypeId)
    {
        var list = feeTypes as IList<FeeTypeDto> ?? feeTypes.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        if (defaultFeeTypeId is Guid id)
        {
            var match = list.FirstOrDefault(f => f.Id == id);
            if (match is not null)
            {
                return match;
            }
        }

        return list.FirstOrDefault(f =>
                   string.Equals(f.Name, "Frais scolaire", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(f.Name, "Frais scolaires", StringComparison.OrdinalIgnoreCase))
               ?? list.FirstOrDefault(f =>
                   f.Name.Contains("scolaire", StringComparison.OrdinalIgnoreCase))
               ?? list.FirstOrDefault();
    }
}
