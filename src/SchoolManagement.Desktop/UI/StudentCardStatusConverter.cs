using System.Globalization;
using System.Windows.Data;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.UI;

/// <summary>Libellés français des statuts de carte élève (l'énumération est sans accents).</summary>
public static class StudentCardStatusLabels
{
    public static string From(StudentCardStatus status) => status switch
    {
        StudentCardStatus.Brouillon => "Brouillon",
        StudentCardStatus.Active => "Active",
        StudentCardStatus.Suspendue => "Suspendue",
        StudentCardStatus.Expiree => "Expirée",
        StudentCardStatus.Perdue => "Perdue",
        StudentCardStatus.Volee => "Volée",
        StudentCardStatus.Remplacee => "Remplacée",
        StudentCardStatus.Desactivee => "Désactivée",
        _ => status.ToString()
    };
}

public sealed class StudentCardStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is StudentCardStatus status ? StudentCardStatusLabels.From(status) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Libellés français des actions historisées sur une carte.</summary>
public sealed class StudentCardActionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is StudentCardHistoryAction action
            ? action switch
            {
                StudentCardHistoryAction.Creation => "Création",
                StudentCardHistoryAction.Modification => "Modification",
                StudentCardHistoryAction.Impression => "Impression",
                StudentCardHistoryAction.Reimpression => "Réimpression",
                StudentCardHistoryAction.Renouvellement => "Renouvellement",
                StudentCardHistoryAction.Desactivation => "Désactivation",
                StudentCardHistoryAction.Perte => "Perte",
                StudentCardHistoryAction.Vol => "Vol",
                StudentCardHistoryAction.Remplacement => "Remplacement",
                StudentCardHistoryAction.SuppressionLogique => "Suppression",
                StudentCardHistoryAction.Activation => "Activation",
                StudentCardHistoryAction.Suspension => "Suspension",
                _ => action.ToString()
            }
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
