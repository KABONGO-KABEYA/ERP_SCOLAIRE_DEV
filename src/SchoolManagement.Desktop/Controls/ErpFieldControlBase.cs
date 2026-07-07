using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace SchoolManagement.Desktop.Controls;

public enum ErpFieldValidationMode
{
    None,
    Required,
    Email,
    Phone
}

public static class ErpFieldValidation
{
    public static bool ValidateRequired(string? value, bool isRequired, bool showValidation, string label, out string? error)
    {
        error = null;
        if (isRequired && string.IsNullOrWhiteSpace(value))
        {
            if (showValidation)
            {
                error = $"Le champ « {label} » est obligatoire.";
            }

            return false;
        }

        return true;
    }

    public static bool ValidateEmail(string? value, ErpFieldValidationMode mode, out string? error)
    {
        error = null;
        if (mode != ErpFieldValidationMode.Email || string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!IsValidEmail(value))
        {
            error = "Adresse e-mail invalide.";
            return false;
        }

        return true;
    }

    public static bool ValidatePhone(string? value, ErpFieldValidationMode mode, out string? error)
    {
        error = null;
        if (mode != ErpFieldValidationMode.Phone || string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!IsValidPhone(value))
        {
            error = "Numéro de téléphone invalide.";
            return false;
        }

        return true;
    }

    public static Brush GetBrush(string key) =>
        global::System.Windows.Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;

    public static bool IsValidEmail(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Contains('@') &&
        value.Contains('.') &&
        value.IndexOf('@') < value.LastIndexOf('.');

    public static bool IsValidPhone(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var digits = 0;
        foreach (var c in value)
        {
            if (char.IsDigit(c))
            {
                digits++;
            }
        }

        return digits is >= 9 and <= 15;
    }
}
